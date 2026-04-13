using Telegram.Bot.Types.ReplyMarkups;
using BankBot.Storage;

namespace BankBot;

public static class KeyboardManager
{
    public static InlineKeyboardMarkup MainMenu(long? userId = null)
    {
        var buttons = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData(Messages.Balance, "balance") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.Transfer, "transfer") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.NewAccount, "create_account") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.MyAccounts, "my_accounts") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.History, "history") }
        };

        if (userId.HasValue && AppConfig.Bot.AdminIds.Contains(userId.Value))
        {
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(Messages.AdminPanel, "admin_panel") });
        }

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup AdminMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(Messages.AllUsers, "admin_users") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.AllAccounts, "admin_accounts") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.BlockUser, "admin_block_user") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.UnblockUser, "admin_unblock_user") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.BlockAccount, "admin_block_account") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.UnblockAccount, "admin_unblock_account") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.DeleteAccount, "admin_delete_account") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.UpdateBalance, "admin_update_balance") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.AddPendingRule, "admin_add_pending") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.GlobalPendingRule, "admin_global_pending") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.PendingRulesList, "admin_pending_list") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.PendingTransactions, "admin_pending_transactions") },
            new[] { InlineKeyboardButton.WithCallbackData(Messages.BackToMenu, "menu") }
        });
    }

    public static InlineKeyboardMarkup BackToMenu()
    {
        return new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(Messages.BackToMenu, "menu"));
    }

    public static InlineKeyboardMarkup BackToAdmin()
    {
        return new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(Messages.BackToAdmin, "admin_panel"));
    }

    public static InlineKeyboardMarkup CancelAction()
    {
        return new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(Messages.Cancel, "cancel"));
    }

    public static InlineKeyboardMarkup AccountSelection(List<Account> accounts, string prefix = "select_acc")
    {
        var buttons = accounts.Select(acc =>
            new[] { InlineKeyboardButton.WithCallbackData(
                $"{acc.Name} ({acc.Balance:F2})",
                $"{prefix}_{acc.AccNumber}") }
        ).ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(Messages.Back, "menu") });
        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup TransferAccountSelection(List<Account> accounts)
    {
        var buttons = accounts.Select(acc =>
            new[] { InlineKeyboardButton.WithCallbackData(
                $"{acc.Name} (Баланс: {acc.Balance:F2})",
                $"transfer_from_{acc.AccNumber}") }
        ).ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(Messages.Cancel, "cancel") });
        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup YesNoKeyboard(string action, string data)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(Messages.Yes, $"confirm_{action}_{data}"),
                InlineKeyboardButton.WithCallbackData(Messages.No, "cancel")
            }
        });
    }
}