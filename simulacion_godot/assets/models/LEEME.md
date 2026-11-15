# Modelos 3D opcionales

La simulación funciona sin ningún archivo aquí: cada objeto tiene una versión procedural.
Si colocas un modelo con **exactamente** uno de estos nombres (`.glb`, `.gltf` u `.obj`), se usa en su lugar.

| Nombre del archivo | Dónde aparece | Fuente sugerida |
| --- | --- | --- |
| `nave_transito.glb` / `.obj` | Tránsito a la Luna y a Marte (vista exterior) | `freecad/nave_spaceapps.py` → carpeta `export/` |
| `cohete_lanzador.glb` / `.obj` | Escenario Tierra, en la plataforma | `freecad/nave_spaceapps.py` → carpeta `export/` |
| `estacion.glb` | Órbita baja | NASA 3D Resources: modelo de la ISS |
| `astronauta.glb` | Al lado del cuerpo de síntomas, como referencia | NASA 3D Resources: traje EMU / astronauta |
| `modulo_lunar.glb` | Superficie lunar | NASA 3D Resources: módulo lunar |
| `modulo_marte.glb` | Superficie de Marte | NASA 3D Resources: hábitat o módulo de Marte |
| `interior_nave.glb` | Vista interior (pasillo) | Pack sci‑fi modular, o el modelo de pasillo de referencia si tienen su licencia |

## Dónde conseguir modelos de referencia

- **NASA 3D Resources** — https://science.nasa.gov/3d-resources/ y https://github.com/nasa/NASA-3D-Resources
  (ISS, Orion, SLS, trajes, rovers, planetas). Muchos vienen en `.3ds`, `.obj` o `.blend`: ábrelos en Blender y
  exporta como **glTF 2.0 (.glb)**. Los logos e insignias de NASA tienen restricciones de uso.
- **NASA en Sketchfab** — https://sketchfab.com/nasa (muchos descargables en glTF).
- **Anatomía para los síntomas** — Z‑Anatomy (https://www.z-anatomy.com/, CC BY‑SA 4.0) y BodyParts3D
  (CC BY‑SA): esqueleto, músculos, ojo, corazón. Útiles para cambiar el cuerpo procedural por anatomía real.
- **Cuerpo humano base** — MakeHuman (exportaciones CC0) o Mixamo (requiere cuenta de Adobe).
- **Interiores sci‑fi gratuitos** — Kenney "Space Kit" (CC0) y los packs sci‑fi modulares de Quaternius (CC0).

Revisa la licencia de cada modelo antes de presentarlo y cítalo en los créditos del proyecto.

## Ajustes

- Los `.obj` de FreeCAD vienen en milímetros y con Z hacia arriba: el cargador los gira y escala solo.
- Si un `.glb` aparece acostado o girado, agrega su nombre en `FIXES` de
  `scripts/environment/model_loader.gd`, por ejemplo `"nave_transito": {"glb_rotate_x": -90.0}`.
- Después de copiar un archivo, abre el proyecto en el editor de Godot para que lo importe.
