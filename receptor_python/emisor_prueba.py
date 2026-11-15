"""Emisor de prueba: imita a la nave (sin abrir Unity) para probar una estación terrena.

    python emisor_prueba.py                              # 300 tramas a ws://127.0.0.1:8765
    python emisor_prueba.py --url ws://192.168.1.20:8765 --ber 2e-3 --dias-por-trama 2
    python emisor_prueba.py --clave-falsa                # comprueba que la estación rechaza a un intruso

Simula una misión a Marte con datos sintéticos, picos de sensores por radiación (SEU) y un canal con
ruido (bits invertidos al azar, ráfagas y tramas perdidas).
"""
from __future__ import annotations

import argparse
import asyncio
import math
import os
import random
import time

from websockets.asyncio.client import connect

import aegis_protocolo as ap

TRIP = [("CMD Alfa", "Comandante", 45), ("PIL Bravo", "Piloto", 38), ("MED Charlie", "Oficial médico", 41), ("ING Delta", "Especialista", 35)]


def datos(dia: int, tormentas: int, tormenta: bool, ber: float) -> dict:
    fase, g, rad = ("Tránsito a Marte", 0.0, 1.8) if dia < 210 else ("Superficie de Marte", 0.38, 0.64)
    trip, alertas = [], []
    for i, (nom, rol, edad) in enumerate(TRIP):
        k = 1 + 0.08 * i
        hueso = 100 - min(dia, 210) * 0.033 * k - max(0, dia - 210) * 0.01 * k
        musculo = 100 - min(dia, 210) * 0.05 * k - max(0, dia - 210) * 0.008 * k
        sans = min(0.95, dia * 0.0016 * k)
        dosis = min(dia, 210) * 1.8 * 0.6 + max(0, dia - 210) * 0.64 + tormentas * 6
        vit = {"fc": 62 + i + random.gauss(0, 1.5), "pas": 116 + random.gauss(0, 3), "pad": 75 + random.gauss(0, 2),
               "spo2": 97 + random.gauss(0, 0.5), "temp_c": 36.9 + random.gauss(0, 0.1), "fr": 14}
        if random.random() < (0.12 if tormenta else 0.02):   # impacto de radiación en un sensor
            var = random.choice(["fc", "spo2", "temp_c"])
            vit[var] = {"fc": 185.0, "spo2": 61.0, "temp_c": 41.8}[var]
        trip.append({"nombre": nom, "rol": rol, "edad": edad, "indice_salud": 90 - dia * 0.05 * k, "hueso_pct": hueso,
                     "musculo_pct": musculo, "vo2_pct": 100 - dia * 0.02, "sans": sans, "dosis_msv": dosis,
                     "riesgo_cancer_pct": dosis * 0.005, "sueno_h": 6.9, "deuda_sueno_h": 3.0, "estres": 40 + dia * 0.01,
                     "calcio_orina_mg": 230, "en_eva": False, "vitales": vit})
    s = max(c["sans"] for c in trip)
    if s >= 0.35:
        alertas.append({"sistema": "Visión (SANS)", "nivel": "CRITICO" if s >= 0.65 else "PRECAUCION", "severidad": s,
                        "tripulantes": [c["nombre"] for c in trip], "detalle": f"Índice SANS {s:.2f}"})
    dmax = max(c["dosis_msv"] for c in trip)
    if dmax / 600 >= 0.35:
        alertas.append({"sistema": "Dosis de radiación", "nivel": "CRITICO" if dmax / 600 >= 0.65 else "PRECAUCION",
                        "severidad": dmax / 600, "tripulantes": [c["nombre"] for c in trip], "detalle": f"Dosis {dmax:.0f} mSv de 600"})
    d_ua = 0.0001 + 1.5 * min(1, dia / 210)
    return {"esquema": "aegis.telemetria.v1", "nave": "AEGIS-PRUEBA", "mision": "Marte · ida y vuelta (prueba)",
            "dia": dia, "hora": 12, "dia_total": 1114, "fase": fase, "fase_clave": "mars", "gravedad_g": g,
            "radiacion_msv_dia": rad, "limite_dosis_msv": 600, "tormentas_solares": tormentas, "tormenta_activa": tormenta,
            "mision_terminada": False,
            "cabina": {"co2_mmhg": 2.1, "co2_max_mmhg": 2.7, "pm25": 6, "o2_pct": 21, "temp_c": 22.5, "humedad_pct": 45, "ruido_db": 58},
            "contramedidas": {"ejercicio": 1, "blindaje": 1, "refugio": True, "depurador": 1, "apoyo": 1, "luz": 1, "gravedad_artificial_g": 0},
            "tripulacion": trip, "alertas": alertas,
            "enlace": {"via": "DSN (prueba)", "distancia_km": d_ua * 1.496e8, "distancia_ua": d_ua, "retraso_luz_s": d_ua * 499,
                       "angulo_sol_grados": 60, "ebn0_db": 9, "ber": ber, "fuentes_ruido": ["ruido simulado del emisor de prueba"]}}


def ruido(b: bytearray, ber: float, rafaga: float) -> None:
    for i in range(len(b) * 8):
        if random.random() < ber:
            b[i >> 3] ^= 1 << (i & 7)
    if random.random() < rafaga:
        ini = random.randrange(0, len(b) - 48)
        for j in range(ini, ini + random.randint(8, 40)):
            b[j] ^= 1 << random.randrange(7)


async def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--url", default="ws://127.0.0.1:8765")
    p.add_argument("--clave", default="clave_nasa.key")
    p.add_argument("--tramas", type=int, default=300)
    p.add_argument("--intervalo", type=float, default=0.1)
    p.add_argument("--dias-por-trama", type=int, default=2)
    p.add_argument("--ber", type=float, default=1e-3, help="tasa de error de bit del canal")
    p.add_argument("--perdida", type=float, default=0.02, help="probabilidad de perder una trama")
    p.add_argument("--clave-falsa", action="store_true", help="firma con una clave inventada (debe ser rechazada)")
    a = p.parse_args()
    maestra = os.urandom(32) if a.clave_falsa else ap.cargar_clave(a.clave)
    k_cif, k_mac = ap.derivar_claves(maestra)
    sesion = random.getrandbits(32)
    tormentas = 0
    async with connect(a.url, max_size=None) as ws:
        print(f"Conectado a {a.url}. Enviando {a.tramas} tramas (BER {a.ber:g})…")
        for seq in range(a.tramas):
            dia = seq * a.dias_por_trama
            tormenta = random.random() < 0.03
            tormentas += tormenta
            bloque = bytearray(ap.sellar(datos(dia, tormentas, tormenta, a.ber), k_cif, k_mac, sesion, seq, int(time.time() * 1000)))
            if random.random() < a.perdida:
                continue
            ruido(bloque, a.ber * (3 if tormenta else 1), 0.1)
            await ws.send(bytes(bloque))
            await asyncio.sleep(a.intervalo)
    print("Listo.")


if __name__ == "__main__":
    asyncio.run(main())
