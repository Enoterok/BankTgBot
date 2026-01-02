from dataclasses import dataclass
from .account import Account

@dataclass
class User:
    user_id: int
    username: str
    created_at: float
    accounts: dict[str, Account]
