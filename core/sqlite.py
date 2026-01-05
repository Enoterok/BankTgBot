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