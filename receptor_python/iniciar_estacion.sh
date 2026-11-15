#!/usr/bin/env bash
# Estacion terrena AEGIS (Python). Crea el entorno la primera vez y arranca.
set -e
cd "$(dirname "$0")"
if [ ! -x .venv/bin/python ]; then
  echo "Creando entorno de Python e instalando librerias..."
  python3 -m venv .venv
  .venv/bin/pip install -r requirements.txt
fi
exec .venv/bin/python receptor.py "$@"
