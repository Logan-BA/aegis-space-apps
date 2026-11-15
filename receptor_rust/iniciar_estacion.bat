@echo off
rem Estacion terrena AEGIS (Rust). Compila la primera vez y arranca.
cd /d "%~dp0"
if not exist "target\release\receptor.exe" cargo build --release
target\release\receptor.exe %*
pause
