import asyncio
from aiogram.exceptions import TelegramBadRequest
from aiogram import Bot, Dispatcher, Router, F, types
from aiogram.filters import Command
from aiogram.fsm.context import FSMContext
from aiogram.fsm.state import StatesGroup, State
from aiogram.utils.keyboard import InlineKeyboardBuilder

from core import moneyOP as op
from core import sqlite as db
from info import TOKEN

# ======================
# FSM
# ======================
class TransferFSM(StatesGroup):
    to_acc = State()
    amount = State()


router = Router()


# ======================
# KEYBOARDS
# ======================
def main_menu():
    kb = InlineKeyboardBuilder()
    kb.button(text="💰 Баланс", callback_data="balance")
    kb.button(text="💸 Перевод", callback_data="transfer")
    kb.adjust(1)
    return kb.as_markup()


# ======================
# START
# ======================
@router.message(Command("start"))
async def start_cmd(message: types.Message):
    status, detail = op.register(
        message.from_user.id,
        message.from_user.username or "unknown"
    )

    if status != op.MONEY_OK:
        await message.answer(f"❌ Ошибка регистрации: {status}")
        return

    text = "👋 С возвращением!" if detail == "already_registered" else "🏦 Добро пожаловать в банк!"
    await message.answer(text, reply_markup=main_menu())


# ======================
# BALANCE
# ======================
@router.callback_query(F.data == "balance")
async def balance_cb(cb: types.CallbackQuery):
    err, accounts = db.get_accounts_by_user(cb.from_user.id)
    if err != db.SQL_OK:
        await cb.answer(f"Ошибка БД: {err}", show_alert=True)
        return

    lines = []
    for acc in accounts:
        lines.append(
            f"💳 <b>{acc['name']}</b>\n"
            f"{acc['acc_number']}\n"
            f"Баланс: {acc['balance']}\n"
            f"В удержании: {acc['pending']}\n"
        )
        
    try:
        await cb.message.edit_text(
            "\n".join(lines),
            reply_markup=main_menu(),
            parse_mode="HTML"
        )
        await cb.answer()
    except TelegramBadRequest:
        await cb.answer("Баланс уже актуальный")

    


# ======================
# TRANSFER START
# ======================
@router.callback_query(F.data == "transfer")
async def transfer_start(cb: types.CallbackQuery, state: FSMContext):
    await state.set_state(TransferFSM.to_acc)
    await cb.message.answer("Введите номер счёта получателя (ACC-XXXX):")
    await cb.answer()


# ======================
# TRANSFER TO_ACC
# ======================
@router.message(TransferFSM.to_acc)
async def transfer_to_acc(message: types.Message, state: FSMContext):
    if not message.text.startswith("ACC-"):
        await message.answer("❌ Неверный формат счёта.")
        return

    await state.update_data(to_acc=message.text.strip())
    await state.set_state(TransferFSM.amount)
    await message.answer("Введите сумму перевода:")


# ======================
# TRANSFER AMOUNT
# ======================
@router.message(TransferFSM.amount)
async def transfer_amount(message: types.Message, state: FSMContext):
    try:
        amount = float(message.text)
        if amount <= 0:
            raise ValueError
    except ValueError:
        await message.answer("❌ Введите корректную сумму.")
        return

    data = await state.get_data()
    to_acc = data["to_acc"]

    err, from_acc = db.get_main_account(message.from_user.id)
    if err != db.SQL_OK:
        await message.answer(f"Ошибка БД: {err}")
        await state.clear()
        return

    status, detail = op.transfer(from_acc["acc_number"], to_acc, amount)

    if status == op.MONEY_OK:
        await message.answer("✅ Перевод выполнен.", reply_markup=main_menu())
    else:
        await message.answer(f"❌ Ошибка: {status}")

    await state.clear()


# ======================
# RUN
# ======================
async def main():
    err, detail = db.init_db()
    if err != db.SQL_OK:
        print(f"DB INIT ERROR: {err}", detail)
        return

    bot = Bot(TOKEN)
    dp = Dispatcher()
    dp.include_router(router)

    await dp.start_polling(bot)


if __name__ == "__main__":
    asyncio.run(main())
