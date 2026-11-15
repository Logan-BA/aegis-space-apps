"""Genera docs/AEGIS_demo.pdf: la demo de 7 diapositivas (16:9) para el envío a NASA Space Apps.

    receptor_python/.venv/Scripts/python docs/generar_demo.py      (requiere reportlab)

Las imágenes son capturas reales del proyecto (docs/capturas). Texto y código elaborados con asistencia de
Super Intelligence (Claude); se indica en el pie de cada diapositiva y en los metadatos del PDF.
"""
import os

from reportlab.lib.colors import HexColor, white
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas

AQUI = os.path.dirname(os.path.abspath(__file__))
CAP = os.path.join(AQUI, "capturas")
SALIDA = os.path.join(AQUI, "AEGIS_demo.pdf")
W, H = 960, 540

FONDO, PANEL, BORDE = HexColor("#0D1219"), HexColor("#151C27"), HexColor("#263142")
TEXTO, TENUE, ACENTO = HexColor("#E6EAF0"), HexColor("#8F9BAD"), HexColor("#4586C7")
OK, AVISO, PELIGRO = HexColor("#4FA36E"), HexColor("#D19A3F"), HexColor("#C9534F")

F, FB = "Helvetica", "Helvetica-Bold"
for normal, negrita in (("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/segoeuib.ttf"),
                        ("C:/Windows/Fonts/arial.ttf", "C:/Windows/Fonts/arialbd.ttf"),
                        ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf")):
    if os.path.exists(normal) and os.path.exists(negrita):
        pdfmetrics.registerFont(TTFont("Demo", normal))
        pdfmetrics.registerFont(TTFont("Demo-B", negrita))
        F, FB = "Demo", "Demo-B"
        break

SI = "Code and text developed with Super Intelligence (SI) assistance (Claude) · all images are real screenshots of the project"


def fondo(c, n, titulo, kicker=""):
    c.setFillColor(FONDO); c.rect(0, 0, W, H, stroke=0, fill=1)
    c.setFillColor(ACENTO); c.rect(0, H - 4, W, 4, stroke=0, fill=1)
    if kicker:
        c.setFillColor(ACENTO); c.setFont(FB, 11); c.drawString(48, H - 46, kicker.upper())
    c.setFillColor(TEXTO); c.setFont(FB, 28); c.drawString(48, H - 82, titulo)
    c.setFillColor(TENUE); c.setFont(F, 8.5)
    c.drawString(48, 18, SI)
    c.drawRightString(W - 48, 18, f"AEGIS · NASA Space Apps Challenge · {n}/7")


def parrafo(c, x, y, ancho, texto, tam=14, color=TEXTO, fuente=None, interlineado=1.35):
    """Escribe texto con salto de línea automático. **negrita** al inicio de una línea se resalta."""
    fuente = fuente or F
    c.setFillColor(color)
    for linea in texto.split("\n"):
        palabras, actual = linea.split(" "), ""
        negrita = linea.startswith("**")
        fnt = FB if negrita else fuente
        palabras = linea.replace("**", "").split(" ")
        for p in palabras:
            prueba = (actual + " " + p).strip()
            if c.stringWidth(prueba, fnt, tam) > ancho:
                c.setFont(fnt, tam); c.drawString(x, y, actual)
                y -= tam * interlineado
                actual = p
            else:
                actual = prueba
        c.setFont(fnt, tam); c.drawString(x, y, actual)
        y -= tam * interlineado
    return y


def vinetas(c, x, y, ancho, items, tam=13.5, espacio=7):
    """Viñetas; si el ítem empieza con 'Título:' el título va en negrita y el resto continúa en la misma línea."""
    for it in items:
        c.setFillColor(ACENTO); c.circle(x + 4, y + tam * 0.33, 3, stroke=0, fill=1)
        titulo, sep, resto = it.partition(": ")
        if not sep or len(titulo) > 28:
            titulo, resto = "", it
        cx = x + 16
        if titulo:
            c.setFont(FB, tam); c.setFillColor(TEXTO)
            c.drawString(cx, y, titulo + ":")
            cx += c.stringWidth(titulo + ": ", FB, tam)
        c.setFont(F, tam); c.setFillColor(TEXTO)
        actual = ""
        for p in resto.split(" "):
            prueba = (actual + " " + p).strip()
            if c.stringWidth(prueba, F, tam) > x + ancho - cx:
                c.drawString(cx, y, actual)
                y -= tam * 1.35
                cx, actual = x + 16, p
            else:
                actual = prueba
        c.drawString(cx, y, actual)
        y -= tam * 1.35 + espacio
    return y


def imagen(c, nombre, x, y, w, h, borde=True):
    ruta = os.path.join(CAP, nombre)
    img = ImageReader(ruta)
    iw, ih = img.getSize()
    esc = min(w / iw, h / ih)
    dw, dh = iw * esc, ih * esc
    dx, dy = x + (w - dw) / 2, y + (h - dh) / 2
    c.drawImage(img, dx, dy, dw, dh)
    if borde:
        c.setStrokeColor(BORDE); c.setLineWidth(1); c.rect(dx, dy, dw, dh, stroke=1, fill=0)


def caja(c, x, y, w, h, titulo, texto, color=ACENTO):
    c.setFillColor(PANEL); c.setStrokeColor(BORDE); c.roundRect(x, y, w, h, 8, stroke=1, fill=1)
    c.setFillColor(color); c.rect(x, y + h - 4, w, 4, stroke=0, fill=1)
    c.setFillColor(TEXTO); c.setFont(FB, 14); c.drawString(x + 14, y + h - 28, titulo)
    parrafo(c, x + 14, y + h - 50, w - 28, texto, 11.5, TENUE)


def flecha(c, x1, y, x2):
    c.setStrokeColor(ACENTO); c.setLineWidth(2); c.line(x1, y, x2 - 6, y)
    c.setFillColor(ACENTO)
    p = c.beginPath(); p.moveTo(x2, y); p.lineTo(x2 - 9, y + 5); p.lineTo(x2 - 9, y - 5); p.close()
    c.drawPath(p, stroke=0, fill=1)


def main():
    c = canvas.Canvas(SALIDA, pagesize=(W, H))
    c.setTitle("AEGIS · Astronaut health monitoring with onboard AI and an encrypted space-to-Earth link")
    c.setAuthor("Team AEGIS · NASA Space Apps Challenge")
    c.setSubject("Project demo (7 slides). Developed with Super Intelligence (SI) assistance: Claude (Anthropic).")
    c.setKeywords("NASA Space Apps, astronaut health, SI-assisted, Unity, Ollama, Piper, Whisper, WebSocket, AES-256")

    # 1 · Portada
    c.setFillColor(FONDO); c.rect(0, 0, W, H, stroke=0, fill=1)
    imagen(c, "simulacion_marte.png", 440, 120, 490, 300)
    c.setFillColor(ACENTO); c.rect(0, H - 4, W, 4, stroke=0, fill=1)
    c.setFillColor(ACENTO); c.setFont(FB, 12); c.drawString(48, 420, "NASA SPACE APPS CHALLENGE · TEAM AEGIS")
    c.setFillColor(TEXTO); c.setFont(FB, 52); c.drawString(48, 352, "AEGIS")
    parrafo(c, 48, 316, 370, "Astronaut health monitoring with an onboard AI medical assistant and an encrypted space-to-Earth link", 19, TEXTO, FB, 1.3)
    parrafo(c, 48, 214, 370, "Simulates the crew's health on ISS, Moon and Mars missions, explains it in plain words, and delivers it securely to ground control.", 13, TENUE)
    c.setFillColor(TENUE); c.setFont(F, 11); c.drawString(48, 120, "github.com/Logan-BA/aegis-space-apps")
    c.setFont(F, 8.5); c.drawString(48, 18, SI); c.drawRightString(W - 48, 18, "1/7")
    c.showPage()

    # 2 · Problema
    fondo(c, 2, "Far from Earth, the crew is on its own", "The problem")
    vinetas(c, 48, 400, 520, [
        "Bone and muscle: without gravity bone density drops about 1–1.5 % per month and muscles shrink.",
        "Radiation: cosmic rays and solar storms; NASA's career limit is 600 mSv of effective dose.",
        "Vision (SANS), sleep, stress and cabin CO₂ add up during long missions.",
        "Distance: on Mars a message takes 3–22 minutes each way; the Sun can block the link for days.",
        "If the crew loses its doctor, someone onboard must explain what is happening and what to do.",
    ])
    caja(c, 610, 290, 300, 125, "What we need", "Continuous monitoring, early warnings, clear explanations for non-experts, and trustworthy data on Earth even with a noisy, delayed link.")
    caja(c, 610, 160, 300, 110, "Who it helps", "Crew members, flight surgeons on Earth, and the public who wants to understand human spaceflight.", OK)
    c.showPage()

    # 3 · Solución
    fondo(c, 3, "One system from the spacecraft to ground control", "Our solution")
    xs = [48, 270, 492, 714]
    datos = [("1 · Spacecraft", "Unity 3D simulation: hourly physiological model of 4 astronauts, cabin air, alerts and countermeasures."),
             ("2 · ASTRA", "Onboard AI nurse with a holographic face: diagnoses, explains and talks with the crew by voice — 100 % local."),
             ("3 · Space link", "Encrypted WebSocket telemetry with error correction through a simulated deep-space noise channel."),
             ("4 · Ground stations", "Python and Rust receivers: decrypt, filter cosmic noise, infer trends, live web panel and PDF reports.")]
    for x, (t, d) in zip(xs, datos):
        caja(c, x, 250, 198, 150, t, d)
    for x in xs[:-1]:
        flecha(c, x + 200, 325, x + 220)
    parrafo(c, 48, 200, 860, "Everything runs offline on ordinary computers: no paid APIs, no cloud, no data leaving the mission. The same telemetry can reach several ground stations at once.", 14, TENUE)
    c.showPage()

    # 4 · Simulación
    fondo(c, 4, "A digital twin of the crew's health", "Simulation · Unity")
    imagen(c, "simulacion_marte.png", 430, 70, 485, 330)
    vinetas(c, 48, 400, 360, [
        "Missions: ISS 6 months / 1 year, Moon 40 days, Mars ~1,100 days, after 14 days of conditioning.",
        "Hourly model: bone, muscle, VO₂max, SANS, radiation dose, sleep, stress, vitals and blood chemistry.",
        "Cabin: CO₂ and dust diffusion–advection in 12 modules.",
        "Early warning: dynamic thresholds (EWMA) + trend forecasts.",
        "Countermeasures computed with their benefit; autopilot applies them.",
    ], 12.5, 6)
    c.showPage()

    # 5 · ASTRA
    fondo(c, 5, "ASTRA: an onboard AI nurse you can talk to", "Onboard assistant")
    imagen(c, "astra_asistente.png", 560, 70, 355, 330)
    vinetas(c, 48, 400, 480, [
        "Diagnoses each astronaut from simulation data and explains causes, effects and what to do in plain words.",
        "Grounded answers: a medical rules engine computes the facts; a local LLM (Qwen2.5 3B on Ollama) only rephrases them.",
        "Realistic hologram with 176 facial expressions, lip sync, blinking and emotions (calm, worried, firm, alert).",
        "Real-time voice: neural speech (Piper) and hands-free microphone (Whisper) — answers in ~5 s, no internet.",
        "Learns from feedback: answers rated useful become examples for the local model.",
    ], 12.5, 6)
    c.showPage()

    # 6 · Enlace
    fondo(c, 6, "Only ground control can read it — even through cosmic noise", "Space-to-Earth link")
    vinetas(c, 48, 400, 500, [
        "Security: AES-256 encryption + HMAC-SHA256 signature + sequence numbers (anti-replay).",
        "Error correction: Hamming(7,4) + interleaving repairs bit errors and bursts; CRC-32 discards the rest.",
        "Physics-based noise: distance (Eb/N0), solar conjunction, solar storms, ionosphere and Van Allen belts.",
        "Sensor glitches from radiation (SEU) removed with a Hampel filter.",
        "Store-and-forward when the link is down, like real spacecraft.",
    ], 12.5, 6)
    caja(c, 600, 300, 310, 105, "Tested end to end", "Spacecraft → Python and Rust stations at once over 410 mission days, including solar conjunction: 81–83 % link quality.", OK)
    caja(c, 600, 175, 310, 105, "Intruder test", "5 of 5 frames signed with a fake key were rejected by both stations. Thousands of bits corrected.", PELIGRO)
    c.showPage()

    # 7 · Control en Tierra + impacto
    fondo(c, 7, "Ground control: live panel and PDF reports", "Impact")
    imagen(c, "panel_estacion.jpg", 48, 190, 430, 230)
    imagen(c, "informe_pdf_p1.png", 500, 110, 200, 310)
    vinetas(c, 720, 410, 200, [
        "Live web panel for any device.",
        "Automatic PDF reports on every event.",
        "Trends and projections, e.g. day the dose limit would be reached.",
    ], 11.5, 5)
    parrafo(c, 48, 160, 430, "Impact: earlier detection, decisions that do not wait 40 minutes for Earth, data that cannot be read or forged, and a clear way to show the public what space does to the human body.", 13, TEXTO)
    c.setFillColor(ACENTO); c.setFont(FB, 13)
    c.drawString(48, 78, "Code: github.com/Logan-BA/aegis-space-apps")
    c.drawString(48, 56, "Try it (Windows): github.com/Logan-BA/aegis-space-apps/releases")
    c.showPage()
    c.save()
    print("Demo creada:", SALIDA)


if __name__ == "__main__":
    main()
