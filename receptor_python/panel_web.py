"""Panel de control web de la estación terrena (sin librerías extra: http.server de Python).

    http://IP_DE_ESTA_PC:8080        panel en vivo (se puede abrir desde otra computadora, tablet o celular)
    GET  /api/estado                 estado actual (JSON): misión, tripulación, alertas, enlace, inferencias
    GET  /api/historia               series por tripulante para los gráficos
    GET  /api/informes               informes generados (PDF, Markdown) y CSV
    POST /api/pdf                    genera un informe PDF ahora y devuelve su enlace
    GET  /informes/<archivo>         descarga/visualiza un informe
"""
from __future__ import annotations

import json
import mimetypes
import os
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import unquote, urlparse

import informe
from estacion import Estacion

CARPETA_PANEL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "panel")
SERIES = ["indice_salud", "dosis_msv", "hueso_pct", "musculo_pct", "sans", "vo2_pct", "fc", "spo2"]


def _reducir(puntos: list[tuple[int, float]], maximo: int = 240) -> list[list[float]]:
    if len(puntos) > maximo:
        paso = len(puntos) / maximo
        puntos = [puntos[int(i * paso)] for i in range(maximo)] + [puntos[-1]]
    return [[d, round(v, 4)] for d, v in puntos]


class Panel:
    def __init__(self, est: Estacion, nombre: str, host: str, puerto: int):
        self.est, self.nombre, self.host, self.puerto = est, nombre, host, puerto
        self.inicio = time.time()

    # ------------------------------------------------------------ datos

    def estado(self) -> dict:
        est = self.est
        with est.lock:
            d = est.ultimo
            s = est.stats
            base = {
                "estacion": self.nombre,
                "hora_utc": time.strftime("%Y-%m-%d %H:%M:%S UTC", time.gmtime()),
                "conectada": est.conexiones > 0,
                "segundos_sin_datos": round(time.time() - est.t_ultimo, 1) if est.t_ultimo else None,
                "stats": {**s.__dict__, "perdidas_canal": s.perdidas_canal, "calidad": round(s.calidad, 1)},
                "picos": [{"dia": p[0], "tripulante": p[1], "sensor": p[2], "leido": p[3], "aceptado": p[4], "motivo": p[5]}
                          for p in list(est.picos)[-10:]][::-1],
                "ultimo_pdf": self._url(est.ultimo_pdf),
                "error_pdf": est.error_pdf,
                "datos": d,
            }
            if d:
                base["estado_general"] = informe.estado_general(d)
                base["inferencias"] = informe.inferencias(est, d)
                base["conclusion"] = informe.conclusion(d)
        return base

    def historia(self) -> dict:
        with self.est.lock:
            return {nombre: {m: _reducir(sorted(series.get(m, {}).items())) for m in SERIES}
                    for nombre, series in self.est.hist.items()}

    def informes(self) -> list[dict]:
        carpeta = self.est.carpeta
        if not os.path.isdir(carpeta):
            return []
        salida = []
        for f in sorted(os.listdir(carpeta), reverse=True):
            if f.startswith("informe_dia_") and f.endswith(".pdf"):
                base = f[:-4]
                ruta = os.path.join(carpeta, f)
                salida.append({"nombre": base.replace("informe_dia_", "Día ").lstrip("0"),
                               "dia": int(base[-4:]) if base[-4:].isdigit() else None,
                               "pdf": f"/informes/{f}",
                               "md": f"/informes/{base}.md" if os.path.exists(os.path.join(carpeta, base + ".md")) else None,
                               "fecha": time.strftime("%Y-%m-%d %H:%M", time.localtime(os.path.getmtime(ruta))),
                               "kb": round(os.path.getsize(ruta) / 1024)})
        return salida

    def pdf_ahora(self) -> dict:
        est = self.est
        with est.lock:
            if not est.ultimo:
                return {"ok": False, "error": "Todavía no se recibió telemetría de la nave."}
            dia = int(est.ultimo.get("dia", 0))
            ruta = os.path.join(est.carpeta, f"informe_dia_{dia:04d}.pdf")
            r = informe.generar_pdf(est, "solicitado desde el panel de control", self.nombre, ruta)
        if not r:
            return {"ok": False, "error": est.error_pdf or "No se pudo crear el PDF."}
        return {"ok": True, "url": self._url(r), "aviso": est.error_pdf}

    def _url(self, ruta: str | None) -> str | None:
        return f"/informes/{os.path.basename(ruta)}" if ruta else None

    # ------------------------------------------------------------ servidor

    def iniciar(self) -> None:
        panel = self

        class Manejador(BaseHTTPRequestHandler):
            server_version = "AEGIS-Panel/1.0"

            def log_message(self, *args) -> None:   # silencio: la consola es para la telemetría
                pass

            def _enviar(self, codigo: int, cuerpo: bytes, tipo: str, extra: dict | None = None) -> None:
                self.send_response(codigo)
                self.send_header("Content-Type", tipo)
                self.send_header("Content-Length", str(len(cuerpo)))
                self.send_header("Cache-Control", "no-store")
                for k, v in (extra or {}).items():
                    self.send_header(k, v)
                self.end_headers()
                self.wfile.write(cuerpo)

            def _json(self, obj, codigo: int = 200) -> None:
                self._enviar(codigo, json.dumps(obj, ensure_ascii=False).encode("utf-8"), "application/json; charset=utf-8")

            def _archivo(self, carpeta: str, nombre: str, descarga: bool = False) -> None:
                nombre = os.path.basename(unquote(nombre))          # evita salir de la carpeta (../)
                ruta = os.path.join(carpeta, nombre)
                if not nombre or not os.path.isfile(ruta):
                    return self._enviar(404, "No encontrado".encode("utf-8"), "text/plain; charset=utf-8")
                tipo = mimetypes.guess_type(nombre)[0] or "application/octet-stream"
                if tipo.startswith("text/") or nombre.endswith((".md", ".csv", ".js")):
                    tipo = ("text/markdown" if nombre.endswith(".md") else "text/javascript" if nombre.endswith(".js")
                            else tipo) + "; charset=utf-8"
                with open(ruta, "rb") as f:
                    datos = f.read()
                extra = {"Content-Disposition": f'{"attachment" if descarga else "inline"}; filename="{nombre}"'}
                self._enviar(200, datos, tipo, extra)

            def do_GET(self) -> None:  # noqa: N802
                ruta = urlparse(self.path).path
                try:
                    if ruta in ("/", "/index.html"):
                        return self._archivo(CARPETA_PANEL, "index.html")
                    if ruta in ("/panel.css", "/panel.js"):
                        return self._archivo(CARPETA_PANEL, ruta[1:])
                    if ruta == "/api/estado":
                        return self._json(panel.estado())
                    if ruta == "/api/historia":
                        return self._json(panel.historia())
                    if ruta == "/api/informes":
                        return self._json(panel.informes())
                    if ruta.startswith("/informes/"):
                        return self._archivo(panel.est.carpeta, ruta[len("/informes/"):], descarga=ruta.endswith(".csv"))
                    self._enviar(404, "No encontrado".encode("utf-8"), "text/plain; charset=utf-8")
                except (BrokenPipeError, ConnectionResetError):
                    pass

            def do_POST(self) -> None:  # noqa: N802
                if urlparse(self.path).path == "/api/pdf":
                    r = panel.pdf_ahora()
                    return self._json(r, 200 if r.get("ok") else 409)
                self._enviar(404, b"", "text/plain")

        servidor = ThreadingHTTPServer((self.host, self.puerto), Manejador)
        servidor.daemon_threads = True
        threading.Thread(target=servidor.serve_forever, name="panel-web", daemon=True).start()
