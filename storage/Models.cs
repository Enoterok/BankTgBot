namespace BankBot.Storage;

public class User
{
    public long Id { get; set; }
    public string? Username { get; set; }
    public long CreatedAt { get; set; }
    public bool Blocked { get; set; }
}

public class Account
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal Pending { get; set; }
    public bool Blocked { get; set; }
    public long CreatedAt { get; set; }
}

public class AccountFullInfo
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal Pending { get; set; }
    public bool Blocked { get; set; }
    public long UserCreated { get; set; }
    public int TotalAccounts { get; set; }
}

public class PendingTransaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string AccNumber { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public long CreatedAt { get; set; }
    public long ReleaseAt { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class GlobalPendingRule
{
    public int Id { get; set; }
    public string? TargetAcc { get; set; }
    public int DelayHours { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool Active { get; set; }
    public long CreatedAt { get; set; }
    public string? AdminUsername { get; set; }
}

public class AdminUserInfo
{
    public long Id { get; set; }
    public string? Username { get; set; }
    public long CreatedAt { get; set; }
    public bool Blocked { get; set; }
    public int AccountCount { get; set; }
    public decimal TotalBalance { get; set; }
}

public class AdminAccountInfo
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal Pending { get; set; }
    public bool Blocked { get; set; }
    public long CreatedAt { get; set; }
}