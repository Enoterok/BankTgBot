using System.Collections.Concurrent;

namespace BankBot;

// Состояния FSM
public enum TransferState
{
    None,
    FromAccount,
    ToAccount,
    Amount
}

public enum CreateAccountState
{
    None,
    Name
}

public enum AdminBlockUserState
{
    None,
    UserId,
    Confirm
}

public enum AdminBlockAccountState
{
    None,
    AccountNumber,
    Confirm
}

public enum AdminDeleteAccountState
{
    None,
    AccountNumber,
    Confirm
}

public enum AdminUpdateBalanceState
{
    None,
    AccountNumber,
    Amount
}

public enum AdminAddPendingState
{
    None,
    TargetAccount,
    DelayHours,
    Reason
}

public enum AdminGlobalPendingState
{
    None,
    DelayHours,
    Reason
}

// Модель данных состояния пользователя
public class UserStateData
{
    public string? FromAccount { get; set; }
    public string? ToAccount { get; set; }
    public string? AccountName { get; set; }
    public string? TargetAccount { get; set; }
    public int DelayHours { get; set; }
    public string? Reason { get; set; }
    public string? Action { get; set; } // "block", "unblock" и т.д.
    public long TargetUserId { get; set; }
    public string? AccountNumber { get; set; }

    // Флаги состояний
    public TransferState TransferState { get; set; }
    public CreateAccountState CreateAccountState { get; set; }
    public AdminBlockUserState AdminBlockUserState { get; set; }
    public AdminBlockAccountState AdminBlockAccountState { get; set; }
    public AdminDeleteAccountState AdminDeleteAccountState { get; set; }
    public AdminUpdateBalanceState AdminUpdateBalanceState { get; set; }
    public AdminAddPendingState AdminAddPendingState { get; set; }
    public AdminGlobalPendingState AdminGlobalPendingState { get; set; }

    // Вспомогательный метод очистки
    public void Clear()
    {
        TransferState = TransferState.None;
        CreateAccountState = CreateAccountState.None;
        AdminBlockUserState = AdminBlockUserState.None;
        AdminBlockAccountState = AdminBlockAccountState.None;
        AdminDeleteAccountState = AdminDeleteAccountState.None;
        AdminUpdateBalanceState = AdminUpdateBalanceState.None;
        AdminAddPendingState = AdminAddPendingState.None;
        AdminGlobalPendingState = AdminGlobalPendingState.None;

        FromAccount = null;
        ToAccount = null;
        AccountName = null;
        TargetAccount = null;
        DelayHours = 0;
        Reason = null;
        Action = null;
        TargetUserId = 0;
        AccountNumber = null;
    }
}

// Глобальное хранилище состояний
public static class UserStateStorage
{
    private static readonly ConcurrentDictionary<long, UserStateData> _states = new();

    public static UserStateData GetOrCreate(long userId)
    {
        return _states.GetOrAdd(userId, _ => new UserStateData());
    }

    public static void Clear(long userId)
    {
        if (_states.TryGetValue(userId, out var state))
            state.Clear();
    }

    public static void Remove(long userId)
    {
        _states.TryRemove(userId, out _);
    }
}