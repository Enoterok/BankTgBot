import random
from core import sqlite as db
import core.errors as errors

# ======================
# MONEY ERROR CODES
# ======================
MONEY_OK = errors.MONEY_OK
MONEY_USER_NOT_FOUND = errors.MONEY_USER_NOT_FOUND
MONEY_ACC_NOT_FOUND = errors.MONEY_ACC_NOT_FOUND
MONEY_BLOCKED = errors.MONEY_BLOCKED
MONEY_NO_FUNDS = errors.MONEY_NO_FUNDS
MONEY_BAD_AMOUNT = errors.MONEY_BAD_AMOUNT


# ======================
# INTERNAL UTILS
# ======================
def _gen_acc():
    return f"ACC-{random.randint(100000000, 999999999)}"


def err_check(err, data=None):
    if err != db.SQL_OK:
        return True
    elif data is None:
        return True
    else:
        return False


# ======================
# REGISTRATION
# ======================
def register(user_id: int, username: str):
    err, _ = db.create_user(user_id, username)

    # пользователь уже существует — это допустимо
    if err == db.SQL_ALREADY_EXISTS: return MONEY_OK, "already_registered"
    if err_check(err, True): return err, None

    acc = _gen_acc()
    err, _ = db.create_account(user_id, "main", acc)

    if err_check(err, True): return err, None

    return MONEY_OK, acc


# ======================
# BALANCE
# ======================
def get_balance(acc_number: str):
    err, acc = db.get_account_by_acc(acc_number)

    if err_check(err, acc): return err, None
    if acc.get("blocked"): return MONEY_BLOCKED, "Get blocked"

    return MONEY_OK, acc.get("balance", 0.0)


# ======================
# TRANSFER
# ======================
def transfer(from_acc: str, to_acc: str, amount: float):
    if amount <= 0: return MONEY_BAD_AMOUNT, None
    
    try:
        db.transfer(from_acc, to_acc, amount)
    except Exception as e:
        return "Unknow error", e

    return MONEY_OK, None
