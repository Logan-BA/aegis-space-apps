"""Informe claro y conciso de lo que ocurre en la misión, inferido solo de la telemetría recibida.

Las inferencias usan tendencias (regresión lineal sobre los últimos días) y los límites que envía la propia
nave (p. ej. el límite de dosis de carrera). No se inventan diagnósticos: se describen los datos, su ritmo
de cambio y la proyección hasta el final de la misión.
"""
from __future__ import annotations

import os
import time

from estacion import Estacion
from filtros import pendiente


def n(x: float, dec: int = 1) -> str:
    """Número con coma decimal."""
    return f"{x:.{dec}f}".replace(".", ",")


def _serie(hist: dict[int, float], dia: int, ventana: int = 60) -> list[tuple[float, float]]:
    return [(float(d), v) for d, v in sorted(hist.items()) if d >= dia - ventana]


def _promedio(tripulacion: list[dict], clave: str) -> float:
    vals = [float(c[clave]) for c in tripulacion if clave in c]
    return sum(vals) / len(vals) if vals else 0.0


def _ritmo_medio(est: Estacion, tripulacion: list[dict], clave: str, dia: int) -> float | None:
    ritmos = [pendiente(_serie(est.hist[c["nombre"]][clave], dia)) for c in tripulacion]
    ritmos = [r for r in ritmos if r is not None]
    return sum(ritmos) / len(ritmos) if ritmos else None


def estado_general(d: dict) -> str:
    niveles = {a.get("nivel") for a in d.get("alertas", [])}
    return "CRÍTICO" if "CRITICO" in niveles else "PRECAUCIÓN" if "PRECAUCION" in niveles else "NOMINAL"


def _quien(trip: list[str], total: int) -> str:
    return "toda la tripulación" if len(trip) == total else ", ".join(trip)


def inferencias(est: Estacion, d: dict) -> list[str]:
    out: list[str] = []
    trip = d.get("tripulacion", [])
    if not trip:
        return out
    dia, total = int(d.get("dia", 0)), float(d.get("dia_total", 0))
    restan = max(0.0, total - dia)
    prev = est.previo

    if prev and prev.get("fase") != d.get("fase"):
        out.append(f"**Cambio de fase:** la nave pasó de *{prev.get('fase')}* a *{d.get('fase')}* "
                   f"({n(d.get('gravedad_g', 0), 2)} g, {n(d.get('radiacion_msv_dia', 0), 2)} mSv/día).")

    # Radiación
    limite = float(d.get("limite_dosis_msv", 600))
    peor = max(trip, key=lambda c: c.get("dosis_msv", 0))
    dosis = float(peor.get("dosis_msv", 0))
    ritmo = pendiente(_serie(est.hist[peor["nombre"]]["dosis_msv"], dia)) or float(d.get("radiacion_msv_dia", 0))
    if float(d.get("enlace", {}).get("distancia_km", 1)) <= 0:
        out.append(f"**Radiación:** en Tierra (solo fondo natural, {n(float(d.get('radiacion_msv_dia', 0)), 3)} mSv/día); "
                   f"dosis acumulada máxima {n(dosis, 0)} de {n(limite, 0)} mSv.")
    elif dosis >= limite:
        out.append(f"**Radiación:** {peor['nombre']} superó el límite de carrera ({n(dosis, 0)} de {n(limite, 0)} mSv).")
    elif ritmo > 0:
        dia_lim = dia + (limite - dosis) / ritmo
        final = dosis + ritmo * restan
        if total and dia_lim <= total:
            out.append(f"**Radiación:** la dosis sube {n(ritmo, 2)} mSv/día ({peor['nombre']}: {n(dosis, 0)} de {n(limite, 0)} mSv). "
                       f"A este ritmo alcanzaría el límite de carrera cerca del día {n(dia_lim, 0)}, **antes del fin de la misión** "
                       f"(día {n(total, 0)}).")
        else:
            out.append(f"**Radiación:** la dosis sube {n(ritmo, 2)} mSv/día ({peor['nombre']}: {n(dosis, 0)} mSv). "
                       f"Proyección al final de la misión: {n(final, 0)} de {n(limite, 0)} mSv (dentro del límite).")

    # Hueso y músculo (promedio de la tripulación)
    for clave, nombre, unidad in (("hueso_pct", "Densidad ósea", "%"), ("musculo_pct", "Masa muscular", "%")):
        prom = _promedio(trip, clave)
        r = _ritmo_medio(est, trip, clave, dia)
        if r is None:
            out.append(f"**{nombre}:** promedio {n(prom)} {unidad} de su valor inicial.")
        elif r < -0.001:
            out.append(f"**{nombre}:** promedio {n(prom)} {unidad}; baja {n(-r * 30, 2)} {unidad} por mes. "
                       f"Si sigue igual, terminaría la misión cerca de {n(prom + r * restan)} {unidad}.")
        elif r > 0.001:
            out.append(f"**{nombre}:** promedio {n(prom)} {unidad} y en recuperación (+{n(r * 30, 2)} {unidad}/mes).")
        else:
            out.append(f"**{nombre}:** estable en {n(prom)} {unidad}.")

    # Visión (SANS)
    sans = max(float(c.get("sans", 0)) for c in trip)
    r = _ritmo_medio(est, trip, "sans", dia)
    tend = "" if r is None else (" y en aumento" if r > 1e-4 else " y bajando" if r < -1e-4 else " y estable")
    out.append(f"**Visión (SANS):** índice máximo {n(sans, 2)} (0 = normal, 1 = daño máximo){tend}.")

    # Tormentas solares
    tormentas = int(d.get("tormentas_solares", 0))
    if est.tormentas_informadas is not None and tormentas > est.tormentas_informadas:
        out.append(f"**Tormentas solares:** {tormentas - est.tormentas_informadas} nueva(s) desde el último informe "
                   f"({tormentas} en total). {'La tripulación usa el refugio blindado.' if d.get('contramedidas', {}).get('refugio') else 'Sin refugio activo.'}")
    elif d.get("tormenta_activa"):
        out.append("**Tormenta solar en curso:** aumentan la dosis y el ruido del enlace.")

    # Sueño y estrés (solo si la nave los marca como problema)
    sistemas = {a["sistema"]: a for a in d.get("alertas", [])}
    for s in ("Sueño", "Estrés psicológico", "Función renal", "Capacidad aeróbica", "Cardiovascular"):
        if s in sistemas:
            a = sistemas[s]
            out.append(f"**{s}:** {a['nivel'].lower().replace('critico', 'crítico').replace('precaucion', 'precaución')} en "
                       f"{_quien(a['tripulantes'], len(trip))} ({a.get('detalle', '')}).")

    # Cabina
    cab = d.get("cabina", {})
    aire = sistemas.get("Calidad del aire")
    out.append(f"**Cabina:** CO₂ medio {n(cab.get('co2_mmhg', 0))} mmHg (máx. {n(cab.get('co2_max_mmhg', 0))}), "
               f"O₂ {n(cab.get('o2_pct', 0))} %, {n(cab.get('temp_c', 0))} °C"
               + (f"; **alerta de calidad del aire** ({aire['nivel'].lower()})." if aire else "; sin alerta de calidad del aire."))
    return out


def conclusion(d: dict) -> str:
    alertas = sorted(d.get("alertas", []), key=lambda a: -a.get("severidad", 0))
    total = len(d.get("tripulacion", []))
    cm = d.get("contramedidas", {})
    activas = ", ".join(
        f"{k} {int(round(float(v) * 100))} %" for k, v in cm.items() if isinstance(v, (int, float)) and not isinstance(v, bool) and k != "gravedad_artificial_g"
    )
    if not alertas:
        return "Tripulación en estado nominal. La telemetría no muestra problemas que requieran acción."
    top = alertas[:2]
    partes = [f"{a['sistema']} ({'crítico' if a['nivel'] == 'CRITICO' else 'precaución'}, {_quien(a['tripulantes'], total)})" for a in top]
    return (f"Prioridad para el equipo médico en Tierra: {' y '.join(partes)}. "
            f"Contramedidas reportadas por la nave: {activas}{', refugio solar activo' if cm.get('refugio') else ''}.")


def _distancia(e: dict) -> str:
    km = float(e.get("distancia_km", 0))
    if km <= 0:
        return "en Tierra"
    if km >= 1.5e6:
        return f"{n(float(e.get('distancia_ua', 0)), 2)} UA ({n(km / 1e6, 0)} millones de km)"
    return f"{n(km / 1000, 0)} mil km"


def _luz(s: float) -> str:
    return f"{n(s / 60, 1)} min" if s >= 60 else f"{n(s, 2)} s"


def generar_pdf(est: Estacion, motivo: str, estacion_nombre: str, ruta: str) -> str | None:
    """Versión PDF (si reportlab está instalado). Nunca detiene la estación si falla."""
    try:
        import shutil

        import informe_pdf
        informe_pdf.generar_pdf(est, motivo, estacion_nombre, ruta)
        shutil.copyfile(ruta, os.path.join(est.carpeta, "ultimo_informe.pdf"))
        est.error_pdf = ""
        return ruta
    except PermissionError:
        est.error_pdf = "el PDF anterior está abierto en otro programa"
        return ruta if os.path.exists(ruta) else None
    except Exception as e:  # noqa: BLE001
        est.error_pdf = f"no se pudo crear el PDF: {e}"
        return None


def generar(est: Estacion, motivo: str, estacion_nombre: str) -> tuple[str, str]:
    """Escribe el informe. Devuelve (titular, ruta)."""
    d = est.ultimo or {}
    trip = d.get("tripulacion", [])
    dia = int(d.get("dia", 0))
    general = estado_general(d)
    alertas = sorted(d.get("alertas", []), key=lambda a: -a.get("severidad", 0))
    criticas = [a for a in alertas if a.get("nivel") == "CRITICO"]
    resumen_alertas = "; ".join(f"{a['sistema']} en {_quien(a['tripulantes'], len(trip))}" for a in (criticas or alertas)[:3])
    titular = f"Día {dia} · {d.get('fase', '?')} · {general}" + (f": {resumen_alertas}" if resumen_alertas else "")

    L: list[str] = []
    L.append(f"# Informe de telemetría · {d.get('nave', 'AEGIS')} · Día {dia} de {n(float(d.get('dia_total', 0)), 0)}")
    L.append("")
    L.append(f"**Misión:** {d.get('mision', '?')} · **Fase:** {d.get('fase', '?')} "
             f"({n(float(d.get('gravedad_g', 0)), 2)} g, {n(float(d.get('radiacion_msv_dia', 0)), 2)} mSv/día)  ")
    L.append(f"**Generado:** {time.strftime('%Y-%m-%d %H:%M:%S UTC', time.gmtime())} · **Estación:** {estacion_nombre} · "
             f"**Motivo:** {motivo}")
    L.append("")
    L.append("## Resumen")
    L.append(f"**Estado general: {general}.**" + (f" {len(criticas)} problema(s) crítico(s): {resumen_alertas}." if criticas else
                                                   f" {resumen_alertas}." if resumen_alertas else ""))
    L.append("")
    L.append(conclusion(d))
    L.append("")
    L.append("## Qué está pasando")
    for x in inferencias(est, d):
        L.append(f"- {x}")
    L.append("")
    L.append("## Tripulación")
    L.append("| Tripulante | Índice | Hueso | Músculo | VO₂ máx | SANS | Dosis (mSv) | FC* | SpO₂* | Problema principal |")
    L.append("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |")
    for c in trip:
        vf = c.get("vitales_filtrados", {})
        suyas = [a for a in alertas if c["nombre"] in a.get("tripulantes", [])]
        prob = f"{suyas[0]['sistema']} ({'crítico' if suyas[0]['nivel'] == 'CRITICO' else 'precaución'})" if suyas else "—"
        L.append(f"| {c['nombre']} ({c.get('rol', '')}) | {n(c.get('indice_salud', 0), 0)} | {n(c.get('hueso_pct', 0))} % | "
                 f"{n(c.get('musculo_pct', 0))} % | {n(c.get('vo2_pct', 0))} % | {n(c.get('sans', 0), 2)} | {n(c.get('dosis_msv', 0), 0)} | "
                 f"{n(vf.get('fc', 0), 0)} | {n(vf.get('spo2', 0), 0)} % | {prob} |")
    L.append("")
    L.append("\\* Signos vitales después del filtro de picos.")
    L.append("")

    e = d.get("enlace", {})
    s = est.stats
    L.append("## Enlace espacio–Tierra y calidad de los datos")
    L.append(f"- **Distancia:** {_distancia(e)} · **retraso de la luz:** {_luz(float(e.get('retraso_luz_s', 0)))} · "
             f"**vía:** {e.get('via', '?')}")
    if float(e.get("distancia_km", 0)) > 0:
        L.append(f"- **Señal:** Eb/N0 {n(float(e.get('ebn0_db', 0)), 1)} dB · tasa de error de bit estimada "
                 f"{float(e.get('ber', 0)):.1e} · Sol a {n(float(e.get('angulo_sol_grados', 0)), 0)}° de la línea de visión")
    if e.get("fuentes_ruido"):
        L.append(f"- **Fuentes de ruido:** {'; '.join(e['fuentes_ruido'])}")
    L.append(f"- **Tramas:** {s.recibidas} recibidas · {s.validas} válidas · {s.tramas_corregidas} reparadas "
             f"({s.bits_corregidos} bits corregidos por Hamming) · {s.descartadas_ruido} descartadas por ruido (CRC) · "
             f"{s.perdidas_canal} perdidas en el camino · {s.rechazadas_clave} rechazadas por clave inválida · "
             f"{s.repetidas} repetidas · **calidad {n(s.calidad, 1)} %**")
    L.append(f"- **Seguridad:** AES-256 + firma HMAC-SHA256 verificada en cada trama válida.")
    if est.picos:
        L.append(f"- **Filtro de picos:** {s.picos_filtrados} lectura(s) descartada(s) (posibles impactos de radiación en los sensores). Últimas:")
        for dia_p, quien, var, bruto, acept, motivo_p in list(est.picos)[-5:]:
            L.append(f"  - Día {dia_p}, {quien}, {var}: {n(bruto)} → {n(acept)} ({motivo_p})")
    else:
        L.append("- **Filtro de picos:** sin lecturas anómalas.")
    L.append("")
    L.append("_Simulación educativa NASA Space Apps (equipo AEGIS). No es un diagnóstico médico._")

    texto = "\n".join(L) + "\n"
    os.makedirs(est.carpeta, exist_ok=True)
    ruta = os.path.join(est.carpeta, f"informe_dia_{dia:04d}.md")
    for p in (ruta, os.path.join(est.carpeta, "ultimo_informe.md")):
        with open(p, "w", encoding="utf-8") as f:
            f.write(texto)
    est.ultimo_pdf = generar_pdf(est, motivo, estacion_nombre, ruta[:-3] + ".pdf")
    est.marcar_informado(d)
    return titular, ruta
