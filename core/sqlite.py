import sqlite3
import time
import os
import core.errors as errors

DB_PATH = "storage/bank.db"
SCHEMA_PATH = "storage/models.sql"


# ======================
# ERROR CODES
# ======================
SQL_OK = errors.SQL_OK
SQL_CONN_ERROR = errors.SQL_CONN_ERROR
SQL_NOT_FOUND = errors.SQL_NOT_FOUND
SQL_ALREADY_EXISTS = errors.SQL_ALREADY_EXISTS
SQL_INTERNAL_ERROR = errors.SQL_INTERNAL_ERROR


def _connect():
    try:
        return sqlite3.connect(DB_PATH)
    except Exception:
        return None


def init_db():
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None

    try:
        cur = conn.cursor()
        # Проверяем наличие таблицы users в системной таблице sqlite_master
        cur.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='users'")
        if cur.fetchone():
            return SQL_OK, None # База уже инициализирована

        # Если таблиц нет — создаем их
        with open(SCHEMA_PATH, "r", encoding="utf-8") as f:
            conn.executescript(f.read())
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()



# ======================
# USERS
# ======================
def create_user(user_id: int, username: str):
    conn = _connect()
    if not conn:
        return SQL_CONN_ERROR, None

    try:
        cur = conn.cursor()
        cur.execute(
            "INSERT INTO users (id, username, created_at) VALUES (?, ?, ?)",
            (user_id, username, int(time.time()))
        )
        conn.commit()
        return SQL_OK, None
    except sqlite3.IntegrityError:
        return SQL_ALREADY_EXISTS, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e) # Ошибка тут: OperationalError('no such table: users')
    finally:
        conn.close()


def get_user(user_id: int):
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None

    try:
        cur = conn.cursor()
        cur.execute("SELECT id, username FROM users WHERE id = ?", (user_id,))
        row = cur.fetchone()
        if not row:
            return SQL_NOT_FOUND, None
        return SQL_OK, {"id": row[0], "username": row[1]}
    finally:
        conn.close()


# ======================
# ACCOUNTS
# ======================
def create_account(user_id: int, name: str, acc_number: str):
    conn = _connect()
    if not conn:
        return SQL_CONN_ERROR, None

    try:
        cur = conn.cursor()
        cur.execute(
            "INSERT INTO accounts (user_id, name, acc_number) VALUES (?, ?, ?)",
            (user_id, name, acc_number)
        )
        conn.commit()
        return SQL_OK, None
    except sqlite3.IntegrityError:
        return SQL_ALREADY_EXISTS, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def get_account_by_acc(acc_number: str):
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None

    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT id, user_id, balance, blocked
            FROM accounts WHERE acc_number = ?
        """, (acc_number,))
        row = cur.fetchone()
        if not row:
            return SQL_NOT_FOUND, None
        return SQL_OK, {
            "id": row[0],
            "user_id": row[1],
            "balance": row[2],
            "blocked": bool(row[3])
        }
    finally:
        conn.close()
        
def get_accounts_by_user(user_id: int):
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None

    try:
        cur = conn.cursor()

        cur.execute("""
            SELECT
                id,
                name,
                acc_number,
                balance,
                pending,
                blocked
            FROM accounts
            WHERE user_id = ?
        """, (user_id,))

        rows = cur.fetchall()
        conn.close()

        if not rows:
            return SQL_NOT_FOUND, None

        accounts = []
        for row in rows:
            accounts.append({
                "id": row[0],
                "name": row[1],
                "acc_number": row[2],
                "balance": row[3],
                "pending": row[4],
                "blocked": bool(row[5])
            })

        return SQL_OK, accounts

    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)


def update_balance(acc_id: int, new_balance: float):
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None

    try:
        conn.execute(
            "UPDATE accounts SET balance = ? WHERE id = ?",
            (new_balance, acc_id)
        )
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()
        
# Добавьте эту функцию в конец файла sqlite.py, в раздел ACCOUNTS:

def get_main_account(user_id: int):
    """Получение основного счета пользователя"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT id, name, acc_number, balance, pending, blocked
            FROM accounts 
            WHERE user_id = ? 
            ORDER BY id 
            LIMIT 1
        """, (user_id,))
        row = cur.fetchone()
        if not row:
            return SQL_NOT_FOUND, None
        
        return SQL_OK, {
            "id": row[0],
            "name": row[1],
            "acc_number": row[2],
            "balance": row[3],
            "pending": row[4],
            "blocked": bool(row[5])
        }
    finally:
        conn.close()
        
def transfer(from_acc_id: int, to_acc_id: int, amount: float):   
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        with conn:
            cursor = conn.cursor()
            cursor.execute(
                "SELECT balance, blocked FROM accounts WHERE id = ?", (from_acc_id,)
            )
            from_acc = cursor.fetchone()
            
            cursor.execute(
                "SELECT balance, blocked FROM accounts WHERE id = ?", (to_acc_id,)
            )
            to_acc = cursor.fetchone()

            if not from_acc or not to_acc: return SQL_NOT_FOUND, "One or both accounts not found"
            if from_acc[1] or to_acc[1]: return SQL_INTERNAL_ERROR, "One of the accounts is blocked"
            if from_acc[0] < amount: return SQL_INTERNAL_ERROR, "Insufficient funds"

            cursor.execute(
                "UPDATE accounts SET balance = balance - ? WHERE id = ?", 
                (amount, from_acc_id)
            )
            cursor.execute(
                "UPDATE accounts SET balance = balance + ? WHERE id = ?", 
                (amount, to_acc_id)
            )

            return SQL_OK, None

    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()
        

# Добавим в существующий sqlite.py новые функции

# ======================
# ADMIN FUNCTIONS
# ======================
def block_account(acc_number: str, block: bool = True):
    """Блокировка/разблокировка счета"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        conn.execute(
            "UPDATE accounts SET blocked = ? WHERE acc_number = ?",
            (1 if block else 0, acc_number)
        )
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def block_user(user_id: int, block: bool = True):
    """Блокировка/разблокировка пользователя (всех его счетов)"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        # Добавляем поле blocked в таблицу users
        # Сначала проверим, есть ли такое поле
        cur = conn.cursor()
        cur.execute("PRAGMA table_info(users)")
        columns = [col[1] for col in cur.fetchall()]
        
        if 'blocked' not in columns:
            # Добавляем поле
            cur.execute("ALTER TABLE users ADD COLUMN blocked INTEGER DEFAULT 0")
        
        # Блокируем пользователя
        conn.execute(
            "UPDATE users SET blocked = ? WHERE id = ?",
            (1 if block else 0, user_id)
        )
        
        # Блокируем все счета пользователя
        conn.execute(
            "UPDATE accounts SET blocked = ? WHERE user_id = ?",
            (1 if block else 0, user_id)
        )
        
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def delete_account(acc_number: str):
    """Удаление счета"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        cur.execute("DELETE FROM accounts WHERE acc_number = ?", (acc_number,))
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def get_all_users():
    """Получение всех пользователей"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT u.id, u.username, u.created_at, 
                   COALESCE(u.blocked, 0) as blocked,
                   COUNT(a.id) as account_count,
                   COALESCE(SUM(a.balance), 0) as total_balance
            FROM users u
            LEFT JOIN accounts a ON u.id = a.user_id
            GROUP BY u.id
            ORDER BY u.created_at DESC
        """)
        
        users = []
        for row in cur.fetchall():
            users.append({
                "id": row[0],
                "username": row[1],
                "created_at": row[2],
                "blocked": bool(row[3]),
                "account_count": row[4],
                "total_balance": row[5]
            })
        return SQL_OK, users
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def get_all_accounts():
    """Получение всех счетов"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT a.id, a.user_id, u.username, a.name, 
                   a.acc_number, a.balance, a.pending, a.blocked,
                   a.created_at
            FROM accounts a
            JOIN users u ON a.user_id = u.id
            ORDER BY a.created_at DESC
        """)
        
        accounts = []
        for row in cur.fetchall():
            accounts.append({
                "id": row[0],
                "user_id": row[1],
                "username": row[2],
                "name": row[3],
                "acc_number": row[4],
                "balance": row[5],
                "pending": row[6],
                "blocked": bool(row[7]),
                "created_at": row[8]
            })
        return SQL_OK, accounts
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def update_account_balance(acc_number: str, new_balance: float):
    """Изменение баланса счета (админ)"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        conn.execute(
            "UPDATE accounts SET balance = ? WHERE acc_number = ?",
            (new_balance, acc_number)
        )
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def add_pending_transaction(account_id: int, amount: float, reason: str, delay_hours: int = 24):
    """Добавление транзакции в pending"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        created_at = int(time.time())
        release_at = created_at + (delay_hours * 3600)
        
        cur.execute("""
            INSERT INTO pending (account_id, amount, created_at, release_at, reason)
            VALUES (?, ?, ?, ?, ?)
        """, (account_id, amount, created_at, release_at, reason))
        
        # Обновляем pending баланс на счете
        cur.execute("""
            UPDATE accounts SET pending = pending + ? WHERE id = ?
        """, (amount, account_id))
        
        conn.commit()
        return SQL_OK, cur.lastrowid
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def get_pending_transactions():
    """Получение всех pending транзакций"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT p.id, p.account_id, a.acc_number, u.username,
                   p.amount, p.created_at, p.release_at, p.reason
            FROM pending p
            JOIN accounts a ON p.account_id = a.id
            JOIN users u ON a.user_id = u.id
            ORDER BY p.release_at
        """)
        
        transactions = []
        for row in cur.fetchall():
            transactions.append({
                "id": row[0],
                "account_id": row[1],
                "acc_number": row[2],
                "username": row[3],
                "amount": row[4],
                "created_at": row[5],
                "release_at": row[6],
                "reason": row[7]
            })
        return SQL_OK, transactions
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def release_pending_transaction(pending_id: int):
    """Освобождение pending транзакции"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        
        # Получаем информацию о pending транзакции
        cur.execute("""
            SELECT account_id, amount FROM pending WHERE id = ?
        """, (pending_id,))
        row = cur.fetchone()
        
        if not row:
            return SQL_NOT_FOUND, None
        
        account_id, amount = row
        
        # Удаляем из pending
        cur.execute("DELETE FROM pending WHERE id = ?", (pending_id,))
        
        # Обновляем балансы
        cur.execute("""
            UPDATE accounts 
            SET pending = pending - ?, balance = balance + ?
            WHERE id = ?
        """, (amount, amount, account_id))
        
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def get_user_by_id(user_id: int):
    """Получение информации о пользователе по ID"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT id, username, created_at, 
                   COALESCE(blocked, 0) as blocked
            FROM users WHERE id = ?
        """, (user_id,))
        
        row = cur.fetchone()
        if not row:
            return SQL_NOT_FOUND, None
        
        return SQL_OK, {
            "id": row[0],
            "username": row[1],
            "created_at": row[2],
            "blocked": bool(row[3])
        }
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def get_account_full_info(acc_number: str):
    """Полная информация о счете"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT a.id, a.user_id, u.username, a.name, 
                   a.acc_number, a.balance, a.pending, a.blocked,
                   u.created_at as user_created,
                   (SELECT COUNT(*) FROM accounts a2 WHERE a2.user_id = a.user_id) as total_accounts
            FROM accounts a
            JOIN users u ON a.user_id = u.id
            WHERE a.acc_number = ?
        """, (acc_number,))
        
        row = cur.fetchone()
        if not row:
            return SQL_NOT_FOUND, None
        
        return SQL_OK, {
            "id": row[0],
            "user_id": row[1],
            "username": row[2],
            "name": row[3],
            "acc_number": row[4],
            "balance": row[5],
            "pending": row[6],
            "blocked": bool(row[7]),
            "user_created": row[8],
            "total_accounts": row[9]
        }
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()
        
# ======================
# GLOBAL PENDING RULES
# ======================
def add_global_pending_rule(target_acc: str = None, delay_hours: int = 24, reason: str = "", admin_id: int = None):
    """Добавление глобального правила pending"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        created_at = int(time.time())
        
        cur.execute("""
            INSERT INTO global_pending_rules (target_acc, delay_hours, reason, active, created_at, created_by)
            VALUES (?, ?, ?, 1, ?, ?)
        """, (target_acc, delay_hours, reason, created_at, admin_id))
        
        conn.commit()
        return SQL_OK, cur.lastrowid
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def get_global_pending_rules():
    """Получение всех глобальных правил"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT g.id, g.target_acc, g.delay_hours, g.reason, 
                   g.active, g.created_at, u.username as admin_username
            FROM global_pending_rules g
            LEFT JOIN users u ON g.created_by = u.id
            ORDER BY g.created_at DESC
        """)
        
        rules = []
        for row in cur.fetchall():
            rules.append({
                "id": row[0],
                "target_acc": row[1],
                "delay_hours": row[2],
                "reason": row[3],
                "active": bool(row[4]),
                "created_at": row[5],
                "admin_username": row[6]
            })
        return SQL_OK, rules
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def toggle_global_pending_rule(rule_id: int, active: bool):
    """Включение/выключение глобального правила"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        conn.execute(
            "UPDATE global_pending_rules SET active = ? WHERE id = ?",
            (1 if active else 0, rule_id)
        )
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def delete_global_pending_rule(rule_id: int):
    """Удаление глобального правила"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        conn.execute("DELETE FROM global_pending_rules WHERE id = ?", (rule_id,))
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def check_pending_rule(target_acc: str):
    """Проверка применения pending правила для счета"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        
        # Проверяем глобальные правила
        # Сначала правила для конкретного счета, затем общие
        cur.execute("""
            SELECT delay_hours, reason 
            FROM global_pending_rules 
            WHERE active = 1 AND (target_acc = ? OR target_acc IS NULL)
            ORDER BY target_acc DESC, created_at DESC
            LIMIT 1
        """, (target_acc,))
        
        row = cur.fetchone()
        if row:
            return SQL_OK, {
                "delay_hours": row[0],
                "reason": row[1],
                "applies": True
            }
        
        return SQL_OK, {"applies": False}
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()

def add_to_pending(account_id: int, amount: float):
    """Добавление суммы к pending счету"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        conn.execute(
            "UPDATE accounts SET pending = pending + ? WHERE id = ?",
            (amount, account_id)
        )
        conn.commit()
        return SQL_OK, None
    except Exception as e:
        return SQL_INTERNAL_ERROR, str(e)
    finally:
        conn.close()


def get_account_by_name_and_user(account_name: str, user_id: int):
    """Получение счета по имени и ID пользователя"""
    conn = _connect()
    if not conn: return SQL_CONN_ERROR, None
    
    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT id, user_id, name, acc_number, balance, pending, blocked
            FROM accounts WHERE name = ? AND user_id = ?
        """, (account_name, user_id))
        row = cur.fetchone()
        if not row:
            return SQL_NOT_FOUND, None
        return SQL_OK, {
            "id": row[0],
            "user_id": row[1],
            "name": row[2],
            "acc_number": row[3],
            "balance": row[4],
            "pending": row[5],
            "blocked": bool(row[6])
        }
    finally:
        conn.close()