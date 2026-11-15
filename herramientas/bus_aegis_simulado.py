# -*- coding: utf-8 -*-
"""
Bus AEGIS SIMULADO para probar Unity sin la PC 1.

Solo usa la biblioteca estándar de Python (3.8+). Acepta conexiones WebSocket en
ws://0.0.0.0:<puerto>/ws con el subprotocolo aegis.v1 y envía cada medio segundo un estado de
misión que avanza unos días. El FORMATO DEL MENSAJE ES UN EJEMPLO: el contrato real lo define
api/ del motor del equipo. Sirve para probar la conexión, la reconexión y el mapeo
(simulacion_unity/Assets/StreamingAssets/aegis_bus.json).

Uso:
  python bus_aegis_simulado.py [--puerto 8001] [--segundos 0]   (0 = sin límite)
"""
import argparse
import base64
import hashlib
import json
import math
import socket
import struct
import threading
import time

GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"


def handshake(conn):
    data = b""
    while b"\r\n\r\n" not in data:
        chunk = conn.recv(4096)
        if not chunk:
            return False
        data += chunk
    lines = data.decode("latin-1").split("\r\n")
    headers = {l.split(":", 1)[0].strip().lower(): l.split(":", 1)[1].strip() for l in lines[1:] if ":" in l}
    key = headers.get("sec-websocket-key")
    if not key:
        return False
    accept = base64.b64encode(hashlib.sha1((key + GUID).encode()).digest()).decode()
    protos = [p.strip() for p in headers.get("sec-websocket-protocol", "").split(",") if p.strip()]
    resp = ("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            "Sec-WebSocket-Accept: %s\r\n" % accept)
    if "aegis.v1" in protos:
        resp += "Sec-WebSocket-Protocol: aegis.v1\r\n"
    conn.sendall((resp + "\r\n").encode())
    print("[bus] cliente conectado: %s · subprotocolos pedidos: %s" % (lines[0], protos or "ninguno"))
    return True


def frame(text):
    payload = text.encode("utf-8")
    n = len(payload)
    if n < 126:
        header = struct.pack("!BB", 0x81, n)
    elif n < 65536:
        header = struct.pack("!BBH", 0x81, 126, n)
    else:
        header = struct.pack("!BBQ", 0x81, 127, n)
    return header + payload


def estado(dia):
    total = 900
    fase = "transit" if dia < 210 or dia > 690 else "surface"
    tripulacion = []
    for i, (nombre, apt) in enumerate([("Astronauta 1", 1.0), ("Astronauta 2", 0.9), ("Astronauta 3", 0.95), ("Astronauta 4", 0.85)]):
        # dosis acumulada: 1,8 mSv/día en tránsito y 0,64 mSv/día en la superficie
        ida, sup = min(dia, 210), max(0, min(dia, 690) - 210)
        vuelta = max(0, dia - 690)
        dosis = (1.8 * (ida + vuelta) + 0.64 * sup) * (0.9 + 0.05 * i)
        tripulacion.append({
            "id": "A%d" % (i + 1),
            "name": nombre,
            "indicators": {
                "dose_msv": round(dosis, 1),
                "sleep_hours": round(6.4 + 0.4 * math.sin(dia / 9.0 + i), 2),
                "bone_density": round(1.0 - 0.0004 * dia / apt, 4),
                "vo2max": round(42.0 - 0.012 * dia / apt, 2),
            },
        })
    alertas = []
    if tripulacion[3]["indicators"]["dose_msv"] > 300:
        alertas.append({"astronaut_id": "A4", "message": "Dosis acumulada por encima de la mitad del límite de carrera",
                        "severity": "medium", "source": "NASA-STD-3001 Vol. 1"})
    if tripulacion[3]["indicators"]["dose_msv"] > 600:
        alertas.append({"astronaut_id": "A4", "message": "Dosis acumulada sobre el límite de carrera (600 mSv)",
                        "severity": "high", "source": "NASA-STD-3001 Vol. 1"})
    solar = {"type": "CME", "start": "2024-05-10T17:00Z", "source": "NASA DONKI"} if 60 <= dia % 300 < 70 else None
    return {"type": "mission_state", "scenario": "Marte 900 días", "phase": fase, "day": dia, "total_days": total,
            "crew": tripulacion, "alerts": alertas, "solar_event": solar}


def atender(conn, addr, paso_dias, fin):
    try:
        if not handshake(conn):
            conn.close()
            return
        dia = 0
        while time.time() < fin:
            conn.sendall(frame(json.dumps(estado(dia), ensure_ascii=False)))
            dia = (dia + paso_dias) % 901
            time.sleep(0.5)
        conn.sendall(struct.pack("!BB", 0x88, 0))   # cierre
    except OSError:
        pass
    finally:
        conn.close()
        print("[bus] cliente desconectado", addr)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--puerto", type=int, default=8001)
    ap.add_argument("--segundos", type=float, default=0)
    ap.add_argument("--paso", type=int, default=5, help="días que avanza cada mensaje")
    a = ap.parse_args()
    fin = time.time() + a.segundos if a.segundos > 0 else float("inf")
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("0.0.0.0", a.puerto))
    srv.listen(5)
    srv.settimeout(0.5)
    print("[bus] escuchando en ws://0.0.0.0:%d/ws (aegis.v1, formato de ejemplo)" % a.puerto)
    while time.time() < fin:
        try:
            conn, addr = srv.accept()
        except socket.timeout:
            continue
        threading.Thread(target=atender, args=(conn, addr, a.paso, fin), daemon=True).start()
    srv.close()
    print("[bus] fin")


if __name__ == "__main__":
    main()
