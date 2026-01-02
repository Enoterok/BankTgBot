from dataclasses import dataclass, field
from typing import Dict, List


@dataclass
class Pending:
    amount: float
    release_at: float
    comment: str = ""


@dataclass
class Account:
    name: str
    balance: float
    acc_number: str
    blocked: bool = False
    pending: List[Pending] = field(default_factory=list)


@dataclass
class User:
    user_id: int
    username: str
    created_at: int
    accounts: Dict[str, Account]
