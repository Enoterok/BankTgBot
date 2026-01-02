import aiosqlite

class SQLStorage:
    def __init__(self, path: str):
        self.path = path

    async def connect(self):
        return await aiosqlite.connect(self.path)

    # -------------------------
    async def get_user(self, user_id: int):
        async with await self.connect() as db:
            async with db.execute(
                "SELECT id, username FROM users WHERE id = ?",
                (user_id,)
            ) as cur:
                return await cur.fetchone()

    async def create_user(self, user_id: int, username: str):
        async with await self.connect() as db:
            await db.execute(
                "INSERT INTO users (id, username) VALUES (?, ?)",
                (user_id, username)
            )
            await db.execute(
                """
                INSERT INTO accounts (user_id, name, acc_number)
                VALUES (?, 'main', 'ACC-' || abs(random()))
                """,
                (user_id,)
            )
            await db.commit()
