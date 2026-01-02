from dataclasses import dataclass, field

@dataclass
class Pending:
    amount: float
    release_at: float
    comment: str = ""

@dataclass
class Account:
    name: str
    acc_number: str
    balance: float
    blocked: bool = False
    pending: list[Pending] = field(default_factory=list)
