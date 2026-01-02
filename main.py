import asyncio
import logging

from aiogram import Bot, Dispatcher, F
from aiogram.types import Message
from aiogram.filters import Command

import info
from comands import CommandsInclude

# =====================================

logging.basicConfig(level=logging.INFO)

bot = Bot(token=info.TOKEN)
dp = Dispatcher()

core = CommandsInclude()  # твоя логика

# =====================================
# START / LIST
# =====================================

@dp.message(Command("start"))
async def start(message: Message):
    await message.answer(
        "Добро пожаловать!\n"
        "Используйте /register для создания аккаунта\n"
        "/list — список команд"
    )


@dp.message(Command("list"))
async def cmd_list(message: Message):
    if core and message.from_user.id in info.ADMIN_ID:
        await message.answer(
            core.command_list_user + core.command_list_admin
        )
    else:
        await message.answer(core.command_list_user)

# =====================================
# Роутер команд
# =====================================

@dp.message(F.text.startswith("/"))
async def command_router(message: Message):
    cmd = message.text.split()[0][1:].split("@")[0]
    func = core.functions.get(cmd)

    if not func:
        await message.answer("❌ Неизвестная команда")
        return

    try:
        # ⚠️ core у тебя синхронный → выносим в thread
        response = await asyncio.to_thread(func, message)

        if response:
            await message.answer(response)

    except Exception as e:
        logging.exception("Ошибка обработки команды")
        await message.answer("⚠️ Внутренняя ошибка. Администратор уведомлён.")

# =====================================
# Запуск
# =====================================

async def main():
    await dp.start_polling(bot)

if __name__ == "__main__":
    asyncio.run(main())
