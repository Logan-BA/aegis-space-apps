"""Genera una clave maestra de 256 bits para el enlace AEGIS.

    python generar_clave.py                  -> crea clave_nasa.key (no sobrescribe)
    python generar_clave.py --salida otra.key --forzar

La MISMA clave debe estar en la simulación (StreamingAssets/enlace/clave_nasa.key) y en cada estación
terrena autorizada. Quien no la tenga no puede leer ni falsificar la telemetría. Compártala por un medio
seguro (USB, gestor de contraseñas), nunca por chat ni en un repositorio público.
"""
import argparse
import os
import secrets
import sys

p = argparse.ArgumentParser()
p.add_argument("--salida", default="clave_nasa.key")
p.add_argument("--forzar", action="store_true", help="sobrescribir si ya existe")
a = p.parse_args()
if os.path.exists(a.salida) and not a.forzar:
    sys.exit(f"'{a.salida}' ya existe. Use --forzar para reemplazarla (las estaciones con la clave vieja dejarán de leer).")
with open(a.salida, "w", encoding="utf-8") as f:
    f.write("# Clave maestra AEGIS (AES-256 + HMAC-SHA256). SECRETA: solo para estaciones autorizadas.\n")
    f.write(secrets.token_hex(32) + "\n")
print(f"Clave creada: {os.path.abspath(a.salida)}")
