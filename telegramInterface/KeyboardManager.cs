using Telegram.Bot.Types.ReplyMarkups;
using BankBot.Storage;

namespace BankBot;

public static class KeyboardManager
{
    public static InlineKeyboardMarkup MainMenu(long? userId = null)
    {
        var buttons = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("💰 Баланс", "balance") },
            new[] { InlineKeyboardButton.WithCallbackData("💸 Перевод", "transfer") },
            new[] { InlineKeyboardButton.WithCallbackData("➕ Новый счет", "create_account") },
            new[] { InlineKeyboardButton.WithCallbackData("📊 Мои счета", "my_accounts") },
            new[] { InlineKeyboardButton.WithCallbackData("📝 История", "history") }
        };

        if (userId.HasValue && AppConfig.Bot.AdminIds.Contains(userId.Value))
        {
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("👑 Админ-панель", "admin_panel") });
        }

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup AdminMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("👥 Все пользователи", "admin_users") },
            new[] { InlineKeyboardButton.WithCallbackData("💳 Все счета", "admin_accounts") },
            new[] { InlineKeyboardButton.WithCallbackData("🔒 Блокировка пользователя", "admin_block_user") },
            new[] { InlineKeyboardButton.WithCallbackData("🔓 Разблокировка пользователя", "admin_unblock_user") },
            new[] { InlineKeyboardButton.WithCallbackData("⛔ Блокировка счета", "admin_block_account") },
            new[] { InlineKeyboardButton.WithCallbackData("✅ Разблокировка счета", "admin_unblock_account") },
            new[] { InlineKeyboardButton.WithCallbackData("🗑️ Удаление счета", "admin_delete_account") },
            new[] { InlineKeyboardButton.WithCallbackData("💰 Изменить баланс", "admin_update_balance") },
            new[] { InlineKeyboardButton.WithCallbackData("⏳ Добавить pending правило", "admin_add_pending") },
            new[] { InlineKeyboardButton.WithCallbackData("🌍 Глобальное pending правило", "admin_global_pending") },
            new[] { InlineKeyboardButton.WithCallbackData("📋 Список pending правил", "admin_pending_list") },
            new[] { InlineKeyboardButton.WithCallbackData("📊 Pending транзакции", "admin_pending_transactions") },
            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад в меню", "menu") }
        });
    }

    public static InlineKeyboardMarkup BackToMenu()
    {
        return new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("⬅️ Назад в меню", "menu"));
    }

    public static InlineKeyboardMarkup BackToAdmin()
    {
        return new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("⬅️ Назад в админ-панель", "admin_panel"));
    }

    public static InlineKeyboardMarkup CancelAction()
    {
        return new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel"));
    }

    public static InlineKeyboardMarkup AccountSelection(List<Account> accounts, string prefix = "select_acc")
    {
        var buttons = accounts.Select(acc =>
            new[] { InlineKeyboardButton.WithCallbackData(
                $"{acc.Name} ({acc.Balance:F2})",
                $"{prefix}_{acc.AccNumber}") }
        ).ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "menu") });
        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup TransferAccountSelection(List<Account> accounts)
    {
        var buttons = accounts.Select(acc =>
            new[] { InlineKeyboardButton.WithCallbackData(
                $"{acc.Name} (Баланс: {acc.Balance:F2})",
                $"transfer_from_{acc.AccNumber}") }
        ).ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel") });
        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup YesNoKeyboard(string action, string data)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да", $"confirm_{action}_{data}"),
                InlineKeyboardButton.WithCallbackData("❌ Нет", "cancel")
            }
        });
    }
}