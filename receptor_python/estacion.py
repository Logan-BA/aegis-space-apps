"""Estado de la estación terrena: recibe tramas, cuenta la calidad del enlace, filtra picos y guarda historia."""
from __future__ import annotations

import csv
import os
import threading
import time
from collections import defaultdict, deque
from dataclasses import dataclass, field

import aegis_protocolo as ap
from filtros import SENSORES, FiltroHampel

METRICAS = ["indice_salud", "hueso_pct", "musculo_pct", "vo2_pct", "sans", "dosis_msv", "estres", "deuda_sueno_h"]


@dataclass
class Estadisticas:
    recibidas: int = 0
    validas: int = 0
    bits_corregidos: int = 0
    tramas_corregidas: int = 0
    descartadas_ruido: int = 0
    rechazadas_clave: int = 0
    invalidas: int = 0
    repetidas: int = 0
    huecos_secuencia: int = 0
    picos_filtrados: int = 0
    otra_sesion: int = 0

    @property
    def perdidas_canal(self) -> int:
        """Tramas que nunca llegaron (huecos en la secuencia que no explican las descartadas)."""
        return max(0, self.huecos_secuencia - self.descartadas_ruido - self.rechazadas_clave - self.invalidas)

    @property
    def calidad(self) -> float:
        total = self.validas + self.descartadas_ruido + self.perdidas_canal
        return 100.0 * self.validas / total if total else 0.0


@dataclass
class Resultado:
    ok: bool
    mensaje: str
    motivo_informe: str | None = None
    datos: dict | None = None


@dataclass
class Estacion:
    k_cif: bytes
    k_mac: bytes
    carpeta: str
    cada_dias: int = 30
    stats: Estadisticas = field(default_factory=Estadisticas)
    ultimo: dict | None = None
    t_ultimo: float = 0.0
    latencia_ms: float = 0.0

    def __post_init__(self) -> None:
        os.makedirs(self.carpeta, exist_ok=True)
        self.sesion: int | None = None
        self.ultima_seq: int | None = None
        self.hist: dict[str, dict[str, dict[int, float]]] = defaultdict(lambda: defaultdict(dict))
        self.filtros: dict[tuple[str, str], FiltroHampel] = {}
        self.picos: deque[tuple[int, str, str, float, float, str]] = deque(maxlen=40)
        self.dia_informe: int | None = None
        self.criticas_informadas: set[str] = set()
        self.tormentas_informadas: int | None = None
        self.fase_informada: str | None = None
        self.terminada_informada = False
        self.previo: dict | None = None   # datos del informe anterior (para comparar)
        self._csv = os.path.join(self.carpeta, "telemetria.csv")
        self.lock = threading.Lock()          # el panel web lee desde otro hilo
        self.conexiones = 0                   # naves conectadas ahora
        self.ultimo_pdf: str | None = None
        self.error_pdf = ""

    # ------------------------------------------------------------ recepción

    def recibir(self, bloque: bytes) -> Resultado:
        self.stats.recibidas += 1
        try:
            t = ap.abrir(bloque, self.k_cif, self.k_mac)
        except ap.ErrorCanal as e:
            self.stats.descartadas_ruido += 1
            return Resultado(False, f"trama descartada por ruido del canal ({e})")
        except ap.ErrorAutenticacion as e:
            self.stats.rechazadas_clave += 1
            return Resultado(False, f"trama RECHAZADA: {e}")
        except (ap.ErrorFormato, ValueError) as e:
            self.stats.invalidas += 1
            return Resultado(False, f"trama inválida ({e})")

        # Una sola nave a la vez: si otra sesión transmite mientras la actual sigue activa, se ignora
        if self.sesion is not None and t.sesion != self.sesion and time.time() - self.t_ultimo < 10:
            self.stats.otra_sesion += 1
            return Resultado(False, f"trama de otra sesión ({t.sesion:08X}) ignorada: la estación sigue a la nave activa")
        # Secuencia: detecta tramas perdidas y repeticiones (ataque de repetición)
        if t.sesion != self.sesion:
            self.sesion, self.ultima_seq = t.sesion, None
        elif self.ultima_seq is not None:
            if t.secuencia <= self.ultima_seq:
                self.stats.repetidas += 1
                return Resultado(False, f"trama #{t.secuencia} repetida: descartada")
            self.stats.huecos_secuencia += t.secuencia - self.ultima_seq - 1
        self.ultima_seq = t.secuencia

        d = t.datos
        if self.ultimo and d.get("dia", 0) < self.ultimo.get("dia", 0):   # misión reiniciada
            self.hist.clear()
            self.filtros.clear()
            self.picos.clear()
            self.previo = None
            self.dia_informe = None
            self.criticas_informadas.clear()
            self.tormentas_informadas = None
            self.terminada_informada = False
        self.stats.validas += 1
        self.stats.bits_corregidos += t.bits_corregidos
        if t.bits_corregidos:
            self.stats.tramas_corregidas += 1
        self.latencia_ms = max(0.0, time.time() * 1000 - t.t_ms)
        self._filtrar(d)
        self._guardar(d, t)
        self.ultimo, self.t_ultimo = d, time.time()

        corr = f" · {t.bits_corregidos} bits corregidos" if t.bits_corregidos else ""
        return Resultado(True, f"trama #{t.secuencia} OK · día {d.get('dia')}{corr}", self._motivo_informe(d), d)

    def _filtrar(self, d: dict) -> None:
        dia = int(d.get("dia", 0))
        for c in d.get("tripulacion", []):
            nombre = c.get("nombre", "?")
            v = c.get("vitales", {})
            filtrados = {}
            for var, (mn, mx, piso) in SENSORES.items():
                if var not in v:
                    continue
                f = self.filtros.setdefault((nombre, var), FiltroHampel(mn, mx, piso))
                aceptado, motivo = f.filtrar(float(v[var]))
                filtrados[var] = aceptado
                if motivo:
                    self.stats.picos_filtrados += 1
                    self.picos.append((dia, nombre, var, float(v[var]), aceptado, motivo))
            c["vitales_filtrados"] = filtrados
            for m in METRICAS:
                if m in c:
                    self.hist[nombre][m][dia] = float(c[m])
            for var in ("fc", "spo2"):
                if var in filtrados:
                    self.hist[nombre][var][dia] = filtrados[var]

    def _guardar(self, d: dict, t: ap.Trama) -> None:
        nuevo = not os.path.exists(self._csv)
        with open(self._csv, "a", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            if nuevo:
                w.writerow(["recibido_utc", "secuencia", "dia", "hora", "fase", "tripulante", "indice_salud", "hueso_pct",
                            "musculo_pct", "vo2_pct", "sans", "dosis_msv", "estres", "fc_filtrada", "spo2_filtrada",
                            "temp_filtrada", "co2_mmhg", "bits_corregidos"])
            ahora = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            for c in d.get("tripulacion", []):
                vf = c.get("vitales_filtrados", {})
                w.writerow([ahora, t.secuencia, d.get("dia"), d.get("hora"), d.get("fase"), c.get("nombre"),
                            c.get("indice_salud"), c.get("hueso_pct"), c.get("musculo_pct"), c.get("vo2_pct"), c.get("sans"),
                            c.get("dosis_msv"), c.get("estres"), vf.get("fc"), vf.get("spo2"), vf.get("temp_c"),
                            d.get("cabina", {}).get("co2_mmhg"), t.bits_corregidos])

    def _motivo_informe(self, d: dict) -> str | None:
        dia = int(d.get("dia", 0))
        criticas = {a["sistema"] for a in d.get("alertas", []) if a.get("nivel") == "CRITICO"}
        tormentas = int(d.get("tormentas_solares", 0))
        if self.dia_informe is None:
            return "primer contacto con la nave"
        if d.get("mision_terminada") and not self.terminada_informada:
            return "fin de la misión"
        nuevas = criticas - self.criticas_informadas
        if nuevas:
            return "nueva alerta crítica: " + ", ".join(sorted(nuevas))
        if self.tormentas_informadas is not None and tormentas > self.tormentas_informadas:
            return "tormenta solar detectada"
        if self.fase_informada and d.get("fase") != self.fase_informada:
            return f"cambio de fase: {d.get('fase')}"
        if self.cada_dias > 0 and dia - self.dia_informe >= self.cada_dias:
            return f"informe periódico (cada {self.cada_dias} días)"
        return None

    def marcar_informado(self, d: dict) -> None:
        self.dia_informe = int(d.get("dia", 0))
        self.criticas_informadas = {a["sistema"] for a in d.get("alertas", []) if a.get("nivel") == "CRITICO"}
        self.tormentas_informadas = int(d.get("tormentas_solares", 0))
        self.fase_informada = d.get("fase")
        self.terminada_informada = bool(d.get("mision_terminada"))
        self.previo = d
