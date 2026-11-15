# Envío a NASA Space Apps Challenge · respuestas listas para copiar

Formulario del proyecto del equipo. Cada bloque corresponde a un campo; el texto en inglés es el que
conviene pegar (los jueces globales leen inglés). Debajo va la versión en español como referencia.

> Plazo: domingo 15 de noviembre, 23:59 hora local de su evento. Guarde con **Save Progress** y al final
> pulse **Submit for Judging**.

---

## Project Summary

### Project Name
```
AEGIS: Astronaut Health Monitoring with an Onboard AI Medical Assistant and an Encrypted Space-to-Earth Link
```
Alternativa más corta: `AEGIS: Onboard AI Medical Assistant and Encrypted Telemetry for Astronaut Health`

### Summary *(50–200 palabras, máx. 1500 caracteres)*
```
AEGIS is a health-monitoring system for astronauts on long missions. A Unity 3D simulation acts as a digital twin of a four-person crew on ISS, Moon and Mars missions, modeling hour by hour bone and muscle loss in low gravity, cosmic radiation and solar storms against NASA's 600 mSv career limit, vision changes (SANS), sleep, stress and cabin CO2, with early warnings and computed countermeasures. Onboard, ASTRA, an AI nurse with a realistic holographic face, diagnoses each astronaut and explains causes, effects and actions in plain language, and talks with the crew by voice in real time. It runs fully offline (local LLM, neural speech and speech recognition), so it keeps working when Earth is 20 minutes away or the Sun blocks the link. Telemetry travels to Earth over an encrypted WebSocket link (AES-256 + HMAC) with error correction through a simulated deep-space noise channel. Ground stations in Python and Rust decrypt it, filter cosmic noise and sensor glitches, infer trends and produce clear reports, PDFs and a live web dashboard. This matters because on Mars the crew cannot wait for Earth: they need early, understandable and trustworthy health information on board and on the ground.
```

<details><summary>Versión en español</summary>

AEGIS es un sistema de monitoreo de salud para astronautas en misiones largas. Una simulación 3D en Unity funciona
como gemelo digital de una tripulación de cuatro personas en misiones a la ISS, la Luna y Marte: modela hora a hora la
pérdida ósea y muscular, la radiación cósmica y las tormentas solares frente al límite de carrera de 600 mSv de la
NASA, la visión (SANS), el sueño, el estrés y el CO₂ de la cabina, con alertas tempranas y contramedidas calculadas.
A bordo, ASTRA, una enfermera con IA y rostro holográfico realista, diagnostica a cada astronauta, explica causas,
efectos y acciones en lenguaje sencillo y conversa por voz en tiempo real, todo sin internet. La telemetría viaja a la
Tierra por un enlace WebSocket cifrado (AES-256 + HMAC) con corrección de errores a través de un canal con ruido
espacial simulado. Estaciones terrenas en Python y Rust la descifran, filtran el ruido y generan informes, PDF y un
panel web en vivo.
</details>

---

## Project Links

### Project Demonstration *(máx. 7 diapositivas o video de 30 s)*
```
https://github.com/Logan-BA/aegis-space-apps/blob/main/docs/AEGIS_demo.pdf
```
(7 diapositivas en PDF; GitHub lo muestra en el navegador sin registrarse.)

### Project
```
https://github.com/Logan-BA/aegis-space-apps
```
App lista para probar en Windows: https://github.com/Logan-BA/aegis-space-apps/releases/tag/v1.0

---

## Project Information

### Project Details
```
WHAT IT DOES
AEGIS connects four pieces into one system, from the spacecraft to ground control:
1) Spacecraft simulation (Unity 6, C#): a digital twin of a four-person crew. Missions: ISS 6 months / 1 year, Moon 40 days and a Mars conjunction-class mission (~1,100 days), each preceded by 14 days of conditioning. An hourly state-space model computes bone density, muscle mass, VO2max, plasma volume, SANS (vision), radiation dose with random solar particle events, sleep debt, stress, vital signs and blood/urine chemistry. Cabin CO2 and dust follow a 1D diffusion-advection model across 12 modules. Early warning uses dynamic thresholds (EWMA, z-score) plus linear-regression forecasts; countermeasures (exercise, shielding, storm shelter, CO2 scrubbing, circadian lighting, psychological support, artificial gravity) are recommended with their computed benefit and can be applied by an autopilot. The scene uses our team's 3D astronaut, anatomy and habitat models and a spacecraft designed in FreeCAD and detailed in Blender.
2) ASTRA, the onboard AI medical assistant: if the crew loses its doctor, ASTRA analyzes the data, explains causes, effects, mission impact and what to do in plain language, and talks with the crew. A rule-based medical engine computes the facts; a local LLM (Qwen2.5 3B on Ollama) only rephrases them, which prevents invented diagnoses. ASTRA has a realistic holographic face (176 blendshapes: lip sync, blinking, gaze, emotions), a neural voice (Piper) and real-time hands-free speech recognition (whisper.cpp) with barge-in. It runs 100 % offline, with no cloud and no per-use cost, and learns from crew feedback.
3) Encrypted space-to-Earth link: every second the spacecraft sends telemetry over WebSocket. Data are compressed, encrypted with AES-256, signed with HMAC-SHA256 and protected with CRC-32 and Hamming(7,4) error correction with interleaving. A physics-based channel model adds noise from distance (Eb/N0 and bit error rate), solar conjunction (blackout), solar storms, the ionosphere and the Van Allen belts, plus radiation-induced sensor glitches (single event upsets). The spacecraft stores and forwards frames when the link is down.
4) Ground stations in Python and Rust: they correct bit errors, discard unrecoverable frames, reject frames not signed with the mission key, remove sensor spikes with a Hampel filter, and infer what is happening (for example, the day the dose limit would be reached at the current rate). They write Markdown/CSV reports; the Python station also serves a live web dashboard (any device on the network) and produces PDF reports automatically on every event or on demand.

RESULTS
In an end-to-end test the simulation transmitted to the Python and Rust stations at the same time across 410 mission days, including solar conjunction: 81-83 % link quality, hundreds of bits corrected and radiation-induced sensor spikes filtered. Frames signed with a fake key were rejected 5 out of 5 times. ASTRA answers in about 2-3 s and starts speaking about 1-2 s later.

BENEFITS AND IMPACT
Earlier detection of health problems; decisions that do not wait 40 minutes for Earth; data on the ground that cannot be read or forged by others; and a clear, visual way to show students and the public what spaceflight does to the human body.

TOOLS AND LANGUAGES
Unity 6 (C#, HLSL shaders), Python (websockets, cryptography, reportlab), Rust (tokio, tokio-tungstenite, RustCrypto), Ollama + Qwen2.5 3B, Piper TTS, whisper.cpp, Microsoft Rocketbox avatar, FreeCAD, Blender. Runs on ordinary Windows and Linux computers.

CREATIVITY AND CONSIDERATIONS
We combined a health digital twin, an empathetic voice assistant and a realistic deep-space communications model in one demonstrable chain. We prioritized privacy and autonomy (everything local), honesty (the LLM never invents facts; the system shows its sources), accessibility (plain-language explanations, Spanish interface, voice) and realism (light-time delay, solar conjunction, error correction). It is an educational simulation, not a medical device.
```

### Use of Super Intelligence (SI)
Marque las opciones que correspondan al uso de IA en **código** y en **texto/documentación**, y, si aparecen,
las de **audio** (la voz de ASTRA es síntesis neuronal) y **modelos/LLM dentro del producto**. **No** marque
imágenes generadas por IA: todas las imágenes son capturas reales. Detalle completo en
[USO_DE_IA.md](../USO_DE_IA.md) (enlace público: https://github.com/Logan-BA/aegis-space-apps/blob/main/USO_DE_IA.md).

### NASA Data *(Link Text → Link Url)*
| Link Text | Link Url |
| --- | --- |
| NASA-STD-3001 Vol. 1 Rev. B — Crew Health (600 mSv career radiation limit) | https://www.nasa.gov/wp-content/uploads/2020/10/2022-01-05_nasa-std-3001_vol.1_rev._b_final_draft_with_signature_010522.pdf |
| NASA OCHMO Radiation Protection Technical Brief | https://www.nasa.gov/wp-content/uploads/2023/03/radiation-protection-technical-brief-ochmo.pdf |
| NASA Human Research Program (spaceflight health hazards) | https://www.nasa.gov/hrp/ |
| NASA Human Research Roadmap (human system risks: bone, muscle, SANS, radiation, behavioral health) | https://humanresearchroadmap.nasa.gov/ |
| NASA DONKI space weather database (solar events; used by the team's AEGIS engine) | https://ccmc.gsfc.nasa.gov/tools/DONKI/ |
| NASA Open Science Data Repository (OSDR) (space health references used by the team's AEGIS engine) | https://osdr.nasa.gov/bio/ |
| NASA Deep Space Network (basis of the space-to-Earth link model) | https://www.nasa.gov/communicating-with-missions/dsn/ |

### Space Agency Partner & Other Data
| Link Text | Link Url |
| --- | --- |
| CCSDS Blue Books (space data link, coding and security standards; reference for the link design) | https://public.ccsds.org/Publications/BlueBooks.aspx |
| Unity 6 (game engine) | https://unity.com/ |
| Ollama (local LLM runtime) | https://ollama.com/ |
| Qwen2.5 3B (LLM, Qwen Research License) | https://ollama.com/library/qwen2.5 |
| Piper TTS (MIT) | https://github.com/rhasspy/piper |
| Piper voice es_AR-daniela-high (CC BY-SA 4.0) | https://huggingface.co/rhasspy/piper-voices |
| OpenSLR 61 (dataset behind the Daniela voice) | https://www.openslr.org/61/ |
| whisper.cpp (MIT) | https://github.com/ggml-org/whisper.cpp |
| OpenAI Whisper model (MIT) | https://github.com/openai/whisper |
| Microsoft Rocketbox avatar library (MIT) — ASTRA's face | https://github.com/microsoft/Microsoft-Rocketbox |
| FreeCAD | https://www.freecad.org/ |
| Blender | https://www.blender.org/ |
| Python websockets (BSD) | https://github.com/python-websockets/websockets |
| Python cryptography | https://cryptography.io/ |
| ReportLab (PDF, BSD) | https://www.reportlab.com/opensource/ |
| Tokio (Rust async runtime) | https://tokio.rs/ |
| tokio-tungstenite (Rust WebSocket) | https://github.com/snapview/tokio-tungstenite |
| RustCrypto (AES, HMAC, SHA-2) | https://github.com/RustCrypto |
| Claude by Anthropic (SI used to write code and documentation) | https://www.anthropic.com/claude |

---

## Project Submission Verification
Marque ambas casillas después de leer la [guía de envío](https://www.spaceappschallenge.org/resources/project-submission-guide/)
y los [términos](https://www.spaceappschallenge.org/legal/). Todos los recursos externos están listados arriba.

## Antes de enviar · lista de verificación
- [ ] Abrir el link del proyecto y el de la demo en una ventana de incógnito (deben verse sin iniciar sesión).
- [ ] Revisar que el nombre del equipo y los miembros estén completos en la pestaña *Members*.
- [ ] Pegar los textos, **Save Progress**, revisar y **Submit for Judging** antes de las 23:59 hora local.
