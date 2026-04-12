using BankBot.Core;
using BankBot.Storage;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace BankBot;

public class BankBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly DatabaseService _db;
    private readonly MoneyService _moneyService;

    public BankBotService(string token, string? proxyUrl = null)
    {
        var httpClientHandler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(proxyUrl))
        {
            httpClientHandler.Proxy = new System.Net.WebProxy(proxyUrl);
            httpClientHandler.UseProxy = true;
        }

        var httpClient = new HttpClient(httpClientHandler);
        _botClient = new TelegramBotClient(token, httpClient);

        _db = new DatabaseService();
        _moneyService = new MoneyService(_db);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var initResult = await _db.InitDbAsync();
        if (!initResult.IsSuccess)
        {
            Console.WriteLine($"Ошибка инициализации БД: {initResult.ErrorMessage}");
            return;
        }

        var me = await _botClient.GetMe(cancellationToken);
        Console.WriteLine($"Бот запущен: @{me.Username}");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            var handler = update.Type switch
            {
                UpdateType.Message => OnMessageReceived(update.Message!),
                UpdateType.CallbackQuery => OnCallbackQueryReceived(update.CallbackQuery!),
                _ => Task.CompletedTask
            };
            await handler;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки: {ex.Message}");
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
            _ => exception.ToString()
        };
        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }

    // ====================== Обработка сообщений ======================
    private async Task OnMessageReceived(Message message)
    {
        if (message.Text == null) return;

        long userId = message.From!.Id;
        var state = UserStateStorage.GetOrCreate(userId);

        if (message.Text.StartsWith("/"))
        {
            await HandleCommand(message);
            return;
        }

        // Обработка FSM
        if (state.TransferState != TransferState.None)
        {
            await HandleTransferState(message, state);
            return;
        }
        if (state.CreateAccountState != CreateAccountState.None)
        {
            await HandleCreateAccountState(message, state);
            return;
        }
        if (state.AdminBlockUserState != AdminBlockUserState.None)
        {
            await HandleAdminBlockUserState(message, state);
            return;
        }
        if (state.AdminBlockAccountState != AdminBlockAccountState.None)
        {
            await HandleAdminBlockAccountState(message, state);
            return;
        }
        if (state.AdminDeleteAccountState != AdminDeleteAccountState.None)
        {
            await HandleAdminDeleteAccountState(message, state);
            return;
        }
        if (state.AdminUpdateBalanceState != AdminUpdateBalanceState.None)
        {
            await HandleAdminUpdateBalanceState(message, state);
            return;
        }
        if (state.AdminAddPendingState != AdminAddPendingState.None)
        {
            await HandleAdminAddPendingState(message, state);
            return;
        }
        if (state.AdminGlobalPendingState != AdminGlobalPendingState.None)
        {
            await HandleAdminGlobalPendingState(message, state);
            return;
        }
    }

    private async Task HandleCommand(Message message)
    {
        string command = message.Text!.Split(' ')[0].ToLower();
        long userId = message.From!.Id;
        string username = message.From.Username ?? "unknown";

        switch (command)
        {
            case "/start":
                await StartCommand(message, userId, username);
                break;
            case "/help":
                await HelpCommand(message);
                break;
            case "/balance":
                await BalanceCommand(message);
                break;
            case "/accounts":
                await AccountsCommand(message);
                break;
            case "/transfer":
                await TransferCommand(message);
                break;
            case "/create_account":
                await CreateAccountCommand(message);
                break;
            case "/admin":
                await AdminCommand(message);
                break;
            case "/admin_users":
                await AdminUsersCommand(message);
                break;
            case "/admin_accounts":
                await AdminAccountsCommand(message);
                break;
        }
    }

    // ====================== Callback обработка ======================
    private async Task OnCallbackQueryReceived(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Data == null) return;

        long userId = callbackQuery.From.Id;
        var state = UserStateStorage.GetOrCreate(userId);
        string data = callbackQuery.Data;

        switch (data)
        {
            case "menu":
                await MenuCallback(callbackQuery);
                return;
            case "cancel":
                await CancelCallback(callbackQuery, state);
                return;
            case "balance":
                await BalanceCallback(callbackQuery);
                return;
            case "transfer":
                await TransferCallback(callbackQuery, state);
                return;
            case "create_account":
                await CreateAccountCallback(callbackQuery, state);
                return;
            case "my_accounts":
                await MyAccountsCallback(callbackQuery);
                return;
            case "history":
                await HistoryCallback(callbackQuery);
                return;
        }

        // Админские callback
        if (data == "admin_panel") await AdminPanelCallback(callbackQuery);
        else if (data == "admin_users") await AdminUsersCallback(callbackQuery);
        else if (data == "admin_accounts") await AdminAccountsCallback(callbackQuery);
        else if (data == "admin_block_user") await AdminBlockUserCallback(callbackQuery, state);
        else if (data == "admin_unblock_user") await AdminUnblockUserCallback(callbackQuery, state);
        else if (data == "admin_block_account") await AdminBlockAccountCallback(callbackQuery, state);
        else if (data == "admin_unblock_account") await AdminUnblockAccountCallback(callbackQuery, state);
        else if (data == "admin_delete_account") await AdminDeleteAccountCallback(callbackQuery, state);
        else if (data == "admin_update_balance") await AdminUpdateBalanceCallback(callbackQuery, state);
        else if (data == "admin_add_pending") await AdminAddPendingCallback(callbackQuery, state);
        else if (data == "admin_global_pending") await AdminGlobalPendingCallback(callbackQuery, state);
        else if (data == "admin_pending_list") await AdminPendingListCallback(callbackQuery);
        else if (data == "admin_pending_transactions") await AdminPendingTransactionsCallback(callbackQuery);
        else if (data.StartsWith("select_acc_"))
            await SelectAccountCallback(callbackQuery, data);
        else if (data.StartsWith("transfer_from_"))
            await SelectTransferFromCallback(callbackQuery, state, data);
        else if (data.StartsWith("confirm_"))
            await ConfirmActionCallback(callbackQuery, state, data);

        await _botClient.AnswerCallbackQuery(callbackQuery.Id);
    }

    // ====================== Реализация команд ======================
    private async Task StartCommand(Message message, long userId, string username)
    {
        var regResult = await _moneyService.RegisterAsync(userId, username);
        string text = regResult.Code == ErrorCode.Ok && regResult.Data == "already_registered"
            ? "👋 С возвращением!"
            : "🏦 Добро пожаловать в банк!";

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: text,
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.MainMenu(userId)
        );
    }

    private async Task HelpCommand(Message message)
    {
        string helpText = @"
<b>📋 Доступные команды:</b>

<b>Основные команды:</b>
/start - Начать работу с ботом
/help - Показать эту справку
/balance - Показать баланс
/accounts - Показать все счета
/transfer - Сделать перевод
/create_account - Создать новый счет

<b>Операции:</b>
• Просмотр баланса и состояния счетов
• Переводы между счетами
• Создание новых счетов
• Просмотр истории операций

<b>Формат счета:</b> ACC-XXXXXXXXX
<b>Перевод на свой счет:</b> Введите имя своего счета
";
        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: helpText,
            parseMode: ParseMode.Html
        );
    }

    private async Task BalanceCommand(Message message)
    {
        long userId = message.From!.Id;
        var accountsResult = await _db.GetAccountsByUserAsync(userId);

        if (accountsResult.Code == ErrorCode.NotFound || accountsResult.Data == null || accountsResult.Data.Count == 0)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "💰 <b>Баланс</b>\n\nУ вас нет счетов.\nСоздайте первый счет через меню.",
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.MainMenu(userId));
            return;
        }

        if (accountsResult.Data.Count == 1)
        {
            var acc = accountsResult.Data[0];
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: MessageFormatter.FormatBalance(acc),
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.BackToMenu());
        }
        else
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Выберите счет для просмотра баланса:",
                replyMarkup: KeyboardManager.AccountSelection(accountsResult.Data));
        }
    }

    private async Task AccountsCommand(Message message)
    {
        await ShowAccounts(message.Chat.Id, message.From!.Id);
    }

    private async Task TransferCommand(Message message)
    {
        long userId = message.From!.Id;
        var accountsResult = await _db.GetAccountsByUserAsync(userId);

        if (accountsResult.Code == ErrorCode.NotFound || accountsResult.Data == null || accountsResult.Data.Count == 0)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "❌ У вас нет счетов для перевода");
            return;
        }

        var state = UserStateStorage.GetOrCreate(userId);
        if (accountsResult.Data.Count == 1)
        {
            state.TransferState = TransferState.ToAccount;
            state.FromAccount = accountsResult.Data[0].AccNumber;
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "💸 <b>Перевод средств</b>\n\n" +
                      "Введите номер счета получателя или имя своего счета:\n\n" +
                      "• <code>ACC-XXXXXXXXX</code> - для перевода на другой счет\n" +
                      "• <b>Имя счета</b> - для перевода на свой счет\n\n" +
                      "<i>Пример: 'Накопительный' (перевод на ваш счет с таким именем)</i>",
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.CancelAction());
        }
        else
        {
            state.TransferState = TransferState.FromAccount;
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "💸 <b>Перевод средств</b>\n\nВыберите счет с которого будете переводить:",
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.TransferAccountSelection(accountsResult.Data));
        }
    }

    private async Task CreateAccountCommand(Message message)
    {
        var state = UserStateStorage.GetOrCreate(message.From!.Id);
        state.CreateAccountState = CreateAccountState.Name;
        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Введите название для нового счета:",
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task AdminCommand(Message message)
    {
        if (!AppConfig.Bot.AdminIds.Contains(message.From!.Id))
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "❌ У вас нет доступа к админ-панели");
            return;
        }

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "👑 <b>Админ-панель банк-бота</b>\n\nВыберите действие:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.AdminMenu());
    }

    private async Task AdminUsersCommand(Message message)
    {
        if (!AppConfig.Bot.AdminIds.Contains(message.From!.Id)) return;
        var usersResult = await _db.GetAllUsersAsync();
        if (!usersResult.IsSuccess)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"❌ Ошибка: {usersResult.ErrorMessage}");
            return;
        }
        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: MessageFormatter.FormatUserList(usersResult.Data),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.BackToAdmin());
    }

    private async Task AdminAccountsCommand(Message message)
    {
        if (!AppConfig.Bot.AdminIds.Contains(message.From!.Id)) return;
        var accountsResult = await _db.GetAllAccountsAsync();
        if (!accountsResult.IsSuccess)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"❌ Ошибка: {accountsResult.ErrorMessage}");
            return;
        }
        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: MessageFormatter.FormatAccountList(accountsResult.Data),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.BackToAdmin());
    }

    // ====================== Callback методы ======================
    private async Task MenuCallback(CallbackQuery callback)
    {
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "🏦 Главное меню банк-бота",
            replyMarkup: KeyboardManager.MainMenu(callback.From.Id)
        );
    }

    private async Task CancelCallback(CallbackQuery callback, UserStateData state)
    {
        state.Clear();
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "❌ Операция отменена",
            replyMarkup: KeyboardManager.MainMenu(callback.From.Id)
        );
    }

    private async Task BalanceCallback(CallbackQuery callback)
    {
        long userId = callback.From.Id;
        var accountsResult = await _db.GetAccountsByUserAsync(userId);

        if (accountsResult.Code == ErrorCode.NotFound || accountsResult.Data == null || accountsResult.Data.Count == 0)
        {
            await _botClient.EditMessageText(
                chatId: callback.Message!.Chat.Id,
                messageId: callback.Message.MessageId,
                text: "💰 <b>Баланс</b>\n\nУ вас нет счетов.\nСоздайте первый счет через меню.",
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.MainMenu(userId));
            return;
        }

        if (accountsResult.Data.Count == 1)
        {
            await _botClient.EditMessageText(
                chatId: callback.Message!.Chat.Id,
                messageId: callback.Message.MessageId,
                text: MessageFormatter.FormatBalance(accountsResult.Data[0]),
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.BackToMenu());
        }
        else
        {
            await _botClient.EditMessageText(
                chatId: callback.Message!.Chat.Id,
                messageId: callback.Message.MessageId,
                text: "Выберите счет для просмотра баланса:",
                replyMarkup: KeyboardManager.AccountSelection(accountsResult.Data));
        }
    }

    private async Task TransferCallback(CallbackQuery callback, UserStateData state)
    {
        long userId = callback.From.Id;
        var accountsResult = await _db.GetAccountsByUserAsync(userId);

        if (accountsResult.Code == ErrorCode.NotFound || accountsResult.Data == null || accountsResult.Data.Count == 0)
        {
            await _botClient.AnswerCallbackQuery(callback.Id, "❌ У вас нет счетов для перевода", showAlert: true);
            return;
        }

        if (accountsResult.Data.Count == 1)
        {
            state.TransferState = TransferState.ToAccount;
            state.FromAccount = accountsResult.Data[0].AccNumber;
            await _botClient.EditMessageText(
                chatId: callback.Message!.Chat.Id,
                messageId: callback.Message.MessageId,
                text: "💸 <b>Перевод средств</b>\n\n" +
                      "Введите номер счета получателя или имя своего счета:\n\n" +
                      "• <code>ACC-XXXXXXXXX</code> - для перевода на другой счет\n" +
                      "• <b>Имя счета</b> - для перевода на свой счет\n\n" +
                      "<i>Пример: 'Накопительный' (перевод на ваш счет с таким именем)</i>",
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.CancelAction());
        }
        else
        {
            state.TransferState = TransferState.FromAccount;
            await _botClient.EditMessageText(
                chatId: callback.Message!.Chat.Id,
                messageId: callback.Message.MessageId,
                text: "💸 <b>Перевод средств</b>\n\nВыберите счет с которого будете переводить:",
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.TransferAccountSelection(accountsResult.Data));
        }
    }

    private async Task CreateAccountCallback(CallbackQuery callback, UserStateData state)
    {
        state.CreateAccountState = CreateAccountState.Name;
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "➕ <b>Создание нового счета</b>\n\nВведите название для нового счета:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task MyAccountsCallback(CallbackQuery callback)
    {
        await ShowAccounts(callback.Message!.Chat.Id, callback.From.Id, editMessageId: callback.Message.MessageId);
    }

    private async Task HistoryCallback(CallbackQuery callback)
    {
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "📝 <b>История операций</b>\n\nФункция в разработке...",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.BackToMenu());
    }

    private async Task SelectAccountCallback(CallbackQuery callback, string data)
    {
        string accNumber = data.Replace("select_acc_", "");
        var accResult = await _db.GetAccountByAccNumberAsync(accNumber);
        if (!accResult.IsSuccess || accResult.Data.UserId != callback.From.Id)
        {
            await _botClient.AnswerCallbackQuery(callback.Id, "Ошибка доступа", showAlert: true);
            return;
        }

        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: MessageFormatter.FormatBalance(accResult.Data),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.BackToMenu());
    }

    private async Task SelectTransferFromCallback(CallbackQuery callback, UserStateData state, string data)
    {
        string accNumber = data.Replace("transfer_from_", "");
        var accResult = await _db.GetAccountByAccNumberAsync(accNumber);
        if (!accResult.IsSuccess || accResult.Data.UserId != callback.From.Id)
        {
            await _botClient.AnswerCallbackQuery(callback.Id, "Ошибка выбора счета", showAlert: true);
            return;
        }

        state.TransferState = TransferState.ToAccount;
        state.FromAccount = accNumber;

        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "💸 <b>Перевод средств</b>\n\n" +
                  "Введите номер счета получателя или имя своего счета:\n\n" +
                  "• <code>ACC-XXXXXXXXX</code> - для перевода на другой счет\n" +
                  "• <b>Имя счета</b> - для перевода на свой счет\n\n" +
                  "<i>Пример: 'Накопительный' (перевод на ваш счет с таким именем)</i>",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    // ====================== Админские callback методы ======================
    private async Task AdminPanelCallback(CallbackQuery callback)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id))
        {
            await _botClient.AnswerCallbackQuery(callback.Id, "❌ У вас нет доступа", showAlert: true);
            return;
        }

        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "👑 <b>Админ-панель банк-бота</b>\n\nВыберите действие:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.AdminMenu()
        );
    }

    private async Task AdminUsersCallback(CallbackQuery callback)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        var usersResult = await _db.GetAllUsersAsync();
        if (!usersResult.IsSuccess)
        {
            await _botClient.AnswerCallbackQuery(callback.Id, $"❌ Ошибка: {usersResult.ErrorMessage}", showAlert: true);
            return;
        }
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: MessageFormatter.FormatUserList(usersResult.Data),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.BackToAdmin());
    }

    private async Task AdminAccountsCallback(CallbackQuery callback)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        var accountsResult = await _db.GetAllAccountsAsync();
        if (!accountsResult.IsSuccess)
        {
            await _botClient.AnswerCallbackQuery(callback.Id, $"❌ Ошибка: {accountsResult.ErrorMessage}", showAlert: true);
            return;
        }
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: MessageFormatter.FormatAccountList(accountsResult.Data),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.BackToAdmin());
    }

    private async Task AdminBlockUserCallback(CallbackQuery callback, UserStateData state)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        state.AdminBlockUserState = AdminBlockUserState.UserId;
        state.Action = "block";
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "🔒 <b>Блокировка пользователя</b>\n\nВведите ID пользователя для блокировки:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task AdminUnblockUserCallback(CallbackQuery callback, UserStateData state)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        state.AdminBlockUserState = AdminBlockUserState.UserId;
        state.Action = "unblock";
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "🔓 <b>Разблокировка пользователя</b>\n\nВведите ID пользователя для разблокировки:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task AdminBlockAccountCallback(CallbackQuery callback, UserStateData state)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        state.AdminBlockAccountState = AdminBlockAccountState.AccountNumber;
        state.Action = "block";
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "⛔ <b>Блокировка счета</b>\n\nВведите номер счета для блокировки:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task AdminUnblockAccountCallback(CallbackQuery callback, UserStateData state)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        state.AdminBlockAccountState = AdminBlockAccountState.AccountNumber;
        state.Action = "unblock";
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "✅ <b>Разблокировка счета</b>\n\nВведите номер счета для разблокировки:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task AdminDeleteAccountCallback(CallbackQuery callback, UserStateData state)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        state.AdminDeleteAccountState = AdminDeleteAccountState.AccountNumber;
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "🗑️ <b>Удаление счета</b>\n\nВведите номер счета для удаления:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task AdminUpdateBalanceCallback(CallbackQuery callback, UserStateData state)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        state.AdminUpdateBalanceState = AdminUpdateBalanceState.AccountNumber;
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "💰 <b>Изменение баланса счета</b>\n\nВведите номер счета:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task AdminAddPendingCallback(CallbackQuery callback, UserStateData state)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        state.AdminAddPendingState = AdminAddPendingState.TargetAccount;
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "⏳ <b>Добавление правила pending</b>\n\nВведите номер счета (или оставьте пустым для всех счетов):",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task AdminGlobalPendingCallback(CallbackQuery callback, UserStateData state)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        state.AdminGlobalPendingState = AdminGlobalPendingState.DelayHours;
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: "🌍 <b>Глобальное правило pending</b>\n\nВведите время задержки в часах:",
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.CancelAction());
    }

    private async Task AdminPendingListCallback(CallbackQuery callback)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        var rulesResult = await _db.GetGlobalPendingRulesAsync();
        if (!rulesResult.IsSuccess)
        {
            await _botClient.AnswerCallbackQuery(callback.Id, $"❌ Ошибка: {rulesResult.ErrorMessage}", showAlert: true);
            return;
        }
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: MessageFormatter.FormatPendingRules(rulesResult.Data),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.BackToAdmin());
    }

    private async Task AdminPendingTransactionsCallback(CallbackQuery callback)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        var transResult = await _db.GetPendingTransactionsAsync();
        if (!transResult.IsSuccess)
        {
            await _botClient.AnswerCallbackQuery(callback.Id, $"❌ Ошибка: {transResult.ErrorMessage}", showAlert: true);
            return;
        }
        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: MessageFormatter.FormatPendingTransactions(transResult.Data),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardManager.BackToAdmin());
    }

    private async Task ConfirmActionCallback(CallbackQuery callback, UserStateData state, string data)
    {
        if (!AppConfig.Bot.AdminIds.Contains(callback.From.Id)) return;
        var parts = data.Split('_');
        if (parts.Length < 3) return;

        string action = parts[1];
        string target = parts[2];
        bool success = false;
        string messageText = "";

        try
        {
            switch (action)
            {
                case "blockuser":
                    var blockUserResult = await _db.BlockUserAsync(long.Parse(target), true);
                    success = blockUserResult.IsSuccess;
                    messageText = success ? $"✅ Пользователь {target} заблокирован" : $"❌ Ошибка: {blockUserResult.ErrorMessage}";
                    break;
                case "unblockuser":
                    var unblockUserResult = await _db.BlockUserAsync(long.Parse(target), false);
                    success = unblockUserResult.IsSuccess;
                    messageText = success ? $"✅ Пользователь {target} разблокирован" : $"❌ Ошибка: {unblockUserResult.ErrorMessage}";
                    break;
                case "blockaccount":
                    var blockAccResult = await _db.BlockAccountAsync(target, true);
                    success = blockAccResult.IsSuccess;
                    messageText = success ? $"✅ Счет {target} заблокирован" : $"❌ Ошибка: {blockAccResult.ErrorMessage}";
                    break;
                case "unblockaccount":
                    var unblockAccResult = await _db.BlockAccountAsync(target, false);
                    success = unblockAccResult.IsSuccess;
                    messageText = success ? $"✅ Счет {target} разблокирован" : $"❌ Ошибка: {unblockAccResult.ErrorMessage}";
                    break;
                case "deleteaccount":
                    var deleteResult = await _db.DeleteAccountAsync(target);
                    success = deleteResult.IsSuccess;
                    messageText = success ? $"✅ Счет {target} удален" : $"❌ Ошибка: {deleteResult.ErrorMessage}";
                    break;
            }
        }
        catch (Exception ex)
        {
            messageText = $"❌ Ошибка: {ex.Message}";
        }

        await _botClient.EditMessageText(
            chatId: callback.Message!.Chat.Id,
            messageId: callback.Message.MessageId,
            text: messageText,
            replyMarkup: KeyboardManager.BackToAdmin());

        state.Clear();
    }

    // ====================== FSM обработчики ======================
    private async Task HandleTransferState(Message message, UserStateData state)
    {
        switch (state.TransferState)
        {
            case TransferState.FromAccount:
                string accInput = message.Text!.Trim();
                if (!accInput.StartsWith("ACC-"))
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Неверный формат счета. Используйте формат: ACC-XXXXXXXXX",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }
                var accResult = await _db.GetAccountByAccNumberAsync(accInput);
                if (!accResult.IsSuccess || accResult.Data.UserId != message.From!.Id)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"❌ Счет {accInput} не найден или не принадлежит вам",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }
                state.FromAccount = accInput;
                state.TransferState = TransferState.ToAccount;
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"✅ Выбран счет: {accResult.Data.Name} ({accInput})\n\n" +
                          "Введите номер счета получателя или имя своего счета:\n\n" +
                          "• <code>ACC-XXXXXXXXX</code> - для перевода на другой счет\n" +
                          "• <b>Имя счета</b> - для перевода на свой счет\n\n" +
                          "<i>Пример: 'Накопительный' (перевод на ваш счет с таким именем)</i>",
                    parseMode: ParseMode.Html,
                    replyMarkup: KeyboardManager.CancelAction());
                break;

            case TransferState.ToAccount:
                string toInput = message.Text!.Trim();
                string? toAccNumber = null;
                if (!toInput.StartsWith("ACC-"))
                {
                    var accByName = await _db.GetAccountByNameAndUserAsync(toInput, message.From!.Id);
                    if (!accByName.IsSuccess)
                    {
                        await _botClient.SendMessage(
                            chatId: message.Chat.Id,
                            text: $"❌ Счет с именем '{toInput}' не найден у вас",
                            replyMarkup: KeyboardManager.CancelAction());
                        return;
                    }
                    toAccNumber = accByName.Data.AccNumber;
                }
                else
                {
                    toAccNumber = toInput;
                }

                if (state.FromAccount == toAccNumber)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Нельзя перевести средства на тот же счет",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }

                var toAccResult = await _db.GetAccountByAccNumberAsync(toAccNumber);
                if (!toAccResult.IsSuccess)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"❌ Счет не найден: {toAccNumber}",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }
                if (toAccResult.Data.Blocked)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Счет получателя заблокирован",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }

                state.ToAccount = toAccNumber;
                state.TransferState = TransferState.Amount;

                var fromAccInfo = await _db.GetAccountByAccNumberAsync(state.FromAccount);
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"✅ Счет получателя найден\n\n" +
                          $"📤 От: {fromAccInfo.Data.Name} ({state.FromAccount})\n" +
                          $"📥 Кому: {toAccResult.Data.Name} ({toAccNumber})\n\n" +
                          $"Введите сумму перевода:",
                    replyMarkup: KeyboardManager.CancelAction());
                break;

            case TransferState.Amount:
                if (!decimal.TryParse(message.Text, out decimal amount) || amount <= 0)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Введите корректную положительную сумму",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }

                var transferResult = await _moneyService.TransferAsync(state.FromAccount!, state.ToAccount!, amount);
                if (transferResult.IsSuccess)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"✅ Перевод выполнен успешно!\n\n📤 От: {state.FromAccount}\n📥 Кому: {state.ToAccount}\n💵 Сумма: {amount}",
                        replyMarkup: KeyboardManager.MainMenu(message.From!.Id));
                }
                else if (transferResult.Code == ErrorCode.PendingTransfer)
                {
                    dynamic info = transferResult.Data!;
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"⏳ Перевод отправлен в ожидание!\n\n" +
                              $"📤 От: {state.FromAccount}\n📥 Кому: {state.ToAccount}\n💵 Сумма: {amount}\n" +
                              $"⏱️ Задержка: {info.delay_hours} часов\n📝 Причина: {info.reason}",
                        replyMarkup: KeyboardManager.MainMenu(message.From!.Id));
                }
                else
                {
                    string errorMsg = transferResult.Code switch
                    {
                        ErrorCode.AccountNotFound => "❌ Счет не найден",
                        ErrorCode.Blocked => "❌ Счет заблокирован",
                        ErrorCode.NoFunds => "❌ Недостаточно средств",
                        ErrorCode.BadAmount => "❌ Некорректная сумма",
                        _ => $"❌ Ошибка: {transferResult.ErrorMessage}"
                    };
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: errorMsg,
                        replyMarkup: KeyboardManager.MainMenu(message.From!.Id));
                }
                state.Clear();
                break;
        }
    }

    private async Task HandleCreateAccountState(Message message, UserStateData state)
    {
        string name = message.Text!.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 50)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "❌ Некорректное название. Должно быть от 1 до 50 символов.",
                replyMarkup: KeyboardManager.CancelAction());
            return;
        }

        var result = await _moneyService.CreateAccountAsync(message.From!.Id, name);
        if (result.IsSuccess)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"✅ Счет создан успешно!\n\n🏷️ Название: {name}\n📟 Номер: {result.Data}",
                replyMarkup: KeyboardManager.MainMenu(message.From.Id));
        }
        else
        {
            string error = result.Code == ErrorCode.Blocked
                ? "❌ Вы заблокированы и не можете создавать счета"
                : $"❌ Ошибка: {result.ErrorMessage}";
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: error,
                replyMarkup: KeyboardManager.MainMenu(message.From.Id));
        }
        state.Clear();
    }

    private async Task HandleAdminBlockUserState(Message message, UserStateData state)
    {
        if (state.AdminBlockUserState == AdminBlockUserState.UserId)
        {
            if (!long.TryParse(message.Text, out long userId))
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Неверный формат ID. Введите числовой ID:",
                    replyMarkup: KeyboardManager.CancelAction());
                return;
            }

            var userResult = await _db.GetUserAsync(userId);
            if (!userResult.IsSuccess)
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"❌ Пользователь с ID {userId} не найден",
                    replyMarkup: KeyboardManager.CancelAction());
                return;
            }

            state.TargetUserId = userId;
            state.AdminBlockUserState = AdminBlockUserState.Confirm;

            string action = state.Action == "unblock" ? "разблокировку" : "блокировку";
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"⚠️ <b>Подтвердите {action} пользователя</b>\n\n" +
                      $"👤 Пользователь: {userResult.Data.Username ?? "Без имени"}\n" +
                      $"🆔 ID: {userId}\n" +
                      $"📅 Создан: {userResult.Data.CreatedAt}\n" +
                      $"📊 Статус: {(userResult.Data.Blocked ? "🔴 Заблокирован" : "🟢 Активен")}\n\n" +
                      (state.Action == "unblock"
                          ? "<b>Разблокировка разблокирует все счета пользователя!</b>"
                          : "<b>Блокировка заблокирует все счета пользователя и запретит создание новых!</b>"),
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.YesNoKeyboard(state.Action == "unblock" ? "unblockuser" : "blockuser", userId.ToString()));
        }
    }

    private async Task HandleAdminBlockAccountState(Message message, UserStateData state)
    {
        if (state.AdminBlockAccountState == AdminBlockAccountState.AccountNumber)
        {
            string accNumber = message.Text!.Trim();
            if (!accNumber.StartsWith("ACC-"))
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Неверный формат счета. Используйте ACC-XXXX:",
                    replyMarkup: KeyboardManager.CancelAction());
                return;
            }

            var accResult = await _db.GetAccountFullInfoAsync(accNumber);
            if (!accResult.IsSuccess)
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"❌ Счет {accNumber} не найден",
                    replyMarkup: KeyboardManager.CancelAction());
                return;
            }

            state.AccountNumber = accNumber;
            state.AdminBlockAccountState = AdminBlockAccountState.Confirm;

            string action = state.Action == "unblock" ? "разблокировку" : "блокировку";
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"⚠️ <b>Подтвердите {action} счета</b>\n\n" +
                      $"💳 Счет: {accResult.Data.Name}\n" +
                      $"📟 Номер: {accNumber}\n" +
                      $"👤 Владелец: {accResult.Data.Username}\n" +
                      $"🆔 ID владельца: {accResult.Data.UserId}\n" +
                      $"💰 Баланс: {accResult.Data.Balance:F2}\n" +
                      $"📊 Статус: {(accResult.Data.Blocked ? "🔴 Заблокирован" : "🟢 Активен")}\n\n" +
                      (state.Action == "unblock"
                          ? "<b>Разблокировка разрешит операции со счетом!</b>"
                          : "<b>Блокировка запретит любые операции со счетом!</b>"),
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.YesNoKeyboard(state.Action == "unblock" ? "unblockaccount" : "blockaccount", accNumber));
        }
    }

    private async Task HandleAdminDeleteAccountState(Message message, UserStateData state)
    {
        if (state.AdminDeleteAccountState == AdminDeleteAccountState.AccountNumber)
        {
            string accNumber = message.Text!.Trim();
            if (!accNumber.StartsWith("ACC-"))
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Неверный формат счета. Используйте ACC-XXXX:",
                    replyMarkup: KeyboardManager.CancelAction());
                return;
            }

            var accResult = await _db.GetAccountFullInfoAsync(accNumber);
            if (!accResult.IsSuccess)
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"❌ Счет {accNumber} не найден",
                    replyMarkup: KeyboardManager.CancelAction());
                return;
            }

            state.AccountNumber = accNumber;
            state.AdminDeleteAccountState = AdminDeleteAccountState.Confirm;

            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"⚠️ <b>⚠️ ВНИМАНИЕ: УДАЛЕНИЕ СЧЕТА ⚠️</b>\n\n" +
                      $"💳 Счет: {accResult.Data.Name}\n" +
                      $"📟 Номер: {accNumber}\n" +
                      $"👤 Владелец: {accResult.Data.Username}\n" +
                      $"🆔 ID владельца: {accResult.Data.UserId}\n" +
                      $"💰 Баланс: {accResult.Data.Balance:F2}\n" +
                      $"⏳ В удержании: {accResult.Data.Pending:F2}\n" +
                      $"📊 Статус: {(accResult.Data.Blocked ? "🔴 Заблокирован" : "🟢 Активен")}\n\n" +
                      "<b>❗ Это действие необратимо! Все средства на счете будут потеряны!</b>\n" +
                      "<b>❗ Все pending транзакции будут удалены!</b>\n" +
                      "<b>❗ Подтвердите удаление:</b>",
                parseMode: ParseMode.Html,
                replyMarkup: KeyboardManager.YesNoKeyboard("deleteaccount", accNumber));
        }
    }

    private async Task HandleAdminUpdateBalanceState(Message message, UserStateData state)
    {
        switch (state.AdminUpdateBalanceState)
        {
            case AdminUpdateBalanceState.AccountNumber:
                string accNumber = message.Text!.Trim();
                if (!accNumber.StartsWith("ACC-"))
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Неверный формат счета. Используйте ACC-XXXX:",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }

                var accResult = await _db.GetAccountFullInfoAsync(accNumber);
                if (!accResult.IsSuccess)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"❌ Счет {accNumber} не найден",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }

                state.AccountNumber = accNumber;
                state.AdminUpdateBalanceState = AdminUpdateBalanceState.Amount;

                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"💰 <b>Изменение баланса счета</b>\n\n" +
                          $"💳 Счет: {accResult.Data.Name}\n" +
                          $"📟 Номер: {accNumber}\n" +
                          $"👤 Владелец: {accResult.Data.Username}\n" +
                          $"💰 Текущий баланс: {accResult.Data.Balance:F2}\n\n" +
                          $"Введите новый баланс:",
                    parseMode: ParseMode.Html,
                    replyMarkup: KeyboardManager.CancelAction());
                break;

            case AdminUpdateBalanceState.Amount:
                if (!decimal.TryParse(message.Text, out decimal newBalance))
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Введите корректную сумму:",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }

                var updateResult = await _db.UpdateAccountBalanceAsync(state.AccountNumber!, newBalance);
                if (updateResult.IsSuccess)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"✅ Баланс счета {state.AccountNumber} изменен на {newBalance:F2}",
                        replyMarkup: KeyboardManager.BackToAdmin());
                }
                else
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"❌ Ошибка: {updateResult.ErrorMessage}",
                        replyMarkup: KeyboardManager.BackToAdmin());
                }
                state.Clear();
                break;
        }
    }

    private async Task HandleAdminAddPendingState(Message message, UserStateData state)
    {
        switch (state.AdminAddPendingState)
        {
            case AdminAddPendingState.TargetAccount:
                string targetAcc = message.Text!.Trim();
                if (!string.IsNullOrEmpty(targetAcc) && !targetAcc.StartsWith("ACC-"))
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Неверный формат счета. Используйте ACC-XXXX или оставьте пустым:",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }
                state.TargetAccount = string.IsNullOrEmpty(targetAcc) ? null : targetAcc;
                state.AdminAddPendingState = AdminAddPendingState.DelayHours;
                string targetText = state.TargetAccount == null ? "для всех счетов" : $"для счета {state.TargetAccount}";
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"⏳ <b>Добавление правила pending {targetText}</b>\n\nВведите время задержки в часах:",
                    parseMode: ParseMode.Html,
                    replyMarkup: KeyboardManager.CancelAction());
                break;

            case AdminAddPendingState.DelayHours:
                if (!int.TryParse(message.Text, out int delayHours) || delayHours <= 0)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Введите корректное количество часов (целое число > 0):",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }
                state.DelayHours = delayHours;
                state.AdminAddPendingState = AdminAddPendingState.Reason;
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "📝 Введите причину задержки:",
                    replyMarkup: KeyboardManager.CancelAction());
                break;

            case AdminAddPendingState.Reason:
                string reason = message.Text!.Trim();
                var ruleResult = await _db.AddGlobalPendingRuleAsync(state.TargetAccount, state.DelayHours, reason, message.From!.Id);
                if (ruleResult.IsSuccess)
                {
                    string targetMsg = state.TargetAccount == null ? "для всех счетов" : $"для счета {state.TargetAccount}";
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"✅ Правило pending добавлено!\n\n🎯 Цель: {targetMsg}\n⏱️ Задержка: {state.DelayHours} часов\n📝 Причина: {reason}\n🆔 ID правила: {ruleResult.Data}",
                        replyMarkup: KeyboardManager.BackToAdmin());
                }
                else
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"❌ Ошибка: {ruleResult.ErrorMessage}",
                        replyMarkup: KeyboardManager.BackToAdmin());
                }
                state.Clear();
                break;
        }
    }

    private async Task HandleAdminGlobalPendingState(Message message, UserStateData state)
    {
        switch (state.AdminGlobalPendingState)
        {
            case AdminGlobalPendingState.DelayHours:
                if (!int.TryParse(message.Text, out int delayHours) || delayHours <= 0)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Введите корректное количество часов (целое число > 0):",
                        replyMarkup: KeyboardManager.CancelAction());
                    return;
                }
                state.DelayHours = delayHours;
                state.AdminGlobalPendingState = AdminGlobalPendingState.Reason;
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "📝 Введите причину задержки:",
                    replyMarkup: KeyboardManager.CancelAction());
                break;

            case AdminGlobalPendingState.Reason:
                string reason = message.Text!.Trim();
                var ruleResult = await _db.AddGlobalPendingRuleAsync(null, state.DelayHours, reason, message.From!.Id);
                if (ruleResult.IsSuccess)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"✅ Глобальное правило pending добавлено!\n\n🎯 Цель: Все счета\n⏱️ Задержка: {state.DelayHours} часов\n📝 Причина: {reason}\n🆔 ID правила: {ruleResult.Data}",
                        replyMarkup: KeyboardManager.BackToAdmin());
                }
                else
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"❌ Ошибка: {ruleResult.ErrorMessage}",
                        replyMarkup: KeyboardManager.BackToAdmin());
                }
                state.Clear();
                break;
        }
    }

    // ====================== Вспомогательные методы ======================
    private async Task ShowAccounts(long chatId, long userId, int? editMessageId = null)
    {
        var accountsResult = await _db.GetAccountsByUserAsync(userId);
        string text;
        InlineKeyboardMarkup keyboard;

        if (accountsResult.Code == ErrorCode.NotFound || accountsResult.Data == null || accountsResult.Data.Count == 0)
        {
            text = "📊 <b>Ваши счета</b>\n\nУ вас еще нет счетов.\nСоздайте первый счет через меню.";
            keyboard = KeyboardManager.BackToMenu();
        }
        else
        {
            var sb = new System.Text.StringBuilder("📊 <b>Ваши счета:</b>\n\n");
            int i = 1;
            foreach (var acc in accountsResult.Data)
            {
                string status = acc.Blocked ? "🔴 Заблокирован" : "🟢 Активен";
                sb.AppendLine($"{i}. <b>{acc.Name}</b>");
                sb.AppendLine($"   📟 <code>{acc.AccNumber}</code>");
                sb.AppendLine($"   💵 Баланс: {acc.Balance:F2}");
                sb.AppendLine($"   ⏳ В удержании: {acc.Pending:F2}");
                sb.AppendLine($"   📊 {status}\n");
                i++;
            }
            text = sb.ToString();
            keyboard = KeyboardManager.BackToMenu();
        }

        if (editMessageId.HasValue)
        {
            await _botClient.EditMessageText(
                chatId: chatId,
                messageId: editMessageId.Value,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard);
        }
        else
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard);
        }
    }
}