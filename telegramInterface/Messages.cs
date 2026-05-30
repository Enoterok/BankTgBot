using System;
using System.IO;
using System.Text.Json;

namespace BankBot;

public static class Messages
{
    // Язык по умолчанию. Меняй на "en" для английского.
    public static string Language { get; set; } = "ru";

    private static readonly Lazy<JsonElement> _root = new(() =>
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "messages.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Messages file not found: {path}");

        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json).RootElement;
    });

    private static string Get(string key)
    {
        var root = _root.Value;

        // Пробуем текущий язык
        if (root.TryGetProperty(Language, out var langSection) &&
            langSection.TryGetProperty(key, out var value))
            return value.GetString() ?? $"[{key}]";

        // Fallback на русский если текущий язык не нашёл ключ
        if (Language != "ru" &&
            root.TryGetProperty("ru", out var ruSection) &&
            ruSection.TryGetProperty(key, out var ruValue))
            return ruValue.GetString() ?? $"[{key}]";

        return $"[{key}]";
    }

    public static string StartWelcome => Get("StartWelcome");
    public static string StartWelcomeBack => Get("StartWelcomeBack");
    public static string MainMenuTitle => Get("MainMenuTitle");
    public static string HelpText => Get("HelpText");
    public static string NoAccounts => Get("NoAccounts");
    public static string SelectAccountForBalance => Get("SelectAccountForBalance");
    public static string NoAccountsForTransfer => Get("NoAccountsForTransfer");
    public static string TransferInstruction => Get("TransferInstruction");
    public static string TransferSelectFrom => Get("TransferSelectFrom");
    public static string EnterAccountName => Get("EnterAccountName");
    public static string AdminNoAccess => Get("AdminNoAccess");
    public static string AdminPanelTitle => Get("AdminPanelTitle");
    public static string OperationCanceled => Get("OperationCanceled");
    public static string HistoryInDevelopment => Get("HistoryInDevelopment");
    public static string CreateAccountPrompt => Get("CreateAccountPrompt");
    public static string MyAccountsNoAccounts => Get("MyAccountsNoAccounts");
    public static string MyAccountsTitle => Get("MyAccountsTitle");
    public static string AccountBlockedStatus => Get("AccountBlockedStatus");
    public static string AccountActiveStatus => Get("AccountActiveStatus");
    public static string UserBlockedStatus => Get("UserBlockedStatus");
    public static string UserActiveStatus => Get("UserActiveStatus");
    public static string PendingRuleGlobal => Get("PendingRuleGlobal");
    public static string PendingRuleActive => Get("PendingRuleActive");
    public static string PendingRuleInactive => Get("PendingRuleInactive");
    public static string PendingRulesTitle => Get("PendingRulesTitle");
    public static string PendingTransactionsTitle => Get("PendingTransactionsTitle");
    public static string NoPendingRules => Get("NoPendingRules");
    public static string NoPendingTransactions => Get("NoPendingTransactions");
    public static string ConfirmBlockUser => Get("ConfirmBlockUser");
    public static string ConfirmUnblockUser => Get("ConfirmUnblockUser");
    public static string ConfirmBlockAccount => Get("ConfirmBlockAccount");
    public static string ConfirmUnblockAccount => Get("ConfirmUnblockAccount");
    public static string ConfirmDeleteAccount => Get("ConfirmDeleteAccount");
    public static string UserBlockedWarning => Get("UserBlockedWarning");
    public static string UserUnblockedWarning => Get("UserUnblockedWarning");
    public static string AccountBlockedWarning => Get("AccountBlockedWarning");
    public static string AccountUnblockedWarning => Get("AccountUnblockedWarning");
    public static string DeleteAccountWarning => Get("DeleteAccountWarning");
    public static string UpdateBalancePrompt => Get("UpdateBalancePrompt");
    public static string AddPendingRulePrompt => Get("AddPendingRulePrompt");
    public static string GlobalPendingRulePrompt => Get("GlobalPendingRulePrompt");
    public static string Error => Get("Error");
    public static string Success => Get("Success");
    public static string AccessDenied => Get("AccessDenied");
    public static string InvalidAccountFormat => Get("InvalidAccountFormat");
    public static string AccountNotFound => Get("AccountNotFound");
    public static string AccountBlocked => Get("AccountBlocked");
    public static string InsufficientFunds => Get("InsufficientFunds");
    public static string InvalidAmount => Get("InvalidAmount");
    public static string UserNotFound => Get("UserNotFound");
    public static string TransferSuccess => Get("TransferSuccess");
    public static string TransferPending => Get("TransferPending");
    public static string AccountCreated => Get("AccountCreated");
    public static string UserBlockedByAdmin => Get("UserBlockedByAdmin");
    public static string SelectTransferAccount => Get("SelectTransferAccount");
    public static string Yes => Get("Yes");
    public static string No => Get("No");
    public static string Cancel => Get("Cancel");
    public static string Back => Get("Back");
    public static string BackToMenu => Get("BackToMenu");
    public static string BackToAdmin => Get("BackToAdmin");
    public static string Balance => Get("Balance");
    public static string Transfer => Get("Transfer");
    public static string NewAccount => Get("NewAccount");
    public static string MyAccounts => Get("MyAccounts");
    public static string History => Get("History");
    public static string AdminPanel => Get("AdminPanel");
    public static string AllUsers => Get("AllUsers");
    public static string AllAccounts => Get("AllAccounts");
    public static string BlockUser => Get("BlockUser");
    public static string UnblockUser => Get("UnblockUser");
    public static string BlockAccount => Get("BlockAccount");
    public static string UnblockAccount => Get("UnblockAccount");
    public static string DeleteAccount => Get("DeleteAccount");
    public static string UpdateBalance => Get("UpdateBalance");
    public static string AddPendingRule => Get("AddPendingRule");
    public static string GlobalPendingRule => Get("GlobalPendingRule");
    public static string PendingRulesList => Get("PendingRulesList");
    public static string PendingTransactions => Get("PendingTransactions");
}