using System.Text;
using BankBot.Storage;

namespace BankBot;

public static class MessageFormatter
{
    public static string FormatAccounts(List<Account> accounts)
    {
        if (accounts == null || accounts.Count == 0)
            return Messages.NoAccounts;

        var sb = new StringBuilder();
        foreach (var acc in accounts)
        {
            string status = acc.Blocked ? Messages.AccountBlockedStatus : Messages.AccountActiveStatus;
            sb.AppendLine($"💳 <b>{acc.Name}</b> {status}");
            sb.AppendLine($"📟 Номер: {acc.AccNumber}");
            sb.AppendLine($"💰 Баланс: {acc.Balance:F2}");
            sb.AppendLine($"⏳ В удержании: {acc.Pending:F2}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string FormatBalance(Account account)
    {
        string status = account.Blocked ? Messages.AccountBlockedStatus : Messages.AccountActiveStatus;
        return $"💰 <b>Баланс счета</b>\n\n" +
               $"💳 Счет: <b>{account.Name}</b>\n" +
               $"📟 Номер: <code>{account.AccNumber}</code>\n" +
               $"💵 Баланс: {account.Balance:F2}\n" +
               $"⏳ В удержании: {account.Pending:F2}\n" +
               $"📊 Статус: {status}";
    }

    public static string FormatUserList(List<AdminUserInfo> users)
    {
        if (users == null || users.Count == 0)
            return Messages.NoAccounts;

        var sb = new StringBuilder("👥 <b>Список пользователей:</b>\n\n");
        int i = 1;
        foreach (var user in users)
        {
            string status = user.Blocked ? Messages.UserBlockedStatus : Messages.UserActiveStatus;
            sb.AppendLine($"{i}. <b>{user.Username ?? "Без имени"}</b> {status}");
            sb.AppendLine($"   🆔 ID: <code>{user.Id}</code>");
            sb.AppendLine($"   📅 Регистрация: <code>{user.CreatedAt}</code>");
            sb.AppendLine($"   💳 Счетов: {user.AccountCount}");
            sb.AppendLine($"   💰 Общий баланс: {user.TotalBalance:F2}\n");
            i++;
        }
        return sb.ToString();
    }

    public static string FormatAccountList(List<AdminAccountInfo> accounts)
    {
        if (accounts == null || accounts.Count == 0)
            return Messages.NoAccounts;

        var sb = new StringBuilder("💳 <b>Список всех счетов:</b>\n\n");
        int i = 1;
        foreach (var acc in accounts)
        {
            string status = acc.Blocked ? Messages.AccountBlockedStatus : Messages.AccountActiveStatus;
            sb.AppendLine($"{i}. <b>{acc.Name}</b> {status}");
            sb.AppendLine($"   📟 Номер: <code>{acc.AccNumber}</code>");
            sb.AppendLine($"   👤 Владелец: {acc.Username} (ID: {acc.UserId})");
            sb.AppendLine($"   💰 Баланс: {acc.Balance:F2}");
            sb.AppendLine($"   ⏳ В удержании: {acc.Pending:F2}\n");
            i++;
        }
        return sb.ToString();
    }

    public static string FormatPendingRules(List<GlobalPendingRule> rules)
    {
        if (rules == null || rules.Count == 0)
            return Messages.NoPendingRules;

        var sb = new StringBuilder(Messages.PendingRulesTitle + "\n\n");
        int i = 1;
        foreach (var rule in rules)
        {
            string target = rule.TargetAcc ?? Messages.PendingRuleGlobal;
            string status = rule.Active ? Messages.PendingRuleActive : Messages.PendingRuleInactive;
            sb.AppendLine($"{i}. Правило #{rule.Id} {status}");
            sb.AppendLine($"   🎯 Цель: {target}");
            sb.AppendLine($"   ⏱️ Задержка: {rule.DelayHours} часов");
            sb.AppendLine($"   📝 Причина: {rule.Reason}");
            sb.AppendLine($"   👤 Создал: {rule.AdminUsername ?? "Система"}\n");
            i++;
        }
        return sb.ToString();
    }

    public static string FormatPendingTransactions(List<PendingTransaction> transactions)
    {
        if (transactions == null || transactions.Count == 0)
            return Messages.NoPendingTransactions;

        var sb = new StringBuilder(Messages.PendingTransactionsTitle + "\n\n");
        int i = 1;
        foreach (var trans in transactions)
        {
            sb.AppendLine($"{i}. Транзакция #{trans.Id}");
            sb.AppendLine($"   💳 Счет: <code>{trans.AccNumber}</code>");
            sb.AppendLine($"   👤 Пользователь: {trans.Username}");
            sb.AppendLine($"   💰 Сумма: {trans.Amount:F2}");
            sb.AppendLine($"   📝 Причина: {trans.Reason}");
            sb.AppendLine($"   🕐 Освободится: <code>{trans.ReleaseAt}</code>\n");
            i++;
        }
        return sb.ToString();
    }
}