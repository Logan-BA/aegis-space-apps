# -*- coding: utf-8 -*-
"""Utilidades compartidas por los scripts de Blender del proyecto (Blender 4.2 o más reciente)."""
import os
import sys
import bpy

BASE = os.path.dirname(os.path.abspath(__file__))
PROYECTO = os.path.dirname(BASE)
ENTRADA = os.path.join(BASE, "entrada")          # OBJ que deja FreeCAD
SALIDA = os.path.join(BASE, "salida")            # .blend y renders
UNITY_MODELS = os.path.join(PROYECTO, "simulacion_unity", "Assets", "SpaceApps", "Resources", "Models")


def argumentos():
    """Argumentos después de '--' en la línea de comandos de Blender."""
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def escena_vacia():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0


def material(nombre, color, metal=0.0, rugosidad=0.5, emision=None, fuerza=0.0):
    """Material Principled BSDF. Color base, metal y rugosidad viajan al FBX; el resto es para renders."""
    m = bpy.data.materials.get(nombre) or bpy.data.materials.new(nombre)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*color, 1.0)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rugosidad
    if emision is not None:
        p.inputs["Emission Color"].default_value = (*emision, 1.0)
        p.inputs["Emission Strength"].default_value = fuerza
    m.diffuse_color = (*color, 1.0)
    return m


def agregar_rayado(m, escala=40.0, intensidad=0.15):
    """Desgaste procedural (ruido -> rugosidad y relieve) solo para renders de Blender."""
    nt = m.node_tree
    p = nt.nodes.get("Principled BSDF")
    ruido = nt.nodes.new("ShaderNodeTexNoise")
    ruido.inputs["Scale"].default_value = escala
    rampa = nt.nodes.new("ShaderNodeMapRange")
    rampa.inputs["To Min"].default_value = p.inputs["Roughness"].default_value
    rampa.inputs["To Max"].default_value = min(1.0, p.inputs["Roughness"].default_value + 0.3)
    nt.links.new(ruido.outputs["Fac"], rampa.inputs["Value"])
    nt.links.new(rampa.outputs["Result"], p.inputs["Roughness"])
    relieve = nt.nodes.new("ShaderNodeBump")
    relieve.inputs["Strength"].default_value = intensidad
    nt.links.new(ruido.outputs["Fac"], relieve.inputs["Height"])
    nt.links.new(relieve.outputs["Normal"], p.inputs["Normal"])


def asignar(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def suavizar(obj, angulo=40.0):
    """Sombreado suave por ángulo (compatible 4.1+)."""
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=angulo * 3.14159 / 180.0)
    except Exception:
        bpy.ops.object.shade_smooth()
    obj.select_set(False)


def biselar(obj, ancho=0.02, segmentos=2):
    mod = obj.modifiers.new("Bisel", "BEVEL")
    mod.width = ancho
    mod.segments = segmentos
    mod.limit_method = "ANGLE"
    mod.harden_normals = False
    obj.modifiers.new("Normales", "WEIGHTED_NORMAL")


def caja(nombre, tam, pos, mat, rot=(0, 0, 0), col=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=pos, rotation=rot)
    o = bpy.context.active_object
    o.name = nombre
    o.scale = tam
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    asignar(o, mat)
    mover(o, col)
    return o


def cilindro(nombre, r, h, pos, mat, rot=(0, 0, 0), lados=32, col=None, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=lados, location=pos, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(radius1=r, radius2=r2, depth=h, vertices=lados, location=pos, rotation=rot)
    o = bpy.context.active_object
    o.name = nombre
    asignar(o, mat)
    mover(o, col)
    return o


def coleccion(nombre):
    c = bpy.data.collections.get(nombre) or bpy.data.collections.new(nombre)
    if c.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(c)
    return c


def mover(obj, col):
    if col is None:
        return
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)


def tubo(nombre, puntos, radio, mat, col=None):
    """Tubería a partir de una polilínea (curva con bisel) convertida a malla."""
    cd = bpy.data.curves.new(nombre, "CURVE")
    cd.dimensions = "3D"
    cd.bevel_depth = radio
    cd.bevel_resolution = 3
    sp = cd.splines.new("POLY")
    sp.points.add(len(puntos) - 1)
    for i, p in enumerate(puntos):
        sp.points[i].co = (p[0], p[1], p[2], 1.0)
    o = bpy.data.objects.new(nombre, cd)
    (col or bpy.context.scene.collection).objects.link(o)
    cd.materials.append(mat)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    return bpy.context.active_object


def unir_por_material(col):
    """Une los objetos de una colección por material: menos draw calls en Unity."""
    grupos = {}
    for o in list(col.objects):
        if o.type != "MESH" or not o.data.materials:
            continue
        grupos.setdefault(o.data.materials[0].name, []).append(o)
    for nombre, objs in grupos.items():
        if len(objs) < 2:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.convert(target="MESH")   # aplica biseles de cada pieza antes de unir
        bpy.ops.object.join()
        bpy.context.active_object.name = nombre


def exportar_fbx(nombre, objetos=None):
    """FBX para Unity (metros, Y arriba). Unity lo carga desde Resources/Models/<nombre>."""
    os.makedirs(UNITY_MODELS, exist_ok=True)
    ruta = os.path.join(UNITY_MODELS, nombre + ".fbx")
    bpy.ops.object.select_all(action="DESELECT")
    for o in (objetos or bpy.context.scene.objects):
        if o.type == "MESH":
            o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=ruta, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True,
        mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True,
    )
    print("[Blender] FBX ->", ruta)
    return ruta


def guardar_blend(nombre):
    os.makedirs(SALIDA, exist_ok=True)
    ruta = os.path.join(SALIDA, nombre + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=ruta)
    print("[Blender] .blend ->", ruta)


def render(nombre, camara_pos, objetivo, lente=35, muestras=64):
    """Render de presentación (Eevee) en blender/salida/<nombre>.png."""
    esc = bpy.context.scene
    for motor in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            esc.render.engine = motor
            break
        except TypeError:
            continue
    try:
        esc.eevee.taa_render_samples = muestras
    except Exception:
        pass
    esc.render.resolution_x, esc.render.resolution_y = 1920, 1080
    cam_data = bpy.data.cameras.new("Camara")
    cam_data.lens = lente
    cam = bpy.data.objects.new("Camara", cam_data)
    esc.collection.objects.link(cam)
    cam.location = camara_pos
    import mathutils
    d = mathutils.Vector(objetivo) - mathutils.Vector(camara_pos)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    esc.camera = cam
    os.makedirs(SALIDA, exist_ok=True)
    esc.render.filepath = os.path.join(SALIDA, nombre + ".png")
    bpy.ops.render.render(write_still=True)
    print("[Blender] render ->", esc.render.filepath)
