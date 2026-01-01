# main.py
import telebot
import info
import comands
import time
import sys
from datetime import datetime

bot = telebot.TeleBot(info.TOKEN)
commandsExpl = comands.CommandsInclude()

def loging(message, response):
    with open('logs/logs.log', 'a', encoding='utf-8') as f:
        f.write(f"[{datetime.now()}] {message.from_user.id} ({message.from_user.username}): {message.text}\n")
        f.write(f"Bot: {response}\n\n")

@bot.message_handler(commands=['start'])
def start_message(message):
    bot.send_message(message.chat.id, "Добро пожаловать! Используйте /register для создания аккаунта. /list - Справка")

@bot.message_handler(commands=['list'])
def list_message(message):
    if comands.is_admin(message.from_user.id):
        bot.send_message(message.chat.id, comands.command_list_user + comands.command_list_admin)
    else:
        bot.send_message(message.chat.id, comands.command_list_user)

@bot.message_handler(func=lambda m: True)
def handle_message(message):
    if message.text.startswith('/'):
        func_name = message.text.split()[0][1:].split('@')[0]
        func = commandsExpl.functions.get(func_name)
        if func:
            try:
                resp = func(message)
                if resp:
                    bot.reply_to(message, resp)
                loging(message, resp)
            except Exception as e:
                print(f"[!] Ошибка: {e}, перезапуск через 5 сек...")
                time.sleep(5)
                python = sys.executable
                sys.argv = [sys.argv[0]]
                import os
                os.execl(python, python, *sys.argv)
        else:
            bot.reply_to(message, "Неизвестная команда.")
    else:
        bot.reply_to(message, "Ваше сообщение принято.")

while True:
    try:
        bot.infinity_polling(timeout=60, long_polling_timeout=60)
    except Exception as e:
        print(f"[!] Ошибка: {e}, перезапуск через 5 сек...")
        time.sleep(5)
