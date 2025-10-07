#!/bin/bash
# run_bot.sh

# --- Отключаем спящий режим и гашение экрана ---
xset s off       # отключить screensaver
xset -dpms       # отключить энергосбережение дисплея
xset s noblank   # не гасить экран

# --- Запуск бота с запретом на sleep/idle ---
echo "Запуск бота..."
systemd-inhibit --what=handle-lid-switch:sleep:idle python3 main.py

# chmod +x run_bot.sh