"""Informe de telemetría en PDF (reportlab): resumen, inferencias, tripulación, gráficos de tendencia,
alertas, calidad del enlace y filtro de picos. Usa los mismos datos e inferencias que el informe Markdown."""
from __future__ import annotations

import os
import re
import time

from reportlab.graphics.charts.lineplots import LinePlot
from reportlab.graphics.shapes import Drawing, Line, Rect, String
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import KeepTogether, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle

import informe
from estacion import Estacion

# ---------------------------------------------------------------- estilo (paleta sobria del proyecto)
AZUL = colors.HexColor("#2F6FB0")
GRAFITO = colors.HexColor("#1B2230")
GRIS = colors.HexColor("#6F7A8A")
GRIS_CLARO = colors.HexColor("#E6EAF0")
FONDO = colors.HexColor("#F4F6F9")
ROJO = colors.HexColor("#B53A3A")
AMBAR = colors.HexColor("#B7832F")
VERDE = colors.HexColor("#3E8E5E")
SERIES = [colors.HexColor(c) for c in ("#2F6FB0", "#B7832F", "#3E8E5E", "#8A4FB0")]

_FUENTE, _NEGRITA = "Helvetica", "Helvetica-Bold"


def _fuentes() -> None:
    """Usa una fuente TrueType con todos los acentos y símbolos (CO₂, →) si existe en el sistema."""
    global _FUENTE, _NEGRITA
    if _FUENTE != "Helvetica":
        return
    pares = [
        ("C:/Windows/Fonts/arial.ttf", "C:/Windows/Fonts/arialbd.ttf"),
        ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/segoeuib.ttf"),
        ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
        ("/usr/share/fonts/dejavu-sans-fonts/DejaVuSans.ttf", "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Bold.ttf"),
        ("/usr/share/fonts/TTF/DejaVuSans.ttf", "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf"),
        ("/System/Library/Fonts/Supplemental/Arial.ttf", "/System/Library/Fonts/Supplemental/Arial Bold.ttf"),
    ]
    for normal, negrita in pares:
        if os.path.exists(normal) and os.path.exists(negrita):
            try:
                pdfmetrics.registerFont(TTFont("AegisSans", normal))
                pdfmetrics.registerFont(TTFont("AegisSans-Bold", negrita))
                pdfmetrics.registerFontFamily("AegisSans", normal="AegisSans", bold="AegisSans-Bold",
                                              italic="AegisSans", boldItalic="AegisSans-Bold")
                _FUENTE, _NEGRITA = "AegisSans", "AegisSans-Bold"
                return
            except Exception:  # noqa: BLE001
                continue


def _txt(s: str) -> str:
    """Escapa para Paragraph y convierte el **negrita** / *cursiva* del Markdown."""
    s = str(s).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    s = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", s)
    s = re.sub(r"\*(.+?)\*", r"<i>\1</i>", s)
    s = s.replace("₂", "2")                # Arial y Helvetica no traen subíndices
    if _FUENTE == "Helvetica":   # sin TrueType: símbolos fuera de WinAnsi
        s = s.replace("→", "->").replace("≈", "~")
    return s


def _estilos() -> dict[str, ParagraphStyle]:
    base = ParagraphStyle("base", fontName=_FUENTE, fontSize=9.2, leading=12.6, textColor=GRAFITO, alignment=TA_LEFT)
    return {
        "base": base,
        "vineta": ParagraphStyle("vineta", parent=base, leftIndent=11, bulletIndent=2, spaceAfter=2.5, bulletFontName=_FUENTE),
        "chico": ParagraphStyle("chico", parent=base, fontSize=7.6, leading=10, textColor=GRIS),
        "h2": ParagraphStyle("h2", parent=base, fontName=_NEGRITA, fontSize=11.5, leading=15, textColor=AZUL,
                             spaceBefore=8, spaceAfter=4),
        "titulo": ParagraphStyle("titulo", parent=base, fontName=_NEGRITA, fontSize=16, leading=20, textColor=colors.white),
        "sub": ParagraphStyle("sub", parent=base, fontSize=8.8, leading=12, textColor=colors.HexColor("#C9D3E0")),
        "celda": ParagraphStyle("celda", parent=base, fontSize=8, leading=10),
        "celdab": ParagraphStyle("celdab", parent=base, fontName=_NEGRITA, fontSize=7.6, leading=9.5),
        "estado": ParagraphStyle("estado", parent=base, fontName=_NEGRITA, fontSize=13, leading=16,
                                 textColor=colors.white, alignment=1),
    }


def _color_estado(general: str):
    return ROJO if general == "CRÍTICO" else AMBAR if general == "PRECAUCIÓN" else VERDE


# ---------------------------------------------------------------- gráficos

def _grafico(titulo: str, series: list[tuple[str, list[tuple[float, float]]]], unidad: str,
             limite: float | None = None, ancho: float = 88 * mm, alto: float = 58 * mm) -> Drawing:
    d = Drawing(ancho, alto)
    d.add(Rect(0, 0, ancho, alto, fillColor=FONDO, strokeColor=None))
    d.add(String(6, alto - 12, titulo, fontName=_NEGRITA, fontSize=8.5, fillColor=GRAFITO))
    validas = [(n, s) for n, s in series if len(s) >= 2]
    if not validas:
        d.add(String(ancho / 2, alto / 2, "Aún no hay datos suficientes", fontName=_FUENTE, fontSize=8,
                     fillColor=GRIS, textAnchor="middle"))
        return d
    lp = LinePlot()
    lp.x, lp.y, lp.width, lp.height = 30, 26, ancho - 40, alto - 52
    lp.data = [s for _, s in validas]
    xs = [p[0] for _, s in validas for p in s]
    ys = [p[1] for _, s in validas for p in s] + ([limite] if limite is not None else [])
    ymin, ymax = min(ys), max(ys)
    if ymax - ymin < 1e-6:
        ymax += 1
        ymin -= 1
    margen = (ymax - ymin) * 0.08
    lp.xValueAxis.valueMin, lp.xValueAxis.valueMax = min(xs), max(xs) if max(xs) > min(xs) else min(xs) + 1
    lp.yValueAxis.valueMin, lp.yValueAxis.valueMax = ymin - margen, ymax + margen
    for eje in (lp.xValueAxis, lp.yValueAxis):
        eje.labels.fontName, eje.labels.fontSize, eje.labels.fillColor = _FUENTE, 6.5, GRIS
        eje.strokeColor, eje.strokeWidth = GRIS_CLARO, 0.6
    lp.yValueAxis.visibleGrid, lp.yValueAxis.gridStrokeColor, lp.yValueAxis.gridStrokeWidth = True, GRIS_CLARO, 0.4
    lp.xValueAxis.labelTextFormat = lambda v: f"{v:.0f}"
    lp.yValueAxis.labelTextFormat = lambda v: f"{v:.2f}".rstrip("0").rstrip(".").replace(".", ",")
    for i in range(len(validas)):
        lp.lines[i].strokeColor = SERIES[i % len(SERIES)]
        lp.lines[i].strokeWidth = 1.2
    d.add(lp)
    if limite is not None and lp.yValueAxis.valueMin <= limite <= lp.yValueAxis.valueMax:
        y = lp.y + (limite - lp.yValueAxis.valueMin) / (lp.yValueAxis.valueMax - lp.yValueAxis.valueMin) * lp.height
        d.add(Line(lp.x, y, lp.x + lp.width, y, strokeColor=ROJO, strokeWidth=0.8, strokeDashArray=[3, 2]))
        d.add(String(lp.x + lp.width - 2, y + 2, f"límite {limite:.0f} {unidad}", fontName=_FUENTE, fontSize=6.5,
                     fillColor=ROJO, textAnchor="end"))
    # leyenda
    x = 6
    for i, (nombre, _) in enumerate(validas):
        d.add(Rect(x, 6, 6, 6, fillColor=SERIES[i % len(SERIES)], strokeColor=None))
        d.add(String(x + 9, 6.5, nombre, fontName=_FUENTE, fontSize=6.5, fillColor=GRAFITO))
        x += 9 + len(nombre) * 3.4 + 10
    d.add(String(ancho - 6, 6.5, "día de misión", fontName=_FUENTE, fontSize=6.5, fillColor=GRIS, textAnchor="end"))
    return d


def _serie(est: Estacion, nombre: str, clave: str) -> list[tuple[float, float]]:
    pts = sorted(est.hist.get(nombre, {}).get(clave, {}).items())
    if len(pts) > 300:
        paso = len(pts) / 300
        pts = [pts[int(i * paso)] for i in range(300)] + [pts[-1]]
    return [(float(dd), float(v)) for dd, v in pts]


# ---------------------------------------------------------------- documento

def generar_pdf(est: Estacion, motivo: str, estacion_nombre: str, ruta: str) -> str:
    _fuentes()
    st = _estilos()
    d = est.ultimo or {}
    trip = d.get("tripulacion", [])
    dia = int(d.get("dia", 0))
    general = informe.estado_general(d)
    alertas = sorted(d.get("alertas", []), key=lambda a: -a.get("severidad", 0))
    n = informe.n
    historia: list = []

    # Encabezado
    cab = Table(
        [[Paragraph(_txt(f"Informe de telemetría · {d.get('nave', 'AEGIS')}"), st["titulo"]),
          Paragraph(_txt(general), st["estado"])],
         [Paragraph(_txt(f"{d.get('mision', '?')} · Día {dia} de {n(float(d.get('dia_total', 0)), 0)} · "
                         f"{d.get('fase', '?')} ({n(float(d.get('gravedad_g', 0)), 2)} g, "
                         f"{n(float(d.get('radiacion_msv_dia', 0)), 2)} mSv/día)"), st["sub"]), ""],
         [Paragraph(_txt(f"Generado {time.strftime('%Y-%m-%d %H:%M UTC', time.gmtime())} · {estacion_nombre} · "
                         f"Motivo: {motivo}"), st["sub"]), ""]],
        colWidths=[140 * mm, 38 * mm])
    cab.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), GRAFITO),
        ("BACKGROUND", (1, 0), (1, -1), _color_estado(general)),
        ("SPAN", (1, 0), (1, -1)),
        ("VALIGN", (1, 0), (1, -1), "MIDDLE"),
        ("LEFTPADDING", (0, 0), (-1, -1), 8), ("RIGHTPADDING", (0, 0), (-1, -1), 8),
        ("TOPPADDING", (0, 0), (-1, 0), 8), ("BOTTOMPADDING", (0, -1), (-1, -1), 8),
        ("LINEBELOW", (0, -1), (-1, -1), 2, AZUL),
    ]))
    historia += [cab, Spacer(1, 8)]

    # Resumen
    criticas = [a for a in alertas if a.get("nivel") == "CRITICO"]
    base = criticas or alertas
    resumen = "; ".join(f"{a['sistema']} en {informe._quien(a['tripulantes'], len(trip))}" for a in base[:3])
    historia.append(Paragraph("Resumen", st["h2"]))
    if criticas:
        historia.append(Paragraph(_txt(f"**Estado general: {general}.** {len(criticas)} problema(s) crítico(s): {resumen}."), st["base"]))
    else:
        historia.append(Paragraph(_txt(f"**Estado general: {general}.** {resumen + '.' if resumen else ''}"), st["base"]))
    historia += [Spacer(1, 3), Paragraph(_txt(informe.conclusion(d)), st["base"])]

    # Qué está pasando
    historia.append(Paragraph("Qué está pasando", st["h2"]))
    for x in informe.inferencias(est, d):
        historia.append(Paragraph(_txt(x), st["vineta"], bulletText="•"))

    # Tripulación
    filas = [[Paragraph(_txt(t), st["celdab"]) for t in
              ("Tripulante", "Índice", "Hueso", "Músculo", "VO2 máx", "SANS", "Dosis mSv", "FC*", "SpO2*", "Problema principal")]]
    estilos_filas = []
    for i, c in enumerate(trip, start=1):
        vf = c.get("vitales_filtrados", {})
        suyas = [a for a in alertas if c["nombre"] in a.get("tripulantes", [])]
        prob = f"{suyas[0]['sistema']} ({'crítico' if suyas[0]['nivel'] == 'CRITICO' else 'precaución'})" if suyas else "—"
        filas.append([Paragraph(f"<b>{_txt(c['nombre'])}</b><br/>{_txt(c.get('rol', ''))}", st["celda"]),
                      n(c.get("indice_salud", 0), 0), f"{n(c.get('hueso_pct', 0))} %", f"{n(c.get('musculo_pct', 0))} %",
                      f"{n(c.get('vo2_pct', 0))} %", n(c.get("sans", 0), 2), n(c.get("dosis_msv", 0), 0),
                      n(vf.get("fc", 0), 0), f"{n(vf.get('spo2', 0), 0)} %", Paragraph(_txt(prob), st["celda"])])
        if suyas:
            estilos_filas.append(("TEXTCOLOR", (9, i), (9, i), ROJO if suyas[0]["nivel"] == "CRITICO" else AMBAR))
    tabla = Table(filas, colWidths=[31 * mm, 14 * mm, 16 * mm, 17 * mm, 15 * mm, 13 * mm, 15 * mm, 10 * mm, 13 * mm, 34 * mm],
                  repeatRows=1)
    tabla.setStyle(TableStyle([
        ("FONTNAME", (0, 0), (-1, -1), _FUENTE), ("FONTSIZE", (0, 0), (-1, -1), 8),
        ("BACKGROUND", (0, 0), (-1, 0), GRIS_CLARO), ("LINEBELOW", (0, 0), (-1, 0), 0.8, AZUL),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, FONDO]),
        ("ALIGN", (1, 1), (8, -1), "RIGHT"), ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("TOPPADDING", (0, 0), (-1, -1), 3), ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
        *estilos_filas,
    ]))
    historia += [Paragraph("Tripulación", st["h2"]), tabla,
                 Paragraph("* Signos vitales después del filtro de picos (lecturas falsas por radiación descartadas).", st["chico"])]

    # Gráficos
    nombres = [c["nombre"] for c in trip]
    limite = float(d.get("limite_dosis_msv", 600))
    g = [
        _grafico("Dosis de radiación acumulada (mSv)", [(nm, _serie(est, nm, "dosis_msv")) for nm in nombres], "mSv", limite),
        _grafico("Índice SANS (visión)", [(nm, _serie(est, nm, "sans")) for nm in nombres], ""),
        _grafico("Densidad ósea (% del inicio)", [(nm, _serie(est, nm, "hueso_pct")) for nm in nombres], "%"),
        _grafico("Masa muscular (% del inicio)", [(nm, _serie(est, nm, "musculo_pct")) for nm in nombres], "%"),
    ]
    graf = Table([[g[0], g[1]], [g[2], g[3]]], colWidths=[90 * mm, 90 * mm])
    graf.setStyle(TableStyle([("LEFTPADDING", (0, 0), (-1, -1), 1), ("RIGHTPADDING", (0, 0), (-1, -1), 1),
                              ("TOPPADDING", (0, 0), (-1, -1), 2), ("BOTTOMPADDING", (0, 0), (-1, -1), 2)]))
    historia.append(KeepTogether([Paragraph("Tendencias de la misión", st["h2"]), graf]))

    # Alertas
    if alertas:
        fa = [[Paragraph(_txt(t), st["celdab"]) for t in ("Sistema", "Nivel", "Tripulantes", "Detalle")]]
        est_a = []
        for i, a in enumerate(alertas, start=1):
            critico = a.get("nivel") == "CRITICO"
            fa.append([Paragraph(_txt(f"**{a['sistema']}**"), st["celda"]), "CRÍTICO" if critico else "PRECAUCIÓN",
                       Paragraph(_txt(informe._quien(a["tripulantes"], len(trip))), st["celda"]),
                       Paragraph(_txt(a.get("detalle", "")), st["celda"])])
            est_a.append(("TEXTCOLOR", (1, i), (1, i), ROJO if critico else AMBAR))
        ta = Table(fa, colWidths=[36 * mm, 22 * mm, 40 * mm, 80 * mm], repeatRows=1)
        ta.setStyle(TableStyle([
            ("FONTNAME", (0, 0), (-1, -1), _FUENTE), ("FONTSIZE", (0, 0), (-1, -1), 8),
            ("FONTNAME", (1, 1), (1, -1), _NEGRITA),
            ("BACKGROUND", (0, 0), (-1, 0), GRIS_CLARO), ("LINEBELOW", (0, 0), (-1, 0), 0.8, AZUL),
            ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, FONDO]), ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
            *est_a,
        ]))
        historia += [Paragraph("Alertas activas", st["h2"]), ta]

    # Enlace
    e = d.get("enlace", {})
    s = est.stats
    km = float(e.get("distancia_km", 0))
    filas_e = [
        ("Distancia", informe._distancia(e)),
        ("Retraso de la luz", informe._luz(float(e.get("retraso_luz_s", 0)))),
        ("Vía", e.get("via", "?")),
    ]
    if km > 0:
        filas_e += [("Señal (Eb/N0)", f"{n(float(e.get('ebn0_db', 0)))} dB · tasa de error de bit {float(e.get('ber', 0)):.1e}"),
                    ("Sol respecto a la línea de visión", f"{n(float(e.get('angulo_sol_grados', 0)), 0)}°")]
    filas_e += [
        ("Fuentes de ruido", "; ".join(e.get("fuentes_ruido", [])) or "—"),
        ("Tramas", f"{s.recibidas} recibidas · {s.validas} válidas · {s.tramas_corregidas} reparadas ({s.bits_corregidos} bits) · "
                   f"{s.descartadas_ruido} descartadas por ruido · {s.perdidas_canal} perdidas · {s.rechazadas_clave} rechazadas por clave"),
        ("Calidad del enlace", f"{n(s.calidad, 1)} %"),
        ("Seguridad", "AES-256 + firma HMAC-SHA256 verificada en cada trama válida"),
    ]
    te = Table([[Paragraph(_txt(f"**{k}**"), st["celda"]), Paragraph(_txt(v), st["celda"])] for k, v in filas_e],
               colWidths=[45 * mm, 133 * mm])
    te.setStyle(TableStyle([("ROWBACKGROUNDS", (0, 0), (-1, -1), [colors.white, FONDO]),
                            ("VALIGN", (0, 0), (-1, -1), "TOP"), ("TOPPADDING", (0, 0), (-1, -1), 2.5),
                            ("BOTTOMPADDING", (0, 0), (-1, -1), 2.5)]))
    historia += [KeepTogether([Paragraph("Enlace espacio–Tierra y calidad de los datos", st["h2"]), te])]

    # Picos filtrados
    if est.picos:
        fp = [[Paragraph(_txt(t), st["celdab"]) for t in ("Día", "Tripulante", "Sensor", "Leído", "Aceptado", "Motivo")]]
        for dia_p, quien, var, bruto, acept, motivo_p in list(est.picos)[-10:]:
            fp.append([str(dia_p), quien, var, n(bruto), n(acept), Paragraph(_txt(motivo_p), st["celda"])])
        tp = Table(fp, colWidths=[12 * mm, 30 * mm, 16 * mm, 18 * mm, 18 * mm, 84 * mm], repeatRows=1)
        tp.setStyle(TableStyle([("FONTNAME", (0, 0), (-1, -1), _FUENTE), ("FONTSIZE", (0, 0), (-1, -1), 8),
                                ("BACKGROUND", (0, 0), (-1, 0), GRIS_CLARO), ("LINEBELOW", (0, 0), (-1, 0), 0.8, AZUL),
                                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, FONDO]),
                                ("TEXTCOLOR", (3, 1), (3, -1), ROJO), ("VALIGN", (0, 0), (-1, -1), "MIDDLE")]))
        historia += [KeepTogether([Paragraph(f"Filtro de picos · {s.picos_filtrados} lectura(s) descartada(s)", st["h2"]),
                                   Paragraph("Posibles impactos de radiación en la electrónica de los sensores (SEU).", st["chico"]),
                                   Spacer(1, 3), tp])]

    def pie(canvas, doc) -> None:
        canvas.saveState()
        canvas.setFont(_FUENTE, 7)
        canvas.setFillColor(GRIS)
        canvas.drawString(15 * mm, 10 * mm, "AEGIS · Simulación educativa NASA Space Apps · No es un diagnóstico médico")
        canvas.drawRightString(195 * mm, 10 * mm, f"CONFIDENCIAL · solo personal autorizado · pág. {doc.page}")
        canvas.restoreState()

    os.makedirs(os.path.dirname(os.path.abspath(ruta)), exist_ok=True)
    doc = SimpleDocTemplate(ruta, pagesize=A4, leftMargin=15 * mm, rightMargin=15 * mm, topMargin=14 * mm, bottomMargin=16 * mm,
                            title=f"Informe AEGIS día {dia}", author=estacion_nombre, subject="Telemetría de salud de la tripulación")
    doc.build(historia, onFirstPage=pie, onLaterPages=pie)
    return ruta
