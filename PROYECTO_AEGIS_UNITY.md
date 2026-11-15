# AEGIS · Escena 3D en Unity + ASTRA (asistente médica con IA local)

> **Para la IA que lea esto:** este documento es el contexto completo del proyecto. Léelo entero antes de
> proponer cambios. Responde y escribe la interfaz **en español**. Respeta las reglas de la sección
> [Reglas del proyecto](#reglas-del-proyecto-no-romper). Cuando no estés seguro de algo, revisa el código
> citado antes de suponer.

---

## 1. Qué es

Proyecto para **NASA Space Apps**, reto *Create Health Monitoring Software for Astronauts on Space Missions*.
Equipo **AEGIS**. Es una **simulación educativa** (no un sistema médico) que muestra cómo cambia la salud de una
tripulación ficticia de 4 astronautas durante una misión espacial (ISS, Luna, Marte): pérdida ósea y muscular
por microgravedad, radiación cósmica y tormentas solares, visión (SANS), sueño, estrés, CO₂ de la cabina, etc.

Esta parte del equipo es la **PC 3: escena 3D en Unity**. Tiene dos modos:

| Modo | Qué hace |
| --- | --- |
| **Local** (por defecto) | Simulación propia completa (modelo fisiológico, sensores, alertas, contramedidas, ASTRA). Funciona sola, sin red. |
| **Bus AEGIS** | Se conecta al motor del equipo (PC 1, Python FastAPI) por WebSocket y solo **representa** en 3D el mismo día, escenario y tripulación que el dashboard web (PC 2, React). |

```text
NASA DONKI + OSDR + escenarios
            ↓
PC 1 · Motor y bus (Python FastAPI)  ws://IP_DE_PC1:8001/ws  (subprotocolo aegis.v1)
            ↓ mismo estado, mismo día
     ┌──────┴──────┐
PC 2 · Web      PC 3 · Unity  ← ESTE PROYECTO
React + TS      Unity 6 + C#
```

Lo más llamativo de la demo es **ASTRA**: una asistente médica con rostro holográfico realista que
diagnostica a la tripulación con los datos de la simulación, explica causas y efectos para el público, habla
con voz natural y **conversa por micrófono en tiempo real**. Todo corre **en la PC, sin internet y sin APIs de
pago**.

---

## 2. Stack

| Herramienta | Versión | Uso |
| --- | --- | --- |
| Unity | **6000.6.9f1** (Unity 6), pipeline *Built-in* | Simulación, escena 3D, interfaz (IMGUI) y app de Windows |
| C# | .NET Standard 2.1 de Unity | Todo el código |
| Blender | 4.5 LTS (vale 4.2+) | Detalle de la nave/interior, preparar modelos del equipo (scripts `bpy`) |
| FreeCAD | 1.1 | Nave y cohete paramétricos + planos PDF (macro Python) |
| Ollama | local, `http://127.0.0.1:11434` | LLM de ASTRA: `qwen2.5:3b` y modelo propio `astra` |
| Piper TTS | 2023.11.14-2 | Voz neuronal local (`es_AR-daniela-high`) |
| whisper.cpp | build b5454 (CPU + OpenBLAS) | Voz a texto local, modelo `ggml-small-q5_1` |
| Python | (opcional) | `herramientas/bus_aegis_simulado.py` para probar el modo bus |

Plataforma objetivo: **Windows 10/11 x64**. PC de desarrollo: i5-12450H, 16 GB RAM, RTX 3050 4 GB.

---

## 3. Estructura de carpetas

```text
Proyecto Space Apps/
├─ INICIAR_DEMO.bat            Lanzador (menú 1–5). Enciende Ollama si está apagado.
├─ LEEME.md                    Guía para personas (cómo usar todo)
├─ ESTADO-EQUIPO.md            Estado del equipo AEGIS completo (web + motor + Unity)
├─ LICENCIAS_TERCEROS.md       Licencias de avatar, Piper, voz, Whisper, Ollama, Qwen
├─ PROYECTO_AEGIS_UNITY.md     ESTE archivo (contexto para IA)
├─ HACKATON.docx               Documento del reto/idea (variables, modelos, sensores)
├─ App/SaludAstronauta/        App compilada (SaludAstronauta.exe, ~519 MB)
├─ IA/                         ASTRA: conocimiento, ejemplos, memoria y "entrenamiento"
│  ├─ conocimiento_astra.md    Resumen médico que se inyecta en el modelo astra
│  ├─ ejemplos_base.jsonl      Ejemplos pregunta→respuesta (few-shot)
│  ├─ memoria_astra.jsonl      Conversaciones y valoraciones (se llena al usar la app)
│  ├─ entrenar_astra.ps1       Crea el modelo Ollama "astra" (Modelfile)
│  └─ Modelfile                Generado por el script anterior
├─ simulacion_unity/           PROYECTO UNITY
│  ├─ CONEXION_AEGIS.md        Protocolo del bus y mapeo de campos
│  └─ Assets/
│     ├─ Scenes/SpaceApps.unity
│     ├─ StreamingAssets/
│     │  ├─ astra.json         Configuración de ASTRA (ver §7)
│     │  ├─ piper/             piper.exe + voz es_AR-daniela-high.onnx (~146 MB)
│     │  └─ whisper/           whisper-server.exe + ggml-small-q5_1.bin (~244 MB)
│     └─ SpaceApps/
│        ├─ Scripts/  (ver §5)
│        ├─ Shaders/  Holograma.shader, HoloAvatar.shader
│        ├─ Editor/   Menús, importadores, build, pruebas
│        └─ Resources/
│           ├─ Models/   FBX/OBJ de Blender y FreeCAD (nave, cohete, astronauta, anatomía…)
│           └─ Astra/    Avatar Rocketbox Medical_Female_02 (FBX + texturas)
├─ blender/                    Scripts bpy (detallar nave, interior, importar modelos del equipo)
├─ freecad/nave_spaceapps.py   Macro paramétrica (nave, cohete, planos)
├─ export/                     Salidas de FreeCAD (STEP, STL, OBJ, FCStd)
├─ herramientas/bus_aegis_simulado.py   Bus AEGIS falso para probar sin la PC 1
├─ Modelo 3D Cuerpo/           Modelos originales del equipo (traje, anatomía, hábitat)
└─ simulacion_godot/           Versión vieja en Godot (YA NO SE USA)
```

---

## 4. Cómo ejecutar y compilar

### Ver la demo
Doble clic en `INICIAR_DEMO.bat`:
1. Simulación local · 2. Marte desde el día 200 · 3. Bus AEGIS simulado · 4. Motor real del equipo (pide IP)
· **5. Marte + conversación por voz con ASTRA (manos libres)**.

### Opciones de arranque de `SaludAstronauta.exe`
| Opción | Efecto |
| --- | --- |
| `-mision N` | Misión 0..4 (4 = Marte ida y vuelta) |
| `-dia N` | Adelanta la misión N días |
| `-interior` / `-play` | Vista interior de la nave / empieza corriendo |
| `-astronauta N` / `-pestana N` | Tripulante seleccionado / pestaña del panel derecho |
| `-sinayuda` | No muestra la ayuda inicial |
| `-aegis ws://IP:8001/ws` | Modo bus AEGIS |
| `-preguntar "texto"` | Le hace esa pregunta a ASTRA al arrancar |
| `-oir audio.wav` | Le "habla" a ASTRA con un audio (prueba de Whisper) |
| `-conversar` | Arranca con el micrófono en manos libres |
| `-capturar ruta.png -esperar s` | Guarda una captura tras `s` segundos y cierra (pruebas automáticas) |

### Abrir en Unity
Unity Hub → Add → `simulacion_unity` → abrir `Assets/Scenes/SpaceApps.unity` → Play.
Menú **Space Apps**: Crear escena principal · Probar simulación · Revisar modelos del equipo ·
**Probar ASTRA (IA local)** · **Compilar app de Windows**.

### Compilar sin abrir el editor (así se ha trabajado)
```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath simulacion_unity -executeMethod SpaceApps.EditorTools.BuildApp.Windows -logFile build.log
```
Éxito = línea `[SpaceApps][Build] Succeeded · 0 errores` en el log. Errores de C# = líneas `error CS`.
Prueba de ASTRA sin interfaz: `-executeMethod SpaceApps.EditorTools.AstraTest.Run`.
Los logs de la app compilada quedan en `%USERPROFILE%\AppData\LocalLow\Space Apps\Salud del Astronauta\Player.log`
(buscar `[ASTRA]` y `[SpaceApps]`).

### Requisitos de ASTRA en otra PC
1. Instalar **Ollama** (https://ollama.com) y ejecutar `ollama pull qwen2.5:3b`.
2. `powershell -ExecutionPolicy Bypass -File IA\entrenar_astra.ps1` (crea el modelo `astra`).
3. Piper, la voz y Whisper ya vienen dentro de `StreamingAssets` (no hace falta instalar nada más).
4. Sin Ollama, ASTRA responde igual con su motor médico propio (más rígido, pero correcto).

---

## 5. Arquitectura del código (`Assets/SpaceApps/Scripts`)

Namespace raíz `SpaceApps` (sub-namespaces `SpaceApps.Assistant`, `SpaceApps.Bus`, `SpaceApps.EditorTools`).

### Orquestador
- **`SpaceAppsSimulation.cs`** — MonoBehaviour principal: crea la escena, cámara, tripulación, UI, `BodyMap`,
  `MedAssistant`; avanza la simulación (paso de 1 hora) a la velocidad elegida; modo local/bus; opciones de
  arranque; capturas. Expone `Sim`, `Selected`, `SpeFlash` (destello de tormenta solar).

### Health — modelo fisiológico (modo local)
| Archivo | Contenido |
| --- | --- |
| `MissionData.cs` | Misiones, fases (tránsito, superficie, retorno), gravedad y radiación por fase |
| `AstronautHealth.cs` | Estado por astronauta con **EDO** de paso horario: hueso, músculo, VO₂ máx, plasma, SANS, dosis, sueño, estrés, signos vitales y química (glucosa, lactato, cortisol, pH, potasio, calcio urinario…) |
| `CabinEnvironment.cs` | **Difusión-advección 1D** de CO₂ y polvo en 12 módulos con ventilación; O₂, ruido, humedad |
| `SensorSuite.cs` | Lecturas simuladas con ruido (ECG, PPG, NDIR, láser PM, EMG, EEG, GSR…) |
| `EarlyWarning.cs` | Umbrales **dinámicos EWMA** (z > 3,5) + umbrales clínicos fijos; **regresión lineal** para pronosticar cruces |
| `Advisor.cs` | Ranking de problemas (`Problem`: sistema, tripulante, severidad 0..1) y **contramedidas calculadas** con su beneficio; piloto automático (`AutoApply`) |
| `Simulation.cs` | Une todo: tripulación, cabina, misión, día/hora, tormentas solares, `StepHour()` |

Severidad: **≥ 0,65 = CRÍTICO**, ≥ 0,35 = PRECAUCIÓN, menor = NOMINAL. Antes de cada misión hay
acondicionamiento mínimo de 14 días (los astronautas parten en buena forma).

### World — escena 3D
`ScenarioBuilder` (Tierra con cohete, órbita con estación, espacio profundo, Luna, Marte), `InteriorBuilder`
(interior de la nave con pantallas de CO₂), `ShipBuilder`/`MeshGen` (geometría procedural de respaldo),
`ModelLibrary` (carga `Resources/Models/<nombre>`; si no existe usa lo procedural), `StarField`, `Mats`.

### Astronaut
`CrewAvatar` (modelo del equipo `astronauta_traje` con brazos/piernas orientados por código, anillo de estado y
luz del pecho al ritmo cardíaco), `AstronautBody` (cuerpo procedural de respaldo), `BodyMap` (modelo anatómico
coloreado por zonas en una RenderTexture, capa 30).

### UI (IMGUI, `partial class SimulationUI`)
`SimulationUI.cs` (barra superior, ayuda, estilos), `.General.cs` (vista general: tripulación, alertas, ASTRA,
mapa corporal, contramedidas), `.Pro.cs` (vista profesional con todos los datos), `.Bus.cs` (modo AEGIS),
`.Assistant.cs` (panel de ASTRA), `VitalMonitor` (ECG/respiración animados), `Icons` (íconos SDF procedurales:
hueso que se agrieta, músculo, corazón, pulmones…), `ChartTexture`, `OrbitCamera` (cámara + entrada con
guardas `ENABLE_INPUT_SYSTEM` / `ENABLE_LEGACY_INPUT_MANAGER`).

### Bus — conexión AEGIS
`AegisBusClient` (System.Net.WebSockets, subprotocolo `aegis.v1`), `AegisLink` (mapea el paquete del bus a la
escena), `MiniJson` (parser JSON propio: `Parse`, `Path("a.b")`, `Text`, `Number`). Detalle en
`simulacion_unity/CONEXION_AEGIS.md`.

### Assistant — ASTRA
| Archivo | Responsabilidad |
| --- | --- |
| `MedAssistant.cs` | Cerebro: arma la pregunta, llama a Ollama en segundo plano, decide emoción, habla, guarda memoria, avisos automáticos (tormenta solar, problemas críticos agrupados por sistema), saludo, valoración ¿Te sirvió? |
| `MedicalKnowledge.cs` | **Motor médico propio**: base de 10 condiciones (qué es, causas, efectos, impacto en la misión, acciones, explicación simple), `Snapshot()` (estado de la simulación en texto para el LLM), `Answer()` (respuesta por intenciones sin LLM), `Announce()`, `Triage()` |
| `OllamaClient.cs` | HTTP a Ollama (`/api/tags`, `/api/chat`, `stream:false`, `keep_alive 30m`), lista de modelos segura entre hilos |
| `SpeechVoice.cs` | Voz: proceso **Piper** persistente (`--json-input`), divide en oraciones y reproduce la primera mientras genera las demás; recorta silencios y normaliza volumen; respaldo **Windows SAPI**; `Level` mueve la boca; `Spoken()` convierte siglas/unidades a palabras |
| `VoiceListener.cs` | Oído: micrófono a 16 kHz, **VAD** por energía con piso de ruido adaptativo, manos libres / presionar para hablar (botón HABLAR o F2), **interrupción** estimando el eco de los parlantes, `whisper-server` local con vocabulario de la tripulación como `prompt`, filtro de alucinaciones de Whisper, palabra clave opcional "ASTRA" |
| `AvatarFace.cs` | Rostro realista: avatar Rocketbox con 176 blendshapes (ARKit `AK_*`, visemas `AA_VI_*`), expresiones por emoción, boca sincronizada, parpadeo, mirada, movimiento de cabeza, tinte por emoción; RenderTexture en capa 29 |
| `HoloFace.cs` | Rostro alternativo de líneas (wireframe procedural), usado si falta el avatar o `"rostro": "lineas"` |
| `IAstraFace` (en `AvatarFace.cs`) | Interfaz común de los dos rostros |

Emociones (`enum Emotion`): `Neutral, Amable, Feliz, Preocupada, Firme, Alerta, Pensando`.

### Flujo de una pregunta a ASTRA
```text
Micrófono → VAD → WAV 16 kHz → whisper-server (≈2 s) → texto
  (o texto escrito / botones rápidos)
        ↓
MedAssistant.Ask()
  ├─ MedicalKnowledge.Snapshot()  → estado real de la simulación
  ├─ MedicalKnowledge.Answer()    → "HECHOS VERIFICADOS" (motor propio)
  └─ Ollama (modelo astra, ≈2–3 s): persona + estado + hechos + ejemplos + historial
        ↓  [ETIQUETA_EMOCIÓN] respuesta ≤ 4 oraciones, sin markdown
  emoción final = la más grave entre LLM y motor propio
        ↓
SpeechVoice (Piper, primera oración en ≈1 s) + AvatarFace (boca, expresión)
        ↓
IA/memoria_astra.jsonl (conversación; "¿Te sirvió? SÍ" la vuelve ejemplo)
```
**Clave de diseño:** el LLM de 3B **no inventa** el diagnóstico: el motor médico calcula los hechos desde las
ecuaciones de la simulación y el LLM solo los redacta de forma amigable. Si Ollama falla, se usa el motor propio.

### Editor
`SpaceAppsSetup.cs` (menús, crear escena, importadores: `SpaceAppsModelImporter` para `Resources/Models`,
`AstraAvatarImporter` para `Resources/Astra` con blendshapes y texturas a 1024), `BuildApp.cs` (build de
Windows; incluye shaders por nombre: si creas un shader nuevo que se busca con `Shader.Find`, **agrégalo a la
lista de `BuildApp`**), `AstraTest.cs`, `AegisBusTest.cs`, `ModelCheck.cs`.

---

## 6. Interfaz (qué ve el público)

- **Barra superior:** misión, iniciar/pausar, velocidad (2/8/30 días por segundo), vistas GENERAL / PROFESIONAL,
  INTERIOR, AEGIS, ayuda.
- **Izquierda:** tarjetas de tripulantes (índice de salud /100 + estado), alertas prioritarias (más grave
  primero), **panel ASTRA** (rostro, respuesta, botones Diagnóstico / ¿Qué hacer? / Explicar / Misión, cuadro de
  texto, ENVIAR, HABLAR, CONVERSAR, VOZ, valoración).
- **Centro:** escena 3D, línea de tiempo de fases, monitor de signos vitales (ECG, respiración, FC, SpO₂, PANI,
  FR, temperatura).
- **Derecha:** tripulante seleccionado, mapa corporal anatómico, sistemas fisiológicos con estado, contramedidas
  recomendadas (piloto automático).
- **Estilo:** paleta grafito profesional, acento azul acero `(0.27, 0.52, 0.78)`, estados sobrios
  NOMINAL / PRECAUCIÓN / CRÍTICO. El equipo pidió explícitamente **nada infantil ni "de juego"**.

---

## 7. Configuración de ASTRA (`StreamingAssets/astra.json`)

```json
{
  "ollama_url": "http://127.0.0.1:11434",
  "modelos": ["astra", "qwen2.5:3b", "llama3.2:3b", "qwen2.5:7b"],
  "temperatura": 0.3,
  "max_tokens": 220,
  "voz": true,
  "voz_motor": "piper",          // o "windows"
  "voz_velocidad": 0.9,          // length_scale de Piper: menor = más rápida
  "voz_volumen": 1.0,
  "rostro": "realista",          // o "lineas"
  "mic_manos_libres": false,
  "mic_interrumpir": true,
  "mic_palabra_clave": false,    // true = en manos libres solo responde si oye "ASTRA"
  "mic_sensibilidad": 1.0,
  "avisos_automaticos": true
}
```
(El archivo real no lleva comentarios; JSON estricto.) En la app compilada está en
`App/SaludAstronauta/SaludAstronauta_Data/StreamingAssets/astra.json` y se puede editar sin recompilar.
Personalidad opcional en `StreamingAssets/astra_persona.txt` (reemplaza `MedAssistant.DefaultPersona`).

### "Entrenamiento" de ASTRA
No se cambian pesos del modelo: `IA/entrenar_astra.ps1` genera un `Modelfile` con
`FROM qwen2.5:3b` + `SYSTEM` (persona + `conocimiento_astra.md`) + `MESSAGE` (ejemplos base + respuestas marcadas
como útiles en `memoria_astra.jsonl`) y ejecuta `ollama create astra`. Formato de cada línea JSONL:
`{"pregunta": "...", "respuesta": "[AMABLE] ...", "util": true}`.

---

## 8. Reglas del proyecto (no romper)

1. **Todo local.** ASTRA usa Ollama, Piper y whisper.cpp en la PC. **No** agregar APIs en la nube ni claves.
2. **Modo bus AEGIS:** Unity solo representa lo que manda la PC 1. **No inventar diagnósticos, tratamientos ni
   umbrales clínicos sin fuente** en ese modo (regla del equipo, ver `ESTADO-EQUIPO.md`). El bus es
   `ws://IP_DE_PC1:8001/ws` con subprotocolo `aegis.v1`.
3. **Simulación educativa:** ASTRA debe aclarar cuando corresponda que no reemplaza a un médico real.
4. **No usar el logo de la NASA** (uso restringido); solo colores inspirados.
5. **Interfaz en español**, textos claros para público general, estilo profesional sobrio.
6. **No bloquear el hilo principal de Unity:** llamadas HTTP/procesos en `Task.Run` con `ConfigureAwait(false)` y
   resultados devueltos al hilo principal por una cola (`ConcurrentQueue<Action>`). Nunca `.Result` /
   `.GetAwaiter().GetResult()` en el hilo de Unity (ya causó un bloqueo).
7. **Codificación de archivos:** C#/JSON/MD en UTF-8 **sin BOM**; scripts `.ps1` en UTF-8 **con BOM**
   (PowerShell 5 lee mal los acentos sin BOM).
8. **Entrada de teclado/ratón** siempre con las dos guardas (`ENABLE_INPUT_SYSTEM` y legacy), como en `OrbitCamera`.
9. Cada cambio se valida **compilando** (comando del §4) y, si toca la UI, con una **captura** (`-capturar`).
10. Licencias: la voz Daniela es **CC BY-SA 4.0** (citarla en la presentación); Qwen2.5-3B es licencia de
    investigación (no comercial). Ver `LICENCIAS_TERCEROS.md`.

---

## 9. Estado actual (verificado)

- [x] Simulación local completa (5 misiones, 6 escenarios 3D + interior), alertas, contramedidas automáticas.
- [x] Modelos del equipo integrados (traje, anatomía, hábitat); nave y cohete de FreeCAD detallados en Blender.
- [x] Modo bus AEGIS + bus simulado para pruebas.
- [x] App de Windows compilada (0 errores) y lanzador.
- [x] ASTRA: Ollama `astra` responde en ~2–3 s con datos de la simulación; voz Piper empieza en ~1–2 s;
      rostro realista con expresiones y boca sincronizada.
- [x] Conversación por micrófono: Whisper transcribe en ~2 s; probado con audio y con micrófono real en manos libres.

## 9b. Enlace espacio–Tierra (telemetría cifrada)

- Unity: `Scripts/Enlace/` → `EnlaceTelemetria` (transmisor WebSocket cliente, almacenar y reenviar), `EnlaceProtocolo`
  (deflate → AES-256-CBC → HMAC-SHA256 → CRC-32 → Hamming(7,4) + entrelazado 16), `EnlaceCanal` (distancia, ángulo Sol-Tierra-nave,
  Eb/N0, BER BPSK, ráfagas, pérdidas). Config: `StreamingAssets/enlace/enlace.json` + `clave_nasa.key`. Opciones `-estacion ws://IP:puerto` y `-sinenlace`.
- Estaciones: `receptor_python/` (puerto 8765) y `receptor_rust/` (puerto 8766), mismo protocolo e informes (`informes/*.md` + `telemetria.csv`).
- Especificación completa: `ENLACE_ESPACIO_TIERRA.md`. Si cambias el protocolo, cámbialo **en los tres** (C#, Python, Rust).

## 10. Pendiente / ideas

- [ ] Probar la demo completa en las 3 PC físicas con el bus real (IP/puerto de la PC 1).
- [ ] Probar la interrupción por voz con parlantes de laptop (si se interrumpe sola: `"mic_interrumpir": false` o auriculares).
- [ ] El modelo de 3B a veces agrega consejos de más (p. ej. "depurador de CO₂" al hablar de radiación):
      mejorar `conocimiento_astra.md`, añadir ejemplos buenos o probar `qwen2.5:7b`.
- [ ] Opcional: Whisper con GPU (build CUDA) para transcribir en ~0,5 s.
- [ ] Opcional: en modo bus, que ASTRA solo explique alertas con fuente (sin diagnosticar), según la regla del equipo.
- [ ] Opcional: visemas reales a partir de fonemas (ahora la boca sigue el volumen de la voz).

## 11. Problemas ya resueltos (no repetir)

- `GetAwaiter().GetResult()` en el hilo de Unity con HttpClient → **bloqueo**. Usar `Task.Run` + `ConfigureAwait(false)`.
- Dos consultas simultáneas a Ollama modificaban la misma `List` → lista corrupta. Ahora se reemplaza la lista entera.
- Voz "borracha": bajar el tono/velocidad por SSML sonaba arrastrado. Ahora Piper con `length_scale 0.9`.
- Whisper confunde términos propios sin contexto → se le pasa un `prompt` con la tripulación y temas.
- Modelos FBX: usar `bakeAxisConversion`; el traje del equipo se importa como *Legacy* y la pose se corrige por código.
- `Get-Content`/`Set-Content` de PowerShell 5 rompen los acentos: usar `[IO.File]::ReadAllText/WriteAllText` con UTF8.

## 12. Cómo pedirle trabajo a una IA (ejemplos)

- "Lee `PROYECTO_AEGIS_UNITY.md` y `Scripts/Assistant/MedAssistant.cs`. Agrega una pregunta rápida 'Tormenta solar'
  que explique el riesgo actual usando `MedicalKnowledge`. Compila con el comando del §4 y muéstrame los errores."
- "En modo bus AEGIS, haz que ASTRA solo resuma las alertas recibidas del bus sin diagnosticar (regla §8.2)."
- "Mejora `IA/conocimiento_astra.md` y `IA/ejemplos_base.jsonl` para que ASTRA no mezcle radiación con CO₂; luego
  ejecuta `IA\entrenar_astra.ps1`."
