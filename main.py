import asyncio
from aiogram.exceptions import TelegramBadRequest
from aiogram import Bot, Dispatcher, Router, F, types
from aiogram.filters import Command, CommandStart
from aiogram.fsm.context import FSMContext
from aiogram.fsm.state import StatesGroup, State
from aiogram.utils.keyboard import InlineKeyboardBuilder
from aiogram.client.session.aiohttp import AiohttpSession
from info import TOKEN, ADMIN_ID, PROXY

from core import moneyOP as op
from core import sqlite as db

# ======================
# FSM STATES
# ======================
class TransferFSM(StatesGroup):
    from_acc = State()  # Новое состояние для выбора счета отправителя
    to_acc = State()
    amount = State()

class CreateAccountFSM(StatesGroup):
    name = State()

# Админские FSM состояния
class AdminBlockUserFSM(StatesGroup):
    user_id = State()
    confirm = State()

class AdminBlockAccountFSM(StatesGroup):
    acc_number = State()
    confirm = State()

class AdminDeleteAccountFSM(StatesGroup):
    acc_number = State()
    confirm = State()

class AdminUpdateBalanceFSM(StatesGroup):
    acc_number = State()
    amount = State()

class AdminAddPendingFSM(StatesGroup):
    target_acc = State()
    delay_hours = State()
    reason = State()

class AdminGlobalPendingFSM(StatesGroup):
    delay_hours = State()
    reason = State()


# ======================
# KEYBOARD FACTORY
# ======================
class KeyboardManager:
    """Управление клавиатурами"""
    
    @staticmethod
    def main_menu(user_id: int = None):
        """Главное меню с проверкой на админа"""
        kb = InlineKeyboardBuilder()
        kb.button(text="💰 Баланс", callback_data="balance")
        kb.button(text="💸 Перевод", callback_data="transfer")
        kb.button(text="➕ Новый счет", callback_data="create_account")
        kb.button(text="📊 Мои счета", callback_data="my_accounts")
        kb.button(text="📝 История", callback_data="history")
        
        # Проверяем, является ли пользователь админом
        if user_id in ADMIN_ID:
            kb.button(text="👑 Админ-панель", callback_data="admin_panel")
        
        kb.adjust(2, 2, 1, 1)
        return kb.as_markup()
    
    @staticmethod
    def admin_menu():
        """Меню администратора"""
        kb = InlineKeyboardBuilder()
        kb.button(text="👥 Все пользователи", callback_data="admin_users")
        kb.button(text="💳 Все счета", callback_data="admin_accounts")
        kb.button(text="🔒 Блокировка пользователя", callback_data="admin_block_user")
        kb.button(text="🔓 Разблокировка пользователя", callback_data="admin_unblock_user")
        kb.button(text="⛔ Блокировка счета", callback_data="admin_block_account")
        kb.button(text="✅ Разблокировка счета", callback_data="admin_unblock_account")
        kb.button(text="🗑️ Удаление счета", callback_data="admin_delete_account")
        kb.button(text="💰 Изменить баланс", callback_data="admin_update_balance")
        kb.button(text="⏳ Добавить pending правило", callback_data="admin_add_pending")
        kb.button(text="🌍 Глобальное pending правило", callback_data="admin_global_pending")
        kb.button(text="📋 Список pending правил", callback_data="admin_pending_list")
        kb.button(text="📊 Pending транзакции", callback_data="admin_pending_transactions")
        kb.button(text="⬅️ Назад в меню", callback_data="menu")
        kb.adjust(2, 2, 2, 2, 1, 1, 1)
        return kb.as_markup()
    
    @staticmethod
    def back_to_menu():
        """Кнопка возврата в меню"""
        kb = InlineKeyboardBuilder()
        kb.button(text="⬅️ Назад в меню", callback_data="menu")
        return kb.as_markup()
    
    @staticmethod
    def back_to_admin():
        """Кнопка возврата в админ-панель"""
        kb = InlineKeyboardBuilder()
        kb.button(text="⬅️ Назад в админ-панель", callback_data="admin_panel")
        return kb.as_markup()
    
    @staticmethod
    def cancel_action():
        """Кнопка отмены действия"""
        kb = InlineKeyboardBuilder()
        kb.button(text="❌ Отмена", callback_data="cancel")
        return kb.as_markup()
    
    @staticmethod
    def account_selection(accounts, prefix="select_acc"):
        """Выбор счета из списка"""
        kb = InlineKeyboardBuilder()
        for acc in accounts:
            kb.button(
                text=f"{acc['name']} ({acc['balance']:.2f})", 
                callback_data=f"{prefix}_{acc['acc_number']}"
            )
        kb.button(text="⬅️ Назад", callback_data="menu")
        kb.adjust(1)
        return kb.as_markup()
    
    @staticmethod
    def yes_no_keyboard(action: str, data: str):
        """Клавиатура с кнопками Да/Нет"""
        kb = InlineKeyboardBuilder()
        kb.button(text="✅ Да", callback_data=f"confirm_{action}_{data}")
        kb.button(text="❌ Нет", callback_data="cancel")
        kb.adjust(2)
        return kb.as_markup()
    
    @staticmethod
    def transfer_account_selection(accounts):
        """Специальная клавиатура для выбора счета при переводе"""
        kb = InlineKeyboardBuilder()
        for acc in accounts:
            kb.button(
                text=f"{acc['name']} (Баланс: {acc['balance']:.2f})", 
                callback_data=f"transfer_from_{acc['acc_number']}"
            )
        kb.button(text="❌ Отмена", callback_data="cancel")
        kb.adjust(1)
        return kb.as_markup()


# ======================
# MESSAGE FORMATTER
# ======================
class MessageFormatter:
    """Форматирование сообщений"""
    
    @staticmethod
    def format_accounts(accounts):
        """Форматирование списка счетов"""
        if not accounts:
            return "У вас нет счетов"
        
        lines = []
        for acc in accounts:
            status = "🔴" if acc['blocked'] else "🟢"
            lines.append(
                f"💳 <b>{acc['name']}</b> {status}\n"
                f"📟 Номер: {acc['acc_number']}\n"
                f"💰 Баланс: {acc['balance']:.2f}\n"
                f"⏳ В удержании: {acc['pending']:.2f}\n"
            )
        return "\n".join(lines)
    
    @staticmethod
    def format_balance(account):
        """Форматирование баланса одного счета"""
        status = "🔴 Заблокирован" if account['blocked'] else "🟢 Активен"
        return (
            f"💰 <b>Баланс счета</b>\n\n"
            f"💳 Счет: <b>{account['name']}</b>\n"
            f"📟 Номер: <code>{account['acc_number']}</code>\n"
            f"💵 Баланс: {account['balance']:.2f}\n"
            f"⏳ В удержании: {account['pending']:.2f}\n"
            f"📊 Статус: {status}"
        )
    
    @staticmethod
    def format_user_list(users):
        """Форматирование списка пользователей"""
        if not users:
            return "Нет пользователей"
        
        text = "👥 <b>Список пользователей:</b>\n\n"
        for i, user in enumerate(users, 1):
            status = "🔴" if user['blocked'] else "🟢"
            text += (
                f"{i}. <b>{user['username'] or 'Без имени'}</b> {status}\n"
                f"   🆔 ID: <code>{user['id']}</code>\n"
                f"   📅 Регистрация: <code>{user['created_at']}</code>\n"
                f"   💳 Счетов: {user['account_count']}\n"
                f"   💰 Общий баланс: {user['total_balance']:.2f}\n\n"
            )
        return text
    
    @staticmethod
    def format_account_list(accounts):
        """Форматирование списка всех счетов"""
        if not accounts:
            return "Нет счетов"
        
        text = "💳 <b>Список всех счетов:</b>\n\n"
        for i, acc in enumerate(accounts, 1):
            status = "🔴" if acc['blocked'] else "🟢"
            text += (
                f"{i}. <b>{acc['name']}</b> {status}\n"
                f"   📟 Номер: <code>{acc['acc_number']}</code>\n"
                f"   👤 Владелец: {acc['username']} (ID: {acc['user_id']})\n"
                f"   💰 Баланс: {acc['balance']:.2f}\n"
                f"   ⏳ В удержании: {acc['pending']:.2f}\n\n"
            )
        return text
    
    @staticmethod
    def format_pending_rules(rules):
        """Форматирование списка pending правил"""
        if not rules:
            return "Нет активных правил"
        
        text = "⏳ <b>Список правил pending:</b>\n\n"
        for i, rule in enumerate(rules, 1):
            target = rule['target_acc'] or "Все счета (глобальное)"
            status = "🟢" if rule['active'] else "🔴"
            text += (
                f"{i}. Правило #{rule['id']} {status}\n"
                f"   🎯 Цель: {target}\n"
                f"   ⏱️ Задержка: {rule['delay_hours']} часов\n"
                f"   📝 Причина: {rule['reason']}\n"
                f"   👤 Создал: {rule['admin_username'] or 'Система'}\n\n"
            )
        return text
    
    @staticmethod
    def format_pending_transactions(transactions):
        """Форматирование списка pending транзакций"""
        if not transactions:
            return "Нет pending транзакций"
        
        text = "⏳ <b>Pending транзакции:</b>\n\n"
        for i, trans in enumerate(transactions, 1):
            text += (
                f"{i}. Транзакция #{trans['id']}\n"
                f"   💳 Счет: <code>{trans['acc_number']}</code>\n"
                f"   👤 Пользователь: {trans['username']}\n"
                f"   💰 Сумма: {trans['amount']:.2f}\n"
                f"   📝 Причина: {trans['reason']}\n"
                f"   🕐 Освободится: <code>{trans['release_at']}</code>\n\n"
            )
        return text


# ======================
# BOT HANDLERS
# ======================
class BotHandlers:
    """Обработчики бота"""
    
    def __init__(self, router: Router):
        self.router = router
        self.register_handlers()
    
    def register_handlers(self):
        """Регистрация всех обработчиков"""
        
        # Команды
        self.router.message(CommandStart())(self.start_handler)
        self.router.message(Command("help"))(self.help_handler)
        self.router.message(Command("balance"))(self.balance_command)
        self.router.message(Command("accounts"))(self.accounts_command)
        self.router.message(Command("transfer"))(self.transfer_command)
        self.router.message(Command("create_account"))(self.create_account_command)
        
        # Админские команды
        self.router.message(Command("admin"))(self.admin_command)
        self.router.message(Command("admin_users"))(self.admin_users_command)
        self.router.message(Command("admin_accounts"))(self.admin_accounts_command)
        
        # Callback-обработчики
        self.router.callback_query(F.data == "menu")(self.menu_callback)
        self.router.callback_query(F.data == "cancel")(self.cancel_callback)
        self.router.callback_query(F.data == "balance")(self.balance_callback)
        self.router.callback_query(F.data == "transfer")(self.transfer_callback)
        self.router.callback_query(F.data == "create_account")(self.create_account_callback)
        self.router.callback_query(F.data == "my_accounts")(self.my_accounts_callback)
        self.router.callback_query(F.data == "history")(self.history_callback)
        self.router.callback_query(F.data.startswith("select_acc_"))(self.select_account_callback)
        self.router.callback_query(F.data.startswith("transfer_from_"))(self.select_transfer_from_callback)
        
        # Админские callback-обработчики
        self.router.callback_query(F.data == "admin_panel")(self.admin_panel_callback)
        self.router.callback_query(F.data == "admin_users")(self.admin_users_callback)
        self.router.callback_query(F.data == "admin_accounts")(self.admin_accounts_callback)
        self.router.callback_query(F.data == "admin_block_user")(self.admin_block_user_callback)
        self.router.callback_query(F.data == "admin_unblock_user")(self.admin_unblock_user_callback)
        self.router.callback_query(F.data == "admin_block_account")(self.admin_block_account_callback)
        self.router.callback_query(F.data == "admin_unblock_account")(self.admin_unblock_account_callback)
        self.router.callback_query(F.data == "admin_delete_account")(self.admin_delete_account_callback)
        self.router.callback_query(F.data == "admin_update_balance")(self.admin_update_balance_callback)
        self.router.callback_query(F.data == "admin_add_pending")(self.admin_add_pending_callback)
        self.router.callback_query(F.data == "admin_global_pending")(self.admin_global_pending_callback)
        self.router.callback_query(F.data == "admin_pending_list")(self.admin_pending_list_callback)
        self.router.callback_query(F.data == "admin_pending_transactions")(self.admin_pending_transactions_callback)
        self.router.callback_query(F.data.startswith("confirm_"))(self.confirm_action_callback)
        
        # Обработчики FSM
        self.router.message(TransferFSM.from_acc)(self.transfer_from_acc_handler)
        self.router.message(TransferFSM.to_acc)(self.transfer_to_acc_handler)
        self.router.message(TransferFSM.amount)(self.transfer_amount_handler)
        self.router.message(CreateAccountFSM.name)(self.create_account_name_handler)
        
        # Админские FSM обработчики
        self.router.message(AdminBlockUserFSM.user_id)(self.admin_block_user_id_handler)
        self.router.message(AdminBlockAccountFSM.acc_number)(self.admin_block_account_number_handler)
        self.router.message(AdminDeleteAccountFSM.acc_number)(self.admin_delete_account_number_handler)
        self.router.message(AdminUpdateBalanceFSM.acc_number)(self.admin_update_balance_acc_handler)
        self.router.message(AdminUpdateBalanceFSM.amount)(self.admin_update_balance_amount_handler)
        self.router.message(AdminAddPendingFSM.target_acc)(self.admin_add_pending_target_handler)
        self.router.message(AdminAddPendingFSM.delay_hours)(self.admin_add_pending_delay_handler)
        self.router.message(AdminAddPendingFSM.reason)(self.admin_add_pending_reason_handler)
        self.router.message(AdminGlobalPendingFSM.delay_hours)(self.admin_global_pending_delay_handler)
        self.router.message(AdminGlobalPendingFSM.reason)(self.admin_global_pending_reason_handler)
    
    # ======================
    # BASIC COMMAND HANDLERS
    # ======================
    async def start_handler(self, message: types.Message):
        """Обработчик команды /start"""
        status, detail = op.register(
            message.from_user.id,
            message.from_user.username or "unknown"
        )

        if status != op.MONEY_OK:
            await message.answer(f"❌ Ошибка регистрации: {status}")
            return

        text = "👋 С возвращением!" if detail == "already_registered" else "🏦 Добро пожаловать в банк!"
        await message.answer(text, reply_markup=KeyboardManager.main_menu(message.from_user.id))
    
    async def help_handler(self, message: types.Message):
        """Обработчик команды /help"""
        help_text = """
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
• Переводы между счетами (можно выбрать с какого счета)
• Перевод на свой счет по имени счета
• Создание новых счетов
• Просмотр истории операций

<b>Формат счета:</b> ACC-XXXXXXXXX
<b>Перевод на свой счет:</b> Введите имя своего счета
        """
        await message.answer(help_text, parse_mode="HTML")
    
    async def balance_command(self, message: types.Message):
        """Обработчик команды /balance"""
        err, accounts = db.get_accounts_by_user(message.from_user.id)
        
        if err == db.SQL_NOT_FOUND:
            await message.answer(
                "💰 <b>Баланс</b>\n\nУ вас нет счетов.\nСоздайте первый счет через меню.",
                parse_mode="HTML",
                reply_markup=KeyboardManager.main_menu(message.from_user.id)
            )
            return
        
        if err != db.SQL_OK:
            await message.answer(f"Ошибка БД: {err}")
            return
        
        if not accounts:
            await message.answer(
                "💰 <b>Баланс</b>\n\nУ вас нет счетов.",
                parse_mode="HTML",
                reply_markup=KeyboardManager.main_menu(message.from_user.id)
            )
            return
        
        # Показываем основной счет или выбор счета
        if len(accounts) == 1:
            text = MessageFormatter.format_balance(accounts[0])
            await message.answer(text, parse_mode="HTML", reply_markup=KeyboardManager.back_to_menu())
        else:
            await message.answer(
                "Выберите счет для просмотра баланса:",
                reply_markup=KeyboardManager.account_selection(accounts)
            )
    
    async def accounts_command(self, message: types.Message):
        """Обработчик команды /accounts"""
        await self.show_accounts(message)
    
    async def transfer_command(self, message: types.Message, state: FSMContext):
        """Обработчик команды /transfer"""
        user_id = message.from_user.id
        
        err, accounts = db.get_accounts_by_user(user_id)
        if err == db.SQL_NOT_FOUND or not accounts:
            await message.answer("❌ У вас нет счетов для перевода")
            return
        
        if len(accounts) == 1:
            # Если только один счет, сразу используем его
            await state.set_state(TransferFSM.to_acc)
            await state.update_data(from_acc=accounts[0]['acc_number'])
            await message.answer(
                "💸 <b>Перевод средств</b>\n\n"
                "Введите номер счета получателя или имя своего счета:\n\n"
                "• <code>ACC-XXXXXXXXX</code> - для перевода на другой счет\n"
                "• <b>Имя счета</b> - для перевода на свой счет\n\n"
                "<i>Пример: 'Накопительный' (перевод на ваш счет с таким именем)</i>",
                parse_mode="HTML",
                reply_markup=KeyboardManager.cancel_action()
            )
        else:
            # Если несколько счетов, предлагаем выбрать
            await state.set_state(TransferFSM.from_acc)
            await message.answer(
                "💸 <b>Перевод средств</b>\n\n"
                "Выберите счет с которого будете переводить:",
                parse_mode="HTML",
                reply_markup=KeyboardManager.transfer_account_selection(accounts)
            )
    
    async def create_account_command(self, message: types.Message, state: FSMContext):
        """Обработчик команды /create_account"""
        await state.set_state(CreateAccountFSM.name)
        await message.answer(
            "Введите название для нового счета:",
            reply_markup=KeyboardManager.cancel_action()
        )
    
    # ======================
    # ADMIN COMMAND HANDLERS
    # ======================
    async def admin_command(self, message: types.Message):
        """Обработчик команды /admin"""
        if message.from_user.id not in ADMIN_ID:
            await message.answer("❌ У вас нет доступа к админ-панели")
            return
        
        await message.answer(
            "👑 <b>Админ-панель банк-бота</b>\n\n"
            "Выберите действие:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.admin_menu()
        )
    
    async def admin_users_command(self, message: types.Message):
        """Обработчик команды /admin_users"""
        if message.from_user.id not in ADMIN_ID:
            await message.answer("❌ У вас нет доступа к этой команде")
            return
        
        err, users = db.get_all_users()
        if err != db.SQL_OK:
            await message.answer(f"❌ Ошибка БД: {err}")
            return
        
        text = MessageFormatter.format_user_list(users)
        await message.answer(text, parse_mode="HTML", reply_markup=KeyboardManager.back_to_admin())
    
    async def admin_accounts_command(self, message: types.Message):
        """Обработчик команды /admin_accounts"""
        if message.from_user.id not in ADMIN_ID:
            await message.answer("❌ У вас нет доступа к этой команде")
            return
        
        err, accounts = db.get_all_accounts()
        if err != db.SQL_OK:
            await message.answer(f"❌ Ошибка БД: {err}")
            return
        
        text = MessageFormatter.format_account_list(accounts)
        await message.answer(text, parse_mode="HTML", reply_markup=KeyboardManager.back_to_admin())
    
    # ======================
    # CALLBACK HANDLERS
    # ======================
    async def menu_callback(self, callback: types.CallbackQuery):
        """Обработка возврата в меню"""
        await callback.message.edit_text(
            "🏦 Главное меню банк-бота",
            reply_markup=KeyboardManager.main_menu(callback.from_user.id)
        )
        await callback.answer()
    
    async def cancel_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка отмены действия"""
        await state.clear()
        await callback.message.edit_text(
            "❌ Операция отменена",
            reply_markup=KeyboardManager.main_menu(callback.from_user.id)
        )
        await callback.answer()
    
    async def balance_callback(self, callback: types.CallbackQuery):
        """Обработка кнопки Баланс"""
        err, accounts = db.get_accounts_by_user(callback.from_user.id)
        
        if err == db.SQL_NOT_FOUND:
            await callback.message.edit_text(
                "💰 <b>Баланс</b>\n\n"
                "У вас нет счетов.\n"
                "Создайте первый счет через меню.",
                parse_mode="HTML",
                reply_markup=KeyboardManager.main_menu(callback.from_user.id)
            )
            await callback.answer()
            return
        
        if err != db.SQL_OK:
            await callback.answer(f"❌ Ошибка БД: {err}", show_alert=True)
            return
        
        if not accounts:
            await callback.message.edit_text(
                "💰 <b>Баланс</b>\n\nУ вас нет счетов.",
                parse_mode="HTML",
                reply_markup=KeyboardManager.main_menu(callback.from_user.id)
            )
            await callback.answer()
            return
        
        if len(accounts) == 1:
            text = MessageFormatter.format_balance(accounts[0])
            try:
                await callback.message.edit_text(
                    text,
                    parse_mode="HTML",
                    reply_markup=KeyboardManager.back_to_menu()
                )
            except TelegramBadRequest:
                pass
        else:
            await callback.message.edit_text(
                "Выберите счет для просмотра баланса:",
                reply_markup=KeyboardManager.account_selection(accounts)
            )
        
        await callback.answer()
    
    async def transfer_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Перевод"""
        user_id = callback.from_user.id
        
        err, accounts = db.get_accounts_by_user(user_id)
        if err == db.SQL_NOT_FOUND or not accounts:
            await callback.answer("❌ У вас нет счетов для перевода", show_alert=True)
            return
        
        if len(accounts) == 1:
            # Если только один счет, сразу используем его
            await state.set_state(TransferFSM.to_acc)
            await state.update_data(from_acc=accounts[0]['acc_number'])
            await callback.message.edit_text(
                "💸 <b>Перевод средств</b>\n\n"
                "Введите номер счета получателя или имя своего счета:\n\n"
                "• <code>ACC-XXXXXXXXX</code> - для перевода на другой счет\n"
                "• <b>Имя счета</b> - для перевода на свой счет\n\n"
                "<i>Пример: 'Накопительный' (перевод на ваш счет с таким именем)</i>",
                parse_mode="HTML",
                reply_markup=KeyboardManager.cancel_action()
            )
        else:
            # Если несколько счетов, предлагаем выбрать
            await state.set_state(TransferFSM.from_acc)
            await callback.message.edit_text(
                "💸 <b>Перевод средств</b>\n\n"
                "Выберите счет с которого будете переводить:",
                parse_mode="HTML",
                reply_markup=KeyboardManager.transfer_account_selection(accounts)
            )
        await callback.answer()
    
    async def select_transfer_from_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка выбора счета для перевода"""
        acc_number = callback.data.replace("transfer_from_", "")
        
        # Проверяем, что счет принадлежит пользователю
        err, account = db.get_account_by_acc(acc_number)
        if err != db.SQL_OK or account['user_id'] != callback.from_user.id:
            await callback.answer("❌ Ошибка выбора счета", show_alert=True)
            return
        
        await state.update_data(from_acc=acc_number)
        await state.set_state(TransferFSM.to_acc)
        await callback.message.edit_text(
            "💸 <b>Перевод средств</b>\n\n"
            "Введите номер счета получателя или имя своего счета:\n\n"
            "• <code>ACC-XXXXXXXXX</code> - для перевода на другой счет\n"
            "• <b>Имя счета</b> - для перевода на свой счет\n\n"
            "<i>Пример: 'Накопительный' (перевод на ваш счет с таким именем)</i>",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def create_account_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Новый счет"""
        await state.set_state(CreateAccountFSM.name)
        await callback.message.edit_text(
            "➕ <b>Создание нового счета</b>\n\n"
            "Введите название для нового счета:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def my_accounts_callback(self, callback: types.CallbackQuery):
        """Обработка кнопки Мои счета"""
        user_id = callback.from_user.id
        
        err, accounts = db.get_accounts_by_user(user_id)
        
        if err == db.SQL_NOT_FOUND:
            # У пользователя нет счетов
            await callback.message.edit_text(
                "📊 <b>Ваши счета</b>\n\n"
                "У вас еще нет счетов.\n"
                "Создайте первый счет через меню.",
                parse_mode="HTML",
                reply_markup=KeyboardManager.back_to_menu()
            )
            await callback.answer()
            return
        
        if err != db.SQL_OK:
            # Другая ошибка БД
            await callback.answer(f"❌ Ошибка БД: {err}", show_alert=True)
            return
        
        # Форматируем сообщение со счетами
        if not accounts:
            text = "📊 <b>Ваши счета</b>\n\nУ вас еще нет счетов."
        else:
            text = "📊 <b>Ваши счета:</b>\n\n"
            for i, acc in enumerate(accounts, 1):
                status = "🔴 Заблокирован" if acc['blocked'] else "🟢 Активен"
                text += (
                    f"{i}. <b>{acc['name']}</b>\n"
                    f"   📟 <code>{acc['acc_number']}</code>\n"
                    f"   💵 Баланс: {acc['balance']:.2f}\n"
                    f"   ⏳ В удержании: {acc['pending']:.2f}\n"
                    f"   📊 {status}\n\n"
                )
        
        try:
            await callback.message.edit_text(
                text,
                parse_mode="HTML",
                reply_markup=KeyboardManager.back_to_menu()
            )
        except TelegramBadRequest:
            # Если сообщение не изменилось
            pass
        
        await callback.answer()
    
    async def history_callback(self, callback: types.CallbackQuery):
        """Обработка кнопки История"""
        # TODO: Добавить функцию получения истории транзакций
        await callback.message.edit_text(
            "📝 <b>История операций</b>\n\n"
            "Функция в разработке...",
            parse_mode="HTML",
            reply_markup=KeyboardManager.back_to_menu()
        )
        await callback.answer()
    
    async def select_account_callback(self, callback: types.CallbackQuery):
        """Обработка выбора счета"""
        acc_number = callback.data.replace("select_acc_", "")
        
        err, account = db.get_account_by_acc(acc_number)
        if err != db.SQL_OK:
            await callback.answer(f"Ошибка получения счета: {err}", show_alert=True)
            return
        
        if account["user_id"] != callback.from_user.id:
            await callback.answer("Это не ваш счет!", show_alert=True)
            return
        
        text = MessageFormatter.format_balance({
            'name': account.get('name', 'Без имени'),
            'acc_number': acc_number,
            'balance': account['balance'],
            'pending': account.get('pending', 0),
            'blocked': account['blocked']
        })
        
        await callback.message.edit_text(
            text,
            parse_mode="HTML",
            reply_markup=KeyboardManager.back_to_menu()
        )
        await callback.answer()
    
    # ======================
    # ADMIN CALLBACK HANDLERS
    # ======================
    async def admin_panel_callback(self, callback: types.CallbackQuery):
        """Обработка кнопки Админ-панель"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        await callback.message.edit_text(
            "👑 <b>Админ-панель банк-бота</b>\n\n"
            "Выберите действие:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.admin_menu()
        )
        await callback.answer()
    
    async def admin_users_callback(self, callback: types.CallbackQuery):
        """Обработка кнопки Все пользователи"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        err, users = db.get_all_users()
        if err != db.SQL_OK:
            await callback.answer(f"❌ Ошибка БД: {err}", show_alert=True)
            return
        
        text = MessageFormatter.format_user_list(users)
        await callback.message.edit_text(
            text,
            parse_mode="HTML",
            reply_markup=KeyboardManager.back_to_admin()
        )
        await callback.answer()
    
    async def admin_accounts_callback(self, callback: types.CallbackQuery):
        """Обработка кнопки Все счета"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        err, accounts = db.get_all_accounts()
        if err != db.SQL_OK:
            await callback.answer(f"❌ Ошибка БД: {err}", show_alert=True)
            return
        
        text = MessageFormatter.format_account_list(accounts)
        await callback.message.edit_text(
            text,
            parse_mode="HTML",
            reply_markup=KeyboardManager.back_to_admin()
        )
        await callback.answer()
    
    async def admin_block_user_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Блокировка пользователя"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        await state.set_state(AdminBlockUserFSM.user_id)
        await callback.message.edit_text(
            "🔒 <b>Блокировка пользователя</b>\n\n"
            "Введите ID пользователя для блокировки:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def admin_unblock_user_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Разблокировка пользователя"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        await state.set_state(AdminBlockUserFSM.user_id)
        # Сохраняем в контексте, что это разблокировка
        await state.update_data(action="unblock")
        await callback.message.edit_text(
            "🔓 <b>Разблокировка пользователя</b>\n\n"
            "Введите ID пользователя для разблокировки:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def admin_block_account_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Блокировка счета"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        await state.set_state(AdminBlockAccountFSM.acc_number)
        await callback.message.edit_text(
            "⛔ <b>Блокировка счета</b>\n\n"
            "Введите номер счета для блокировки:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def admin_unblock_account_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Разблокировка счета"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        await state.set_state(AdminBlockAccountFSM.acc_number)
        await state.update_data(action="unblock")
        await callback.message.edit_text(
            "✅ <b>Разблокировка счета</b>\n\n"
            "Введите номер счета для разблокировки:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def admin_delete_account_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Удаление счета"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        await state.set_state(AdminDeleteAccountFSM.acc_number)
        await callback.message.edit_text(
            "🗑️ <b>Удаление счета</b>\n\n"
            "Введите номер счета для удаления:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def admin_update_balance_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Изменить баланс"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        await state.set_state(AdminUpdateBalanceFSM.acc_number)
        await callback.message.edit_text(
            "💰 <b>Изменение баланса счета</b>\n\n"
            "Введите номер счета:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def admin_add_pending_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Добавить pending правило"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        await state.set_state(AdminAddPendingFSM.target_acc)
        await callback.message.edit_text(
            "⏳ <b>Добавление правила pending</b>\n\n"
            "Введите номер счета (или оставьте пустым для всех счетов):",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def admin_global_pending_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка кнопки Глобальное pending правило"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        await state.set_state(AdminGlobalPendingFSM.delay_hours)
        await callback.message.edit_text(
            "🌍 <b>Глобальное правило pending</b>\n\n"
            "Введите время задержки в часах:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
        await callback.answer()
    
    async def admin_pending_list_callback(self, callback: types.CallbackQuery):
        """Обработка кнопки Список pending правил"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        err, rules = db.get_global_pending_rules()
        if err != db.SQL_OK:
            await callback.answer(f"❌ Ошибка БД: {err}", show_alert=True)
            return
        
        text = MessageFormatter.format_pending_rules(rules)
        await callback.message.edit_text(
            text,
            parse_mode="HTML",
            reply_markup=KeyboardManager.back_to_admin()
        )
        await callback.answer()
    
    async def admin_pending_transactions_callback(self, callback: types.CallbackQuery):
        """Обработка кнопки Pending транзакции"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        err, transactions = db.get_pending_transactions()
        if err != db.SQL_OK:
            await callback.answer(f"❌ Ошибка БД: {err}", show_alert=True)
            return
        
        text = MessageFormatter.format_pending_transactions(transactions)
        await callback.message.edit_text(
            text,
            parse_mode="HTML",
            reply_markup=KeyboardManager.back_to_admin()
        )
        await callback.answer()
    
    async def confirm_action_callback(self, callback: types.CallbackQuery, state: FSMContext):
        """Обработка подтверждения действия"""
        if callback.from_user.id not in ADMIN_ID:
            await callback.answer("❌ У вас нет доступа", show_alert=True)
            return
        
        data = callback.data.split("_")
        if len(data) < 3:
            await callback.answer("❌ Ошибка формата", show_alert=True)
            return
        
        action = data[1]
        target_data = data[2]
        
        try:
            if action == "blockuser":
                user_id = int(target_data)
                err, _ = db.block_user(user_id, True)
                if err == db.SQL_OK:
                    await callback.message.edit_text(
                        f"✅ Пользователь {user_id} заблокирован",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
                else:
                    await callback.message.edit_text(
                        f"❌ Ошибка: {err}",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
            
            elif action == "unblockuser":
                user_id = int(target_data)
                err, _ = db.block_user(user_id, False)
                if err == db.SQL_OK:
                    await callback.message.edit_text(
                        f"✅ Пользователь {user_id} разблокирован",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
                else:
                    await callback.message.edit_text(
                        f"❌ Ошибка: {err}",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
            
            elif action == "blockaccount":
                err, _ = db.block_account(target_data, True)
                if err == db.SQL_OK:
                    await callback.message.edit_text(
                        f"✅ Счет {target_data} заблокирован",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
                else:
                    await callback.message.edit_text(
                        f"❌ Ошибка: {err}",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
            
            elif action == "unblockaccount":
                err, _ = db.block_account(target_data, False)
                if err == db.SQL_OK:
                    await callback.message.edit_text(
                        f"✅ Счет {target_data} разблокирован",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
                else:
                    await callback.message.edit_text(
                        f"❌ Ошибка: {err}",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
            
            elif action == "deleteaccount":
                err, _ = db.delete_account(target_data)
                if err == db.SQL_OK:
                    await callback.message.edit_text(
                        f"✅ Счет {target_data} удален",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
                else:
                    await callback.message.edit_text(
                        f"❌ Ошибка: {err}",
                        reply_markup=KeyboardManager.back_to_admin()
                    )
        
        except Exception as e:
            await callback.message.edit_text(
                f"❌ Ошибка: {str(e)}",
                reply_markup=KeyboardManager.back_to_admin()
            )
        
        await state.clear()
        await callback.answer()
    
    # ======================
    # FSM HANDLERS
    # ======================
    async def transfer_from_acc_handler(self, message: types.Message, state: FSMContext):
        """Обработка ввода счета отправителя через сообщение"""
        # Этот обработчик для случая, если пользователь ввел номер счета вручную
        acc_number = message.text.strip()
        
        if not acc_number.startswith("ACC-"):
            await message.answer(
                "❌ Неверный формат счета.\n"
                "Используйте формат: ACC-XXXXXXXXX",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        # Проверяем, что счет принадлежит пользователю
        err, account = db.get_account_by_acc(acc_number)
        if err != db.SQL_OK or account['user_id'] != message.from_user.id:
            await message.answer(
                f"❌ Счет {acc_number} не найден или не принадлежит вам",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        await state.update_data(from_acc=acc_number)
        await state.set_state(TransferFSM.to_acc)
        
        await message.answer(
            f"✅ Выбран счет: {account['name']} ({acc_number})\n\n"
            f"Введите номер счета получателя или имя своего счета:\n\n"
            f"• <code>ACC-XXXXXXXXX</code> - для перевода на другой счет\n"
            f"• <b>Имя счета</b> - для перевода на свой счет\n\n"
            f"<i>Пример: 'Накопительный' (перевод на ваш счет с таким именем)</i>",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
    
    async def transfer_to_acc_handler(self, message: types.Message, state: FSMContext):
        """Обработка номера счета для перевода"""
        to_acc_input = message.text.strip()
        data = await state.get_data()
        from_acc = data.get("from_acc")
        user_id = message.from_user.id
        
        # Если пользователь не выбрал счет отправителя, возвращаемся к выбору
        if not from_acc:
            await state.set_state(TransferFSM.from_acc)
            await message.answer(
                "❌ Сначала выберите счет отправителя",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        to_acc = None
        
        # Проверяем, является ли ввод именем счета (не начинается с ACC-)
        if not to_acc_input.startswith("ACC-"):
            # Ищем счет по имени у текущего пользователя
            err, account = db.get_account_by_name_and_user(to_acc_input, user_id)
            if err == db.SQL_OK:
                to_acc = account['acc_number']
            else:
                await message.answer(
                    f"❌ Счет с именем '{to_acc_input}' не найден у вас\n"
                    f"Введите номер счета (ACC-...) или имя своего счета:",
                    reply_markup=KeyboardManager.cancel_action()
                )
                return
        else:
            to_acc = to_acc_input
        
        # Проверяем, что не пытаемся перевести на тот же счет
        if from_acc == to_acc:
            await message.answer(
                "❌ Нельзя перевести средства на тот же счет",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        # Проверяем существование счета получателя
        err, to_account = db.get_account_by_acc(to_acc)
        if err != db.SQL_OK:
            await message.answer(
                f"❌ Счет не найден: {to_acc}",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        if to_account.get("blocked"):
            await message.answer(
                "❌ Счет получателя заблокирован",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        await state.update_data(to_acc=to_acc)
        await state.set_state(TransferFSM.amount)
        
        # Получаем информацию о счете отправителя для отображения
        err, from_account = db.get_account_by_acc(from_acc)
        from_name = from_account.get('name', 'Без имени') if err == db.SQL_OK else 'Неизвестно'
        
        await message.answer(
            f"✅ Счет получателя найден\n\n"
            f"📤 От: {from_name} ({from_acc})\n"
            f"📥 Кому: {to_account.get('name', 'Без имени')} ({to_acc})\n\n"
            f"Введите сумму перевода:",
            reply_markup=KeyboardManager.cancel_action()
        )
    
    async def transfer_amount_handler(self, message: types.Message, state: FSMContext):
        """Обработка суммы перевода"""
        try:
            amount = float(message.text)
            if amount <= 0:
                raise ValueError
        except ValueError:
            await message.answer(
                "❌ Введите корректную положительную сумму",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        data = await state.get_data()
        to_acc = data["to_acc"]
        from_acc = data["from_acc"]
        
        # Выполняем перевод
        status, detail = op.transfer(from_acc, to_acc, amount)
        
        if status == op.MONEY_OK:
            await message.answer(
                f"✅ Перевод выполнен успешно!\n\n"
                f"📤 От: {from_acc}\n"
                f"📥 Кому: {to_acc}\n"
                f"💵 Сумма: {amount}",
                reply_markup=KeyboardManager.main_menu(message.from_user.id)
            )
        elif status == op.MONEY_PENDING_TRANSFER:
            # Обработка pending перевода
            delay_hours = detail.get("delay_hours", 24)
            reason = detail.get("reason", "Не указана")
            
            await message.answer(
                f"⏳ Перевод отправлен в ожидание!\n\n"
                f"📤 От: {from_acc}\n"
                f"📥 Кому: {to_acc}\n"
                f"💵 Сумма: {amount}\n"
                f"⏱️ Задержка: {delay_hours} часов\n"
                f"📝 Причина: {reason}\n\n"
                f"Средства будут зачислены после проверки.",
                reply_markup=KeyboardManager.main_menu(message.from_user.id)
            )
        else:
            error_message = {
                op.MONEY_ACC_NOT_FOUND: "❌ Счет не найден",
                op.MONEY_BLOCKED: "❌ Счет заблокирован",
                op.MONEY_NO_FUNDS: "❌ Недостаточно средств",
                op.MONEY_BAD_AMOUNT: "❌ Некорректная сумма",
            }.get(status, f"❌ Ошибка: {status}")
            
            await message.answer(
                f"{error_message}\n{detail if detail else ''}",
                reply_markup=KeyboardManager.main_menu(message.from_user.id)
            )
        
        await state.clear()
    
    async def create_account_name_handler(self, message: types.Message, state: FSMContext):
        """Обработка названия нового счета"""
        account_name = message.text.strip()
        
        if not account_name or len(account_name) > 50:
            await message.answer(
                "❌ Некорректное название.\n"
                "Название должно быть от 1 до 50 символов.",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        # Создаем новый счет
        status, result = op.create_account(message.from_user.id, account_name)
        
        if status == op.MONEY_OK:
            await message.answer(
                f"✅ Счет создан успешно!\n\n"
                f"🏷️ Название: {account_name}\n"
                f"📟 Номер: {result}",
                reply_markup=KeyboardManager.main_menu(message.from_user.id)
            )
        elif status == op.MONEY_BLOCKED:
            await message.answer(
                "❌ Вы заблокированы и не можете создавать новые счета",
                reply_markup=KeyboardManager.main_menu(message.from_user.id)
            )
        else:
            await message.answer(
                f"❌ Ошибка создания счета: {status}",
                reply_markup=KeyboardManager.main_menu(message.from_user.id)
            )
        
        await state.clear()
    
    # ======================
    # ADMIN FSM HANDLERS
    # ======================
    async def admin_block_user_id_handler(self, message: types.Message, state: FSMContext):
        """Обработка ID пользователя для блокировки/разблокировки"""
        try:
            user_id = int(message.text)
        except ValueError:
            await message.answer(
                "❌ Неверный формат ID. Введите числовой ID:",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        # Проверяем существование пользователя
        err, user = db.get_user_by_id(user_id)
        if err != db.SQL_OK:
            await message.answer(
                f"❌ Пользователь с ID {user_id} не найден",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        data = await state.get_data()
        action = data.get("action", "block")
        
        if action == "block":
            await state.update_data(user_id=user_id)
            await state.set_state(AdminBlockUserFSM.confirm)
            await message.answer(
                f"⚠️ <b>Подтвердите блокировку пользователя</b>\n\n"
                f"👤 Пользователь: {user['username'] or 'Без имени'}\n"
                f"🆔 ID: {user_id}\n"
                f"📅 Создан: {user['created_at']}\n"
                f"📊 Статус: {'🔴 Заблокирован' if user['blocked'] else '🟢 Активен'}\n\n"
                f"<b>Блокировка заблокирует все счета пользователя и запретит создание новых!</b>",
                parse_mode="HTML",
                reply_markup=KeyboardManager.yes_no_keyboard("blockuser", str(user_id))
            )
        else:
            # Разблокировка
            await state.update_data(user_id=user_id)
            await state.set_state(AdminBlockUserFSM.confirm)
            await message.answer(
                f"⚠️ <b>Подтвердите разблокировку пользователя</b>\n\n"
                f"👤 Пользователь: {user['username'] or 'Без имени'}\n"
                f"🆔 ID: {user_id}\n"
                f"📅 Создан: {user['created_at']}\n"
                f"📊 Статус: {'🔴 Заблокирован' if user['blocked'] else '🟢 Активен'}\n\n"
                f"<b>Разблокировка разблокирует все счета пользователя!</b>",
                parse_mode="HTML",
                reply_markup=KeyboardManager.yes_no_keyboard("unblockuser", str(user_id))
            )
    
    async def admin_block_account_number_handler(self, message: types.Message, state: FSMContext):
        """Обработка номера счета для блокировки/разблокировки"""
        acc_number = message.text.strip()
        
        if not acc_number.startswith("ACC-"):
            await message.answer(
                "❌ Неверный формат счета. Используйте ACC-XXXX:",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        # Получаем информацию о счете
        err, account = db.get_account_full_info(acc_number)
        if err != db.SQL_OK:
            await message.answer(
                f"❌ Счет {acc_number} не найден",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        data = await state.get_data()
        action = data.get("action", "block")
        
        if action == "block":
            await state.update_data(acc_number=acc_number)
            await message.answer(
                f"⚠️ <b>Подтвердите блокировку счета</b>\n\n"
                f"💳 Счет: {account['name']}\n"
                f"📟 Номер: {acc_number}\n"
                f"👤 Владелец: {account['username']}\n"
                f"🆔 ID владельца: {account['user_id']}\n"
                f"💰 Баланс: {account['balance']:.2f}\n"
                f"📊 Статус: {'🔴 Заблокирован' if account['blocked'] else '🟢 Активен'}\n\n"
                f"<b>Блокировка запретит любые операции со счетом!</b>",
                parse_mode="HTML",
                reply_markup=KeyboardManager.yes_no_keyboard("blockaccount", acc_number)
            )
        else:
            # Разблокировка
            await state.update_data(acc_number=acc_number)
            await message.answer(
                f"⚠️ <b>Подтвердите разблокировку счета</b>\n\n"
                f"💳 Счет: {account['name']}\n"
                f"📟 Номер: {acc_number}\n"
                f"👤 Владелец: {account['username']}\n"
                f"🆔 ID владельца: {account['user_id']}\n"
                f"💰 Баланс: {account['balance']:.2f}\n"
                f"📊 Статус: {'🔴 Заблокирован' if account['blocked'] else '🟢 Активen'}\n\n"
                f"<b>Разблокировка разрешит операции со счетом!</b>",
                parse_mode="HTML",
                reply_markup=KeyboardManager.yes_no_keyboard("unblockaccount", acc_number)
            )
        
        await state.clear()
    
    async def admin_delete_account_number_handler(self, message: types.Message, state: FSMContext):
        """Обработка номера счета для удаления"""
        acc_number = message.text.strip()
        
        if not acc_number.startswith("ACC-"):
            await message.answer(
                "❌ Неверный формат счета. Используйте ACC-XXXX:",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        # Получаем информацию о счете
        err, account = db.get_account_full_info(acc_number)
        if err != db.SQL_OK:
            await message.answer(
                f"❌ Счет {acc_number} не найден",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        await state.update_data(acc_number=acc_number)
        await message.answer(
            f"⚠️ <b>⚠️ ВНИМАНИЕ: УДАЛЕНИЕ СЧЕТА ⚠️</b>\n\n"
            f"💳 Счет: {account['name']}\n"
            f"📟 Номер: {acc_number}\n"
            f"👤 Владелец: {account['username']}\n"
            f"🆔 ID владельца: {account['user_id']}\n"
            f"💰 Баланс: {account['balance']:.2f}\n"
            f"⏳ В удержании: {account['pending']:.2f}\n"
            f"📊 Статус: {'🔴 Заблокирован' if account['blocked'] else '🟢 Активен'}\n\n"
            f"<b>❗ Это действие необратимо! Все средства на счете будут потеряны!</b>\n"
            f"<b>❗ Все pending транзакции будут удалены!</b>\n"
            f"<b>❗ Подтвердите удаление:</b>",
            parse_mode="HTML",
            reply_markup=KeyboardManager.yes_no_keyboard("deleteaccount", acc_number)
        )
        await state.clear()
    
    async def admin_update_balance_acc_handler(self, message: types.Message, state: FSMContext):
        """Обработка номера счета для изменения баланса"""
        acc_number = message.text.strip()
        
        if not acc_number.startswith("ACC-"):
            await message.answer(
                "❌ Неверный формат счета. Используйте ACC-XXXX:",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        # Получаем информацию о счете
        err, account = db.get_account_full_info(acc_number)
        if err != db.SQL_OK:
            await message.answer(
                f"❌ Счет {acc_number} не найден",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        await state.update_data(acc_number=acc_number)
        await state.set_state(AdminUpdateBalanceFSM.amount)
        
        await message.answer(
            f"💰 <b>Изменение баланса счета</b>\n\n"
            f"💳 Счет: {account['name']}\n"
            f"📟 Номер: {acc_number}\n"
            f"👤 Владелец: {account['username']}\n"
            f"💰 Текущий баланс: {account['balance']:.2f}\n\n"
            f"Введите новый баланс:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
    
    async def admin_update_balance_amount_handler(self, message: types.Message, state: FSMContext):
        """Обработка нового баланса счета"""
        try:
            new_balance = float(message.text)
        except ValueError:
            await message.answer(
                "❌ Введите корректную сумму:",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        data = await state.get_data()
        acc_number = data["acc_number"]
        
        err, _ = db.update_account_balance(acc_number, new_balance)
        if err == db.SQL_OK:
            await message.answer(
                f"✅ Баланс счета {acc_number} изменен на {new_balance:.2f}",
                reply_markup=KeyboardManager.back_to_admin()
            )
        else:
            await message.answer(
                f"❌ Ошибка: {err}",
                reply_markup=KeyboardManager.back_to_admin()
            )
        
        await state.clear()
    
    async def admin_add_pending_target_handler(self, message: types.Message, state: FSMContext):
        """Обработка целевого счета для pending правила"""
        target_acc = message.text.strip()
        
        if target_acc and not target_acc.startswith("ACC-"):
            await message.answer(
                "❌ Неверный формат счета. Используйте ACC-XXXX или оставьте пустым:",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        if target_acc == "":
            target_acc = None
        
        await state.update_data(target_acc=target_acc)
        await state.set_state(AdminAddPendingFSM.delay_hours)
        
        target_text = "для всех счетов" if target_acc is None else f"для счета {target_acc}"
        await message.answer(
            f"⏳ <b>Добавление правила pending {target_text}</b>\n\n"
            f"Введите время задержки в часах:",
            parse_mode="HTML",
            reply_markup=KeyboardManager.cancel_action()
        )
    
    async def admin_add_pending_delay_handler(self, message: types.Message, state: FSMContext):
        """Обработка времени задержки для pending правила"""
        try:
            delay_hours = int(message.text)
            if delay_hours <= 0:
                raise ValueError
        except ValueError:
            await message.answer(
                "❌ Введите корректное количество часов (целое число > 0):",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        await state.update_data(delay_hours=delay_hours)
        await state.set_state(AdminAddPendingFSM.reason)
        
        await message.answer(
            "📝 Введите причину задержки:",
            reply_markup=KeyboardManager.cancel_action()
        )
    
    async def admin_add_pending_reason_handler(self, message: types.Message, state: FSMContext):
        """Обработка причины для pending правила"""
        reason = message.text.strip()
        
        data = await state.get_data()
        target_acc = data.get("target_acc")
        delay_hours = data.get("delay_hours")
        
        err, rule_id = db.add_global_pending_rule(target_acc, delay_hours, reason, message.from_user.id)
        if err == db.SQL_OK:
            target_text = "для всех счетов" if target_acc is None else f"для счета {target_acc}"
            await message.answer(
                f"✅ Правило pending добавлено!\n\n"
                f"🎯 Цель: {target_text}\n"
                f"⏱️ Задержка: {delay_hours} часов\n"
                f"📝 Причина: {reason}\n"
                f"🆔 ID правила: {rule_id}",
                reply_markup=KeyboardManager.back_to_admin()
            )
        else:
            await message.answer(
                f"❌ Ошибка: {err}",
                reply_markup=KeyboardManager.back_to_admin()
            )
        
        await state.clear()
    
    async def admin_global_pending_delay_handler(self, message: types.Message, state: FSMContext):
        """Обработка времени задержки для глобального pending правила"""
        try:
            delay_hours = int(message.text)
            if delay_hours <= 0:
                raise ValueError
        except ValueError:
            await message.answer(
                "❌ Введите корректное количество часов (целое число > 0):",
                reply_markup=KeyboardManager.cancel_action()
            )
            return
        
        await state.update_data(delay_hours=delay_hours)
        await state.set_state(AdminGlobalPendingFSM.reason)
        
        await message.answer(
            "📝 Введите причину задержки:",
            reply_markup=KeyboardManager.cancel_action()
        )
    
    async def admin_global_pending_reason_handler(self, message: types.Message, state: FSMContext):
        """Обработка причины для глобального pending правила"""
        reason = message.text.strip()
        
        data = await state.get_data()
        delay_hours = data.get("delay_hours")
        
        err, rule_id = db.add_global_pending_rule(None, delay_hours, reason, message.from_user.id)
        if err == db.SQL_OK:
            await message.answer(
                f"✅ Глобальное правило pending добавлено!\n\n"
                f"🎯 Цель: Все счета\n"
                f"⏱️ Задержка: {delay_hours} часов\n"
                f"📝 Причина: {reason}\n"
                f"🆔 ID правила: {rule_id}",
                reply_markup=KeyboardManager.back_to_admin()
            )
        else:
            await message.answer(
                f"❌ Ошибка: {err}",
                reply_markup=KeyboardManager.back_to_admin()
            )
        
        await state.clear()
    
    # ======================
    # UTILITY METHODS
    # ======================
    async def show_accounts(self, message: types.Message, callback: types.CallbackQuery = None):
        """Показать все счета пользователя"""
        user_id = message.from_user.id if hasattr(message, 'from_user') else callback.from_user.id
        
        err, accounts = db.get_accounts_by_user(user_id)
        
        if err == db.SQL_NOT_FOUND:
            # У пользователя нет счетов
            text = "📊 <b>Ваши счета</b>\n\nУ вас еще нет счетов.\nСоздайте первый счет через меню."
            keyboard = KeyboardManager.back_to_menu()
        elif err != db.SQL_OK:
            # Ошибка БД
            error_msg = f"❌ Ошибка БД: {err}"
            if callback:
                await callback.answer(error_msg, show_alert=True)
            else:
                await message.answer(error_msg)
            return
        else:
            # Есть счета
            if not accounts:
                text = "📊 <b>Ваши счета</b>\n\nУ вас еще нет счетов."
            else:
                text = "📊 <b>Ваши счета:</b>\n\n"
                for i, acc in enumerate(accounts, 1):
                    status = "🔴 Заблокирован" if acc['blocked'] else "🟢 Активен"
                    text += (
                        f"{i}. <b>{acc['name']}</b>\n"
                        f"   📟 <code>{acc['acc_number']}</code>\n"
                        f"   💵 Баланс: {acc['balance']:.2f}\n"
                        f"   ⏳ В удержании: {acc['pending']:.2f}\n"
                        f"   📊 {status}\n\n"
                    )
            keyboard = KeyboardManager.back_to_menu()
        
        if callback:
            try:
                await callback.message.edit_text(
                    text,
                    parse_mode="HTML",
                    reply_markup=keyboard
                )
                await callback.answer()
            except TelegramBadRequest:
                await callback.answer()
        else:
            await message.answer(text, parse_mode="HTML", reply_markup=keyboard)


# ======================
# APPLICATION SETUP
# ======================
class BankBotApplication:
    """Основной класс приложения банк-бота"""
    
    def __init__(self, token: str):
        self.token = token
        self.session = AiohttpSession() 
        session = AiohttpSession(proxy=PROXY)
        self.bot = Bot(token=token)
        self.dp = Dispatcher()
        self.router = Router()
        
        # Инициализация
        self.setup()
    
    def setup(self):
        """Настройка приложения"""
        # Подключаем роутер
        self.dp.include_router(self.router)
        
        # Инициализация обработчиков
        BotHandlers(self.router)
    
    async def run(self):
        """Запуск бота"""
        # Инициализация БД
        err, detail = db.init_db()
        if err != db.SQL_OK:
            print(f"Ошибка инициализации БД: {err} - {detail}")
            return
        
        print("Бот запущен...")
        try:
            await self.bot.delete_webhook(drop_pending_updates=True)
            await self.dp.start_polling(self.bot)
        finally:
            await self.bot.session.close()


# ======================
# MAIN ENTRY POINT
# ======================
async def main():
    """Точка входа"""
    app = BankBotApplication(TOKEN)
    await app.run()


if __name__ == "__main__":
    asyncio.run(main())
    