using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BankBot.Storage;

public class DatabaseService
{
    private readonly string _dbPath;
    private readonly string _schemaPath;

    public DatabaseService(string dbPath = "storage/bank.db", string schemaPath = "storage/models.sql")
    {
        _dbPath = dbPath;
        _schemaPath = schemaPath;
        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private SqliteConnection CreateConnection()
    {
        try
        {
            var connection = new SqliteConnection($"Data Source={_dbPath}");
            return connection;
        }
        catch
        {
            // В C# лучше пробрасывать исключение, но для соответствия стилю Python возвращаем null,
            // который потом обрабатывается как ConnError.
            return null!;
        }
    }

    // ====================== Инициализация ======================
    public async Task<DbResult<bool>> InitDbAsync()
    {
        await using var connection = CreateConnection();
        if (connection == null)
            return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='users'";
            var tableExists = await cmd.ExecuteScalarAsync();

            if (tableExists != null)
                return DbResult<bool>.Ok(true); // уже инициализирована

            // Создаём таблицы из schema.sql
            var schemaSql = await File.ReadAllTextAsync(_schemaPath);
            cmd.CommandText = schemaSql;
            await cmd.ExecuteNonQueryAsync();

            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    // ====================== USERS ======================
    public async Task<DbResult<bool>> CreateUserAsync(long userId, string username)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO users (id, username, created_at) VALUES ($id, $username, $createdAt)";
            cmd.Parameters.AddWithValue("$id", userId);
            cmd.Parameters.AddWithValue("$username", username);
            cmd.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            await cmd.ExecuteNonQueryAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            return DbResult<bool>.Fail(ErrorCode.AlreadyExists);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<User>> GetUserAsync(long userId)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<User>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, username, created_at, COALESCE(blocked, 0) FROM users WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return DbResult<User>.Fail(ErrorCode.NotFound);

            var user = new User
            {
                Id = reader.GetInt64(0),
                Username = reader.IsDBNull(1) ? null : reader.GetString(1),
                CreatedAt = reader.GetInt64(2),
                Blocked = reader.GetInt32(3) != 0
            };
            return DbResult<User>.Ok(user);
        }
        catch (Exception ex)
        {
            return DbResult<User>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    // ====================== ACCOUNTS ======================
    public async Task<DbResult<bool>> CreateAccountAsync(long userId, string name, string accNumber)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO accounts (user_id, name, acc_number) VALUES ($userId, $name, $accNumber)";
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$accNumber", accNumber);

            await cmd.ExecuteNonQueryAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return DbResult<bool>.Fail(ErrorCode.AlreadyExists);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<Account>> GetAccountByAccNumberAsync(string accNumber)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<Account>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, user_id, name, acc_number, balance, pending, blocked, created_at
                FROM accounts WHERE acc_number = $accNumber";
            cmd.Parameters.AddWithValue("$accNumber", accNumber);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return DbResult<Account>.Fail(ErrorCode.NotFound);

            var acc = new Account
            {
                Id = reader.GetInt32(0),
                UserId = reader.GetInt64(1),
                Name = reader.GetString(2),
                AccNumber = reader.GetString(3),
                Balance = reader.GetDecimal(4),
                Pending = reader.GetDecimal(5),
                Blocked = reader.GetInt32(6) != 0,
                CreatedAt = reader.GetInt64(7)
            };
            return DbResult<Account>.Ok(acc);
        }
        catch (Exception ex)
        {
            return DbResult<Account>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<List<Account>>> GetAccountsByUserAsync(long userId)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<List<Account>>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, name, acc_number, balance, pending, blocked, created_at
                FROM accounts WHERE user_id = $userId";
            cmd.Parameters.AddWithValue("$userId", userId);

            var accounts = new List<Account>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                accounts.Add(new Account
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    AccNumber = reader.GetString(2),
                    Balance = reader.GetDecimal(3),
                    Pending = reader.GetDecimal(4),
                    Blocked = reader.GetInt32(5) != 0,
                    CreatedAt = reader.GetInt64(6),
                    UserId = userId
                });
            }

            return accounts.Count > 0
                ? DbResult<List<Account>>.Ok(accounts)
                : DbResult<List<Account>>.Fail(ErrorCode.NotFound);
        }
        catch (Exception ex)
        {
            return DbResult<List<Account>>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<Account>> GetMainAccountAsync(long userId)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<Account>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, name, acc_number, balance, pending, blocked, created_at
                FROM accounts WHERE user_id = $userId ORDER BY id LIMIT 1";
            cmd.Parameters.AddWithValue("$userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return DbResult<Account>.Fail(ErrorCode.NotFound);

            var acc = new Account
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                AccNumber = reader.GetString(2),
                Balance = reader.GetDecimal(3),
                Pending = reader.GetDecimal(4),
                Blocked = reader.GetInt32(5) != 0,
                CreatedAt = reader.GetInt64(6),
                UserId = userId
            };
            return DbResult<Account>.Ok(acc);
        }
        catch (Exception ex)
        {
            return DbResult<Account>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<Account>> GetAccountByNameAndUserAsync(string accountName, long userId)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<Account>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, name, acc_number, balance, pending, blocked, created_at
                FROM accounts WHERE name = $name AND user_id = $userId";
            cmd.Parameters.AddWithValue("$name", accountName);
            cmd.Parameters.AddWithValue("$userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return DbResult<Account>.Fail(ErrorCode.NotFound);

            var acc = new Account
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                AccNumber = reader.GetString(2),
                Balance = reader.GetDecimal(3),
                Pending = reader.GetDecimal(4),
                Blocked = reader.GetInt32(5) != 0,
                CreatedAt = reader.GetInt64(6),
                UserId = userId
            };
            return DbResult<Account>.Ok(acc);
        }
        catch (Exception ex)
        {
            return DbResult<Account>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<bool>> UpdateBalanceAsync(int accountId, decimal newBalance)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE accounts SET balance = $balance WHERE id = $id";
            cmd.Parameters.AddWithValue("$balance", newBalance);
            cmd.Parameters.AddWithValue("$id", accountId);

            await cmd.ExecuteNonQueryAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    // ====================== TRANSFER ======================
    public async Task<DbResult<bool>> TransferAsync(int fromAccountId, int toAccountId, decimal amount)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction as SqliteTransaction;

            // Проверка from
            cmd.CommandText = "SELECT balance, blocked FROM accounts WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", fromAccountId);
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    return DbResult<bool>.Fail(ErrorCode.NotFound, "From account not found");
                if (reader.GetInt32(1) != 0)
                    return DbResult<bool>.Fail(ErrorCode.InternalError, "From account is blocked");
                if (reader.GetDecimal(0) < amount)
                    return DbResult<bool>.Fail(ErrorCode.InternalError, "Insufficient funds");
            }

            // Проверка to
            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT blocked FROM accounts WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", toAccountId);
            var toBlockedObj = await cmd.ExecuteScalarAsync();
            if (toBlockedObj == null)
                return DbResult<bool>.Fail(ErrorCode.NotFound, "To account not found");
            if (Convert.ToInt32(toBlockedObj) != 0)
                return DbResult<bool>.Fail(ErrorCode.InternalError, "To account is blocked");

            // Списание
            cmd.Parameters.Clear();
            cmd.CommandText = "UPDATE accounts SET balance = balance - $amount WHERE id = $id";
            cmd.Parameters.AddWithValue("$amount", amount);
            cmd.Parameters.AddWithValue("$id", fromAccountId);
            await cmd.ExecuteNonQueryAsync();

            // Зачисление
            cmd.Parameters.Clear();
            cmd.CommandText = "UPDATE accounts SET balance = balance + $amount WHERE id = $id";
            cmd.Parameters.AddWithValue("$amount", amount);
            cmd.Parameters.AddWithValue("$id", toAccountId);
            await cmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    // ====================== PENDING ======================
    public async Task<DbResult<int>> AddPendingTransactionAsync(int accountId, decimal amount, string reason, int delayHours = 24)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<int>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction as SqliteTransaction;

            var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var releaseAt = createdAt + delayHours * 3600;

            cmd.CommandText = @"
                INSERT INTO pending (account_id, amount, created_at, release_at, reason)
                VALUES ($accountId, $amount, $createdAt, $releaseAt, $reason)";
            cmd.Parameters.AddWithValue("$accountId", accountId);
            cmd.Parameters.AddWithValue("$amount", amount);
            cmd.Parameters.AddWithValue("$createdAt", createdAt);
            cmd.Parameters.AddWithValue("$releaseAt", releaseAt);
            cmd.Parameters.AddWithValue("$reason", reason);
            await cmd.ExecuteNonQueryAsync();

            // Обновляем pending баланс
            cmd.Parameters.Clear();
            cmd.CommandText = "UPDATE accounts SET pending = pending + $amount WHERE id = $accountId";
            cmd.Parameters.AddWithValue("$amount", amount);
            cmd.Parameters.AddWithValue("$accountId", accountId);
            await cmd.ExecuteNonQueryAsync();

            // Получаем last_insert_rowid
            cmd.CommandText = "SELECT last_insert_rowid()";
            var rowId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            await transaction.CommitAsync();
            return DbResult<int>.Ok(rowId);
        }
        catch (Exception ex)
        {
            return DbResult<int>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<bool>> AddToPendingAsync(int accountId, decimal amount)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE accounts SET pending = pending + $amount WHERE id = $id";
            cmd.Parameters.AddWithValue("$amount", amount);
            cmd.Parameters.AddWithValue("$id", accountId);
            await cmd.ExecuteNonQueryAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<List<PendingTransaction>>> GetPendingTransactionsAsync()
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<List<PendingTransaction>>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT p.id, p.account_id, a.acc_number, u.username,
                       p.amount, p.created_at, p.release_at, p.reason
                FROM pending p
                JOIN accounts a ON p.account_id = a.id
                JOIN users u ON a.user_id = u.id
                ORDER BY p.release_at";

            var list = new List<PendingTransaction>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PendingTransaction
                {
                    Id = reader.GetInt32(0),
                    AccountId = reader.GetInt32(1),
                    AccNumber = reader.GetString(2),
                    Username = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Amount = reader.GetDecimal(4),
                    CreatedAt = reader.GetInt64(5),
                    ReleaseAt = reader.GetInt64(6),
                    Reason = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                });
            }

            return DbResult<List<PendingTransaction>>.Ok(list);
        }
        catch (Exception ex)
        {
            return DbResult<List<PendingTransaction>>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<bool>> ReleasePendingTransactionAsync(int pendingId)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction as SqliteTransaction;

            // Получаем данные pending
            cmd.CommandText = "SELECT account_id, amount FROM pending WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", pendingId);
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    return DbResult<bool>.Fail(ErrorCode.NotFound);
                var accountId = reader.GetInt32(0);
                var amount = reader.GetDecimal(1);

                // Удаляем pending
                cmd.Parameters.Clear();
                cmd.CommandText = "DELETE FROM pending WHERE id = $id";
                cmd.Parameters.AddWithValue("$id", pendingId);
                await cmd.ExecuteNonQueryAsync();

                // Обновляем баланс и pending счета
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    UPDATE accounts
                    SET pending = pending - $amount, balance = balance + $amount
                    WHERE id = $accountId";
                cmd.Parameters.AddWithValue("$amount", amount);
                cmd.Parameters.AddWithValue("$accountId", accountId);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    // ====================== ADMIN FUNCTIONS ======================
    public async Task<DbResult<bool>> BlockAccountAsync(string accNumber, bool block = true)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE accounts SET blocked = $blocked WHERE acc_number = $accNumber";
            cmd.Parameters.AddWithValue("$blocked", block ? 1 : 0);
            cmd.Parameters.AddWithValue("$accNumber", accNumber);
            await cmd.ExecuteNonQueryAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<bool>> BlockUserAsync(long userId, bool block = true)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction as SqliteTransaction;

            // Проверяем наличие колонки blocked в users (если нет — добавляем)
            await EnsureUserBlockedColumnAsync(connection, cmd);

            cmd.Parameters.Clear();
            cmd.CommandText = "UPDATE users SET blocked = $blocked WHERE id = $userId";
            cmd.Parameters.AddWithValue("$blocked", block ? 1 : 0);
            cmd.Parameters.AddWithValue("$userId", userId);
            await cmd.ExecuteNonQueryAsync();

            cmd.Parameters.Clear();
            cmd.CommandText = "UPDATE accounts SET blocked = $blocked WHERE user_id = $userId";
            cmd.Parameters.AddWithValue("$blocked", block ? 1 : 0);
            cmd.Parameters.AddWithValue("$userId", userId);
            await cmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    private async Task EnsureUserBlockedColumnAsync(SqliteConnection connection, SqliteCommand cmd)
    {
        cmd.CommandText = "PRAGMA table_info(users)";
        bool hasBlocked = false;
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1) == "blocked")
                {
                    hasBlocked = true;
                    break;
                }
            }
        }
        if (!hasBlocked)
        {
            cmd.CommandText = "ALTER TABLE users ADD COLUMN blocked INTEGER DEFAULT 0";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<DbResult<bool>> DeleteAccountAsync(string accNumber)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM accounts WHERE acc_number = $accNumber";
            cmd.Parameters.AddWithValue("$accNumber", accNumber);
            await cmd.ExecuteNonQueryAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<List<AdminUserInfo>>> GetAllUsersAsync()
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<List<AdminUserInfo>>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT u.id, u.username, u.created_at, 
                       COALESCE(u.blocked, 0) as blocked,
                       COUNT(a.id) as account_count,
                       COALESCE(SUM(a.balance), 0) as total_balance
                FROM users u
                LEFT JOIN accounts a ON u.id = a.user_id
                GROUP BY u.id
                ORDER BY u.created_at DESC";

            var list = new List<AdminUserInfo>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AdminUserInfo
                {
                    Id = reader.GetInt64(0),
                    Username = reader.IsDBNull(1) ? null : reader.GetString(1),
                    CreatedAt = reader.GetInt64(2),
                    Blocked = reader.GetInt32(3) != 0,
                    AccountCount = reader.GetInt32(4),
                    TotalBalance = reader.GetDecimal(5)
                });
            }
            return DbResult<List<AdminUserInfo>>.Ok(list);
        }
        catch (Exception ex)
        {
            return DbResult<List<AdminUserInfo>>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<List<AdminAccountInfo>>> GetAllAccountsAsync()
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<List<AdminAccountInfo>>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT a.id, a.user_id, u.username, a.name, 
                       a.acc_number, a.balance, a.pending, a.blocked, a.created_at
                FROM accounts a
                JOIN users u ON a.user_id = u.id
                ORDER BY a.created_at DESC";

            var list = new List<AdminAccountInfo>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AdminAccountInfo
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt64(1),
                    Username = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Name = reader.GetString(3),
                    AccNumber = reader.GetString(4),
                    Balance = reader.GetDecimal(5),
                    Pending = reader.GetDecimal(6),
                    Blocked = reader.GetInt32(7) != 0,
                    CreatedAt = reader.GetInt64(8)
                });
            }
            return DbResult<List<AdminAccountInfo>>.Ok(list);
        }
        catch (Exception ex)
        {
            return DbResult<List<AdminAccountInfo>>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<bool>> UpdateAccountBalanceAsync(string accNumber, decimal newBalance)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE accounts SET balance = $balance WHERE acc_number = $accNumber";
            cmd.Parameters.AddWithValue("$balance", newBalance);
            cmd.Parameters.AddWithValue("$accNumber", accNumber);
            await cmd.ExecuteNonQueryAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<AccountFullInfo>> GetAccountFullInfoAsync(string accNumber)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<AccountFullInfo>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT a.id, a.user_id, u.username, a.name, a.acc_number,
                       a.balance, a.pending, a.blocked, u.created_at,
                       (SELECT COUNT(*) FROM accounts a2 WHERE a2.user_id = a.user_id) as total_accounts
                FROM accounts a
                JOIN users u ON a.user_id = u.id
                WHERE a.acc_number = $accNumber";
            cmd.Parameters.AddWithValue("$accNumber", accNumber);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return DbResult<AccountFullInfo>.Fail(ErrorCode.NotFound);

            var info = new AccountFullInfo
            {
                Id = reader.GetInt32(0),
                UserId = reader.GetInt64(1),
                Username = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Name = reader.GetString(3),
                AccNumber = reader.GetString(4),
                Balance = reader.GetDecimal(5),
                Pending = reader.GetDecimal(6),
                Blocked = reader.GetInt32(7) != 0,
                UserCreated = reader.GetInt64(8),
                TotalAccounts = reader.GetInt32(9)
            };
            return DbResult<AccountFullInfo>.Ok(info);
        }
        catch (Exception ex)
        {
            return DbResult<AccountFullInfo>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    // ====================== GLOBAL PENDING RULES ======================
    public async Task<DbResult<int>> AddGlobalPendingRuleAsync(string? targetAcc, int delayHours, string reason, long? adminId)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<int>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO global_pending_rules (target_acc, delay_hours, reason, active, created_at, created_by)
                VALUES ($targetAcc, $delayHours, $reason, 1, $createdAt, $adminId)";
            cmd.Parameters.AddWithValue("$targetAcc", targetAcc as object ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$delayHours", delayHours);
            cmd.Parameters.AddWithValue("$reason", reason);
            cmd.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("$adminId", adminId as object ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = "SELECT last_insert_rowid()";
            var rowId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return DbResult<int>.Ok(rowId);
        }
        catch (Exception ex)
        {
            return DbResult<int>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<List<GlobalPendingRule>>> GetGlobalPendingRulesAsync()
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<List<GlobalPendingRule>>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT g.id, g.target_acc, g.delay_hours, g.reason, 
                       g.active, g.created_at, u.username
                FROM global_pending_rules g
                LEFT JOIN users u ON g.created_by = u.id
                ORDER BY g.created_at DESC";

            var list = new List<GlobalPendingRule>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new GlobalPendingRule
                {
                    Id = reader.GetInt32(0),
                    TargetAcc = reader.IsDBNull(1) ? null : reader.GetString(1),
                    DelayHours = reader.GetInt32(2),
                    Reason = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Active = reader.GetInt32(4) != 0,
                    CreatedAt = reader.GetInt64(5),
                    AdminUsername = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }
            return DbResult<List<GlobalPendingRule>>.Ok(list);
        }
        catch (Exception ex)
        {
            return DbResult<List<GlobalPendingRule>>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<bool>> ToggleGlobalPendingRuleAsync(int ruleId, bool active)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE global_pending_rules SET active = $active WHERE id = $id";
            cmd.Parameters.AddWithValue("$active", active ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", ruleId);
            await cmd.ExecuteNonQueryAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<bool>> DeleteGlobalPendingRuleAsync(int ruleId)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<bool>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM global_pending_rules WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", ruleId);
            await cmd.ExecuteNonQueryAsync();
            return DbResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return DbResult<bool>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }

    public async Task<DbResult<CheckPendingRuleResult>> CheckPendingRuleAsync(string targetAcc)
    {
        await using var connection = CreateConnection();
        if (connection == null) return DbResult<CheckPendingRuleResult>.Fail(ErrorCode.ConnError);

        try
        {
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT delay_hours, reason 
                FROM global_pending_rules 
                WHERE active = 1 AND (target_acc = $targetAcc OR target_acc IS NULL)
                ORDER BY target_acc DESC, created_at DESC
                LIMIT 1";
            cmd.Parameters.AddWithValue("$targetAcc", targetAcc);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return DbResult<CheckPendingRuleResult>.Ok(new CheckPendingRuleResult
                {
                    Applies = true,
                    DelayHours = reader.GetInt32(0),
                    Reason = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                });
            }
            return DbResult<CheckPendingRuleResult>.Ok(new CheckPendingRuleResult { Applies = false });
        }
        catch (Exception ex)
        {
            return DbResult<CheckPendingRuleResult>.Fail(ErrorCode.InternalError, ex.Message);
        }
    }
}

public class CheckPendingRuleResult
{
    public bool Applies { get; set; }
    public int DelayHours { get; set; }
    public string Reason { get; set; } = string.Empty;
}