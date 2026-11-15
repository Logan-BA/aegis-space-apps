"""Filtros de la estación terrena.

Ruido digital (bits invertidos) ya lo resuelven Hamming + CRC en aegis_protocolo.py.
Aquí se trata el ruido que llega DENTRO de datos válidos: un impacto de radiación en la electrónica de un
sensor (SEU, "single event upset") puede producir una lectura absurda (FC 180, SpO2 60 %). Se usan:
  - Límites de plausibilidad del sensor (valores físicamente imposibles para una persona en reposo).
  - Filtro de Hampel: compara cada lectura con la mediana de las últimas y la descarta si se aleja más
    de k desviaciones robustas (MAD). Es el filtro clásico para quitar picos sin suavizar la tendencia.
Además, tendencias por regresión lineal para inferir hacia dónde va cada indicador.
"""
from __future__ import annotations

from collections import deque
from statistics import median

# variable: (mínimo plausible, máximo plausible, piso del umbral de Hampel)
SENSORES = {
    "fc": (30.0, 200.0, 10.0),      # latidos por minuto en reposo
    "pas": (70.0, 200.0, 15.0),     # presión sistólica mmHg
    "pad": (40.0, 130.0, 12.0),     # presión diastólica mmHg
    "spo2": (75.0, 100.0, 3.0),     # saturación de oxígeno %
    "temp_c": (34.0, 41.0, 0.8),    # temperatura central °C
}


class FiltroHampel:
    def __init__(self, minimo: float, maximo: float, piso: float, ventana: int = 9, k: float = 3.5):
        self.min, self.max, self.piso, self.k = minimo, maximo, piso, k
        self.valores: deque[float] = deque(maxlen=ventana)

    def filtrar(self, x: float) -> tuple[float, str | None]:
        """Devuelve (valor aceptado, motivo del descarte o None)."""
        ref = median(self.valores) if self.valores else None
        if not (self.min <= x <= self.max):
            return (ref if ref is not None else x), "fuera del rango físico del sensor"
        if len(self.valores) >= 5:
            mad = median(abs(v - ref) for v in self.valores) * 1.4826
            if abs(x - ref) > max(self.k * mad, self.piso):
                return ref, f"pico aislado; mediana reciente {ref:.1f}".replace(".", ",")
        self.valores.append(x)
        return x, None


def pendiente(puntos: list[tuple[float, float]]) -> float | None:
    """Pendiente por mínimos cuadrados (unidades por día). None si no hay datos suficientes."""
    if len(puntos) < 3:
        return None
    n = len(puntos)
    mx = sum(p[0] for p in puntos) / n
    my = sum(p[1] for p in puntos) / n
    sxx = sum((p[0] - mx) ** 2 for p in puntos)
    if sxx == 0:
        return None
    return sum((p[0] - mx) * (p[1] - my) for p in puntos) / sxx
