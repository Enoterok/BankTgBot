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
MONEY_PENDING_TRANSFER = "MONEY_PENDING_TRANSFER"  # Новый код для pending перевода

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
# USER BLOCK CHECK
# ======================
def is_user_blocked(user_id: int):
    """Проверка, заблокирован ли пользователь"""
    err, user = db.get_user_by_id(user_id)
    if err != db.SQL_OK:
        return False  # Не нашли пользователя или ошибка
    return user.get("blocked", False)


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
    
    if err_check(err, True): 
        return err, None

    acc = _gen_acc()
    err, _ = db.create_account(user_id, "main", acc)

    if err_check(err, True): 
        return err, None

    return MONEY_OK, acc


# ======================
# BALANCE
# ======================
def get_balance(acc_number: str):
    err, acc = db.get_account_by_acc(acc_number)

    if err_check(err, acc): 
        return err, None
    if acc.get("blocked"): 
        return MONEY_BLOCKED, "Get blocked"

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
    err, rule = db.check_pending_rule(to_acc)
    if err != db.SQL_OK:
        return err, None
    
    if rule.get("applies", False):
        # Если есть pending правило, создаем pending транзакцию
        delay_hours = rule.get("delay_hours", 24)
        reason = rule.get("reason", "Pending rule")
        
        # Создаем pending транзакцию
        err, pending_id = db.add_pending_transaction(
            to_account["id"], 
            amount, 
            reason, 
            delay_hours
        )
        
        if err != db.SQL_OK:
            return err, pending_id
        
        # Списание средств со счета отправителя
        err, _ = db.update_balance(from_account["id"], from_account["balance"] - amount)
        if err != db.SQL_OK:
            return err, None
        
        # Добавляем сумму в pending получателя
        err, _ = db.add_to_pending(to_account["id"], amount)
        if err != db.SQL_OK:
            return err, None
        
        return MONEY_PENDING_TRANSFER, {
            "delay_hours": delay_hours,
            "reason": reason,
            "pending_id": pending_id
        }
    
    # Обычный перевод
    err, result = db.transfer(from_account["id"], to_account["id"], amount)
    
    # Передаем ошибку из sqlite.py без изменений
    if err != db.SQL_OK:
        return err, result
    
    return MONEY_OK, None


def create_account(user_id: int, account_name: str):
    """Создание нового счета"""
    # Проверяем, не заблокирован ли пользователь
    if is_user_blocked(user_id):
        return MONEY_BLOCKED, "User is blocked"
    
    # Генерируем номер счета
    import random
    acc_number = f"ACC-{user_id}-{random.randint(1000, 9999)}"
    
    err, _ = db.create_account(user_id, account_name, acc_number)
    if err_check(err, True):
        return err, None
    
    return MONEY_OK, acc_number


# ======================
# ADMIN FUNCTIONS
# ======================
def admin_block_user(user_id: int, block: bool = True):
    """Блокировка/разблокировка пользователя"""
    err, _ = db.block_user(user_id, block)
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, None


def admin_block_account(acc_number: str, block: bool = True):
    """Блокировка/разблокировка счета"""
    err, _ = db.block_account(acc_number, block)
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, None


def admin_delete_account(acc_number: str):
    """Удаление счета"""
    err, _ = db.delete_account(acc_number)
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, None


def admin_update_balance(acc_number: str, new_balance: float):
    """Изменение баланса счета"""
    err, _ = db.update_account_balance(acc_number, new_balance)
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, None


def admin_add_pending_rule(target_acc: str = None, delay_hours: int = 24, reason: str = "", admin_id: int = None):
    """Добавление pending правила"""
    err, rule_id = db.add_global_pending_rule(target_acc, delay_hours, reason, admin_id)
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, rule_id


def admin_get_pending_rules():
    """Получение всех pending правил"""
    err, rules = db.get_global_pending_rules()
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, rules


def admin_get_pending_transactions():
    """Получение всех pending транзакций"""
    err, transactions = db.get_pending_transactions()
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, transactions


def admin_release_pending(pending_id: int):
    """Освобождение pending транзакции"""
    err, _ = db.release_pending_transaction(pending_id)
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, None


def admin_get_all_users():
    """Получение всех пользователей"""
    err, users = db.get_all_users()
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, users


def admin_get_all_accounts():
    """Получение всех счетов"""
    err, accounts = db.get_all_accounts()
    if err != db.SQL_OK:
        return err, None
    return MONEY_OK, accounts