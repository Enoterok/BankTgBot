from time import time
from storage.base import Storage
from core.exceptions import (
    UserNotFound,
    AccountNotFound,
    AccountBlocked,
    NotEnoughBalance,
    InvalidAmount,
    AmbiguousUsername,
    SelfTransferNotAllowed
)

HOLD_LIMIT = 10_000
HOLD_TIME = 3600  # 1 час


def transfer(
    storage: Storage,
    sender_id: int,
    from_acc: str,
    target: str,
    amount: float,
    comment: str = ""
):
    # 🔒 Проверка суммы
    if amount <= 0:
        raise InvalidAmount("Сумма должна быть положительной")

    if amount > 1_000_000:
        raise InvalidAmount("Сумма слишком велика")

    sender = storage.get_user(sender_id)
    if not sender:
        raise UserNotFound("Отправитель не найден")

    if from_acc not in sender.accounts:
        raise AccountNotFound("Счёт отправителя не найден")

    from_account = sender.accounts[from_acc]

    if from_account.blocked:
        raise AccountBlocked("Счёт отправителя заблокирован")

    if from_account.balance < amount:
        raise NotEnoughBalance("Недостаточно средств")

    # ===============================
    # 🔁 Перевод между СВОИМИ счетами
    # ===============================
    if not target.startswith("ACC-") and not target.startswith("@"):
        if target not in sender.accounts:
            raise AccountNotFound("Целевой счёт не найден")

        if target == from_acc:
            raise SelfTransferNotAllowed("Нельзя переводить на тот же счёт")

        to_account = sender.accounts[target]

        if to_account.blocked:
            raise AccountBlocked("Целевой счёт заблокирован")

        from_account.balance -= amount
        to_account.balance += amount

        storage.save_user(sender)

        return {
            "type": "self",
            "from": from_acc,
            "to": target,
            "amount": amount,
            "comment": comment
        }

    # ===============================
    # 👤 Перевод по username
    # ===============================
    if target.startswith("@"):
        username = target.lstrip("@")
        matches = storage.find_user_by_username(username)

        if len(matches) > 1:
            raise AmbiguousUsername("Несколько пользователей с таким username")

        if not matches:
            raise UserNotFound("Пользователь не найден")

        target_id = matches[0]

        if target_id == sender_id:
            raise SelfTransferNotAllowed(
                "Используйте имя счёта для перевода самому себе"
            )

        recipient = storage.get_user(target_id)
        to_account = recipient.accounts.get("main")

        if not to_account:
            raise AccountNotFound("У получателя нет основного счёта")

    # ===============================
    # 🎯 Перевод по ACC-номеру
    # ===============================
    else:
        found = storage.find_account(target)
        if not found:
            raise AccountNotFound("Счёт не найден")

        target_id, acc_name = found
        recipient = storage.get_user(target_id)
        to_account = recipient.accounts[acc_name]

    if to_account.blocked:
        raise AccountBlocked("Целевой счёт заблокирован")

    # ===============================
    # ⏳ Удержание (pending)
    # ===============================
    from_account.balance -= amount

    if amount >= HOLD_LIMIT:
        to_account.pending.append({
            "amount": amount,
            "release_at": time() + HOLD_TIME,
            "comment": comment
        })
        status = "pending"
    else:
        to_account.balance += amount
        status = "instant"

    storage.save_user(sender)
    storage.save_user(recipient)

    return {
        "type": "external",
        "status": status,
        "from": from_acc,
        "to_user": target_id,
        "to_account": to_account.name,
        "amount": amount,
        "comment": comment
    }
