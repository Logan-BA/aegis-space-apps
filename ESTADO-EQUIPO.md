# AEGIS — Estado y reparto del trabajo

**Estamos construyendo una herramienta visual para que un astronauta entienda su propia salud durante una misión larga y sepa qué necesita atención.** No es una web comercial ni un sistema médico: es una simulación para el reto *Create Health Monitoring Software for Astronauts on Space Missions* de NASA Space Apps.

**Nuestra responsabilidad es el dashboard web.** La lógica ya comparte una misma misión por WebSocket. Ahora estamos mejorando su presentación para que se entienda de un vistazo, siguiendo el boceto del equipo.

## La idea en una imagen

```text
DONKI + OSDR + escenarios
           ↓
PC 1 · Motor y bus (Python FastAPI)
           ↓ mismo estado, mismo día
     ┌─────┴─────┐
     ↓           ↓
PC 2 · Web    PC 3 · Escena 3D
React + TS    Unity + C#/HLSL
(NUESTRO)     (EQUIPO UNITY)
```

Las tres PC usan una **misma línea de tiempo**. Cambiar el día en el dashboard también cambia lo que recibe Unity.

## Qué ve la persona

El dashboard se organiza en cuatro zonas, como el boceto:

1. **Misión:** escenario, recorrido, astronauta, día y control de días.
2. **Indicadores personales:** dosis, sueño, densidad ósea y capacidad aeróbica, con sus referencias.
3. **Tendencia:** gráficas de esos indicadores durante la misión.
4. **Señales y seguimiento:** lo observado, contexto solar/misión con fuente y rutina o referencias disponibles.

**La escena 3D real se presenta en Unity.** En la web mostramos una representación sencilla de la misión para explicar qué está pasando; no estamos creando una segunda escena 3D en el navegador.

## Qué datos usamos

| Fuente o entrada | Para qué sirve |
| --- | --- |
| NASA DONKI | Fechas reales de eventos solares que influyen en la radiación simulada. |
| NASA OSDR | Estudios y referencias de salud espacial para contextualizar las señales. |
| Motor de simulación | Calcula la evolución de la tripulación ficticia día por día. |
| Escenarios | Tormenta 90 días, Marte 900 días, Tranquilo y Límite. |

La muestra está en **JSON + SQLite** y permite ejecutar la demo **sin conexión a NASA**. La tripulación y sus lecturas son simuladas. No inventamos diagnósticos, tratamientos ni umbrales clínicos sin una fuente.

## Qué ya está hecho y comprobado

- [x] Motor de simulación y escenarios.
- [x] API REST y bus WebSocket `aegis.v1`.
- [x] Conexión del dashboard en red para trabajar con otra computadora.
- [x] Cuatro paneles del astronauta, gráficas, referencias y control de días.
- [x] Corrección: abrir otro dashboard no reinicia la misión que están viendo los demás.
- [x] Prueba real por LAN: dos clientes recibieron el mismo día y el mismo paquete.
- [x] 74 pruebas Python, 39 pruebas del contrato API y 9 pruebas del cliente; TypeScript y build correctos.

La web antigua y su servidor duplicado fueron retirados. La versión vigente está en `client/`; el único backend de la demo está en `api/`.

## En qué estamos trabajando ahora

**Hacer que el dashboard sea más fácil de entender**, no añadir funciones innecesarias:

- Ruta de misión visible, con Tierra, nave, Marte y evento solar.
- Indicadores personales más grandes y mejor agrupados.
- Menos texto técnico compitiendo con la información importante.
- Mejor lectura de la relación entre evento solar, dosis y evolución.
- Diseño adaptado a pantallas estrechas y accesible con teclado.

La primera propuesta visual está en:
`design/astronaut-readability/01-mision-primero.html`.

**Es una maqueta, no todavía el dashboard funcional final.** La especificación de este trabajo está en `odd/tasks/astronaut-readability.md`.

## Qué falta

- [ ] Aplicar y validar visualmente la nueva presentación del dashboard.
- [ ] Compilar y revisar la escena Unity en una computadora con Unity Editor; aquí no hay editor disponible.
- [ ] Probar la demo completa en las tres computadoras físicas de la presentación.
- [ ] Coordinar con Unity la IP y el puerto del bus antes de la demo.

El explicador LLM queda **planeado, no implementado**. Si se desarrolla más adelante, resumirá alertas con fuentes NASA y **no diagnosticará**.

BLE, MQTT, pulseras y sensores son integraciones opcionales. **Los circuitos AC/DC no forman parte de este proyecto ni de nuestro trabajo.**

## Configuración para el día de la demo

Sustituir los ejemplos por las IP reales de las tres PC, en la misma red.

### PC 1 · Motor y bus

Desde la raíz del repositorio:

```bash
AEGIS_DASHBOARD_ORIGIN=http://IP_DE_PC2:5173 uv run python -m api
```

Usar un solo worker para que exista **un solo reloj de misión**.

### PC 2 · Dashboard (nuestro)

```bash
cd client
npm install
AEGIS_API_TARGET=http://IP_DE_PC1:8001 npm run dev
```

Abrir `http://IP_DE_PC2:5173` y elegir escenario, astronauta y día.

### PC 3 · Unity

Configurar el WebSocket del proyecto Unity:

```text
ws://IP_DE_PC1:8001/ws
Subprotocolo: aegis.v1
```

## Próximo paso del equipo

Cerrar la mejora visual del dashboard y ensayar juntos este recorrido: **elegir misión → elegir astronauta → cambiar día → ver indicadores y alertas en React → ver el mismo cambio en Unity**.
