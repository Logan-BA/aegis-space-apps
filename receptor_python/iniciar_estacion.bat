@echo off
rem Estacion terrena AEGIS (Python). Crea el entorno la primera vez y arranca.
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" (
  echo Creando entorno de Python e instalando librerias...
  py -3 -m venv .venv 2>nul || python -m venv .venv
  .venv\Scripts\python -m pip install -r requirements.txt
)
.venv\Scripts\python receptor.py %*
pause
