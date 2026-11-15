# -*- coding: utf-8 -*-
"""
Blender: detalla la nave y el cohete que exporta FreeCAD y los envía a Unity.

  FreeCAD (nave_spaceapps.py) -> blender/entrada/*.obj  (piezas + colores, metros)
  este script                  -> materiales PBR, biseles, detalles (greebles), antena, RCS,
                                  refuerzos, celdas solares
                               -> simulacion_unity/Assets/SpaceApps/Resources/Models/<nombre>.fbx
                               -> blender/salida/<nombre>.blend (+ .png si se pide render)

Uso (sin abrir Blender):
  blender -b -P detallar_nave.py -- [nave_transito|cohete_lanzador|todo] [--render]
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import comun as C  # noqa: E402


def materiales():
    m = {
        "blanco": C.material("Pintura_Blanca", (0.85, 0.85, 0.83), 0.05, 0.45),
        "gris": C.material("Metal_Gris", (0.55, 0.56, 0.60), 0.85, 0.35),
        "oscuro": C.material("Metal_Oscuro", (0.07, 0.07, 0.08), 0.9, 0.45),
        "oro": C.material("Aislante_MLI", (0.85, 0.62, 0.22), 1.0, 0.28),
        "solar": C.material("Celda_Solar", (0.04, 0.07, 0.24), 0.4, 0.12),
        "vidrio": C.material("Vidrio", (0.03, 0.07, 0.12), 0.9, 0.04),
        "naranja": C.material("Espuma_Naranja", (0.80, 0.42, 0.14), 0.0, 0.75),
    }
    C.agregar_rayado(m["oro"], escala=12.0, intensidad=0.45)     # aislante arrugado
    C.agregar_rayado(m["blanco"], escala=60.0, intensidad=0.05)
    C.agregar_rayado(m["naranja"], escala=30.0, intensidad=0.2)
    return m


def importar(nombre, mats):
    ruta = os.path.join(C.ENTRADA, nombre + ".obj")
    if not os.path.exists(ruta):
        ruta = os.path.join(C.PROYECTO, "export", nombre + ".obj")
    if not os.path.exists(ruta):
        raise SystemExit("No se encontró %s.obj: ejecute primero freecad/nave_spaceapps.py" % nombre)
    bpy.ops.wm.obj_import(filepath=ruta)   # OBJ en Y arriba -> Blender Z arriba; coordenadas = FreeCAD en metros
    col = C.coleccion(nombre)
    piezas = {}
    for o in list(bpy.context.selected_objects):
        clave = o.data.materials[0].name.split(".")[0] if o.data.materials else "gris"
        C.asignar(o, mats.get(clave, mats["gris"]))
        C.mover(o, col)
        piezas[o.name.split(".")[0]] = o
    print("[Blender] importadas %d piezas de %s" % (len(piezas), ruta))
    return col, piezas


def anillos(col, mat, r, zs, alto=0.12):
    for z in zs:
        C.cilindro("Refuerzo", r, alto, (0, 0, z), mat, lados=64, col=col)


def greebles(col, mat, r, z0, z1, n, evitar=(), semilla=1):
    """Cajas pequeñas sobre un casco cilíndrico (paneles de servicio, cajas de electrónica)."""
    rnd = random.Random(semilla)
    for _ in range(n):
        a = rnd.uniform(0, 2 * math.pi)
        if any(abs(math.atan2(math.sin(a - e), math.cos(a - e))) < 0.35 for e in evitar):
            continue
        z = rnd.uniform(z0, z1)
        w, h, d = rnd.uniform(0.3, 1.2), rnd.uniform(0.3, 1.5), rnd.uniform(0.05, 0.25)
        C.caja("Greeble", (d, w, h), (math.cos(a) * (r + d / 2), math.sin(a) * (r + d / 2), z), mat, rot=(0, 0, a), col=col)


def detallar_nave(mats):
    col, piezas = importar("nave_transito", mats)
    for nombre in ("Etapa_Propulsion", "Casco_Habitat", "Modulo_Servicio", "Capsula_Tripulacion"):
        if nombre in piezas:
            C.biselar(piezas[nombre], 0.04, 2)
            C.suavizar(piezas[nombre])
    # Etapa de propulsión (r 4.2, z 3-18): refuerzos y cajas, evitando los radiadores (±Y)
    anillos(col, mats["oscuro"], 4.24, [3.3, 7.0, 10.5, 14.0, 17.7])
    greebles(col, mats["gris"], 4.2, 4.0, 17.0, 40, evitar=(math.pi / 2, -math.pi / 2), semilla=3)
    # Toberas: cardán y anillo interno
    for i in range(3):
        a = 2 * math.pi * i / 3
        C.cilindro("Cardan", 0.45, 0.6, (math.cos(a) * 2.5, math.sin(a) * 2.5, 3.2), mats["gris"], lados=24, col=col)
    # Hábitat (r 4.2, z 18-30): refuerzos, escotilla, pasamanos, ventanas
    anillos(col, mats["gris"], 4.24, [18.25, 29.75])
    greebles(col, mats["blanco"], 4.2, 19.0, 25.0, 25, evitar=(0.0, math.pi), semilla=5)
    C.cilindro("Escotilla", 0.7, 0.12, (0, -4.26, 21.5), mats["gris"], rot=(math.pi / 2, 0, 0), lados=32, col=col)
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        C.caja("Ventana", (0.08, 0.6, 0.45), (math.cos(a) * 4.25, math.sin(a) * 4.25, 26.0), mats["vidrio"], rot=(0, 0, a), col=col)
    for z in (20.0, 22.5, 27.5):
        C.cilindro("Pasamanos", 0.03, 2.2, (0.0, -4.32, z), mats["oro"], rot=(0, math.pi / 2, 0), lados=8, col=col)
    # Paneles solares: mástil y separación de celdas
    for s in (-1, 1):
        cx = s * 16.2
        C.cilindro("Mastil", 0.08, 20.0, (cx, 0, 24.06), mats["gris"], rot=(0, math.pi / 2, 0), lados=12, col=col)
        for k in range(11):
            C.caja("Junta", (0.04, 6.0, 0.02), (cx - 10 + k * 2.0, 0, 24.04), mats["gris"], col=col)
    # Módulo de servicio (r 2.5, z 32-36): bloques RCS y antena de alta ganancia
    for k in range(4):
        a = 2 * math.pi * k / 4 + math.pi / 4
        x, y = math.cos(a) * 2.6, math.sin(a) * 2.6
        C.caja("BloqueRCS", (0.35, 0.5, 0.5), (x, y, 35.2), mats["gris"], rot=(0, 0, a), col=col)
        for dz, rx in ((0.3, 0), (-0.3, math.pi)):
            C.cilindro("ToberaRCS", 0.04, 0.18, (x * 1.06, y * 1.06, 35.2 + dz), mats["oscuro"], rot=(rx, 0, 0), lados=12, col=col, r2=0.09)
    C.cilindro("BrazoAntena", 0.06, 3.0, (2.5 + 1.5, 0, 33.0), mats["gris"], rot=(0, math.pi / 2, 0), lados=12, col=col)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, location=(5.6, 0, 33.0), segments=32, ring_count=16)
    plato = bpy.context.active_object
    plato.name = "PlatoAntena"
    plato.scale = (0.25, 1.0, 1.0)
    bpy.ops.object.transform_apply(scale=True)
    C.asignar(plato, mats["blanco"])
    C.mover(plato, col)
    # Cápsula: anillo de acople y escudo térmico visible
    C.cilindro("AnilloAcople", 0.6, 0.3, (0, 0, 39.45), mats["gris"], lados=32, col=col)
    C.cilindro("EscudoTermico", 2.55, 0.2, (0, 0, 36.05), mats["oscuro"], lados=64, col=col)
    C.unir_por_material(col)
    return col


def detallar_cohete(mats):
    col, piezas = importar("cohete_lanzador", mats)
    for o in piezas.values():
        C.suavizar(o)
    x0 = 40.0   # la macro coloca el cohete 40 m al lado de la nave
    # Etapa central: franjas de los tanques y líneas de alimentación
    anillos_x = [(x0, 0, z) for z in (8.0, 25.0, 50.0, 66.0)]
    for p in anillos_x:
        C.cilindro("Franja", 4.24, 0.25, p, mats["oscuro"], lados=64, col=col)
    C.cilindro("LineaAlimentacion", 0.25, 60.0, (x0 + 4.35, 0, 36.0), mats["naranja"], lados=16, col=col)
    # Propulsores: segmentos
    for s in (-1, 1):
        bx = x0 + s * (4.2 + 1.85 + 0.3)
        for z in (12.0, 22.0, 32.0, 42.0, 52.0):
            C.cilindro("Segmento", 1.88, 0.2, (bx, 0, z), mats["gris"], lados=48, col=col)
        C.caja("Union", (1.2, 0.6, 0.6), ((x0 + bx) / 2, 0, 56.0), mats["gris"], col=col)
    # Torre de escape: aletas
    for k in range(4):
        a = 2 * math.pi * k / 4
        C.caja("AletaEscape", (0.05, 0.6, 1.0), (x0 + math.cos(a) * 0.5, math.sin(a) * 0.5, 98.5), mats["blanco"], rot=(0, 0, a), col=col)
    C.unir_por_material(col)
    return col


def preparar_render():
    esc = bpy.context.scene
    world = bpy.data.worlds.new("Espacio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.004, 0.005, 0.008, 1.0)
    esc.world = world
    for nombre, energia, rot in (("Sol", 4.0, (50, -30, 40)), ("Relleno", 0.6, (110, 20, -140))):
        luz = bpy.data.lights.new(nombre, "SUN")
        luz.energy = energia
        o = bpy.data.objects.new(nombre, luz)
        o.rotation_euler = tuple(math.radians(v) for v in rot)
        esc.collection.objects.link(o)


def main():
    args = C.argumentos()
    que = next((a for a in args if not a.startswith("--")), "todo")
    hacer_render = "--render" in args
    for nombre, funcion, cam, obj in (
        ("nave_transito", detallar_nave, (70, -85, 45), (0, 0, 19)),
        ("cohete_lanzador", detallar_cohete, (40 + 120, -230, 75), (40, 0, 52)),
    ):
        if que not in ("todo", nombre):
            continue
        C.escena_vacia()
        mats = materiales()
        col = funcion(mats)
        C.exportar_fbx(nombre, list(col.objects))
        if hacer_render:
            preparar_render()
            C.render(nombre, cam, obj, lente=40)
        C.guardar_blend(nombre)


main()
