extends RefCounted
## Carga modelos 3D opcionales desde res://assets/models/ (descargados de NASA o exportados de FreeCAD).
## Si el archivo no existe devuelve null y el escenario usa la geometría procedural.
##
## Nombres que busca el proyecto (extensión .glb, .gltf u .obj):
##   nave_transito   -> nave de tránsito a Marte / Luna   (exportada por freecad/nave_spaceapps.py)
##   cohete_lanzador -> cohete en la plataforma (escenario Tierra)
##   estacion        -> estación espacial (escenario ISS)
##   astronauta      -> modelo de referencia (traje) junto al cuerpo de síntomas
##   modulo_marte    -> hábitat en la superficie de Marte
##   modulo_lunar    -> módulo de aterrizaje lunar

const MODEL_DIR := "res://assets/models/"

## Ajustes por modelo. FreeCAD trabaja en Z arriba y milímetros: los .obj exportados
## se giran -90° en X y se escalan automáticamente (si miden más de 500 unidades).
const FIXES := {}


static func try_load(base_name: String, target_size := 0.0) -> Node3D:
	for ext in ["glb", "gltf", "obj"]:
		var path: String = MODEL_DIR + base_name + "." + ext
		if not ResourceLoader.exists(path):
			continue
		var res: Resource = load(path)
		var model: Node3D = null
		if res is PackedScene:
			model = (res as PackedScene).instantiate() as Node3D
		elif res is Mesh:
			var mi := MeshInstance3D.new()
			mi.mesh = res as Mesh
			model = mi
		if model == null:
			continue
		var holder := Node3D.new()
		holder.name = base_name
		holder.add_child(model)
		var fix: Dictionary = FIXES.get(base_name, {})
		if ext == "obj" and fix.has("obj_rotate_x"):
			model.rotation_degrees.x = float(fix["obj_rotate_x"])
		elif ext != "obj" and fix.has("glb_rotate_x"):
			model.rotation_degrees.x = float(fix["glb_rotate_x"])
		_normalize(holder, model, target_size)
		print("[ModelLoader] usando modelo importado: ", path)
		return holder
	return null


## Escala mm -> m si hace falta, ajusta a target_size (si > 0) y apoya la base en y = 0.
static func _normalize(holder: Node3D, model: Node3D, target_size: float) -> void:
	var box := _aabb_of(holder)
	if box.size == Vector3.ZERO:
		return
	var longest := maxf(box.size.x, maxf(box.size.y, box.size.z))
	if target_size > 0.0:
		model.scale *= target_size / longest
	elif longest > 500.0:
		model.scale *= 0.001
	box = _aabb_of(holder)
	model.position -= Vector3(box.get_center().x, box.position.y, box.get_center().z)


static func _aabb_of(root: Node3D) -> AABB:
	var acc: Array = []
	for c in root.get_children():
		_collect(c, Transform3D.IDENTITY, acc)
	return acc[0] if not acc.is_empty() else AABB()


static func _collect(n: Node, parent_xf: Transform3D, acc: Array) -> void:
	var xf := parent_xf
	if n is Node3D:
		xf = parent_xf * (n as Node3D).transform
	if n is MeshInstance3D and (n as MeshInstance3D).mesh != null:
		var box: AABB = xf * (n as MeshInstance3D).mesh.get_aabb()
		if acc.is_empty():
			acc.append(box)
		else:
			acc[0] = (acc[0] as AABB).merge(box)
	for c in n.get_children():
		_collect(c, xf, acc)
