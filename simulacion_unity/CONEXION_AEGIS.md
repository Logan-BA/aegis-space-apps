# Unity (PC 3) ↔ bus AEGIS (PC 1)

Unity se conecta al motor por WebSocket y muestra en 3D **la misma misión y el mismo día** que el dashboard.
En este modo Unity no calcula nada propio: no genera diagnósticos, tratamientos ni umbrales; solo representa lo
que envía el motor (indicadores, alertas con su fuente y eventos solares).

## Día de la demo

1. PC 1 arranca el motor (`uv run python -m api`, un solo worker).
2. En PC 3 abrir el proyecto en Unity, escena `Assets/Scenes/SpaceApps.unity`, **Play**.
3. Botón **Modo: Local** → pasa a **Modo: Bus AEGIS**.
4. En el panel izquierdo escribir `ws://IP_DE_PC1:8001/ws` y pulsar **Conectar**.
   El estado debe decir **Conectado (aegis.v1)** y el contador de mensajes debe subir.
5. Cambiar el día en el dashboard (PC 2) → la escena de Unity cambia de fase, de colores y de gráfico.

Con una build de Windows también se puede conectar al abrir:

```bash
SpaceApps.exe -aegis ws://IP_DE_PC1:8001/ws
```

La URL por defecto y el subprotocolo están en `Assets/StreamingAssets/aegis_bus.json`.

## Qué toma Unity de cada mensaje

| Dato | Para qué lo usa | Nombres que reconoce (editables en `aegis_bus.json`) |
| --- | --- | --- |
| Día y días totales | Encabezado, eje del gráfico, fase de Marte si no llega la fase | `day`, `dia`, `mission_day`, `total_days`… |
| Escenario | Elegir la escena 3D | `scenario`, `escenario`… (Marte, Tormenta, Tranquilo, Límite) |
| Fase | Escena exacta: tránsito, superficie, órbita, Tierra | `phase`, `fase`, `segment`, `location`… |
| Tripulación | Lista de astronautas para elegir | `crew`, `astronauts`, `tripulacion`… con `id` y `name` |
| Dosis, sueño, densidad ósea, capacidad aeróbica | Colores del cuerpo, halo de radiación, gráfico | `dose_msv`, `sleep_hours`, `bone_density`, `vo2max`… (pueden venir anidados) |
| Alertas | LED del astronauta (rojo si el nivel es alto) y pestaña Alertas con su fuente | `alerts` con `message`, `severity`, `source`, `astronaut_id` |
| Evento solar | Destello naranja en la escena | `solar_event`, `donki_event`, `cme`… |

Si un campo no aparece: pestaña **Mensaje** (muestra el JSON recibido), agregar su nombre en `aegis_bus.json`
y pulsar **Recargar mapeo**; no hace falta recompilar. Si el motor espera un mensaje del cliente al conectarse,
ponerlo en `"mensaje_inicial"`.

La densidad ósea y la capacidad aeróbica se muestran como % del inicio: acepta fracción (0,95), porcentaje (95)
o unidades absolutas (por ejemplo ml/kg/min, relativo al primer día recibido). Los colores del cuerpo son una
escala visual, no un umbral clínico.

## Probar sin la PC 1

```bash
python herramientas/bus_aegis_simulado.py --puerto 8001
```

Envía un estado de ejemplo de "Marte 900 días" cada medio segundo (el formato es de ejemplo, no el contrato real).
Prueba automática desde Unity: menú **Space Apps → Probar conexión con el bus AEGIS**.

Verificado en esta PC (Unity 6000.6.9f1) contra el bus simulado: conexión con subprotocolo `aegis.v1`, lectura
de día, escenario, fase, 4 astronautas con indicadores anidados, alertas con fuente y evento solar.
**Falta confirmar con el motor real**: los nombres de campo de `aegis.v1` no estaban disponibles en esta PC.
