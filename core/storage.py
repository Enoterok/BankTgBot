import json
import aiofiles
import os

class JsonStorage:
    def __init__(self, path: str):
        self.path = path

    async def load(self):
        if not os.path.exists(self.path):
            return {"users": {}}

        async with aiofiles.open(self.path, "r", encoding="utf-8") as f:
            raw = await f.read()
            if not raw.strip():
                return {"users": {}}
            return json.loads(raw)

    async def save(self, data: dict):
        tmp = self.path + ".tmp"
        async with aiofiles.open(tmp, "w", encoding="utf-8") as f:
            await f.write(json.dumps(data, ensure_ascii=False, indent=2))
        os.replace(tmp, self.path)
