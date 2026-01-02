-- users
CREATE TABLE users (
    id BIGINT PRIMARY KEY,
    username TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- accounts
CREATE TABLE accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id BIGINT REFERENCES users(id),
    name TEXT,
    acc_number TEXT UNIQUE,
    balance REAL DEFAULT 0,
    blocked BOOLEAN DEFAULT FALSE
);

-- pending (удержания)
CREATE TABLE pending (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    account_id INTEGER REFERENCES accounts(id),
    amount REAL NOT NULL,
    release_at TIMESTAMP NOT NULL,
    comment TEXT,
    processed BOOLEAN DEFAULT FALSE
);

-- transfers
CREATE TABLE transfers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    from_account INTEGER,
    to_account INTEGER,
    amount REAL,
    comment TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
