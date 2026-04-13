using BankBot.Core;
using BankBot.Storage;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BankBot;

public class ConsoleInterface
{
    private readonly DatabaseService _db;
    private readonly MoneyService _money;

    public ConsoleInterface(DatabaseService db, MoneyService money)
    {
        _db = db;
        _money = money;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("=== Bank Bot Console Admin ===");
        Console.WriteLine("Type 'help' for commands, 'exit' to quit.");

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            await ProcessCommand(input);
        }
    }

    private async Task ProcessCommand(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var command = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        try
        {
            switch (command)
            {
                case "help":
                    ShowHelp();
                    break;
                case "users":
                    await ShowUsers();
                    break;
                case "accounts":
                    await ShowAccounts();
                    break;
                case "balance":
                    await ShowBalance(args);
                    break;
                case "block":
                    await BlockCommand(args);
                    break;
                case "unblock":
                    await UnblockCommand(args);
                    break;
                case "setbalance":
                    await SetBalance(args);
                    break;
                case "pending":
                    await PendingCommand(args);
                    break;
                case "addrule":
                    await AddPendingRule(args);
                    break;
                case "release":
                    await ReleasePending(args);
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine(@"
Available commands:
  users                              - list all users
  accounts                           - list all accounts
  balance <acc_number>               - show account balance
  block user <id>                    - block user
  unblock user <id>                  - unblock user
  block account <acc_number>         - block account
  unblock account <acc_number>       - unblock account
  setbalance <acc_number> <amount>   - set account balance
  pending rules                      - list pending rules
  pending transactions                - list pending transactions
  addrule [target_acc] <hours> <reason> - add pending rule (empty target = global)
  release <pending_id>               - release pending transaction
  exit                               - quit console
");
    }

    private async Task ShowUsers()
    {
        var result = await _db.GetAllUsersAsync();
        if (!result.IsSuccess)
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
            return;
        }

        foreach (var u in result.Data)
        {
            Console.WriteLine($"[{u.Id}] {u.Username ?? "N/A"} | Created: {u.CreatedAt} | Blocked: {u.Blocked} | Accounts: {u.AccountCount} | Balance: {u.TotalBalance:F2}");
        }
    }

    private async Task ShowAccounts()
    {
        var result = await _db.GetAllAccountsAsync();
        if (!result.IsSuccess)
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
            return;
        }

        foreach (var a in result.Data)
        {
            Console.WriteLine($"{a.AccNumber} | {a.Name} | User: {a.Username} ({a.UserId}) | Balance: {a.Balance:F2} | Pending: {a.Pending:F2} | Blocked: {a.Blocked}");
        }
    }

    private async Task ShowBalance(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: balance <acc_number>");
            return;
        }

        var acc = args[0];
        var result = await _db.GetAccountByAccNumberAsync(acc);
        if (!result.IsSuccess)
        {
            Console.WriteLine($"Account not found: {acc}");
            return;
        }

        var a = result.Data;
        Console.WriteLine($"Account: {a.Name} ({a.AccNumber})");
        Console.WriteLine($"Owner: {a.UserId}");
        Console.WriteLine($"Balance: {a.Balance:F2}");
        Console.WriteLine($"Pending: {a.Pending:F2}");
        Console.WriteLine($"Blocked: {a.Blocked}");
    }

    private async Task BlockCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: block user <id> | block account <acc_number>");
            return;
        }

        var type = args[0].ToLowerInvariant();
        var target = args[1];

        if (type == "user")
        {
            if (!long.TryParse(target, out var userId))
            {
                Console.WriteLine("Invalid user ID");
                return;
            }
            var result = await _db.BlockUserAsync(userId, true);
            Console.WriteLine(result.IsSuccess ? $"User {userId} blocked." : $"Error: {result.ErrorMessage}");
        }
        else if (type == "account")
        {
            var result = await _db.BlockAccountAsync(target, true);
            Console.WriteLine(result.IsSuccess ? $"Account {target} blocked." : $"Error: {result.ErrorMessage}");
        }
        else
        {
            Console.WriteLine("Unknown block target. Use 'user' or 'account'.");
        }
    }

    private async Task UnblockCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: unblock user <id> | unblock account <acc_number>");
            return;
        }

        var type = args[0].ToLowerInvariant();
        var target = args[1];

        if (type == "user")
        {
            if (!long.TryParse(target, out var userId))
            {
                Console.WriteLine("Invalid user ID");
                return;
            }
            var result = await _db.BlockUserAsync(userId, false);
            Console.WriteLine(result.IsSuccess ? $"User {userId} unblocked." : $"Error: {result.ErrorMessage}");
        }
        else if (type == "account")
        {
            var result = await _db.BlockAccountAsync(target, false);
            Console.WriteLine(result.IsSuccess ? $"Account {target} unblocked." : $"Error: {result.ErrorMessage}");
        }
        else
        {
            Console.WriteLine("Unknown unblock target. Use 'user' or 'account'.");
        }
    }

    private async Task SetBalance(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: setbalance <acc_number> <new_balance>");
            return;
        }

        var acc = args[0];
        if (!decimal.TryParse(args[1], out var newBalance))
        {
            Console.WriteLine("Invalid amount");
            return;
        }

        var result = await _db.UpdateAccountBalanceAsync(acc, newBalance);
        Console.WriteLine(result.IsSuccess ? $"Balance updated to {newBalance:F2}" : $"Error: {result.ErrorMessage}");
    }

    private async Task PendingCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: pending rules | pending transactions");
            return;
        }

        var sub = args[0].ToLowerInvariant();
        if (sub == "rules")
        {
            var rules = await _db.GetGlobalPendingRulesAsync();
            if (!rules.IsSuccess)
            {
                Console.WriteLine($"Error: {rules.ErrorMessage}");
                return;
            }
            foreach (var r in rules.Data)
            {
                Console.WriteLine($"ID:{r.Id} | Target:{r.TargetAcc ?? "GLOBAL"} | Delay:{r.DelayHours}h | Active:{r.Active} | Reason:{r.Reason}");
            }
        }
        else if (sub == "transactions")
        {
            var trans = await _db.GetPendingTransactionsAsync();
            if (!trans.IsSuccess)
            {
                Console.WriteLine($"Error: {trans.ErrorMessage}");
                return;
            }
            foreach (var t in trans.Data)
            {
                Console.WriteLine($"ID:{t.Id} | Acc:{t.AccNumber} | Amount:{t.Amount:F2} | ReleaseAt:{t.ReleaseAt} | Reason:{t.Reason}");
            }
        }
        else
        {
            Console.WriteLine("Unknown pending subcommand.");
        }
    }

    private async Task AddPendingRule(string[] args)
    {
        // args: [target_acc] <hours> <reason>
        // Если первый аргумент начинается с "ACC-" или "GLOBAL", это target, иначе считаем что сразу hours
        string? target = null;
        int hours;
        string reason;

        if (args.Length >= 2 && args[0].StartsWith("ACC-", StringComparison.OrdinalIgnoreCase))
        {
            target = args[0];
            if (!int.TryParse(args[1], out hours) || hours <= 0)
            {
                Console.WriteLine("Invalid hours");
                return;
            }
            reason = string.Join(" ", args.Skip(2));
        }
        else if (args.Length >= 2)
        {
            if (!int.TryParse(args[0], out hours) || hours <= 0)
            {
                Console.WriteLine("Invalid hours");
                return;
            }
            reason = string.Join(" ", args.Skip(1));
        }
        else
        {
            Console.WriteLine("Usage: addrule [target_acc] <hours> <reason>");
            Console.WriteLine("Example: addrule ACC-123456789 48 Suspicious activity");
            Console.WriteLine("Example: addrule 24 Global delay reason");
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            Console.WriteLine("Reason cannot be empty");
            return;
        }

        var result = await _db.AddGlobalPendingRuleAsync(target, hours, reason, null);
        Console.WriteLine(result.IsSuccess ? $"Rule added with ID {result.Data}" : $"Error: {result.ErrorMessage}");
    }

    private async Task ReleasePending(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var pendingId))
        {
            Console.WriteLine("Usage: release <pending_id>");
            return;
        }

        var result = await _db.ReleasePendingTransactionAsync(pendingId);
        Console.WriteLine(result.IsSuccess ? "Pending transaction released." : $"Error: {result.ErrorMessage}");
    }
}