"""Estación terrena AEGIS (Python): recibe por WebSocket la telemetría cifrada que transmite la simulación.

Uso:
    python receptor.py                       # escucha en 0.0.0.0:8765 con la clave clave_nasa.key
    python receptor.py --puerto 9000 --cada-dias 10 --nombre "Estación Madrid"

En la simulación (Unity): StreamingAssets/enlace/enlace.json -> "estaciones": ["ws://IP_DE_ESTA_PC:8765"]
o lanzar SaludAstronauta.exe -estacion ws://IP_DE_ESTA_PC:8765
"""
from __future__ import annotations

import argparse
import asyncio
import os
import socket
import sys
import time

from websockets.asyncio.server import serve
from websockets.exceptions import ConnectionClosed

import aegis_protocolo as ap
import informe
from estacion import Estacion
from panel_web import Panel

VERDE, AMARILLO, ROJO, GRIS, NEGRITA, FIN = "\033[32m", "\033[33m", "\033[31m", "\033[90m", "\033[1m", "\033[0m"


def ips_locales() -> list[str]:
    ips = set()
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            ips.add(info[4][0])
    except OSError:
        pass
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
            s.connect(("10.255.255.255", 1))   # no envía nada: solo elige la interfaz de salida
            ips.add(s.getsockname()[0])
    except OSError:
        pass
    return sorted(i for i in ips if not i.startswith("127."))


def linea(texto: str) -> None:
    print("\r\033[K" + texto, flush=True)


def estado(est: Estacion) -> None:
    s = est.stats
    d = est.ultimo or {}
    txt = (f"{GRIS}[{time.strftime('%H:%M:%S')}]{FIN} día {d.get('dia', '-')} · {d.get('fase', '-')} · "
           f"válidas {s.validas}/{s.recibidas} · bits corregidos {s.bits_corregidos} · "
           f"descartadas {s.descartadas_ruido} · perdidas {s.perdidas_canal} · picos filtrados {s.picos_filtrados} · "
           f"calidad {s.calidad:.0f} %")
    print("\r\033[K" + txt, end="", flush=True)


async def principal(args: argparse.Namespace) -> None:
    k_cif, k_mac = ap.derivar_claves(ap.cargar_clave(args.clave))
    est = Estacion(k_cif, k_mac, args.informes, args.cada_dias)

    async def atender(ws) -> None:
        origen = f"{ws.remote_address[0]}:{ws.remote_address[1]}" if ws.remote_address else "?"
        linea(f"{VERDE}● Nave conectada desde {origen}{FIN}")
        est.conexiones += 1
        try:
            async for msg in ws:
                if isinstance(msg, str):
                    continue
                with est.lock:   # el panel web lee los mismos datos desde otro hilo
                    r = est.recibir(msg)
                    titular = ruta = None
                    if r.ok and r.motivo_informe:
                        titular, ruta = informe.generar(est, r.motivo_informe, args.nombre)
                if not r.ok:
                    color = ROJO if "RECHAZADA" in r.mensaje else AMARILLO
                    linea(f"{color}✗ {r.mensaje}{FIN}")
                elif titular:
                    general = informe.estado_general(est.ultimo or {})
                    color = ROJO if general == "CRÍTICO" else AMARILLO if general == "PRECAUCIÓN" else VERDE
                    pdf = f" + PDF" if est.ultimo_pdf else f" (sin PDF: {est.error_pdf})"
                    linea(f"{NEGRITA}{color}■ INFORME ({r.motivo_informe}){FIN} {titular}\n  {GRIS}→ {ruta}{pdf}{FIN}")
                estado(est)
        except ConnectionClosed:
            pass
        finally:
            est.conexiones -= 1
        linea(f"{AMARILLO}○ Nave desconectada ({origen}){FIN}")

    async with serve(atender, args.host, args.puerto, max_size=8 * 1024 * 1024, ping_interval=20, ping_timeout=60):
        print(f"{NEGRITA}Estación terrena AEGIS · {args.nombre}{FIN}")
        print(f"Escuchando en ws://{args.host}:{args.puerto}  (clave: {os.path.abspath(args.clave)})")
        for ip in ips_locales():
            print(f"  Desde otra computadora, la simulación debe enviar a:  ws://{ip}:{args.puerto}")
        if args.panel:
            Panel(est, args.nombre, args.host, args.panel).iniciar()
            print(f"{NEGRITA}Panel de control:{FIN} http://localhost:{args.panel}")
            for ip in ips_locales():
                print(f"  Desde otra computadora, tablet o celular:  http://{ip}:{args.panel}")
        print(f"Informes en: {os.path.abspath(args.informes)}   ·   Ctrl+C para terminar\n")
        try:
            await asyncio.Future()
        finally:
            if est.ultimo:
                with est.lock:
                    titular, ruta = informe.generar(est, "cierre de la estación", args.nombre)
                linea(f"{NEGRITA}■ INFORME FINAL{FIN} {titular}\n  → {ruta}")


def main() -> None:
    p = argparse.ArgumentParser(description="Estación terrena AEGIS: recibe, verifica, descifra, filtra e informa.")
    p.add_argument("--host", default="0.0.0.0", help="interfaz de escucha (0.0.0.0 = todas)")
    p.add_argument("--puerto", type=int, default=8765)
    p.add_argument("--clave", default="clave_nasa.key", help="archivo con la clave de 256 bits")
    p.add_argument("--informes", default="informes", help="carpeta de salida")
    p.add_argument("--cada-dias", type=int, default=30, help="informe periódico cada N días de misión (0 = solo eventos)")
    p.add_argument("--nombre", default="Estación terrena Python")
    p.add_argument("--panel", type=int, default=8080, help="puerto del panel de control web (0 = sin panel)")
    args = p.parse_args()
    if os.name == "nt":
        os.system("")   # activa los colores ANSI en la consola de Windows
    if not os.path.exists(args.clave):
        sys.exit(f"No se encontró la clave '{args.clave}'. Cópiela de la simulación o genere una con: python generar_clave.py")
    try:
        asyncio.run(principal(args))
    except KeyboardInterrupt:
        print("\nEstación detenida.")


if __name__ == "__main__":
    main()
