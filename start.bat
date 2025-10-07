@echo on
:RESTART
python main.py
timeout /t 5
goto RESTART
