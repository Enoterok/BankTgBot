PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY,
    username TEXT,
    created_at INTEGER,
    blocked INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    name TEXT NOT NULL,
    acc_number TEXT UNIQUE NOT NULL,
    balance REAL NOT NULL DEFAULT 0,
    pending REAL NOT NULL DEFAULT 0,
    blocked INTEGER NOT NULL DEFAULT 0,
    created_at INTEGER DEFAULT (strftime('%s', 'now')),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS pending (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    account_id INTEGER NOT NULL,
    amount REAL NOT NULL,
    created_at INTEGER NOT NULL,
    release_at INTEGER,
    reason TEXT,
    FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS transactions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    from_acc INTEGER,
    to_acc INTEGER,
    amount REAL NOT NULL,
    comment TEXT,
    created_at INTEGER NOT NULL
);

-- Таблица для глобальных правил pending
CREATE TABLE IF NOT EXISTS global_pending_rules (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    target_acc TEXT NULL,  -- NULL для глобального правила
    delay_hours INTEGER NOT NULL DEFAULT 24,
    reason TEXT,
    active INTEGER NOT NULL DEFAULT 1,
    created_at INTEGER NOT NULL,
    created_by INTEGER,
    FOREIGN KEY (created_by) REFERENCES users(id)
);

-- === Индексы ===
CREATE INDEX IF NOT EXISTS idx_accounts_user ON accounts(user_id);
CREATE INDEX IF NOT EXISTS idx_pending_release ON pending(release_at);
CREATE INDEX IF NOT EXISTS idx_global_pending ON global_pending_rules(active, target_acc);