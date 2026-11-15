# Enlace espacio–Tierra AEGIS · telemetría cifrada por WebSocket

La simulación (la **nave**) transmite cada segundo el estado de la misión a una o varias **estaciones terrenas**
en otras computadoras. Solo quien tiene la clave de la misión ("la NASA") puede leer los datos. Cada estación
corrige el ruido del espacio, verifica la autenticidad, descifra, filtra lecturas falsas de los sensores y escribe
**informes claros** de lo que está pasando.

```text
 NAVE (Unity, SaludAstronauta.exe)                                   ESTACIONES TERRENAS
 ┌──────────────────────────────┐   WebSocket binario      ┌─────────────────────────────────────┐
 │ estado de la misión (JSON)   │   ws://IP:8765 ────────► │ receptor_python/  (Python 3.10+)    │
 │ → comprimir (deflate)        │                          │ receptor_rust/    (Rust)  ws://:8766│
 │ → cifrar  AES-256-CBC        │   canal con ruido:       │                                     │
 │ → firmar  HMAC-SHA256        │   bits invertidos,       │ 1. corregir errores (Hamming)       │
 │ → CRC-32                     │   ráfagas, tramas        │ 2. CRC-32 → descartar lo irreparable│
 │ → Hamming(7,4) + entrelazado │   perdidas, retraso      │ 3. HMAC → rechazar intrusos         │
 │ → modelo del canal espacial  │   de la luz              │ 4. descifrar y descomprimir         │
 └──────────────────────────────┘                          │ 5. filtro de picos (Hampel)         │
                                                           │ 6. tendencias → INFORME .md + CSV   │
                                                           └─────────────────────────────────────┘
```

Se usa **solo WebSocket** (no MQTT): un canal persistente, bidireccional y simple, que funciona en cualquier red
local o en Internet y tiene librerías maduras en C#, Python y Rust.

---

## 1. ¿Por qué hace falta todo esto? (justificación)

Una nave en Marte está a **55–400 millones de km**. La señal tarda **3 a 22 minutos** en llegar y viaja por un medio
lleno de interferencias. Lo que modela la simulación:

| Fenómeno real | Efecto en los datos | Cómo lo simulamos | Cómo lo resuelve la estación |
| --- | --- | --- | --- |
| **Pérdida por distancia** (la potencia cae con el cuadrado de la distancia) + ruido térmico de los receptores y ruido galáctico | Bits invertidos al azar | Eb/N0 baja 20·log10(d); BER de una modulación BPSK = ½·erfc(√Eb/N0) | Código corrector **Hamming(7,4)**: repara 1 bit por cada 7 |
| **Ionosfera y cinturones de Van Allen** (cerca de la Tierra) | Ráfagas de errores | Ráfagas de 8–48 bytes en órbita baja y al salir de la Tierra | **Entrelazado**: reparte la ráfaga entre muchas palabras para que cada una tenga 1 solo error |
| **Conjunción solar** (el Sol entre la Tierra y Marte) y **centelleo del viento solar** | Señal muy débil, apagón de comunicaciones | Penalización cuando el Sol está a menos de 10° de la línea de visión; apagón por debajo de 2° | **CRC-32** detecta lo irreparable y se descarta; la nave **almacena y reenvía** cuando vuelve la conexión |
| **Tormentas solares** (partículas energéticas) | Más ruido + lecturas absurdas en sensores (SEU, *single event upset*) | −3 dB de señal, más ráfagas y picos falsos en signos vitales | **Filtro de Hampel** + límites de plausibilidad del sensor |
| **Retraso de la luz** | Los datos son del pasado (minutos) | Se calcula y se informa; opcionalmente se espera (`retraso_luz_escala`) | Cada informe indica la distancia y el retraso |
| **Espionaje o suplantación** | Alguien lee o falsifica la telemetría | — | **AES-256** (confidencialidad) + **HMAC-SHA256** (autenticidad e integridad) + número de secuencia (anti-repetición) |

Las misiones reales usan los estándares **CCSDS** (Consultative Committee for Space Data Systems): códigos
Reed-Solomon, convolucionales, turbo o LDPC, y seguridad **SDLS** con AES-GCM. Este prototipo usa versiones
más simples con la **misma idea**, para que se entienda y se pueda implementar igual en C#, Python y Rust.

---

## 2. Protocolo (versión 1)

### Trama interna (todos los enteros en big-endian)

| Bytes | Campo | Descripción |
| ---: | --- | --- |
| 4 | `AEGS` | identificador |
| 1 | versión | `1` |
| 1 | flags | bit 0 = contenido comprimido con deflate |
| 4 | sesión | número aleatorio por cada arranque de la nave |
| 4 | secuencia | contador de tramas (detecta pérdidas y repeticiones) |
| 8 | t_ms | hora UTC de envío (milisegundos Unix) |
| 16 | IV | vector inicial aleatorio de AES |
| 4 | largo | bytes del contenido cifrado |
| largo | cifrado | AES-256-CBC (PKCS#7) de deflate(JSON UTF-8) |
| 32 | HMAC | HMAC-SHA256 de todo lo anterior |
| 4 | CRC-32 | CRC-32 IEEE (igual a `zlib.crc32`) de todo lo anterior |

### Claves
Un único archivo `clave_nasa.key` con **64 caracteres hexadecimales (256 bits)**. Se derivan dos claves:
`k_cif = HMAC-SHA256(maestra, "AEGIS-ENC-v1")` y `k_mac = HMAC-SHA256(maestra, "AEGIS-MAC-v1")`
(se cifra y luego se firma: *encrypt-then-MAC*).

### Bloque en el aire (corrección de errores)
1. `datos = largo_trama (4 bytes) + trama`
2. Cada byte → 2 palabras **Hamming(7,4)** (una por nibble), cada palabra en un byte (bits 0..6 = p1 p2 d1 p3 d2 d3 d4).
3. Se rellena con ceros hasta un múltiplo de 16 y se **entrelaza** con profundidad 16:
   `salida[c·filas + r] = palabra[r·16 + c]` (se escribe por filas y se lee por columnas).

El bloque ocupa el doble que la trama: es el precio de poder reparar errores sin pedir retransmisión (con 3 a 44
minutos de ida y vuelta, pedir "repíteme" a Marte es muy lento).

### Contenido (JSON `aegis.telemetria.v1`)
```json
{
  "esquema": "aegis.telemetria.v1", "nave": "AEGIS-1", "mision": "Marte · ida y vuelta",
  "dia": 480, "hora": 13, "dia_total": 1114, "fase": "Superficie de Marte", "fase_clave": "mars_surface",
  "gravedad_g": 0.38, "radiacion_msv_dia": 0.64, "limite_dosis_msv": 600,
  "tormentas_solares": 2, "tormenta_activa": false, "mision_terminada": false,
  "cabina": { "co2_mmhg": 2.0, "co2_max_mmhg": 2.6, "pm25": 5, "o2_pct": 21, "temp_c": 22.5, "humedad_pct": 45, "ruido_db": 55 },
  "contramedidas": { "ejercicio": 1, "blindaje": 1, "refugio": true, "depurador": 1, "apoyo": 1, "luz": 1, "gravedad_artificial_g": 0 },
  "tripulacion": [ { "nombre": "CMD Alfa", "rol": "Comandante", "edad": 45, "indice_salud": 57,
      "hueso_pct": 94.8, "musculo_pct": 88.3, "vo2_pct": 91.9, "sans": 0.76, "dosis_msv": 428, "riesgo_cancer_pct": 2.1,
      "sueno_h": 6.9, "deuda_sueno_h": 3.1, "estres": 46, "calcio_orina_mg": 235, "en_eva": false,
      "vitales": { "fc": 62, "pas": 115, "pad": 74, "spo2": 97, "temp_c": 37.2, "fr": 13 } } ],
  "alertas": [ { "sistema": "Visión (SANS)", "nivel": "CRITICO", "severidad": 0.76,
      "tripulantes": ["CMD Alfa", "PIL Bravo"], "detalle": "Índice SANS 0,76" } ],
  "enlace": { "via": "DSN (Red de Espacio Profundo)", "distancia_km": 2.26e8, "distancia_ua": 1.51,
      "retraso_luz_s": 754, "angulo_sol_grados": 38, "ebn0_db": 10.9, "ber": 2.1e-6,
      "fuentes_ruido": ["pérdida por distancia y ruido térmico/galáctico"] }
}
```

---

## 3. Cómo usarlo

### En la computadora de la nave (simulación)
Configuración en `simulacion_unity/Assets/StreamingAssets/enlace/enlace.json` (en la app compilada:
`App/SaludAstronauta/SaludAstronauta_Data/StreamingAssets/enlace/enlace.json`, se edita sin recompilar):

```json
{ "activo": true, "estaciones": ["ws://192.168.1.20:8765", "ws://192.168.1.21:8766"],
  "intervalo_s": 1.0, "clave": "clave_nasa.key", "ruido_cosmico": true, "picos_seu": true, "retraso_luz_escala": 0.0 }
```
También se puede indicar al lanzar: `SaludAstronauta.exe -estacion ws://192.168.1.20:8765` (se puede repetir)
o desactivar con `-sinenlace`. Arriba del cronograma aparece **● ENLACE 2/2 · distancia · minutos luz · BER**
(verde = todas las estaciones conectadas).

### En las computadoras de Tierra
- Python: ver [receptor_python/README.md](receptor_python/README.md) (puerto 8765 por defecto).
- Rust: ver [receptor_rust/README.md](receptor_rust/README.md) (puerto 8766 por defecto).

Las dos estaciones deben tener **el mismo `clave_nasa.key`** que la nave. Para una clave nueva:
`python generar_clave.py` (o `receptor --generar-clave`) y copiarla a la nave
(`StreamingAssets/enlace/clave_nasa.key`) y a cada estación autorizada por un medio seguro (USB).

### Panel de control web (estación Python)
`http://localhost:8080` (o `http://IP:8080` desde otro equipo): estado en vivo, tripulación, gráficos de tendencia,
alertas, enlace, picos filtrados y lista de informes con **PDF**. El botón **Generar informe PDF** crea uno al instante.
La opción 6 de `INICIAR_DEMO.bat` lo abre automáticamente.

### Qué produce cada estación (carpeta `informes/`)
- `informe_dia_XXXX.md` y `ultimo_informe.md`: informe al **primer contacto**, en cada **tormenta solar**,
  **cambio de fase**, **nueva alerta crítica**, **fin de la misión**, cada N días (`--cada-dias`) y al cerrar.
- `telemetria.csv`: cada trama válida, por tripulante, con los signos vitales ya filtrados.

### Red y firewall
La primera vez Windows pregunta si permite la conexión: aceptar en **redes privadas**. En Linux:
`sudo ufw allow 8765/tcp` (y `8766/tcp`). Las computadoras deben estar en la misma red (o con el puerto abierto).

---

## 4. Verificado

- Nave (C#) → estación Python y estación Rust a la vez: mismas tramas descifradas, informes generados por eventos
  (primer contacto, tormentas, cambio de fase, nuevas alertas críticas).
- Corrección de miles de bits por Hamming; tramas irreparables descartadas por CRC.
- Emisor con **clave falsa**: 5 de 5 tramas **rechazadas** por las dos estaciones.
- Picos de sensores (FC 185, SpO₂ 61 %, 41,8 °C) eliminados por el filtro.

_Simulación educativa para NASA Space Apps (equipo AEGIS). No es un sistema médico ni de comunicaciones real._
