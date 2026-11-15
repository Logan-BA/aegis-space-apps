# -*- coding: utf-8 -*-
"""
Macro FreeCAD (probada para la API de FreeCAD 0.21 / 1.0)
Proyecto NASA Space Apps - Salud de astronautas

Genera dos modelos 3D paramétricos y sus planos:
  1. Nave de tránsito (Luna/Marte): motores, etapa de propulsión, hábitat de
     3 cubiertas con refugio contra radiación, adaptador, módulo de servicio,
     cápsula, paneles solares y radiadores.
  2. Cohete lanzador de carga pesada: etapa central, 2 propulsores laterales,
     interetapa, etapa superior, cápsula y torre de escape.

Salidas en ../export/ (junto a esta carpeta):
  - NaveSpaceApps.FCStd           documento FreeCAD
  - nave_transito.step / .obj / .stl / .glb
  - cohete_lanzador.step / .obj / .stl / .glb
  - Plano_Nave.pdf, Plano_Cohete.pdf   (solo con interfaz gráfica)

Uso: FreeCAD > Macro > Macros... > seleccionar este archivo > Ejecutar.
Para copiar a Godot: los .glb/.obj de export/ van a simulacion_godot/assets/models/.

Unidades: milímetros (unidad nativa de FreeCAD). Cambie las medidas en P_NAVE y P_COHETE.
"""

import os
import glob

import FreeCAD as App
import Part

GUI = App.GuiUp
if GUI:
    import FreeCADGui as Gui

# ----------------------------------------------------------------- parámetros (mm)
P_NAVE = {
    "motor_n": 3, "motor_r_salida": 1200, "motor_r_garganta": 400, "motor_h": 3000, "motor_dist_eje": 2500,
    "etapa_r": 4200, "etapa_h": 15000,
    "radiador_h": 10000, "radiador_ancho": 5000, "radiador_esp": 80,
    "hab_r": 4200, "hab_h": 12000, "hab_pared": 150, "hab_cubiertas": 3,
    "refugio_r": 1250, "refugio_pared": 200,       # núcleo blindado con agua/polietileno
    "panel_largo": 20000, "panel_ancho": 6000, "panel_esp": 60, "brazo_largo": 2000,
    "adaptador_r": 1000, "adaptador_h": 2000,
    "sm_r": 2500, "sm_h": 4000,
    "capsula_r_base": 2500, "capsula_r_tope": 750, "capsula_h": 3300,
}

P_COHETE = {
    "x_offset": 40000,                               # se coloca al lado de la nave
    "motor_n": 4, "motor_r_salida": 700, "motor_r_garganta": 350, "motor_h": 4000,
    "central_r": 4200, "central_h": 65000,
    "booster_r": 1850, "booster_h": 54000, "booster_ojiva_h": 5000, "booster_sep": 300,
    "booster_tobera_r": 1300, "booster_tobera_h": 3000,
    "interetapa_h": 4000,
    "superior_r": 2500, "superior_h": 14000,
    "adaptador_h": 2000, "sm_h": 4000,
    "capsula_r_base": 2500, "capsula_r_tope": 750, "capsula_h": 3300,
    "les_cubierta_h": 3000, "les_torre_r": 200, "les_torre_h": 6000, "les_punta_h": 1500,
}

COLORES = {
    "blanco": (0.90, 0.90, 0.88), "gris": (0.55, 0.56, 0.60), "oscuro": (0.15, 0.15, 0.17),
    "oro": (0.85, 0.65, 0.25), "solar": (0.10, 0.14, 0.38), "naranja": (0.85, 0.45, 0.15),
    "vidrio": (0.05, 0.12, 0.20),
}

V = App.Vector
Z = V(0, 0, 1)

# ----------------------------------------------------------------- utilidades
try:
    BASE_DIR = os.path.dirname(os.path.abspath(__file__))
except NameError:
    BASE_DIR = os.path.join(os.path.expanduser("~"), "Desktop")
OUT_DIR = os.path.abspath(os.path.join(BASE_DIR, "..", "export"))
os.makedirs(OUT_DIR, exist_ok=True)

DOC_NAME = "NaveSpaceApps"
if DOC_NAME in App.listDocuments():
    App.closeDocument(DOC_NAME)
doc = App.newDocument(DOC_NAME)


def grupo(nombre, padre=None):
    g = doc.addObject("App::DocumentObjectGroup", nombre)
    if padre is not None:
        padre.addObject(g)
    return g


COLOR_DE = {}   # nombre de objeto -> clave de color (para el .mtl)


def agregar(nombre, forma, color, grp):
    obj = doc.addObject("Part::Feature", nombre)
    obj.Shape = forma
    grp.addObject(obj)
    COLOR_DE[obj.Name] = color
    if GUI:
        obj.ViewObject.ShapeColor = COLORES[color]
    return obj


def cilindro(r, h, z0, x=0.0, y=0.0):
    return Part.makeCylinder(r, h, V(x, y, z0), Z)


def cono(r1, r2, h, z0, x=0.0, y=0.0):
    return Part.makeCone(r1, r2, h, V(x, y, z0), Z)


def caja(lx, ly, lz, cx, cy, z0):
    """Caja centrada en (cx, cy) en planta, desde z0 hacia arriba."""
    return Part.makeBox(lx, ly, lz, V(cx - lx / 2.0, cy - ly / 2.0, z0))


# ----------------------------------------------------------------- 1. nave de tránsito
def construir_nave(p):
    g = grupo("Nave_Transito")
    objs = []
    z = 0.0

    import math
    gm = grupo("Motores", g)
    for i in range(p["motor_n"]):
        a = 2 * math.pi * i / p["motor_n"]
        x, y = math.cos(a) * p["motor_dist_eje"], math.sin(a) * p["motor_dist_eje"]
        s = cono(p["motor_r_salida"], p["motor_r_garganta"], p["motor_h"], z, x, y)
        objs.append(agregar("Tobera_%d" % (i + 1), s, "oscuro", gm))
    z += p["motor_h"]

    z_etapa = z
    objs.append(agregar("Etapa_Propulsion", cilindro(p["etapa_r"], p["etapa_h"], z), "oro", g))
    for signo, nombre in ((1, "Radiador_1"), (-1, "Radiador_2")):
        cy = signo * (p["etapa_r"] + p["radiador_ancho"] / 2.0)
        s = caja(p["radiador_esp"], p["radiador_ancho"], p["radiador_h"], 0, cy, z + (p["etapa_h"] - p["radiador_h"]) / 2.0)
        objs.append(agregar(nombre, s, "blanco", g))
    z += p["etapa_h"]

    # Hábitat hueco con cubiertas y refugio central
    gh = grupo("Habitat", g)
    z_hab = z
    ext = cilindro(p["hab_r"], p["hab_h"], z)
    inn = cilindro(p["hab_r"] - p["hab_pared"], p["hab_h"] - 2 * p["hab_pared"], z + p["hab_pared"])
    casco = ext.cut(inn)
    objs.append(agregar("Casco_Habitat", casco, "blanco", gh))
    alto_cub = (p["hab_h"] - 2 * p["hab_pared"]) / p["hab_cubiertas"]
    for k in range(1, p["hab_cubiertas"]):
        zc = z + p["hab_pared"] + k * alto_cub
        disco = cilindro(p["hab_r"] - p["hab_pared"], 80, zc).cut(cilindro(p["refugio_r"], 80, zc))
        objs.append(agregar("Cubierta_%d" % k, disco, "gris", gh))
    ref_ext = cilindro(p["refugio_r"], p["hab_h"] - 2 * p["hab_pared"], z + p["hab_pared"])
    ref_int = cilindro(p["refugio_r"] - p["refugio_pared"], p["hab_h"] - 2 * p["hab_pared"] - 400, z + p["hab_pared"] + 200)
    objs.append(agregar("Refugio_Radiacion", ref_ext.cut(ref_int), "oro", gh))
    franja = cilindro(p["hab_r"] + 20, 600, z + p["hab_h"] * 0.66).cut(cilindro(p["hab_r"], 600, z + p["hab_h"] * 0.66))
    objs.append(agregar("Ventanas", franja, "vidrio", gh))

    # Paneles solares
    gs = grupo("Paneles_Solares", g)
    zc = z + p["hab_h"] / 2.0
    for signo, nombre in ((1, "Ala_Derecha"), (-1, "Ala_Izquierda")):
        brazo = Part.makeCylinder(150, p["brazo_largo"], V(signo * p["hab_r"], 0, zc), V(signo, 0, 0))
        objs.append(agregar("Brazo_" + nombre, brazo, "gris", gs))
        cx = signo * (p["hab_r"] + p["brazo_largo"] + p["panel_largo"] / 2.0)
        panel = caja(p["panel_largo"], p["panel_ancho"], p["panel_esp"], cx, 0, zc - p["panel_esp"] / 2.0)
        objs.append(agregar("Panel_" + nombre, panel, "solar", gs))
    z += p["hab_h"]

    objs.append(agregar("Adaptador_Acople", cilindro(p["adaptador_r"], p["adaptador_h"], z), "gris", g))
    z += p["adaptador_h"]
    objs.append(agregar("Modulo_Servicio", cilindro(p["sm_r"], p["sm_h"], z), "blanco", g))
    z += p["sm_h"]
    objs.append(agregar("Capsula_Tripulacion", cono(p["capsula_r_base"], p["capsula_r_tope"], p["capsula_h"], z), "gris", g))
    z += p["capsula_h"]

    info = {
        "altura_total_m": z / 1000.0,
        "envergadura_m": 2 * (p["hab_r"] + p["brazo_largo"] + p["panel_largo"]) / 1000.0,
        "diametro_m": 2 * p["hab_r"] / 1000.0,
        "volumen_habitable_m3": math.pi * ((p["hab_r"] - p["hab_pared"]) ** 2) * (p["hab_h"] - 2 * p["hab_pared"]) / 1e9,
        "z_habitat": z_hab, "z_etapa": z_etapa,
    }
    return g, objs, info


# ----------------------------------------------------------------- 2. cohete lanzador
def construir_cohete(p):
    import math
    g = grupo("Cohete_Lanzador")
    objs = []
    x0 = p["x_offset"]
    z = 0.0

    gm = grupo("Motores_Centrales", g)
    for i in range(p["motor_n"]):
        a = 2 * math.pi * i / p["motor_n"] + math.pi / 4
        x, y = x0 + math.cos(a) * 1800, math.sin(a) * 1800
        objs.append(agregar("Motor_%d" % (i + 1), cono(p["motor_r_salida"], p["motor_r_garganta"], p["motor_h"], z, x, y), "oscuro", gm))
    z += p["motor_h"]
    objs.append(agregar("Etapa_Central", cilindro(p["central_r"], p["central_h"], z, x0), "naranja", g))

    gb = grupo("Propulsores", g)
    for signo, nombre in ((1, "Propulsor_Derecho"), (-1, "Propulsor_Izquierdo")):
        bx = x0 + signo * (p["central_r"] + p["booster_r"] + p["booster_sep"])
        zb = p["motor_h"] - p["booster_tobera_h"] + 1000
        objs.append(agregar("Tobera_" + nombre, cono(p["booster_tobera_r"], p["booster_r"] * 0.35, p["booster_tobera_h"], zb, bx), "oscuro", gb))
        objs.append(agregar(nombre, cilindro(p["booster_r"], p["booster_h"], zb + p["booster_tobera_h"], bx), "blanco", gb))
        objs.append(agregar("Ojiva_" + nombre, cono(p["booster_r"], 1, p["booster_ojiva_h"], zb + p["booster_tobera_h"] + p["booster_h"], bx), "blanco", gb))
    z += p["central_h"]

    objs.append(agregar("Interetapa", cono(p["central_r"], p["superior_r"], p["interetapa_h"], z, x0), "naranja", g))
    z += p["interetapa_h"]
    objs.append(agregar("Etapa_Superior", cilindro(p["superior_r"], p["superior_h"], z, x0), "blanco", g))
    z += p["superior_h"]
    objs.append(agregar("Adaptador_Carga", cilindro(p["superior_r"], p["adaptador_h"], z, x0), "gris", g))
    z += p["adaptador_h"]
    objs.append(agregar("Modulo_Servicio_L", cilindro(p["superior_r"], p["sm_h"], z, x0), "blanco", g))
    z += p["sm_h"]
    objs.append(agregar("Capsula_L", cono(p["capsula_r_base"], p["capsula_r_tope"], p["capsula_h"], z, x0), "gris", g))
    z += p["capsula_h"]
    gl = grupo("Sistema_Escape", g)
    objs.append(agregar("Cubierta_Escape", cono(p["capsula_r_tope"], 300, p["les_cubierta_h"], z, x0), "blanco", gl))
    z += p["les_cubierta_h"]
    objs.append(agregar("Torre_Escape", cilindro(p["les_torre_r"], p["les_torre_h"], z, x0), "gris", gl))
    z += p["les_torre_h"]
    objs.append(agregar("Punta_Escape", cono(250, 1, p["les_punta_h"], z, x0), "oscuro", gl))
    z += p["les_punta_h"]

    info = {
        "altura_total_m": z / 1000.0,
        "ancho_m": 2 * (p["central_r"] + 2 * p["booster_r"] + p["booster_sep"]) / 1000.0,
        "diametro_central_m": 2 * p["central_r"] / 1000.0,
    }
    return g, objs, info


# ----------------------------------------------------------------- exportación
def exportar(objs, base):
    rutas = []
    ruta = os.path.join(OUT_DIR, base + ".step")
    try:
        import Import
        Import.export(objs, ruta)
        rutas.append(ruta)
    except Exception as e:
        App.Console.PrintWarning("STEP no exportado: %s\n" % e)

    # STL unido (impresión 3D, en mm)
    try:
        import Mesh
        import MeshPart
        malla = Mesh.Mesh()
        for o in objs:
            malla.addMesh(MeshPart.meshFromShape(Shape=o.Shape, LinearDeflection=40.0, AngularDeflection=0.35, Relative=False))
        r = os.path.join(OUT_DIR, base + ".stl")
        malla.write(r)
        rutas.append(r)
    except Exception as e:
        App.Console.PrintWarning("STL no exportado: %s\n" % e)

    # OBJ por piezas con colores (.mtl), en METROS y con Y hacia arriba:
    # lo leen directo Blender, Unity y Godot sin girar ni escalar.
    try:
        rutas += exportar_obj_colores(objs, base)
    except Exception as e:
        App.Console.PrintWarning("OBJ no exportado: %s\n" % e)

    # glTF binario con colores (requiere interfaz gráfica)
    if GUI:
        try:
            import ImportGui
            r = os.path.join(OUT_DIR, base + ".glb")
            ImportGui.export(objs, r)
            rutas.append(r)
        except Exception as e:
            App.Console.PrintWarning("GLB no exportado (use el OBJ): %s\n" % e)
    return rutas


PROYECTO = os.path.abspath(os.path.join(BASE_DIR, ".."))
# Copias automáticas del OBJ: (carpeta raíz que debe existir, subcarpeta destino)
DESTINOS_OBJ = [
    ("simulacion_unity", os.path.join("simulacion_unity", "Assets", "SpaceApps", "Resources", "Models", "freecad")),
    ("blender", os.path.join("blender", "entrada")),
]


def exportar_obj_colores(objs, base, tolerancia_mm=30.0):
    lineas = ["# nave_spaceapps.py - unidades: metros, eje Y hacia arriba", "mtllib %s.mtl" % base]
    usados = set()
    desplazamiento = 1
    for o in objs:
        puntos, triangulos = o.Shape.tessellate(tolerancia_mm)
        if not triangulos:
            continue
        color = COLOR_DE.get(o.Name, "gris")
        usados.add(color)
        lineas.append("o %s" % o.Name)
        for p in puntos:
            # FreeCAD (X, Y, Z-arriba, mm) -> (X, Z, -Y) en metros: rotación pura, conserva el sentido de las caras
            lineas.append("v %.4f %.4f %.4f" % (p.x / 1000.0, p.z / 1000.0, -p.y / 1000.0))
        lineas.append("usemtl %s" % color)
        for t in triangulos:
            lineas.append("f %d %d %d" % (t[0] + desplazamiento, t[1] + desplazamiento, t[2] + desplazamiento))
        desplazamiento += len(puntos)
    mtl = ["# colores de nave_spaceapps.py"]
    for c in sorted(usados):
        r, g, b = COLORES[c]
        metal = c in ("oro", "gris", "oscuro")
        mtl += ["newmtl %s" % c, "Kd %.3f %.3f %.3f" % (r, g, b), "Ka 0 0 0",
                "Ks %s" % ("0.6 0.6 0.6" if metal else "0.15 0.15 0.15"), "Ns %d" % (200 if metal else 40), "d 1", ""]

    rutas = []
    extra = [os.path.join(PROYECTO, sub) for raiz, sub in DESTINOS_OBJ if os.path.isdir(os.path.join(PROYECTO, raiz))]
    for carpeta in [OUT_DIR] + extra:
        os.makedirs(carpeta, exist_ok=True)
        ro = os.path.join(carpeta, base + ".obj")
        with open(ro, "w", encoding="utf-8") as f:
            f.write("\n".join(lineas) + "\n")
        with open(os.path.join(carpeta, base + ".mtl"), "w", encoding="utf-8") as f:
            f.write("\n".join(mtl) + "\n")
        rutas.append(ro)
    return rutas


# ----------------------------------------------------------------- planos TechDraw
def buscar_plantilla():
    base = os.path.join(App.getResourceDir(), "Mod", "TechDraw", "Templates")
    for patron in ("**/A3_Landscape_blank.svg", "**/A3_Landscape*.svg", "**/A3*.svg", "**/*.svg"):
        hallados = glob.glob(os.path.join(base, patron), recursive=True)
        if hallados:
            return sorted(hallados)[0]
    return None


def plano(nombre, objs, escala, textos, iso_escala):
    try:
        import TechDraw  # noqa: F401
    except Exception as e:
        App.Console.PrintWarning("TechDraw no disponible: %s\n" % e)
        return None
    plantilla = buscar_plantilla()
    pagina = doc.addObject("TechDraw::DrawPage", nombre)
    tpl = doc.addObject("TechDraw::DrawSVGTemplate", "Plantilla_" + nombre)
    if plantilla:
        tpl.Template = plantilla
    pagina.Template = tpl

    # Vistas ortogonales: frontal, superior y lateral derecha (1.er diedro / ISO)
    grupo_p = doc.addObject("TechDraw::DrawProjGroup", "Vistas_" + nombre)
    pagina.addView(grupo_p)
    grupo_p.Source = objs
    grupo_p.ScaleType = "Custom"
    grupo_p.Scale = escala
    grupo_p.addProjection("Front")
    grupo_p.Anchor.Direction = V(0, -1, 0)
    grupo_p.Anchor.XDirection = V(1, 0, 0)
    grupo_p.addProjection("Top")
    grupo_p.addProjection("Right")
    grupo_p.X = 150
    grupo_p.Y = 150

    iso = doc.addObject("TechDraw::DrawViewPart", "Isometrica_" + nombre)
    pagina.addView(iso)
    iso.Source = objs
    iso.Direction = V(1, -1, 0.8)
    iso.ScaleType = "Custom"
    iso.Scale = iso_escala
    iso.X = 330
    iso.Y = 190

    nota = doc.addObject("TechDraw::DrawViewAnnotation", "Datos_" + nombre)
    pagina.addView(nota)
    nota.Text = textos
    nota.TextSize = 4.0
    nota.X = 330
    nota.Y = 70
    doc.recompute()
    if GUI:
        try:
            import TechDrawGui
            TechDrawGui.exportPageAsPdf(pagina, os.path.join(OUT_DIR, nombre + ".pdf"))
        except Exception as e:
            App.Console.PrintWarning("PDF no exportado (abra la página y use Archivo > Exportar PDF): %s\n" % e)
    return pagina


# ----------------------------------------------------------------- ejecución
g_nave, objs_nave, info_nave = construir_nave(P_NAVE)
g_cohete, objs_cohete, info_cohete = construir_cohete(P_COHETE)
doc.recompute()

rutas = []
rutas += exportar(objs_nave, "nave_transito")
rutas += exportar(objs_cohete, "cohete_lanzador")

plano("Plano_Nave", objs_nave, 1.0 / 300.0, [
    "NAVE DE TRANSITO - SPACE APPS",
    "Escala vistas 1:300 (mm)",
    "Altura total: %.1f m" % info_nave["altura_total_m"],
    "Envergadura paneles: %.1f m" % info_nave["envergadura_m"],
    "Diametro habitat: %.1f m" % info_nave["diametro_m"],
    "Volumen habitable: %.0f m3" % info_nave["volumen_habitable_m3"],
    "Refugio radiacion: D %.1f m" % (2 * P_NAVE["refugio_r"] / 1000.0),
    "Cubiertas: %d" % P_NAVE["hab_cubiertas"],
], 1.0 / 400.0)

plano("Plano_Cohete", objs_cohete, 1.0 / 500.0, [
    "COHETE LANZADOR - SPACE APPS",
    "Escala vistas 1:500 (mm)",
    "Altura total: %.1f m" % info_cohete["altura_total_m"],
    "Ancho con propulsores: %.1f m" % info_cohete["ancho_m"],
    "Diametro etapa central: %.1f m" % info_cohete["diametro_central_m"],
], 1.0 / 700.0)

fcstd = os.path.join(OUT_DIR, DOC_NAME + ".FCStd")
doc.saveAs(fcstd)
rutas.append(fcstd)

if GUI:
    Gui.activeDocument().activeView().viewIsometric()
    Gui.SendMsgToActiveView("ViewFit")

App.Console.PrintMessage("\n=== Nave Space Apps generada ===\n")
App.Console.PrintMessage("Nave: %.1f m de alto, %.1f m de envergadura\n" % (info_nave["altura_total_m"], info_nave["envergadura_m"]))
App.Console.PrintMessage("Cohete: %.1f m de alto\n" % info_cohete["altura_total_m"])
for r in rutas:
    App.Console.PrintMessage("  -> %s\n" % r)
