namespace BankBot.Storage;

public enum ErrorCode
{
    Ok = 0,
    ConnError = 1,
    NotFound = 2,
    AlreadyExists = 3,
    InternalError = 4,

    UserNotFound = 100,
    AccountNotFound = 101,
    Blocked = 102,
    NoFunds = 103,
    BadAmount = 104,
    PendingTransfer = 105
}


public class DbResult<T>
{
    public ErrorCode Code { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsSuccess => Code == ErrorCode.Ok;

    public static DbResult<T> Ok(T data) =>
        new() { Code = ErrorCode.Ok, Data = data };

    public static DbResult<T> Ok() =>
        new() { Code = ErrorCode.Ok };

    public static DbResult<T> Fail(ErrorCode code, string? message = null) =>
        new() { Code = code, ErrorMessage = message };
    public static DbResult<T> Fail(ErrorCode code, T data, string? message = null) =>
        new() { Code = code, Data = data, ErrorMessage = message };
}