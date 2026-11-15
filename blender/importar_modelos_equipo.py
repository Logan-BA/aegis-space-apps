# -*- coding: utf-8 -*-
"""
Prepara los modelos 3D del equipo (carpeta "Modelo 3D Cuerpo") para Unity:

  Astronauta.fbx  -> Models/astronauta_traje.fbx  traje con esqueleto y animaciones (Idle, Walk…).
                     La textura de paleta (multpaleta.png) no venía en la carpeta: se reconstruyen
                     los colores agrupando las caras por su celda de la paleta (UV).
  lv2__fbx.fbx    -> Models/anatomia.fbx           cuerpo anatómico a 1,80 m, dividido por zonas
                     (Cabeza, Ojos, Torso, Brazos, Pelvis, Piernas) para el mapa corporal.
                     Se agrega una prenda base (short de compresión) sobre la pelvis.
  Base.fbx        -> Models/modulo_marte.fbx y modulo_lunar.fbx  hábitat de superficie.

Uso: blender -b -P importar_modelos_equipo.py -- [--render]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
import mathutils  # noqa: E402
import comun as C  # noqa: E402

ORIGEN = os.path.join(C.PROYECTO, "Modelo 3D Cuerpo")
RENDER = "--render" in C.argumentos()


def limpiar():
    C.escena_vacia()


def importar_fbx(nombre):
    ruta = os.path.join(ORIGEN, nombre)
    if not os.path.exists(ruta):
        raise SystemExit("Falta " + ruta)
    bpy.ops.import_scene.fbx(filepath=ruta)
    return list(bpy.context.scene.objects)


def caja_mundo(objs):
    mn = mathutils.Vector((1e9, 1e9, 1e9)); mx = -mn
    for o in objs:
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(c)
            mn = mathutils.Vector(map(min, mn, w)); mx = mathutils.Vector(map(max, mx, w))
    return mn, mx


def render_previa(nombre, objs, distancia=3.0, objetivo_z=None):
    if not RENDER:
        return
    mn, mx = caja_mundo(objs)
    centro = (mn + mx) / 2
    h = max((mx - mn).x, (mx - mn).y, (mx - mn).z)
    world = bpy.data.worlds.new("Fondo"); world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.06, 0.08, 1)
    bpy.context.scene.world = world
    for nombre_luz, energia, rot in (("Clave", 3.5, (55, 0, 35)), ("Relleno", 1.2, (70, 0, -130))):
        l = bpy.data.lights.new(nombre_luz, "SUN"); l.energy = energia
        o = bpy.data.objects.new(nombre_luz, l); o.rotation_euler = tuple(math.radians(v) for v in rot)
        bpy.context.scene.collection.objects.link(o)
    cz = objetivo_z if objetivo_z is not None else centro.z
    C.render(nombre, (centro.x + h * 0.9, centro.y - h * distancia, cz + h * 0.25), (centro.x, centro.y, cz), lente=50)


# ---------------------------------------------------------------- 1. traje de astronauta

# Colores por celda de la paleta (u, v redondeados a 1/16) -> (nombre, color, metal, rugosidad, emisión)
PALETA = {
    (0.0625, 0.75): ("Traje", (0.86, 0.87, 0.86), 0.0, 0.75, None),
    (0.25, 0.6875): ("Traje", (0.86, 0.87, 0.86), 0.0, 0.75, None),
    (0.4375, 0.6875): ("Detalle", (0.55, 0.58, 0.62), 0.4, 0.5, None),
    (0.375, 0.6875): ("Detalle", (0.55, 0.58, 0.62), 0.4, 0.5, None),
    (0.1875, 0.6875): ("Articulacion", (0.24, 0.26, 0.29), 0.3, 0.6, None),
    (0.1875, 0.8125): ("Articulacion", (0.24, 0.26, 0.29), 0.3, 0.6, None),
    (0.3125, 0.6875): ("Metal", (0.45, 0.47, 0.5), 0.8, 0.35, None),
    (0.0625, 0.9375): ("Metal", (0.45, 0.47, 0.5), 0.8, 0.35, None),
    (0.0625, 0.4375): ("Panel", (0.16, 0.32, 0.52), 0.2, 0.4, None),
    (0.4375, 0.625): ("Suela", (0.12, 0.12, 0.13), 0.0, 0.9, None),
    (0.125, 0.9375): ("Visor", (0.62, 0.48, 0.2), 1.0, 0.08, None),
    (0.3125, 0.8125): ("LuzMochila", (0.3, 0.75, 0.9), 0.0, 0.3, ((0.3, 0.75, 0.9), 3.0)),
    (0.0, 0.0): ("Lampara", (1.0, 0.95, 0.85), 0.0, 0.3, ((1.0, 0.95, 0.85), 5.0)),
}


def material_traje(nombre, col, metal, rug, em):
    if em is None:
        return C.material("Traje_" + nombre, col, metal, rug)
    return C.material("Traje_" + nombre, col, metal, rug, emision=em[0], fuerza=em[1])


def preparar_traje():
    limpiar()
    objs = importar_fbx("Astronauta.fbx")
    mats = {}
    for o in objs:
        if o.type != "MESH":
            continue
        me = o.data
        uv = me.uv_layers.active.data if me.uv_layers else None
        me.materials.clear()
        slots = {}
        for p in me.polygons:
            key = (0.0625, 0.75)
            if uv:
                u = sum(uv[i].uv[0] for i in p.loop_indices) / p.loop_total
                v = sum(uv[i].uv[1] for i in p.loop_indices) / p.loop_total
                key = (round(u * 16) / 16, round(v * 16) / 16)
            nombre, col, metal, rug, em = PALETA.get(key, ("Traje", (0.86, 0.87, 0.86), 0.0, 0.75, None))
            if nombre not in mats:
                mats[nombre] = material_traje(nombre, col, metal, rug, em)
            if nombre not in slots:
                me.materials.append(mats[nombre])
                slots[nombre] = len(me.materials) - 1
            p.material_index = slots[nombre]
        C.suavizar(o, 35)
    exportar_fbx_con_esqueleto("astronauta_traje", objs)
    render_previa("astronauta_traje", objs, 2.6)
    C.guardar_blend("astronauta_traje")


def exportar_fbx_con_esqueleto(nombre, objs):
    os.makedirs(C.UNITY_MODELS, exist_ok=True)
    ruta = os.path.join(C.UNITY_MODELS, nombre + ".fbx")
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=ruta, use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, path_mode="COPY", embed_textures=True,
    )
    print("[Blender] FBX ->", ruta)


# ---------------------------------------------------------------- 2. cuerpo anatómico

def preparar_anatomia():
    limpiar()
    importar_fbx("lv2__fbx.fbx")
    cuerpo = next(o for o in bpy.context.scene.objects if o.type == "MESH")
    cuerpo.name = "Anatomia"
    bpy.context.view_layer.objects.active = cuerpo
    cuerpo.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    me = cuerpo.data
    bm = bmesh.new()
    bm.from_mesh(me)
    # escala a 1,80 m, pies en z = 0, centrado en X/Y
    mn = mathutils.Vector((min(v.co.x for v in bm.verts), min(v.co.y for v in bm.verts), min(v.co.z for v in bm.verts)))
    mx = mathutils.Vector((max(v.co.x for v in bm.verts), max(v.co.y for v in bm.verts), max(v.co.z for v in bm.verts)))
    k = 1.80 / (mx.z - mn.z)
    cx, cy = (mn.x + mx.x) / 2, (mn.y + mx.y) / 2
    for v in bm.verts:
        v.co = mathutils.Vector(((v.co.x - cx) * k, (v.co.y - cy) * k, (v.co.z - mn.z) * k))

    # suavizar la zona genital (vista médica neutra)
    zona = [v for v in bm.verts if abs(v.co.x) < 0.1 and 0.78 < v.co.z < 0.98]
    ref = [v.co.y for v in bm.verts if 0.12 < abs(v.co.x) < 0.16 and 0.82 < v.co.z < 0.95]
    if zona and ref:
        y_lim = min(ref) + 0.015   # el frente mira hacia -Y
        for v in zona:
            if v.co.y < y_lim:
                v.co.y = y_lim
        for _ in range(12):
            bmesh.ops.smooth_vert(bm, verts=zona, factor=0.6, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(me)
    bm.free()

    zonas = {
        "Cabeza": (0.70, 0.73, 0.78), "Ojos": (0.70, 0.73, 0.78), "Torso": (0.70, 0.73, 0.78),
        "Brazos": (0.70, 0.73, 0.78), "Pelvis": (0.16, 0.19, 0.24), "Piernas": (0.70, 0.73, 0.78),
    }
    me.materials.clear()
    idx = {}
    for nombre, col in zonas.items():
        me.materials.append(C.material("Zona_" + nombre, col, 0.0, 0.55))
        idx[nombre] = len(me.materials) - 1
    for p in me.polygons:
        c, n = p.center, p.normal
        if c.z > 1.56:
            zona = "Ojos" if (1.62 < c.z < 1.70 and 0.012 < abs(c.x) < 0.07 and n.y < -0.5 and c.y < -0.05) else "Cabeza"
        elif abs(c.x) > 0.21 and c.z > 0.62:
            zona = "Brazos"
        elif c.z > 0.98:
            zona = "Torso"
        elif c.z > 0.80:
            zona = "Pelvis"
        else:
            zona = "Piernas"
        p.material_index = idx[zona]
    C.suavizar(cuerpo, 60)
    C.exportar_fbx("anatomia", [cuerpo])
    render_previa("anatomia", [cuerpo], 2.4)
    C.guardar_blend("anatomia")


# ---------------------------------------------------------------- 3. hábitat de superficie

COLORES_BASE = {
    "Piso": ((0.32, 0.31, 0.30), 0.0, 0.9), "Teto": ((0.82, 0.83, 0.84), 0.1, 0.6),
    "Base": ((0.85, 0.86, 0.86), 0.1, 0.55), "Janelas": ((0.05, 0.1, 0.16), 0.9, 0.05),
    "VidroPorta": ((0.05, 0.1, 0.16), 0.9, 0.05), "Porta": ((0.5, 0.52, 0.56), 0.6, 0.4),
}


def preparar_base():
    limpiar()
    objs = importar_fbx("Base.fbx")
    for o in objs:
        if o.type != "MESH":
            continue
        clave = next((k for k in COLORES_BASE if o.name.startswith(k)), None)
        col, metal, rug = COLORES_BASE.get(clave, ((0.55, 0.57, 0.6), 0.6, 0.4))
        C.asignar(o, C.material("Base_" + (clave or "Metal"), col, metal, rug))
        C.suavizar(o, 30)
    meshes = [o for o in objs if o.type == "MESH"]
    C.exportar_fbx("modulo_marte", meshes)
    C.exportar_fbx("modulo_lunar", meshes)
    render_previa("modulo_base", meshes, 1.6)
    C.guardar_blend("modulo_base")


preparar_traje()
preparar_anatomia()
preparar_base()
print("[Blender] Modelos del equipo listos")
