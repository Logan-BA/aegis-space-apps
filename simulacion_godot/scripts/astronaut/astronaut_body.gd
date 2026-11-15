extends Node3D
## Cuerpo humano procedural con zonas que se iluminan según los síntomas.
## - Verde -> amarillo -> rojo según la severidad de cada zona (0..1)
## - Piernas y brazos se adelgazan con la atrofia muscular ("piernas de pájaro")
## - La cabeza se hincha con el desplazamiento de fluidos
## - Un halo rojo aumenta con la dosis de radiación acumulada
## - En microgravedad adopta la postura neutra y flota

const BASE_SKIN := Color(0.80, 0.84, 0.90)
const BONE_COLOR := Color(0.95, 0.93, 0.85)

var _regions := {}                       # zona -> Array[MeshInstance3D]
var _severity := {}                      # zona -> 0..1
var _limb_meshes: Array[MeshInstance3D] = []
var _arm_meshes: Array[MeshInstance3D] = []
var _head: MeshInstance3D
var _aura: MeshInstance3D
var _aura_mat: StandardMaterial3D
var _body: Node3D
var _shoulders: Array[Node3D] = []
var _elbows: Array[Node3D] = []
var _hips: Array[Node3D] = []
var _knees: Array[Node3D] = []
var _floating := false
var _float_blend := 0.0
var _t := 0.0


func _ready() -> void:
	_build()


func _build() -> void:
	_body = Node3D.new()
	_body.name = "Cuerpo"
	add_child(_body)

	var pelvis := _add(_body, _box(Vector3(0.32, 0.16, 0.18)), Vector3(0, 0.98, 0), "pelvis", BONE_COLOR)
	pelvis.name = "Pelvis"
	var torso := _add(_body, _capsule(0.17, 0.62), Vector3(0, 1.32, 0), "chest", BASE_SKIN)
	torso.scale = Vector3(1.0, 1.0, 0.75)
	_add(_body, _cylinder(0.03, 0.62), Vector3(0, 1.30, -0.13), "spine", BONE_COLOR)
	_add(_body, _sphere(0.06), Vector3(0.05, 1.42, 0.10), "chest", Color(0.85, 0.35, 0.35))  # corazón
	_add(_body, _cylinder(0.06, 0.12), Vector3(0, 1.66, 0), "head", BASE_SKIN)
	_head = _add(_body, _sphere(0.12), Vector3(0, 1.80, 0), "head", BASE_SKIN)
	for sx in [-1.0, 1.0]:
		_add(_body, _sphere(0.022), Vector3(sx * 0.045, 1.82, 0.105), "eyes", Color(0.15, 0.2, 0.3))

	for sx in [-1.0, 1.0]:
		# Brazo
		var shoulder := Node3D.new()
		shoulder.position = Vector3(sx * 0.23, 1.56, 0)
		_body.add_child(shoulder)
		_shoulders.append(shoulder)
		_arm_meshes.append(_add(shoulder, _capsule(0.055, 0.32), Vector3(0, -0.16, 0), "arms", BASE_SKIN))
		var elbow := Node3D.new()
		elbow.position = Vector3(0, -0.32, 0)
		shoulder.add_child(elbow)
		_elbows.append(elbow)
		_arm_meshes.append(_add(elbow, _capsule(0.045, 0.28), Vector3(0, -0.14, 0), "arms", BASE_SKIN))
		_add(elbow, _sphere(0.05), Vector3(0, -0.31, 0), "arms", BASE_SKIN)
		# Pierna
		var hip := Node3D.new()
		hip.position = Vector3(sx * 0.10, 0.92, 0)
		_body.add_child(hip)
		_hips.append(hip)
		_limb_meshes.append(_add(hip, _capsule(0.075, 0.46), Vector3(0, -0.23, 0), "legs", BASE_SKIN))
		var knee := Node3D.new()
		knee.position = Vector3(0, -0.46, 0)
		hip.add_child(knee)
		_knees.append(knee)
		_limb_meshes.append(_add(knee, _capsule(0.06, 0.42), Vector3(0, -0.21, 0), "legs", BASE_SKIN))
		_add(knee, _box(Vector3(0.10, 0.06, 0.24)), Vector3(0, -0.44, 0.05), "legs", BASE_SKIN)

	# Halo de radiación
	_aura_mat = StandardMaterial3D.new()
	_aura_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_aura_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_aura_mat.albedo_color = Color(1.0, 0.15, 0.35, 0.0)
	_aura_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	_aura = MeshInstance3D.new()
	_aura.name = "HaloRadiacion"
	_aura.mesh = _capsule(0.45, 2.1)
	_aura.material_override = _aura_mat
	_aura.position = Vector3(0, 1.0, 0)
	_aura.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_body.add_child(_aura)


## severity: diccionario de health_model.region_severity()
func set_health(severity: Dictionary, muscle_pct: float, plasma_deficit: float, floating: bool) -> void:
	_severity = severity
	_floating = floating
	var k := lerpf(0.68, 1.0, clampf((muscle_pct - 70.0) / 30.0, 0.0, 1.0))
	for mi in _limb_meshes:
		mi.scale = Vector3(k, 1.0, k)
	var ka := lerpf(0.8, 1.0, clampf((muscle_pct - 70.0) / 30.0, 0.0, 1.0))
	for mi in _arm_meshes:
		mi.scale = Vector3(ka, 1.0, ka)
	if _head:
		var puff := 1.0 + plasma_deficit / 12.0 * 0.12
		_head.scale = Vector3(puff, puff * 0.97, puff)


func _process(delta: float) -> void:
	_t += delta
	var pulse := 0.65 + 0.35 * sin(_t * 4.0)
	for region in _regions.keys():
		var s: float = _severity.get(region, 0.0)
		var col := _severity_color(s)
		for mi: MeshInstance3D in _regions[region]:
			var m := mi.material_override as StandardMaterial3D
			var base: Color = mi.get_meta("base_color")
			m.albedo_color = base.lerp(col, clampf(s * 1.3, 0.0, 0.85))
			m.emission_enabled = s > 0.12
			m.emission = col
			m.emission_energy_multiplier = s * 1.6 * pulse
	var rad: float = _severity.get("radiation", 0.0)
	_aura_mat.albedo_color.a = rad * 0.32 * (0.8 + 0.2 * sin(_t * 2.0))
	_aura.visible = rad > 0.02

	# Postura: de pie (0) <-> postura neutra flotando (1)
	_float_blend = move_toward(_float_blend, 1.0 if _floating else 0.0, delta * 0.8)
	var f := _float_blend
	for i in 2:
		var side := -1.0 if i == 0 else 1.0
		_shoulders[i].rotation = Vector3(deg_to_rad(-35.0 * f), 0, deg_to_rad(side * 25.0 * f))
		_elbows[i].rotation = Vector3(deg_to_rad(-45.0 * f), 0, 0)
		_hips[i].rotation = Vector3(deg_to_rad(-40.0 * f), 0, deg_to_rad(side * 6.0 * f))
		_knees[i].rotation = Vector3(deg_to_rad(55.0 * f), 0, 0)
	_body.position.y = f * (0.35 + sin(_t * 0.8) * 0.08)
	_body.rotation.z = f * sin(_t * 0.37) * 0.15
	_body.rotation.x = f * sin(_t * 0.29) * 0.1


static func _severity_color(s: float) -> Color:
	var green := Color(0.25, 0.85, 0.45)
	var yellow := Color(1.0, 0.85, 0.2)
	var red := Color(1.0, 0.18, 0.12)
	if s < 0.5:
		return green.lerp(yellow, s * 2.0)
	return yellow.lerp(red, (s - 0.5) * 2.0)


# ---------------------------------------------------------------- helpers

func _add(parent: Node3D, mesh: Mesh, pos: Vector3, region: String, base: Color) -> MeshInstance3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = base
	m.roughness = 0.55
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = m
	mi.position = pos
	mi.set_meta("base_color", base)
	parent.add_child(mi)
	if not _regions.has(region):
		_regions[region] = []
	(_regions[region] as Array).append(mi)
	return mi


static func _capsule(r: float, h: float) -> CapsuleMesh:
	var c := CapsuleMesh.new()
	c.radius = r
	c.height = h
	c.radial_segments = 24
	c.rings = 8
	return c


static func _cylinder(r: float, h: float) -> CylinderMesh:
	var c := CylinderMesh.new()
	c.top_radius = r
	c.bottom_radius = r
	c.height = h
	c.radial_segments = 16
	return c


static func _sphere(r: float) -> SphereMesh:
	var s := SphereMesh.new()
	s.radius = r
	s.height = r * 2.0
	s.radial_segments = 24
	s.rings = 12
	return s


static func _box(size: Vector3) -> BoxMesh:
	var b := BoxMesh.new()
	b.size = size
	return b
