# comands.py
import telebot
import info
import os
import json
import threading
import time
import random
from datetime import datetime
import sys

bot = telebot.TeleBot(info.TOKEN)

DB_PATH = 'db/db.json'
TRANS_LOG = 'logs/transLog.log'
FEEDBACK_LOG = 'logs/feedback.log'

HOLD_LIMIT = 10000
HOLD_TIME = 3600  # 1 час

def is_admin(Uid):
    return Uid in info.ADMIN_ID

def generate_acc_number():
    prefix = "ACC-"
    number = random.randint(100000000, 999999999)
    return prefix + str(number)

command_list_user = """
/register (или /r) - зарегистрировать аккаунт
/balance (или /b) - проверить баланс
/transfer (или /t) <счёт-откуда> <сумма> <куда> [комментарий] - перевести средства
/feedback (или /f) <текст> - обратная связь
/myid (или /m) - показать ваш Telegram ID
"""

command_list_admin = """
/stats - статистика
/masschange <add|sub|set> <сумма> [исключения] - массовое изменение балансов
/editbalance <ACC> <add|sub|set> <сумма> [причина] - редактирование баланса
/reply <user_id> <текст> - ответ пользователю
/reply_all <текст> - глобальное сообщение всем пользователям
/block <ACC> [причина] - блокировка счёта
/unblock <ACC> - разблокировка счёта
/reset - сброс базы и логов
/shutdown - выключение бота
/restart - перезапуск бота
"""

class CommandsInclude:
    def __init__(self):
        self.db_path = DB_PATH
        self.lock = threading.Lock()
        self._ensure_db()
        self.functions = {
            # пользовательские команды
            'register': self.register, 'r': self.register,
            'balance': self.balance, 'b': self.balance,
            'transfer': self.transfer, 't': self.transfer,
            'feedback': self.feedback, 'f': self.feedback,
            'myid': self.myid, 'm': self.myid,

            # админ команды
            'stats': self.stats,
            'masschange': self.masschange,
            'editbalance': self.editbalance,
            'reply': self.reply,
            'reply_all': self.reply_all,
            'block': self.block,
            'unblock': self.unblock,
            'reset': self.reset,
            'shutdown': self.shutdown,
            'restart': self.restart
        }

    # ====== Работа с базой ======
    def _ensure_db(self):
        if not os.path.exists(self.db_path):
            with open(self.db_path, 'w', encoding='utf-8') as f:
                json.dump({'users': {}}, f, ensure_ascii=False, indent=2)

    

    def _get_user(self, user_id):
        db = self._load_db()
        return db['users'].get(str(user_id))

    def _save_user(self, user_id, user_data):
        db = self._load_db()
        db['users'][str(user_id)] = user_data
        self._save_db(db)

    def _find_user_by_acc_number(self, acc_number):
        db = self._load_db()
        for uid, u in db['users'].items():
            for acc_name, acc_data in u.get('accounts', {}).items():
                if acc_data.get('acc_number') == acc_number:
                    return int(uid), acc_name
        return None, None

    # ====== Логирование ======
    def _log_transaction(self, text):
        with open(TRANS_LOG, 'a', encoding='utf-8') as f:
            f.write(f"[{datetime.now()}] {text}\n")

    def _log_feedback(self, user, text):
        with open(FEEDBACK_LOG, 'a', encoding='utf-8') as f:
            f.write(f"[{datetime.now()}] {user}: {text}\n")

    # ====== Команды пользователя ======
    def register(self, message):
        user_id = message.from_user.id
        username = message.from_user.username or ''
        with self.lock:
            db = self._load_db()
            users = db.setdefault('users', {})
            key = str(user_id)
            if key in users:
                return "Вы уже зарегистрированы."
            acc_number = generate_acc_number()
            users[key] = {
                'username': username,
                'created_at': str(message.date),
                'accounts': {
                    'main': {
                        'balance': 0.0,
                        'acc_number': acc_number,
                        'blocked': False,
                        'pending': []
                    }
                }
            }
            self._save_db(db)
        return f"✅ Регистрация успешна! Ваш основной счёт 'main' создан.\nНомер счёта: {acc_number}"

    def balance(self, message):
        user_id = message.from_user.id
        with self.lock:
            user = self._get_user(user_id)
            if not user:
                return "Вы не зарегистрированы. Введите /register"
            lines = []
            for name, data in user['accounts'].items():
                status = "🚫 Заблокирован" if data.get('blocked') else "✅ Активен"
                pending_sum = sum(
                    p['amount'] for p in data.get('pending', [])
                )
                lines.append(
                    f"💳 {name} - {data['acc_number']}\n"
                    f"💰 Баланс: {data['balance']}\n"
                    f"⏳ В удержании: {pending_sum}\n"
                    f"{status}"
                )
        return "\n".join(lines)

    def transfer(self, message):
        parts = message.text.split(maxsplit=4)
        if len(parts) < 4:
            return "Использование: /transfer <откуда> <сумма> <куда> [комментарий]"

        from_acc = parts[1].strip()

        try:
            amount = float(parts[2])
        except ValueError:
            return "❌ Сумма должна быть числом."

        # 🔒 Проверки суммы
        if amount <= 0:
            return "❌ Сумма перевода должна быть положительным числом."
        if amount > 1_000_000:
            return "❌ Сумма слишком велика. Максимум — 1 000 000."

        target = parts[3].strip()
        comment = parts[4] if len(parts) > 4 else ""
        sender_id = message.from_user.id

        with self.lock:
            sender = self._get_user(sender_id)
            if not sender:
                return "Вы не зарегистрированы. Введите /register"

            if from_acc not in sender['accounts']:
                return f"Счёт '{from_acc}' не найден."

            if sender['accounts'][from_acc].get('blocked'):
                return f"🚫 Счёт '{from_acc}' заблокирован."

            if sender['accounts'][from_acc]['balance'] < amount:
                return "❌ Недостаточно средств."

            # 🔁 Перевод между своими счетами
            if not target.startswith("ACC-") and not target.startswith("@"):
                if target not in sender['accounts']:
                    return f"Счёт '{target}' не найден среди ваших."
                if from_acc == target:
                    return "❌ Нельзя переводить самому себе на тот же счёт."
                if sender['accounts'][target].get('blocked'):
                    return f"🚫 Целевой счёт '{target}' заблокирован."

                sender['accounts'][from_acc]['balance'] -= amount
                sender['accounts'][target]['balance'] += amount
                self._save_user(sender_id, sender)
                self._log_transaction(
                    f"SELF_TRANSFER | {sender_id}:{from_acc}->{target} | {amount} | {comment}"
                )
                return (
                    f"✅ Переведено {amount} с '{from_acc}' на '{target}' (свой счёт)\n"
                    f"💬 {comment or 'Без комментария'}"
                )


            
            # 👤 Перевод по username (@username → main)
            if target.startswith("@"):
                target_id = self._find_user_by_username(target)

                if target_id == "AMBIGUOUS":
                    return (
                        "❌ Найдено несколько пользователей с таким username.\n"
                        "➡️ Используйте номер счёта (ACC-XXXXXX)."
                    )

                if not target_id:
                    return "❌ Пользователь не найден."

                if target_id == sender_id:
                    return "❌ Используйте имя счёта (main / sub1) для перевода между своими счетами."
                
                recipient = self._get_user(target_id)
                target_acc = "main"

                if target_acc not in recipient['accounts']:
                    return "❌ У получателя нет основного счёта."

                if recipient['accounts'][target_acc].get('blocked'):
                    return "🚫 Счёт получателя заблокирован."

            # 🎯 Перевод по ACC-номеру
            else:
                target_id, target_acc = self._find_user_by_acc_number(target)
                if not target_id:
                    return "❌ Целевой счёт не найден."

                recipient = self._get_user(target_id)
                if recipient['accounts'][target_acc].get('blocked'):
                    return f"🚫 Целевой счёт {target} заблокирован."

            # 💸 Списание / зачисление
            sender['accounts'][from_acc]['balance'] -= amount
            recipient['accounts'][target_acc]['balance'] += amount

            self._save_user(sender_id, sender)
            self._save_user(target_id, recipient)

            self._log_transaction(
                f"TRANSFER | from:{sender_id}/{from_acc} -> to:{target_id}/{target_acc} | {amount} | {comment}"
            )

            # ✉️ Уведомление получателю
            try:
                bot.send_message(
                    target_id,
                    f"📩 Вам поступил перевод {amount} от пользователя {sender_id}\n"
                    f"💬 {comment or 'Без комментария'}"
                )
            except Exception as e:
                print(f"[!] Не удалось отправить уведомление: {e}")

        return f"✅ Переведено {amount} → {target}\n💬 {comment or 'Без комментария'}"


    def feedback(self, message):
        parts = message.text.split(maxsplit=1)
        if len(parts) < 2:
            return "Использование: /feedback <текст>"
        text = parts[1].strip()
        user_tag = f"@{message.from_user.username}" if message.from_user.username else f"id:{message.from_user.id}"

        # Логируем в файл
        self._log_feedback(user_tag, text)

        # Подготовка списка админов (поддерживаем int, str, list, tuple)
        admins = info.ADMIN_ID
        if isinstance(admins, (int, str)):
            admins = [admins]
        elif admins is None:
            admins = []

        # Отправляем каждому админу
        for adm in admins:
            try:
                adm_id = int(adm)
            except Exception:
                # если не получилось привести к int — пропускаем
                continue
            try:
                bot.send_message(adm_id, f"📨 Новое сообщение через /feedback\nОт: {user_tag}\nID: {message.from_user.id}\n\nТекст:\n{text}")
            except Exception as e:
                print(f"[!] Ошибка при отправке админу {adm_id}: {e}")

        return "✅ Сообщение передано администрации."


    def myid(self, message):
        return f"Ваш Telegram ID: {message.from_user.id}"
    
    def _find_user_by_username(self, username):
        db = self._load_db()
        username = username.lstrip('@').lower()

        matches = []

        for uid, u in db.get('users', {}).items():
            if u.get('username', '').lower() == username:
                matches.append(int(uid))

        if len(matches) == 1:
            return matches[0]

        if len(matches) > 1:
            return "AMBIGUOUS"  # ⚠️ неоднозначно

        return None


    # ====== Админ-команды ======
    def stats(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        db = self._load_db()
        return f"📊 Зарегистрировано пользователей: {len(db['users'])}"

    def reply(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        parts = message.text.split(maxsplit=2)
        if len(parts) < 3:
            return "Использование: /reply <user_id> <текст>"
        try:
            target_id = int(parts[1])
        except:
            return "Неверный user_id"
        text = "💬 Сообщение от администратора:\n" + parts[2]
        try:
            bot.send_message(target_id, text)
        except:
            return "Ошибка при отправке"
        return f"✅ Ответ отправлен {target_id}"
    
    def reply_all(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        parts = message.text.split(maxsplit=1)
        if len(parts) < 2:
            return "Использование: /reply_all <текст>"
        text = "📢 Сообщение от администратора:\n" + parts[1]

        sent, failed = 0, 0
        try:
            with open(DB_PATH, "r", encoding="utf-8") as f:
                data = json.load(f)
            users = data.get("users", {})
        except Exception as e:
            return f"Ошибка чтения базы данных: {e}"

        for user_id in users.keys():
            try:
                bot.send_message(int(user_id), text)
                sent += 1
                time.sleep(0.3)  # пауза для избежания flood limit
            except Exception as e:
                print(f"[!] Ошибка при отправке пользователю {user_id}: {e}")
                failed += 1

        return f"✅ Рассылка завершена.\n📨 Успешно: {sent}\n⚠️ Ошибок: {failed}"


    def block(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        parts = message.text.split(maxsplit=2)
        if len(parts) < 2:
            return "Использование: /block <ACC> [причина]"
        acc = parts[1]
        reason = parts[2] if len(parts) > 2 else "Не указана"
        uid, acc_name = self._find_user_by_acc_number(acc)
        if not uid:
            return "❌ Счёт не найден."
        user = self._get_user(uid)
        user['accounts'][acc_name]['blocked'] = True
        self._save_user(uid, user)
        self._log_transaction(f"BLOCK | {acc} | {reason}")
        try:
            bot.send_message(uid, f"🚫 Ваш счёт {acc} был заблокирован. Причина: {reason}")
        except:
            pass
        return f"✅ Счёт {acc} заблокирован."

    def unblock(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        parts = message.text.split()
        if len(parts) < 2:
            return "Использование: /unblock <ACC>"
        acc = parts[1]
        uid, acc_name = self._find_user_by_acc_number(acc)
        if not uid:
            return "❌ Счёт не найден."
        user = self._get_user(uid)
        user['accounts'][acc_name]['blocked'] = False
        self._save_user(uid, user)
        self._log_transaction(f"UNBLOCK | {acc}")
        try:
            bot.send_message(uid, f"✅ Ваш счёт {acc} разблокирован.")
        except:
            pass
        return f"✅ Счёт {acc} разблокирован."

    def masschange(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        parts = message.text.split()
        if len(parts) < 3:
            return "Использование: /masschange <add|sub|set> <сумма> [исключения]"
        action = parts[1].lower()
        try:
            amount = float(parts[2])
        except:
            return "Сумма должна быть числом."
        exclude_accs = parts[3:] if len(parts) > 3 else []
        with self.lock:
            db = self._load_db()
            for uid, user in db['users'].items():
                for acc_name, acc in user['accounts'].items():
                    if acc['acc_number'] in exclude_accs:
                        continue
                    if action == 'add':
                        acc['balance'] += amount
                    elif action == 'sub':
                        acc['balance'] -= amount
                    elif action == 'set':
                        acc['balance'] = amount
            self._save_db(db)
        return f"✅ Балансы изменены ({action} {amount})"

    def editbalance(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        parts = message.text.split()
        if len(parts) < 4:
            return "Использование: /editbalance <ACC> <add|sub|set> <сумма> [причина]"
        acc_number = parts[1]
        action = parts[2].lower()
        try:
            amount = float(parts[3])
        except:
            return "Сумма должна быть числом."
        reason = " ".join(parts[4:]) if len(parts) > 4 else ""
        with self.lock:
            uid, acc_name = self._find_user_by_acc_number(acc_number)
            if not uid:
                return "Счёт не найден."
            user = self._get_user(uid)
            if user['accounts'][acc_name].get('blocked'):
                return f"🚫 Счёт {acc_number} заблокирован."
            if action == 'add':
                user['accounts'][acc_name]['balance'] += amount
            elif action == 'sub':
                user['accounts'][acc_name]['balance'] -= amount
            elif action == 'set':
                user['accounts'][acc_name]['balance'] = amount
            self._save_user(uid, user)
            self._log_transaction(f"ADMIN_EDIT | {acc_number} {action} {amount} ({reason})")
        return f"✅ Баланс {acc_number} обновлён."

    def reset(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        with self.lock:
            self._save_db({'users': {}})
            open(TRANS_LOG, 'w').close()
            open(FEEDBACK_LOG, 'w').close()
        return "♻️ База и логи сброшены."

    def shutdown(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        sys.exit(0)

    def restart(self, message):
        if not is_admin(message.from_user.id):
            return "❌ Нет доступа"
        python = sys.executable
        os.execl(python, python, *sys.argv)
