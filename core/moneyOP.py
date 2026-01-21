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
MONEY_INTERNAL_ERROR = errors.MONEY_INTERNAL_ERROR

# ======================
# INTERNAL UTILS
# ======================

def is_user_blocked(user_id: int):
    """Проверка, заблокирован ли пользователь"""
    err, user = db.get_user(user_id)
    if err != db.SQL_OK:
        return False  # Не нашли пользователя или ошибка
    return user.get("blocked", False)


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
    if err == db.SQL_ALREADY_EXISTS: 
        # Проверяем, не заблокирован ли пользователь
        if is_user_blocked(user_id):
            return MONEY_BLOCKED, "User is blocked"
        return MONEY_OK, "already_registered"
    
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
    if amount <= 0: 
        return MONEY_BAD_AMOUNT, None
    
    # Получаем информацию о счете отправителя
    err, from_account = db.get_account_by_acc(from_acc)
    if err_check(err, from_account):
        return err, None
    if from_account.get("blocked"):
        return MONEY_BLOCKED, "Sender account is blocked"
    
    # Получаем информацию о счете получателя
    err, to_account = db.get_account_by_acc(to_acc)
    if err_check(err, to_account):
        return err, None
    if to_account.get("blocked"):
        return MONEY_BLOCKED, "Recipient account is blocked"
    
    # Проверяем достаточность средств
    if from_account.get("balance", 0) < amount:
        return MONEY_NO_FUNDS, "Insufficient funds"
    
    # Проверяем pending правила
    err, pending_info = check_transfer_pending(to_acc, amount)
    if err != db.SQL_OK:
        return err, pending_info
    
    if pending_info.get("pending", False):
        # Если есть pending правило, списываем средства но не зачисляем сразу
        # Списание со счета отправителя
        err = db.update_account_balance(from_acc, from_account["balance"] - amount)
        if err != db.SQL_OK:
            return err, None
        
        return MONEY_OK, {
            "pending": True,
            "message": f"Перевод отправлен в ожидание на {pending_info['delay_hours']} часов. Причина: {pending_info['reason']}"
        }
    
    # Обычный перевод
    err, result = db.transfer(from_account["id"], to_account["id"], amount)
    
    # Передаем ошибку из sqlite.py без изменений
    if err != db.SQL_OK:
        return err, result
    
    return MONEY_OK, None

# Добавьте эту функцию в moneyOP.py:

def create_account(user_id: int, account_name: str):
    # Проверяем, не заблокирован ли пользователь
    if is_user_blocked(user_id):
        return MONEY_BLOCKED, "User is blocked"
    
    # Генерируем номер счета
    acc_number = f"ACC-{user_id}-{random.randint(1000, 9999)}"
    
    err, _ = db.create_account(user_id, account_name, acc_number)
    if err_check(err, True):
        return err, None
    
    return MONEY_OK, acc_number

def check_transfer_pending(to_acc: str, amount: float):
    """Проверка, нужно ли применять pending для перевода"""
    err, rule = db.check_pending_rule(to_acc)
    if err != db.SQL_OK:
        return err, None
    
    if rule.get("applies", False):
        # Получаем информацию о счете получателя
        err, account = db.get_account_by_acc(to_acc)
        if err != db.SQL_OK:
            return err, None
        
        # Добавляем в pending
        pending_id = db.add_pending_transaction(
            account["id"], 
            amount, 
            rule["reason"], 
            rule["delay_hours"]
        )
        
        return MONEY_OK, {
            "pending": True,
            "delay_hours": rule["delay_hours"],
            "reason": rule["reason"],
            "pending_id": pending_id
        }
    
    return MONEY_OK, {"pending": False}