@echo off
cd /d "%~dp0"
python main.py
if errorlevel 1 (
    echo.
    echo Daca lipsesc dependentele: python -m pip install -r requirements.txt
    pause
)
