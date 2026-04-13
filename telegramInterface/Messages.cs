using System;
using System.IO;
using System.Text.Json;

namespace BankBot;

public static class Messages
{
    private static readonly Lazy<JsonElement> _root = new(() =>
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "messages.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Messages file not found: {path}");

        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json).RootElement;
    });

    private static string GetString(string key)
    {
        var parts = key.Split(':');
        var current = _root.Value;
        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out current))
                return $"[{key}]";
        }
        return current.GetString() ?? $"[{key}]";
    }

    // Свойства для доступа к сообщениям
    public static string StartWelcome => GetString("Bot:StartWelcome");
    public static string StartWelcomeBack => GetString("Bot:StartWelcomeBack");
    public static string MainMenuTitle => GetString("Bot:MainMenuTitle");
    public static string HelpText => GetString("Bot:HelpText");
    public static string NoAccounts => GetString("Bot:NoAccounts");
    public static string SelectAccountForBalance => GetString("Bot:SelectAccountForBalance");
    public static string NoAccountsForTransfer => GetString("Bot:NoAccountsForTransfer");
    public static string TransferInstruction => GetString("Bot:TransferInstruction");
    public static string TransferSelectFrom => GetString("Bot:TransferSelectFrom");
    public static string EnterAccountName => GetString("Bot:EnterAccountName");
    public static string AdminNoAccess => GetString("Bot:AdminNoAccess");
    public static string AdminPanelTitle => GetString("Bot:AdminPanelTitle");
    public static string OperationCanceled => GetString("Bot:OperationCanceled");
    public static string HistoryInDevelopment => GetString("Bot:HistoryInDevelopment");
    public static string CreateAccountPrompt => GetString("Bot:CreateAccountPrompt");
    public static string MyAccountsNoAccounts => GetString("Bot:MyAccountsNoAccounts");
    public static string MyAccountsTitle => GetString("Bot:MyAccountsTitle");
    public static string AccountBlockedStatus => GetString("Bot:AccountBlockedStatus");
    public static string AccountActiveStatus => GetString("Bot:AccountActiveStatus");
    public static string UserBlockedStatus => GetString("Bot:UserBlockedStatus");
    public static string UserActiveStatus => GetString("Bot:UserActiveStatus");
    public static string PendingRuleGlobal => GetString("Bot:PendingRuleGlobal");
    public static string PendingRuleActive => GetString("Bot:PendingRuleActive");
    public static string PendingRuleInactive => GetString("Bot:PendingRuleInactive");
    public static string PendingRulesTitle => GetString("Bot:PendingRulesTitle");
    public static string PendingTransactionsTitle => GetString("Bot:PendingTransactionsTitle");
    public static string NoPendingRules => GetString("Bot:NoPendingRules");
    public static string NoPendingTransactions => GetString("Bot:NoPendingTransactions");
    public static string ConfirmBlockUser => GetString("Bot:ConfirmBlockUser");
    public static string ConfirmUnblockUser => GetString("Bot:ConfirmUnblockUser");
    public static string ConfirmBlockAccount => GetString("Bot:ConfirmBlockAccount");
    public static string ConfirmUnblockAccount => GetString("Bot:ConfirmUnblockAccount");
    public static string ConfirmDeleteAccount => GetString("Bot:ConfirmDeleteAccount");
    public static string UserBlockedWarning => GetString("Bot:UserBlockedWarning");
    public static string UserUnblockedWarning => GetString("Bot:UserUnblockedWarning");
    public static string AccountBlockedWarning => GetString("Bot:AccountBlockedWarning");
    public static string AccountUnblockedWarning => GetString("Bot:AccountUnblockedWarning");
    public static string DeleteAccountWarning => GetString("Bot:DeleteAccountWarning");
    public static string UpdateBalancePrompt => GetString("Bot:UpdateBalancePrompt");
    public static string AddPendingRulePrompt => GetString("Bot:AddPendingRulePrompt");
    public static string GlobalPendingRulePrompt => GetString("Bot:GlobalPendingRulePrompt");
    public static string Error => GetString("Bot:Error");
    public static string Success => GetString("Bot:Success");
    public static string AccessDenied => GetString("Bot:AccessDenied");
    public static string InvalidAccountFormat => GetString("Bot:InvalidAccountFormat");
    public static string AccountNotFound => GetString("Bot:AccountNotFound");
    public static string AccountBlocked => GetString("Bot:AccountBlocked");
    public static string InsufficientFunds => GetString("Bot:InsufficientFunds");
    public static string InvalidAmount => GetString("Bot:InvalidAmount");
    public static string UserNotFound => GetString("Bot:UserNotFound");
    public static string TransferSuccess => GetString("Bot:TransferSuccess");
    public static string TransferPending => GetString("Bot:TransferPending");
    public static string AccountCreated => GetString("Bot:AccountCreated");
    public static string UserBlockedByAdmin => GetString("Bot:UserBlockedByAdmin");
    public static string SelectTransferAccount => GetString("Bot:SelectTransferAccount");
    public static string Yes => GetString("Bot:Yes");
    public static string No => GetString("Bot:No");
    public static string Cancel => GetString("Bot:Cancel");
    public static string Back => GetString("Bot:Back");
    public static string BackToMenu => GetString("Bot:BackToMenu");
    public static string BackToAdmin => GetString("Bot:BackToAdmin");
    public static string Balance => GetString("Bot:Balance");
    public static string Transfer => GetString("Bot:Transfer");
    public static string NewAccount => GetString("Bot:NewAccount");
    public static string MyAccounts => GetString("Bot:MyAccounts");
    public static string History => GetString("Bot:History");
    public static string AdminPanel => GetString("Bot:AdminPanel");
    public static string AllUsers => GetString("Bot:AllUsers");
    public static string AllAccounts => GetString("Bot:AllAccounts");
    public static string BlockUser => GetString("Bot:BlockUser");
    public static string UnblockUser => GetString("Bot:UnblockUser");
    public static string BlockAccount => GetString("Bot:BlockAccount");
    public static string UnblockAccount => GetString("Bot:UnblockAccount");
    public static string DeleteAccount => GetString("Bot:DeleteAccount");
    public static string UpdateBalance => GetString("Bot:UpdateBalance");
    public static string AddPendingRule => GetString("Bot:AddPendingRule");
    public static string GlobalPendingRule => GetString("Bot:GlobalPendingRule");
    public static string PendingRulesList => GetString("Bot:PendingRulesList");
    public static string PendingTransactions => GetString("Bot:PendingTransactions");
}