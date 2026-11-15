# -*- coding: utf-8 -*-
"""
Blender: pasillo interior de la nave (estilo de las imágenes de referencia del equipo):
marcos hexagonales biselados, piso de rejilla con textura, foso inferior, paredes de tuberías con
codos, tanques, pasarelas laterales con barandas, escalera, consolas con pantallas azules,
tiras de luz y compuertas con paneles en ambos extremos.

Salida:
  simulacion_unity/Assets/SpaceApps/Resources/Models/interior_nave.fbx   (Unity lo usa solo)
  blender/salida/interior_nave.blend (+ interior_nave.png con --render)

Uso:
  blender -b -P crear_interior_nave.py -- [--render]

Ejes en Blender: pasillo a lo largo de Y (de -24 a +24 m), X lateral, Z arriba, piso en Z = 0.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
import comun as C  # noqa: E402

LARGO = 48.0
PASO_MARCO = 8.0
MARCO = [(-1.8, 0.0), (1.8, 0.0), (3.0, 1.2), (3.0, 3.6), (1.8, 4.8), (-1.8, 4.8), (-3.0, 3.6), (-3.0, 1.2)]
GROSOR_MARCO = 0.35
rnd = random.Random(1977)


def textura_rejilla():
    n = 128
    img = bpy.data.images.new("Rejilla", n, n)
    px = []
    for j in range(n):
        for i in range(n):
            borde = i % 32 < 3 or j % 32 < 3
            fino = i % 8 == 0 or j % 8 == 0
            v = 0.55 if borde else 0.38 if fino else 0.05
            px += [v, v, v * 1.05, 1.0]
    img.pixels = px
    os.makedirs(C.SALIDA, exist_ok=True)
    img.filepath_raw = os.path.join(C.SALIDA, "rejilla.png")
    img.file_format = "PNG"
    img.save()
    return img


def materiales():
    m = {
        "metal": C.material("Metal_Pared", (0.20, 0.21, 0.23), 0.8, 0.45),
        "marco": C.material("Metal_Marco", (0.32, 0.33, 0.35), 0.85, 0.35),
        "tubo": C.material("Metal_Tubo", (0.16, 0.17, 0.19), 0.9, 0.35),
        "tubo_claro": C.material("Tubo_Aislado", (0.48, 0.49, 0.51), 0.3, 0.6),
        "puerta": C.material("Metal_Puerta", (0.55, 0.56, 0.58), 0.85, 0.3),
        "panel": C.material("Panel_Puerta", (0.35, 0.36, 0.38), 0.85, 0.4),
        "techo": C.material("Techo", (0.07, 0.07, 0.08), 0.6, 0.6),
        "luz": C.material("Tira_Luz", (0.9, 0.95, 1.0), 0.0, 0.3, emision=(0.85, 0.92, 1.0), fuerza=12.0),
        "pantalla": C.material("Pantalla", (0.1, 0.4, 0.9), 0.0, 0.2, emision=(0.15, 0.5, 1.0), fuerza=6.0),
    }
    for k in ("metal", "marco", "puerta", "panel"):
        C.agregar_rayado(m[k], escala=35.0, intensidad=0.08)
    rej = C.material("Rejilla", (0.6, 0.6, 0.62), 0.8, 0.5)
    nt = rej.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = textura_rejilla()
    nt.links.new(tex.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
    m["rejilla"] = rej
    return m


def desplazar_poligono(pts, t):
    """Desplaza un polígono convexo (sentido antihorario) hacia adentro una distancia t."""
    n = len(pts)
    lineas = []
    for i in range(n):
        (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % n]
        dx, dy = x1 - x0, y1 - y0
        L = math.hypot(dx, dy)
        nx, ny = -dy / L, dx / L      # normal hacia adentro (polígono antihorario)
        lineas.append(((x0 + nx * t, y0 + ny * t), (dx, dy)))
    res = []
    for i in range(n):
        (p, d), (q, e) = lineas[i - 1], lineas[i]
        den = d[0] * e[1] - d[1] * e[0]
        s = ((q[0] - p[0]) * e[1] - (q[1] - p[1]) * e[0]) / den
        res.append((p[0] + d[0] * s, p[1] + d[1] * s))
    return res


def marco(col, y, mats):
    """Anillo octogonal (perfil del marco) de 0.5 m de profundidad, con bisel."""
    ext = MARCO
    inn = desplazar_poligono(MARCO, GROSOR_MARCO)
    n = len(ext)
    verts = []
    for dy in (-0.25, 0.25):
        verts += [(x, y + dy, z) for x, z in ext]
        verts += [(x, y + dy, z) for x, z in inn]
    of, if_, ob, ib = 0, n, 2 * n, 3 * n
    caras = []
    for i in range(n):
        j = (i + 1) % n
        caras.append((of + i, of + j, if_ + j, if_ + i))      # frente
        caras.append((ob + i, ib + i, ib + j, ob + j))        # atrás
        caras.append((of + i, ob + i, ob + j, of + j))        # exterior
        caras.append((if_ + i, if_ + j, ib + j, ib + i))      # interior
    me = bpy.data.meshes.new("Marco")
    me.from_pydata(verts, [], caras)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("Marco", me)
    col.objects.link(o)
    C.asignar(o, mats["marco"])
    C.biselar(o, 0.03, 2)
    # Tiras de luz en los chaflanes superiores y el techo del marco
    for i in (3, 4, 5):
        a, b = inn[i], inn[(i + 1) % n]
        mx, mz = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        C.caja("TiraLuz", (L * 0.5, 0.08, 0.04), (mx, y - 0.26, mz), mats["luz"], rot=(0, -ang, 0), col=col)


def tuberias(col, mats, lado):
    y0, y1 = -LARGO / 2, LARGO / 2
    for i in range(16):
        z = 0.3 + i * 0.45 + rnd.uniform(-0.08, 0.08)
        x = lado * (4.0 + rnd.uniform(0.0, 1.8))
        r = rnd.uniform(0.05, 0.16)
        pts = [(x, y0, z)]
        y = y0
        while True:
            y += rnd.uniform(4.0, 12.0)
            if y >= y1:
                break
            # codo escalonado: la tubería salta a otra altura o profundidad
            if rnd.random() < 0.5:
                nz = z + rnd.choice((-0.3, 0.3))
                pts += [(x, y, z), (x, y + 0.4, nz)]
                z = nz
            else:
                nx = x + lado * rnd.choice((-0.25, 0.25))
                pts += [(x, y, z), (nx, y + 0.4, z)]
                x = nx
            y += 0.4
        pts.append((x, y1, z))
        C.tubo("Tubo", pts, r, mats["tubo_claro"] if rnd.random() < 0.2 else mats["tubo"], col)
    for k in range(10):
        y = y0 + 3 + k * 4.5 + rnd.uniform(-1, 1)
        x = lado * rnd.uniform(3.9, 5.2)
        C.cilindro("Bajante", 0.08, 6.6, (x, y, 3.3), mats["tubo"], lados=12, col=col)
        C.cilindro("Brida", 0.13, 0.08, (x, y, rnd.uniform(1.0, 5.5)), mats["marco"], lados=16, col=col)
    # Tanques horizontales arriba
    for dz in (6.0, 6.9):
        C.cilindro("Tanque", 0.42, LARGO - 1, (lado * 5.6, 0, dz), mats["tubo_claro"], rot=(math.pi / 2, 0, 0), lados=24, col=col)


def pasarela(col, mats, lado):
    C.caja("PasarelaLateral", (1.6, LARGO, 0.08), (lado * 3.9, 0, 2.6), mats["rejilla"], col=col)
    xr = lado * 3.15
    C.tubo("Baranda", [(xr, -LARGO / 2, 3.6), (xr, LARGO / 2, 3.6)], 0.03, mats["marco"], col)
    C.tubo("BarandaMedia", [(xr, -LARGO / 2, 3.1), (xr, LARGO / 2, 3.1)], 0.02, mats["marco"], col)
    y = -LARGO / 2 + 1
    while y < LARGO / 2:
        C.cilindro("Poste", 0.025, 1.0, (xr, y, 3.1), mats["marco"], lados=8, col=col)
        y += 2.0


def escalera(col, mats, lado, y0):
    pasos = 12
    for k in range(pasos):
        C.caja("Escalon", (1.0, 0.3, 0.05), (lado * 3.9, y0 + k * 0.35, 0.22 * (k + 1)), mats["rejilla"], col=col)
    C.tubo("BarandaEscalera", [(lado * 3.35, y0, 1.0), (lado * 3.35, y0 + pasos * 0.35, 3.6)], 0.03, mats["marco"], col)


def compuerta(col, mats, y, signo):
    C.caja("Compuerta", (6.2, 0.3, 5.0), (0, y, 2.4), mats["puerta"], col=col)
    f = y - signo * 0.18
    C.caja("PanelSuperior", (3.2, 0.1, 0.8), (0, f, 3.4), mats["panel"], col=col)
    C.caja("PanelInferior", (3.6, 0.1, 0.9), (0, f, 1.3), mats["panel"], col=col)
    for s in (-1, 1):
        C.caja("PanelLateral", (0.9, 0.1, 2.6), (s * 2.2, f, 2.4), mats["panel"], col=col)
    marco(col, y - signo * 0.6, mats)


def main():
    hacer_render = "--render" in C.argumentos()
    C.escena_vacia()
    mats = materiales()
    col = C.coleccion("interior_nave")

    C.caja("Pasarela", (3.6, LARGO, 0.12), (0, 0, -0.06), mats["rejilla"], col=col)
    C.caja("Riel", (0.12, LARGO, 0.08), (0, 0, 0.01), mats["marco"], col=col)
    C.caja("Foso", (14.0, LARGO, 0.1), (0, 0, -0.8), mats["metal"], col=col)
    C.caja("Techo", (14.0, LARGO, 0.3), (0, 0, 7.5), mats["techo"], col=col)
    for s in (-1, 1):
        C.caja("Pared", (0.2, LARGO, 8.6), (s * 7.0, 0, 3.4), mats["metal"], col=col)
        tuberias(col, mats, s)
        pasarela(col, mats, s)
        for k in range(3):
            y = -14 + k * 14
            C.caja("Consola", (0.4, 0.8, 1.2), (s * 3.25, y, 0.6), mats["metal"], col=col)
            C.caja("Pantalla", (0.03, 0.55, 0.4), (s * 3.04, y, 1.25), mats["pantalla"], rot=(0, s * 0.3, 0), col=col)
    escalera(col, mats, 1, 6.0)

    y = -LARGO / 2 + 4
    while y < LARGO / 2:
        marco(col, y, mats)
        y += PASO_MARCO
    compuerta(col, mats, -LARGO / 2, -1)
    compuerta(col, mats, LARGO / 2, 1)

    # UV de la rejilla: repetir el patrón cada 1.8 m
    for o in col.objects:
        if o.type == "MESH" and o.data.materials and o.data.materials[0].name == "Rejilla" and o.data.uv_layers:
            ox, oy = o.dimensions.x / 1.8, o.dimensions.y / 1.8
            for d in o.data.uv_layers.active.data:
                d.uv = (d.uv[0] * ox, d.uv[1] * oy)

    C.unir_por_material(col)
    C.exportar_fbx("interior_nave", list(col.objects))
    if hacer_render:
        for yy in range(-20, 21, 8):
            luz = bpy.data.lights.new("Luz", "POINT")
            luz.energy = 400
            lo = bpy.data.objects.new("Luz", luz)
            lo.location = (0, yy, 4.3)
            bpy.context.scene.collection.objects.link(lo)
        C.render("interior_nave", (0.0, -21.0, 1.7), (0.0, 24.0, 2.2), lente=24)
    C.guardar_blend("interior_nave")


main()
