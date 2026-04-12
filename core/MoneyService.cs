using System;
using System.Threading.Tasks;
using BankBot.Storage;

namespace BankBot.Core;

public class MoneyService
{
    private readonly DatabaseService _db;
    private static readonly Random _random = new();

    public MoneyService(DatabaseService db)
    {
        _db = db;
    }

    // ====================== UTILS ======================
    private string GenerateAccountNumber()
    {
        return $"ACC-{_random.Next(100_000_000, 999_999_999)}";
    }

    private string GenerateAccountNumberForUser(long userId)
    {
        return $"ACC-{userId}-{_random.Next(1000, 9999)}";
    }

    private async Task<bool> IsUserBlockedAsync(long userId)
    {
        var userResult = await _db.GetUserAsync(userId);
        return userResult.IsSuccess && userResult.Data.Blocked;
    }

    // Вспомогательный метод для обработки ошибок из БД
    private ErrorCode MapDbError(ErrorCode dbError)
    {
        // Прямое отображение, можно добавить преобразования если нужно
        return dbError;
    }

    // ====================== REGISTRATION ======================
    public async Task<DbResult<string>> RegisterAsync(long userId, string username)
    {
        // Создаём пользователя
        var createUserResult = await _db.CreateUserAsync(userId, username);
        if (createUserResult.Code == ErrorCode.AlreadyExists)
        {
            // Проверяем блокировку
            if (await IsUserBlockedAsync(userId))
                return DbResult<string>.Fail(ErrorCode.Blocked, "User is blocked");
            return DbResult<string>.Ok("already_registered");
        }
        if (!createUserResult.IsSuccess)
            return DbResult<string>.Fail(MapDbError(createUserResult.Code), createUserResult.ErrorMessage);

        // Создаём основной счёт
        var accNumber = GenerateAccountNumber();
        var createAccResult = await _db.CreateAccountAsync(userId, "main", accNumber);
        if (!createAccResult.IsSuccess)
            return DbResult<string>.Fail(MapDbError(createAccResult.Code), createAccResult.ErrorMessage);

        return DbResult<string>.Ok(accNumber);
    }

    // ====================== BALANCE ======================
    public async Task<DbResult<decimal>> GetBalanceAsync(string accNumber)
    {
        var accResult = await _db.GetAccountByAccNumberAsync(accNumber);
        if (!accResult.IsSuccess)
            return DbResult<decimal>.Fail(MapDbError(accResult.Code), accResult.ErrorMessage);

        if (accResult.Data.Blocked)
            return DbResult<decimal>.Fail(ErrorCode.Blocked, "Account is blocked");

        return DbResult<decimal>.Ok(accResult.Data.Balance);
    }

    // ====================== TRANSFER ======================
    public async Task<DbResult<object>> TransferAsync(string fromAcc, string toAcc, decimal amount)
    {
        if (amount <= 0)
            return DbResult<object>.Fail(ErrorCode.BadAmount, "Amount must be positive");

        var fromAccountResult = await _db.GetAccountByAccNumberAsync(fromAcc);
        if (!fromAccountResult.IsSuccess)
            return DbResult<object>.Fail(MapDbError(fromAccountResult.Code), fromAccountResult.ErrorMessage);
        var fromAccount = fromAccountResult.Data;
        if (fromAccount.Blocked)
            return DbResult<object>.Fail(ErrorCode.Blocked, "Sender account is blocked");

        var toAccountResult = await _db.GetAccountByAccNumberAsync(toAcc);
        if (!toAccountResult.IsSuccess)
            return DbResult<object>.Fail(MapDbError(toAccountResult.Code), toAccountResult.ErrorMessage);
        var toAccount = toAccountResult.Data;
        if (toAccount.Blocked)
            return DbResult<object>.Fail(ErrorCode.Blocked, "Recipient account is blocked");

        if (fromAccount.Balance < amount)
            return DbResult<object>.Fail(ErrorCode.NoFunds, "Insufficient funds");

        var ruleResult = await _db.CheckPendingRuleAsync(toAcc);
        if (!ruleResult.IsSuccess)
            return DbResult<object>.Fail(MapDbError(ruleResult.Code), ruleResult.ErrorMessage);

        if (ruleResult.Data.Applies)
        {
            int delayHours = ruleResult.Data.DelayHours;
            string reason = ruleResult.Data.Reason;

            var pendingResult = await _db.AddPendingTransactionAsync(
                toAccount.Id, amount, reason, delayHours);
            if (!pendingResult.IsSuccess)
                return DbResult<object>.Fail(MapDbError(pendingResult.Code), pendingResult.ErrorMessage);

            var updateFromResult = await _db.UpdateBalanceAsync(fromAccount.Id, fromAccount.Balance - amount);
            if (!updateFromResult.IsSuccess)
                return DbResult<object>.Fail(MapDbError(updateFromResult.Code), updateFromResult.ErrorMessage);

            var addPendingResult = await _db.AddToPendingAsync(toAccount.Id, amount);
            if (!addPendingResult.IsSuccess)
                return DbResult<object>.Fail(MapDbError(addPendingResult.Code), addPendingResult.ErrorMessage);

            var pendingInfo = new
            {
                delay_hours = delayHours,
                reason = reason,
                pending_id = pendingResult.Data
            };
            return DbResult<object>.Fail(ErrorCode.PendingTransfer, pendingInfo);
        }

        // Обычный перевод
        var transferResult = await _db.TransferAsync(fromAccount.Id, toAccount.Id, amount);
        if (!transferResult.IsSuccess)
            return DbResult<object>.Fail(MapDbError(transferResult.Code), transferResult.ErrorMessage);

        return DbResult<object>.Ok();
    }

    // ====================== CREATE ACCOUNT ======================
    public async Task<DbResult<string>> CreateAccountAsync(long userId, string accountName)
    {
        if (await IsUserBlockedAsync(userId))
            return DbResult<string>.Fail(ErrorCode.Blocked, "User is blocked");

        var accNumber = GenerateAccountNumberForUser(userId);
        var result = await _db.CreateAccountAsync(userId, accountName, accNumber);
        if (!result.IsSuccess)
            return DbResult<string>.Fail(MapDbError(result.Code), result.ErrorMessage);

        return DbResult<string>.Ok(accNumber);
    }

    // ====================== ADMIN FUNCTIONS ======================
    public async Task<DbResult<bool>> AdminBlockUserAsync(long userId, bool block = true)
    {
        var result = await _db.BlockUserAsync(userId, block);
        return result.IsSuccess
            ? DbResult<bool>.Ok(true)
            : DbResult<bool>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }

    public async Task<DbResult<bool>> AdminBlockAccountAsync(string accNumber, bool block = true)
    {
        var result = await _db.BlockAccountAsync(accNumber, block);
        return result.IsSuccess
            ? DbResult<bool>.Ok(true)
            : DbResult<bool>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }

    public async Task<DbResult<bool>> AdminDeleteAccountAsync(string accNumber)
    {
        var result = await _db.DeleteAccountAsync(accNumber);
        return result.IsSuccess
            ? DbResult<bool>.Ok(true)
            : DbResult<bool>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }

    public async Task<DbResult<bool>> AdminUpdateBalanceAsync(string accNumber, decimal newBalance)
    {
        var result = await _db.UpdateAccountBalanceAsync(accNumber, newBalance);
        return result.IsSuccess
            ? DbResult<bool>.Ok(true)
            : DbResult<bool>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }

    public async Task<DbResult<int>> AdminAddPendingRuleAsync(string? targetAcc, int delayHours, string reason, long? adminId)
    {
        var result = await _db.AddGlobalPendingRuleAsync(targetAcc, delayHours, reason, adminId);
        return result.IsSuccess
            ? DbResult<int>.Ok(result.Data)
            : DbResult<int>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }

    public async Task<DbResult<List<GlobalPendingRule>>> AdminGetPendingRulesAsync()
    {
        var result = await _db.GetGlobalPendingRulesAsync();
        return result.IsSuccess
            ? DbResult<List<GlobalPendingRule>>.Ok(result.Data)
            : DbResult<List<GlobalPendingRule>>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }

    public async Task<DbResult<List<PendingTransaction>>> AdminGetPendingTransactionsAsync()
    {
        var result = await _db.GetPendingTransactionsAsync();
        return result.IsSuccess
            ? DbResult<List<PendingTransaction>>.Ok(result.Data)
            : DbResult<List<PendingTransaction>>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }

    public async Task<DbResult<bool>> AdminReleasePendingAsync(int pendingId)
    {
        var result = await _db.ReleasePendingTransactionAsync(pendingId);
        return result.IsSuccess
            ? DbResult<bool>.Ok(true)
            : DbResult<bool>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }

    public async Task<DbResult<List<AdminUserInfo>>> AdminGetAllUsersAsync()
    {
        var result = await _db.GetAllUsersAsync();
        return result.IsSuccess
            ? DbResult<List<AdminUserInfo>>.Ok(result.Data)
            : DbResult<List<AdminUserInfo>>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }

    public async Task<DbResult<List<AdminAccountInfo>>> AdminGetAllAccountsAsync()
    {
        var result = await _db.GetAllAccountsAsync();
        return result.IsSuccess
            ? DbResult<List<AdminAccountInfo>>.Ok(result.Data)
            : DbResult<List<AdminAccountInfo>>.Fail(MapDbError(result.Code), result.ErrorMessage);
    }
}