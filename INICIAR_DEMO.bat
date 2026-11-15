@echo off
chcp 65001 >nul
title Salud del Astronauta - Space Apps
cd /d "%~dp0"
set "APP=%~dp0App\SaludAstronauta\SaludAstronauta.exe"
if not exist "%APP%" (
  echo No se encontro la aplicacion. Abrir simulacion_unity en Unity y usar el menu
  echo   Space Apps ^> Compilar app de Windows
  pause
  exit /b 1
)
rem ASTRA (asistente medica): enciende la IA local Ollama si esta instalada y apagada
tasklist /fi "imagename eq ollama.exe" 2>nul | find /i "ollama.exe" >nul
if errorlevel 1 (
  if exist "%LOCALAPPDATA%\Programs\Ollama\ollama.exe" (
    start "" /min "%LOCALAPPDATA%\Programs\Ollama\ollama.exe" serve
  ) else (
    where ollama >nul 2>nul && start "" /min ollama serve
  )
)
echo.
echo   SALUD DEL ASTRONAUTA - NASA Space Apps
echo   =====================================
echo.
echo   1. Simulacion local (recomendado: funciona sola, sin red)
echo   2. Mision a Marte ya en marcha (arranca en el dia 200)
echo   3. Conectada a un bus AEGIS simulado (prueba de la conexion con el equipo)
echo   4. Conectada al motor real del equipo (pide la IP de la PC 1)
echo   5. Mision a Marte + conversar por voz con ASTRA (microfono manos libres)
echo   6. Mision a Marte transmitiendo telemetria cifrada a las estaciones terrenas (Python y Rust)
echo.
echo   ASTRA (asistente medica con IA local) esta en el panel izquierdo, abajo.
echo   Hablale: boton CONVERSAR (manos libres) o mantener F2 / boton HABLAR.
echo.
set /p OP="  Elige del 1 al 6 y presiona Enter: "

if "%OP%"=="2" (
  start "" "%APP%" -mision 4 -dia 200 -sinayuda -play
  exit /b
)
if "%OP%"=="5" (
  start "" "%APP%" -mision 4 -dia 200 -sinayuda -conversar
  exit /b
)
if "%OP%"=="6" goto enlace
if "%OP%"=="3" goto bus
if "%OP%"=="4" goto real
start "" "%APP%"
exit /b

:bus
set "PY="
where python >nul 2>nul && set "PY=python"
if not defined PY if exist "C:\Program Files\FreeCAD 1.1\bin\python.exe" set "PY=C:\Program Files\FreeCAD 1.1\bin\python.exe"
if not defined PY (
  echo No se encontro Python para el bus simulado. Instale Python 3 o use la opcion 1.
  pause
  exit /b 1
)
start "Bus AEGIS simulado (cerrar al terminar)" "%PY%" "%~dp0herramientas\bus_aegis_simulado.py" --puerto 8001 --paso 2
timeout /t 2 >nul
start "" "%APP%" -aegis ws://127.0.0.1:8001/ws
exit /b

:real
set /p IP="  IP de la PC 1 (motor AEGIS), por ejemplo 192.168.1.20: "
start "" "%APP%" -aegis ws://%IP%:8001/ws
exit /b

:enlace
rem Estaciones terrenas en esta misma PC (puertos 8765 y 8766). En otra PC: ver ENLACE_ESPACIO_TIERRA.md
if exist "%~dp0receptor_python\.venv\Scripts\python.exe" start "Estacion terrena Python" /d "%~dp0receptor_python" cmd /k ".venv\Scripts\python receptor.py"
if exist "%~dp0receptor_rust\target\release\receptor.exe" start "Estacion terrena Rust" /d "%~dp0receptor_rust" cmd /k "target\release\receptor.exe"
timeout /t 2 >nul
start "" "%APP%" -mision 4 -dia 200 -sinayuda -play
rem Panel de control de la estación Python (informes PDF): http://localhost:8080
timeout /t 3 >nul
start "" http://localhost:8080
exit /b
