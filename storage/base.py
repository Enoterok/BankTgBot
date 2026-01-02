from abc import ABC, abstractmethod
from typing import Optional, List, Tuple
from storage.models import User


class Storage(ABC):

    @abstractmethod
    def get_user(self, user_id: int) -> Optional[User]:
        pass

    @abstractmethod
    def save_user(self, user: User):
        pass

    @abstractmethod
    def find_account(self, acc_number: str) -> Optional[Tuple[int, str]]:
        """Возвращает (user_id, acc_name)"""
        pass

    @abstractmethod
    def find_user_by_username(self, username: str) -> List[int]:
        pass
