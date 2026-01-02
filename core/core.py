import asyncio
import random
from datetime import datetime

from .storage import JsonStorage
from info import ADMIN_ID

class Core:
    def __init__(self, storage):
        self.storage = storage
        self.lock = asyncio.Lock()

        self.functions = {
            "register": self.register,
            "balance": self.balance,
            "transfer": self.transfer,
        }

    # ============================
    async def register(self, message):
        uid = message.from_user.id
        username = message.from_user.username or ""

        async with self.lock:
            db = await self.storage.load()
            users = db.setdefault("users", {})

            if await self.storage.get_user(uid):
                return "Вы уже зарегистрированы"
            
            await self.storage.create_user(uid, username)
            return "✅ Регистрация успешна"

        return f"✅ Аккаунт создан\n{acc}"

    # ============================
    async def balance(self, message):
        user_id = str(message.from_user.id)

        async with self.lock:
            db = await self.storage.load()
            user = db["users"].get(user_id)

            if not user:
                return "Вы не зарегистрированы"

            lines = []
            for name, acc in user["accounts"].items():
                pending = sum(p["amount"] for p in acc.get("pending", []))
                status = "🚫" if acc.get("blocked") else "✅"

                lines.append(
                    f"{name} | {acc['acc_number']}\n"
                    f"💰 {acc['balance']} | ⏳ {pending} | {status}"
                )

        return "\n\n".join(lines)
