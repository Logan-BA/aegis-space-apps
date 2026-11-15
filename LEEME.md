# Proyecto Space Apps — Salud de astronautas

Simulación 3D de cómo el entorno espacial (gravedad, radiación, aire de la cabina, ruido, aislamiento) afecta la
salud de una tripulación, con detección temprana de anomalías y sensores simulados.

**Stack:** FreeCAD 1.1 (ingeniería y planos) → Blender 4.2+ (detalle y materiales) → Unity 6 (simulación y escena).

Informe científico (variables ↔ salud, ecuaciones, fuentes): https://claude.ai/artifact/36nrq9NmzyuQJSUj1TMZfR

## Flujo de trabajo

```
freecad/nave_spaceapps.py ──► export/  (STEP, STL, OBJ, FCStd, planos PDF)
        │                      └─► blender/entrada/*.obj  y  Unity Resources/Models/freecad/*.obj
        ▼
blender/detallar_nave.py ────► Unity Resources/Models/nave_transito.fbx, cohete_lanzador.fbx
blender/crear_interior_nave.py ► Unity Resources/Models/interior_nave.fbx
        ▼
simulacion_unity/  ──────────► carga el FBX de Blender; si no existe, el OBJ de FreeCAD;
                               si tampoco, la geometría procedural (mismas medidas)
```

Cada paso es opcional: la simulación de Unity funciona sola, y mejora cuando existen los modelos.

## Ver la simulación (sin abrir Unity)

Doble clic en **`INICIAR_DEMO.bat`** y elegir una opción:

1. Simulación local (funciona sola, sin red). Al abrir aparece una ayuda que explica la pantalla.
2. Misión a Marte ya en marcha desde el día 200.
3. Conectada a un bus AEGIS simulado (prueba de la conexión con el equipo).
4. Conectada al motor real del equipo (pide la IP de la PC 1).

La aplicación está en `App/SaludAstronauta/SaludAstronauta.exe` (Windows, 519 MB con la voz neuronal y el reconocimiento de voz; opción 5 del lanzador = conversar por voz). Para recompilarla después de
cambiar el código: en Unity, menú **Space Apps → Compilar app de Windows**.

## Modelos 3D del equipo (carpeta `Modelo 3D Cuerpo`)

`blender/importar_modelos_equipo.py` los prepara para Unity (ya ejecutado; repetir si cambian):

| Archivo del equipo | Uso en la simulación |
| --- | --- |
| `Astronauta.fbx` (traje con esqueleto) | Los 4 tripulantes en la escena. La textura de paleta no venía: los colores se reconstruyen por celda UV. La pose (de pie / postura neutra en microgravedad) se controla desde Unity. |
| `lv2__fbx.fbx` (cuerpo anatómico) | Mapa corporal del panel derecho: zonas cabeza, ojos, tórax, brazos, pelvis y piernas coloreadas según el estado del tripulante seleccionado. |
| `Base.fbx` (hábitat) | Hábitat en la superficie de Marte y de la Luna. |

Los archivos `.max` (3ds Max) y `.ZTL` (ZBrush) no se pueden abrir sin esos programas.

```bash
"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" -b -P blender\importar_modelos_equipo.py -- --render
```

## Conexión con AEGIS (PC 3 del equipo)

Unity tiene dos modos. **Bus AEGIS**: se conecta a `ws://IP_DE_PC1:8001/ws` (subprotocolo `aegis.v1`) y
representa en 3D el mismo día, escenario y tripulación que el dashboard, sin diagnósticos propios.
**Local**: la simulación propia de abajo, para demostrar sin red. Pasos y mapeo de campos en
[simulacion_unity/CONEXION_AEGIS.md](simulacion_unity/CONEXION_AEGIS.md).

## ASTRA: asistente médica con IA local (Ollama)

Panel izquierdo, abajo: rostro holográfico realista (avatar médico *Medical_Female_02* de Microsoft Rocketbox,
licencia MIT) con 176 gestos faciales: boca sincronizada con la voz, parpadeo, mirada, movimiento de cabeza y
expresiones (amable, feliz, preocupada, firme, alerta, pensando). Diagnostica a cada tripulante con los datos de la simulación, explica causas, efectos en el
cuerpo y en la misión y qué hacer, en lenguaje para el público. Avisa sola cuando hay un problema crítico o una
tormenta solar.

- **Sin tokens de pago y sin internet**: usa [Ollama](https://ollama.com) en la misma PC (`http://127.0.0.1:11434`).
  Instalado: Ollama + `qwen2.5:3b` (1,9 GB) y el modelo propio `astra`. Responde en ~2–3 s.
- **Cómo evita inventar**: el motor médico propio (`MedicalKnowledge.cs`) calcula los hechos con las ecuaciones de
  la simulación; la IA solo los redacta de forma amigable. La gravedad de la emoción la decide el motor médico.
- **Sin Ollama** (o si se apaga) responde igual con el motor médico propio; el pie del panel dice qué fuente usó.
- **Voz**: voz neuronal local **Piper** con la voz femenina `es_AR-daniela-high`
  (`simulacion_unity/Assets/StreamingAssets/piper/`, ~146 MB). Divide la respuesta en oraciones y empieza a hablar
  en ~1–2 s, con volumen normalizado. Si falta Piper usa la voz de Windows (SAPI).
- **Conversación por micrófono en tiempo real (100 % local)**: **whisper.cpp** con el modelo `small`
  (`StreamingAssets/whisper/`, ~244 MB) transcribe en ~2 s, con el vocabulario de la tripulación como pista.
  - **CONVERSAR** (arriba del rostro): manos libres. Detecta cuándo empiezas y terminas de hablar (0,8 s de silencio)
    y responde sola. Si hablas fuerte mientras ASTRA habla, se calla y te escucha (estima el eco de los parlantes).
  - **HABLAR** (mantener presionado) o la tecla **F2**: presionar para hablar; útil con ruido de público.
  - Con mucho público, `"mic_palabra_clave": true` hace que en manos libres solo responda si la frase dice "ASTRA".
    `mic_sensibilidad` ajusta la detección de voz (mayor = más sensible).
  - Si Windows bloquea el micrófono: *Configuración → Privacidad y seguridad → Micrófono → Permitir que las apps de
    escritorio accedan*. Con auriculares la interrupción funciona mejor (no hay eco).
- **Aprendizaje**: cada conversación se guarda en `IA/memoria_astra.jsonl`. Los botones **¿Te sirvió? SÍ/NO** marcan
  respuestas: las útiles se usan de ejemplo al instante. Para "entrenarla" con esos ejemplos (crea el modelo `astra`):

```bash
powershell -ExecutionPolicy Bypass -File IA\entrenar_astra.ps1
```

  Es entrenamiento por instrucciones y ejemplos (no cambia los pesos del modelo). Conocimiento médico editable en
  `IA/conocimiento_astra.md`; ejemplos base en `IA/ejemplos_base.jsonl`.
- **Configuración**: `simulacion_unity/Assets/StreamingAssets/astra.json` (en la app compilada:
  `App/SaludAstronauta/SaludAstronauta_Data/StreamingAssets/astra.json`): orden de modelos, temperatura, `voz_motor` (`piper` o `windows`), `voz_velocidad` (menor = más rápida),
  `voz_volumen`, `rostro` (`realista` o `lineas`) y avisos.
- **Prueba**: menú **Space Apps → Probar ASTRA (IA local)**, o
  `SaludAstronauta.exe -mision 4 -dia 480 -preguntar "¿Cómo está CMD Alfa?"`. Con `-conversar` arranca en manos
  libres y con `-oir pregunta.wav` le "habla" con un audio (prueba del reconocimiento de voz).

Es una simulación educativa: ASTRA no reemplaza a un médico real. Licencias del avatar, Piper, la voz y el modelo en
[LICENCIAS_TERCEROS.md](LICENCIAS_TERCEROS.md) (la voz Daniela es CC BY-SA 4.0: hay que citarla en la presentación).

## Enlace espacio–Tierra: telemetría cifrada a otras computadoras

La simulación transmite cada segundo, por **WebSocket**, el estado de la misión a **estaciones terrenas** en otras
computadoras: comprimido, **cifrado con AES-256 y firmado con HMAC-SHA256** (solo quien tiene `clave_nasa.key`
puede leerlo), protegido con un **código corrector Hamming + entrelazado + CRC**, y pasando por un **canal con
ruido espacial simulado** (distancia, conjunción solar, tormentas, ionosfera y cinturones de Van Allen). Cada
estación corrige los errores, rechaza intrusos, filtra picos de sensores causados por radiación y escribe
**informes** de lo que pasa en la misión.

- `receptor_python/`: estación en Python (puerto 8765) con **panel de control web** en <http://localhost:8080> e **informes PDF**.
  `receptor_rust/`: estación en Rust (puerto 8766).
- Configuración de la nave: `StreamingAssets/enlace/enlace.json` (lista de estaciones `ws://IP:puerto`) o
  `SaludAstronauta.exe -estacion ws://IP:8765`. Opción **6** de `INICIAR_DEMO.bat` = todo en esta PC.
- Detalle, protocolo y justificación: [ENLACE_ESPACIO_TIERRA.md](ENLACE_ESPACIO_TIERRA.md).

## 1. Unity (simulación)

1. Abrir **Unity Hub → Add → `simulacion_unity`** (Unity 6000.6.9f1).
2. Abrir `Assets/Scenes/SpaceApps.unity` y presionar **Play**.
   Si la escena no existe: menú **Space Apps → Crear escena principal**.
3. Menú **Space Apps → Probar simulación** corre una misión a Marte completa sin gráficos y muestra el resumen
   en la consola.

### Qué simula (basado en el documento HACKATON)

| Bloque del documento | Implementación |
| --- | --- |
| 1. Variables de salud | FC, HRV, presión arterial, SpO₂, frecuencia respiratoria, temperatura central; glucosa, lactato, cortisol, pH, potasio, calcio urinario; EMG, EEG, GSR; sueño y REM, gasto energético, METs, IMC. Más hueso, músculo, VO₂ máx, plasma, SANS, dosis de radiación y estrés. |
| 2. Variables ambientales | En la nave: CO₂ por módulo, PM2.5/PM10, CO, NO₂, SO₂, O₃, O₂ y ppO₂, temperatura, humedad, presión, ruido. En la Tierra: aire urbano con picos de tráfico, UV y ozono. Polvo lunar/marciano que entra en cada EVA. |
| 3. Modelos matemáticos | EDO de espacio de estados por astronauta (paso de 1 hora); **difusión‑advección 1D** del CO₂ y del polvo en 12 módulos con ventilación; **regresión lineal** para pronosticar cuándo se cruza un umbral. |
| 4. Diagnóstico | Umbrales **dinámicos** (media y desviación móviles EWMA, z > 3,5) + umbrales clínicos fijos; clasificación verde/amarillo/rojo; bitácora de causas. |
| 5. Sensores y actuadores | Lecturas con ruido de ECG, PPG, manguito, termistor, IR, GSR, EMG, EEG, acelerómetro, NDIR (CO₂), láser PM, electroquímicos (serie MQ), BME280, micrófono, UV. Actuadores: **LED RGB** en el pecho del astronauta y **vibración háptica** al generarse una alerta. |

Misiones: ISS 6 meses / 1 año, Artemis (Luna 40 días), Marte clase conjunción (~920 días) o modo libre. Antes de
cada misión corre el acondicionamiento (mínimo 14 días). Escenarios 3D: Tierra con el cohete en la plataforma,
órbita baja con estación, espacio profundo con la nave, superficie lunar, superficie de Marte e **interior de la
nave** (botón Vista Interior/Exterior), con pantallas de CO₂ por módulo en las paredes.

Contramedidas: ejercicio, blindaje, refugio para tormentas solares, depurador de CO₂/HEPA, apoyo psicológico,
iluminación circadiana y gravedad artificial. **Exportar CSV** guarda todos los días en `simulacion_unity/Resultados/`.

Controles: clic derecho + arrastrar = girar · rueda = zoom · Espacio = iniciar/pausar · Enter = preguntar a ASTRA · F2 (mantener) = hablarle.

## 2. FreeCAD (nave, cohete y planos)

- Con interfaz: *Macro → Macros… → `freecad/nave_spaceapps.py` → Ejecutar* (genera además `.glb` y los planos PDF).
- Sin interfaz:

```bash
"C:\Program Files\FreeCAD 1.1\bin\freecadcmd.exe" freecad\nave_spaceapps.py
```

Medidas en `P_NAVE` y `P_COHETE` (mm). Nave: 39,3 m de alto, 52,4 m de envergadura, hábitat de 8,4 m con 3 cubiertas
y refugio contra radiación. Cohete: 106,8 m con 2 propulsores laterales.

## 3. Blender (detalle y materiales)

Instalado: Blender 4.5 LTS (funciona con 4.2 o más reciente). Doble clic en `blender/ejecutar_blender.bat`
(o `ejecutar_blender.bat --render` para guardar imágenes de presentación en `blender/salida/`).

- `detallar_nave.py`: importa los OBJ de FreeCAD, aplica materiales PBR (pintura, aislante dorado MLI, celdas
  solares, metal), biseles, refuerzos, cajas de servicio, escotilla, ventanas, bloques RCS, antena de alta ganancia.
- `crear_interior_nave.py`: pasillo con marcos hexagonales, piso de rejilla con textura, foso, tuberías con codos,
  tanques, pasarelas con barandas, escalera, consolas y compuertas en ambos extremos.

## Carpetas

```
freecad/            macro paramétrica de FreeCAD
export/             salidas de FreeCAD (STEP, STL, OBJ, FCStd)
blender/            scripts de Blender, entrada (OBJ) y salida (.blend, renders)
simulacion_unity/   proyecto Unity 6 (Assets/SpaceApps/Scripts: Health, World, Astronaut, UI, Assistant, Bus)
IA/                 ASTRA: conocimiento, ejemplos, memoria y script de entrenamiento (Ollama)
receptor_python/    Estación terrena en Python (WebSocket, descifrado, filtro, informes)
receptor_rust/      Estación terrena en Rust (lo mismo, ejecutable nativo)
simulacion_godot/   versión anterior en Godot (ya no se usa)
```

## Limitaciones

El modelo es educativo: sirve para comparar escenarios y contramedidas, no para predecir la salud de una persona
real. Las tasas y umbrales están explicados en el informe y en los comentarios de cada script.
