import asyncio
import datetime

class PendingProcessor:
    def __init__(self, storage, interval=10):
        self.storage = storage
        self.interval = interval
        self.running = False

    async def start(self):
        self.running = True
        while self.running:
            try:
                await self.process()
            except Exception as e:
                print(f"[PENDING ERROR] {e}")
            await asyncio.sleep(self.interval)

    async def stop(self):
        self.running = False

    async def process(self):
        async with await self.storage.connect() as db:
            async with db.execute("""
                SELECT id, account_id, amount
                FROM pending
                WHERE processed = 0
                  AND release_at <= CURRENT_TIMESTAMP
            """) as cur:
                rows = await cur.fetchall()

            for pid, acc_id, amount in rows:
                await db.execute("BEGIN")

                await db.execute("""
                    UPDATE accounts
                    SET balance = balance + ?
                    WHERE id = ?
                """, (amount, acc_id))

                await db.execute("""
                    UPDATE pending
                    SET processed = 1
                    WHERE id = ?
                """, (pid,))

                await db.commit()

                print(f"[PENDING] Released {amount} to account {acc_id}")
