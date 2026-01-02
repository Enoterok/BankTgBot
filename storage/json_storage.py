import json
import os
import threading
from storage.base import Storage
from storage.models import User, Account, Pending


class JsonStorage(Storage):

    def __init__(self, path: str):
        self.path = path
        self.lock = threading.Lock()
        self._ensure()

    def _ensure(self):
        if not os.path.exists(self.path):
            with open(self.path, "w", encoding="utf-8") as f:
                json.dump({"users": {}}, f, indent=2)

    def _load(self):
        with open(self.path, "r", encoding="utf-8") as f:
            return json.load(f)

    def _save(self, data):
        tmp = self.path + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2)
        os.replace(tmp, self.path)

    # =========================

    def get_user(self, user_id: int):
        with self.lock:
            data = self._load()
            raw = data["users"].get(str(user_id))
            if not raw:
                return None

            accounts = {}
            for name, acc in raw["accounts"].items():
                accounts[name] = Account(
                    name=name,
                    balance=acc["balance"],
                    acc_number=acc["acc_number"],
                    blocked=acc.get("blocked", False),
                    pending=[
                        Pending(**p) for p in acc.get("pending", [])
                    ]
                )

            return User(
                user_id=user_id,
                username=raw.get("username", ""),
                created_at=int(raw.get("created_at", 0)),
                accounts=accounts
            )

    def save_user(self, user: User):
        with self.lock:
            data = self._load()
            data["users"][str(user.user_id)] = {
                "username": user.username,
                "created_at": user.created_at,
                "accounts": {
                    name: {
                        "balance": acc.balance,
                        "acc_number": acc.acc_number,
                        "blocked": acc.blocked,
                        "pending": [
                            {
                                "amount": p.amount,
                                "release_at": p.release_at,
                                "comment": p.comment
                            } for p in acc.pending
                        ]
                    }
                    for name, acc in user.accounts.items()
                }
            }
            self._save(data)

    def find_account(self, acc_number: str):
        with self.lock:
            data = self._load()
            for uid, u in data["users"].items():
                for name, acc in u["accounts"].items():
                    if acc["acc_number"] == acc_number:
                        return int(uid), name
        return None

    def find_user_by_username(self, username: str):
        username = username.lower()
        matches = []

        with self.lock:
            data = self._load()
            for uid, u in data["users"].items():
                if u.get("username", "").lower() == username:
                    matches.append(int(uid))

        return matches
