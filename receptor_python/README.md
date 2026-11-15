# Estación terrena AEGIS · Python

Recibe por **WebSocket** la telemetría cifrada que transmite la simulación (la nave), corrige el ruido del canal
espacial, verifica que venga de la nave (firma), descifra, filtra lecturas falsas de los sensores y escribe
**informes** de lo que pasa en la misión. Protocolo y justificación: [../ENLACE_ESPACIO_TIERRA.md](../ENLACE_ESPACIO_TIERRA.md).

## Archivos

| Archivo | Qué hace |
| --- | --- |
| `receptor.py` | Servidor WebSocket (puerto 8765). Muestra el estado en vivo y avisa cada informe. |
| `aegis_protocolo.py` | Hamming(7,4) + entrelazado, CRC-32, HMAC-SHA256, AES-256-CBC, deflate. |
| `filtros.py` | Filtro de Hampel y límites de plausibilidad de los sensores; tendencias (regresión lineal). |
| `estacion.py` | Estado de la estación: estadísticas del enlace, historia por tripulante, CSV. |
| `informe.py` | Informe en Markdown: resumen, qué está pasando (tendencias y proyecciones), tripulación, enlace. |
| `informe_pdf.py` | Informe en **PDF** con encabezado de estado, inferencias, tabla de tripulación, gráficos de tendencia, alertas, enlace y picos filtrados. |
| `panel_web.py` + `panel/` | **Panel de control web** (puerto 8080): estado en vivo, gráficos, alertas, informes PDF. Sin internet ni librerías externas en el navegador. |
| `generar_clave.py` | Crea una clave de 256 bits (`clave_nasa.key`). |
| `emisor_prueba.py` | Imita a la nave para probar sin Unity (incluye `--clave-falsa` para probar un intruso). |
| `requirements.txt` | Librerías: `websockets`, `cryptography` y `reportlab` (PDF). |
| `clave_nasa.key` | Clave de la misión (la misma que usa la simulación). **Secreta.** |

## Requisitos
Python **3.10 o más reciente**.

## Instalación

### Windows (PowerShell)
```bash
winget install -e --id Python.Python.3.12
```
```bash
cd receptor_python
```
```bash
py -m venv .venv
```
```bash
.venv\Scripts\python -m pip install -r requirements.txt
```

### Linux (Ubuntu / Debian)
```bash
sudo apt update && sudo apt install -y python3 python3-venv python3-pip
```
```bash
cd receptor_python
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
```
(Fedora: `sudo dnf install -y python3 python3-pip`; Arch: `sudo pacman -S python python-pip`.)

## Uso

Windows:
```bash
.venv\Scripts\python receptor.py
```
Linux:
```bash
.venv/bin/python receptor.py
```

Opciones:

| Opción | Por defecto | Descripción |
| --- | --- | --- |
| `--puerto` | `8765` | Puerto WebSocket |
| `--host` | `0.0.0.0` | Interfaz (0.0.0.0 = acepta conexiones de otras computadoras) |
| `--clave` | `clave_nasa.key` | Archivo de la clave |
| `--informes` | `informes` | Carpeta de salida |
| `--cada-dias` | `30` | Informe periódico cada N días de misión (0 = solo por eventos) |
| `--nombre` | `Estación terrena Python` | Nombre que aparece en los informes |
| `--panel` | `8080` | Puerto del panel de control web (`0` = sin panel) |

## Panel de control e informes PDF

Al arrancar, la estación abre un **panel de control web**:

- En esta PC: <http://localhost:8080>
- Desde otra computadora, tablet o celular de la misma red: `http://IP_DE_ESTA_PC:8080` (la consola muestra la dirección exacta).

El panel se actualiza solo cada segundo y muestra:
- **Indicadores:** estado general, día de misión, distancia a la Tierra y retraso de la luz, calidad del enlace, tormentas solares y picos filtrados.
- **Tripulación:** índice de salud, hueso, músculo, VO₂, SANS, dosis y signos vitales ya filtrados de cada tripulante.
- **Tendencias:** gráficos de dosis (con el límite de carrera), SANS, densidad ósea y masa muscular.
- **Situación:** qué está pasando y la conclusión para el equipo médico, alertas activas, datos del enlace, fuentes de ruido y picos descartados.
- **Informes:** lista con botones **PDF** y **Texto**, y descarga del **CSV** con toda la telemetría.

**Generar informe PDF** (botón azul) crea el informe del momento y lo abre en una pestaña. Además, la estación genera
un PDF por sí sola en cada evento (primer contacto, tormenta solar, cambio de fase, nueva alerta crítica, fin de la misión,
cada `--cada-dias` días y al cerrar). Todo queda en `informes/` (`informe_dia_XXXX.pdf`, `.md` y `ultimo_informe.pdf`).

Si Windows pregunta por el firewall, permita también el puerto 8080 en redes privadas. En Linux: `sudo ufw allow 8080/tcp`.

Al arrancar muestra la dirección que hay que poner en la simulación, por ejemplo
`ws://192.168.1.20:8765`: agréguela a `estaciones` en `StreamingAssets/enlace/enlace.json` o lance
`SaludAstronauta.exe -estacion ws://192.168.1.20:8765`.

También hay atajos: `iniciar_estacion.bat` (Windows) e `iniciar_estacion.sh` (Linux) crean el entorno la
primera vez y arrancan la estación.

### Probar sin la simulación
En otra terminal:
```bash
.venv\Scripts\python emisor_prueba.py --ber 1.5e-3
```
```bash
.venv\Scripts\python emisor_prueba.py --clave-falsa
```
(en Linux use `.venv/bin/python`). La segunda debe mostrar `trama RECHAZADA` en la estación.

## Qué verás
```text
● Nave conectada desde 192.168.1.30:52520
■ INFORME (cambio de fase: Superficie de Marte) Día 227 · Superficie de Marte · PRECAUCIÓN: Visión (SANS) en toda la tripulación; …
  → informes/informe_dia_0227.md
✗ trama descartada por ruido del canal (CRC no coincide: errores que el código no pudo corregir)
[19:50:12] día 230 · Superficie de Marte · válidas 120/123 · bits corregidos 412 · descartadas 3 · perdidas 0 · picos filtrados 2 · calidad 98 %
```

Seguridad: no publique `clave_nasa.key` (está en `.gitignore`). Sin esa clave nadie puede leer ni falsificar los datos.
