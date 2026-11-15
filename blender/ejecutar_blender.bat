@echo off
REM Ejecuta los scripts de Blender sin abrir la interfaz.
REM Uso: ejecutar_blender.bat            -> exporta a Unity
REM      ejecutar_blender.bat --render   -> ademas guarda renders PNG en blender\salida
setlocal
set "BLENDER="
for /d %%D in ("C:\Program Files\Blender Foundation\Blender*") do set "BLENDER=%%D\blender.exe"
if not defined BLENDER (
  where blender >nul 2>nul && set "BLENDER=blender"
)
if not defined BLENDER (
  echo No se encontro Blender. Instale Blender 4.2 LTS o mas reciente: https://www.blender.org/download/
  exit /b 1
)
cd /d "%~dp0"
echo == Nave y cohete (desde FreeCAD) ==
"%BLENDER%" -b -P detallar_nave.py -- todo %1
echo == Interior de la nave ==
"%BLENDER%" -b -P crear_interior_nave.py -- %1
echo Listo. Los FBX estan en simulacion_unity\Assets\SpaceApps\Resources\Models
endlocal
