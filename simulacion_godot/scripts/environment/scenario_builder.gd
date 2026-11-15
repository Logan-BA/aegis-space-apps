extends RefCounted
## Construye cada escenario 3D (cielo, luz, planetas, terreno y estructuras).
## El astronauta siempre está en el origen; el escenario se arma a su alrededor.
## Los nodos del grupo "spin" giran lentamente (lo hace main.gd).

const SKY_SHADER := preload("res://shaders/space_sky.gdshader")
const ATMO_SHADER := preload("res://shaders/atmosphere_glow.gdshader")
const TERRAIN_SHADER := preload("res://shaders/terrain.gdshader")
const Ship := preload("res://scripts/environment/ship_builder.gd")
const ModelLoader := preload("res://scripts/environment/model_loader.gd")
const Interior := preload("res://scripts/environment/interior_builder.gd")

const SCENE_LABELS := {
	"ship_interior": "Interior de la nave - pasillo principal",
	"earth": "Tierra - centro de entrenamiento",
	"leo": "Órbita baja - estación espacial",
	"deep_space_moon": "Espacio profundo - rumbo a la Luna",
	"deep_space_mars": "Espacio profundo - rumbo a Marte",
	"moon_surface": "Superficie lunar",
	"mars_surface": "Superficie de Marte",
}


static func build(scene_key: String) -> Node3D:
	var root := Node3D.new()
	root.name = "Escenario_" + scene_key
	match scene_key:
		"earth":
			_build_earth_ground(root)
		"leo":
			_build_leo(root)
		"deep_space_moon":
			_build_deep_space(root, false)
		"deep_space_mars":
			_build_deep_space(root, true)
		"moon_surface":
			_build_moon_surface(root)
		"mars_surface":
			_build_mars_surface(root)
		"ship_interior":
			Interior.build(root)
		_:
			_build_leo(root)
	return root


# ---------------------------------------------------------------- escenarios

static func _build_earth_ground(root: Node3D) -> void:
	_environment(root, 1.0, Color(0.22, 0.45, 0.85), Color(0.70, 0.82, 0.95), Color(0.3, 0.3, 0.3), 0.9, false)
	_sun(root, Vector3(-40, -35, 0), 1.3)
	_terrain(root, Color(0.22, 0.35, 0.15), Color(0.45, 0.48, 0.30), 3.0, 0.02, 11)
	# Plataforma de lanzamiento con el cohete (modelo de FreeCAD si existe)
	var rocket := ModelLoader.try_load("cohete_lanzador")
	if rocket == null:
		rocket = Ship.launcher()
	rocket.position = Vector3(45, 0, -120)
	root.add_child(rocket)
	var pad := Ship.part(root, Ship.box(Vector3(30, 1.0, 30)), Ship.mat(Color(0.5, 0.5, 0.5)), Vector3(45, -0.5, -120))
	pad.name = "Plataforma"
	Ship.part(root, Ship.box(Vector3(4, 110, 4)), Ship.mat(Color(0.35, 0.35, 0.38), 0.7, 0.4), Vector3(57, 55, -120), Vector3.ZERO, "TorreServicio")
	# Centro de entrenamiento
	Ship.part(root, Ship.box(Vector3(24, 8, 14)), Ship.mat(Color(0.85, 0.85, 0.82)), Vector3(-22, 4, -26), Vector3(0, 20, 0), "CentroEntrenamiento")
	# Piso de la pista de entrenamiento
	Ship.part(root, Ship.box(Vector3(10, 0.05, 10)), Ship.mat(Color(0.3, 0.3, 0.33)), Vector3(0, 0.02, 0), Vector3.ZERO, "Pista")


static func _build_leo(root: Node3D) -> void:
	_environment(root, 0.0, Color.BLACK, Color.BLACK, Color.BLACK, 0.12, true)
	_sun(root, Vector3(-25, -60, 0), 1.6)
	var station := ModelLoader.try_load("estacion", 100.0)
	if station == null:
		station = Ship.station()
	station.position = Vector3(5, -2, -65)
	station.rotation_degrees = Vector3(10, 25, 0)
	root.add_child(station)
	_planet(root, "Tierra", 3000.0, Vector3(0, -3400, -800), _earth_gradient(), Color(0.35, 0.6, 1.0), 0.012, 3)


static func _build_deep_space(root: Node3D, to_mars: bool) -> void:
	_environment(root, 0.0, Color.BLACK, Color.BLACK, Color.BLACK, 0.08, true)
	_sun(root, Vector3(-15, 70, 0), 1.5)
	var ship := ModelLoader.try_load("nave_transito")
	if ship == null:
		ship = Ship.transit_ship()
	var holder := Node3D.new()
	holder.name = "NaveHorizontal"
	holder.add_child(ship)
	ship.position = Vector3(0, -20, 0)      # centra la nave (~39 m) sobre su eje
	holder.rotation_degrees = Vector3(0, 20, 90)
	holder.position = Vector3(0, 0, -40)
	root.add_child(holder)
	if to_mars:
		_planet(root, "Marte", 140.0, Vector3(-500, 120, -1700), _mars_gradient(), Color(1.0, 0.55, 0.35), 0.02, 7)
		_planet(root, "TierraLejana", 6.0, Vector3(900, -150, 2500), _earth_gradient(), Color(0.35, 0.6, 1.0), 0.02, 3)
	else:
		_planet(root, "Luna", 120.0, Vector3(450, 80, -1500), _moon_gradient(), Color(0, 0, 0, 0), 0.03, 5)
		_planet(root, "Tierra", 90.0, Vector3(-900, -200, 2200), _earth_gradient(), Color(0.35, 0.6, 1.0), 0.012, 3)


static func _build_moon_surface(root: Node3D) -> void:
	_environment(root, 0.0, Color.BLACK, Color.BLACK, Color.BLACK, 0.1, true)
	_sun(root, Vector3(-18, -50, 0), 1.8)
	_terrain(root, Color(0.30, 0.30, 0.31), Color(0.66, 0.66, 0.64), 7.0, 0.015, 21)
	_rocks(root, Color(0.5, 0.5, 0.5), 60, 42)
	var lander := ModelLoader.try_load("modulo_lunar", 7.0)
	if lander == null:
		lander = Ship.lander()
	lander.position = Vector3(9, 0, -14)
	root.add_child(lander)
	_planet(root, "TierraEnCielo", 70.0, Vector3(-300, 420, -1400), _earth_gradient(), Color(0.35, 0.6, 1.0), 0.012, 3)


static func _build_mars_surface(root: Node3D) -> void:
	_environment(root, 0.85, Color(0.62, 0.45, 0.33), Color(0.86, 0.66, 0.48), Color(0.4, 0.2, 0.1), 0.55, true)
	_sun(root, Vector3(-30, -40, 0), 1.0, Color(1.0, 0.88, 0.75))
	_terrain(root, Color(0.45, 0.20, 0.10), Color(0.76, 0.45, 0.25), 9.0, 0.012, 33)
	_rocks(root, Color(0.42, 0.22, 0.14), 80, 7)
	var hab := ModelLoader.try_load("modulo_marte", 20.0)
	if hab == null:
		hab = Ship.mars_habitat()
	hab.position = Vector3(-16, 0, -18)
	hab.rotation_degrees.y = 30
	root.add_child(hab)
	var lander := Ship.lander()
	lander.position = Vector3(14, 0, -22)
	root.add_child(lander)


# ---------------------------------------------------------------- piezas comunes

static func _environment(root: Node3D, atmosphere: float, zenith: Color, horizon: Color, ground: Color, ambient: float, stars: bool) -> void:
	var sky_mat := ShaderMaterial.new()
	sky_mat.shader = SKY_SHADER
	sky_mat.set_shader_parameter("atmosphere", atmosphere)
	sky_mat.set_shader_parameter("zenith_color", zenith)
	sky_mat.set_shader_parameter("horizon_color", horizon)
	sky_mat.set_shader_parameter("ground_color", ground)
	if not stars:
		sky_mat.set_shader_parameter("star_threshold", 1.1)
	var sky := Sky.new()
	sky.sky_material = sky_mat
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = horizon.lerp(Color(0.55, 0.6, 0.75), 0.5) if atmosphere > 0.0 else Color(0.55, 0.6, 0.75)
	env.ambient_light_energy = ambient
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.glow_enabled = true
	env.glow_intensity = 0.6
	env.glow_bloom = 0.05
	var we := WorldEnvironment.new()
	we.environment = env
	root.add_child(we)


static func _sun(root: Node3D, rot_deg: Vector3, energy: float, color := Color(1.0, 0.97, 0.92)) -> void:
	var l := DirectionalLight3D.new()
	l.name = "Sol"
	l.rotation_degrees = rot_deg
	l.light_energy = energy
	l.light_color = color
	l.shadow_enabled = true
	l.directional_shadow_max_distance = 150.0
	root.add_child(l)


static func _planet(root: Node3D, planet_name: String, radius: float, pos: Vector3, gradient: Gradient, glow: Color, freq: float, seed_value: int) -> void:
	var noise := FastNoiseLite.new()
	noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
	noise.frequency = freq
	noise.fractal_octaves = 5
	noise.seed = seed_value
	var tex := NoiseTexture2D.new()
	tex.width = 1024
	tex.height = 512
	tex.seamless = true
	tex.noise = noise
	tex.color_ramp = gradient
	var m := StandardMaterial3D.new()
	m.albedo_texture = tex
	m.roughness = 0.9
	var sphere := SphereMesh.new()
	sphere.radius = radius
	sphere.height = radius * 2.0
	sphere.radial_segments = 96
	sphere.rings = 48
	var mi := MeshInstance3D.new()
	mi.name = planet_name
	mi.mesh = sphere
	mi.material_override = m
	mi.position = pos
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.add_to_group("spin")
	root.add_child(mi)
	if glow.a > 0.0:
		var atmo_mat := ShaderMaterial.new()
		atmo_mat.shader = ATMO_SHADER
		atmo_mat.set_shader_parameter("glow_color", glow)
		var shell := SphereMesh.new()
		shell.radius = radius * 1.035
		shell.height = radius * 2.07
		shell.radial_segments = 64
		shell.rings = 32
		var atmo := MeshInstance3D.new()
		atmo.name = planet_name + "_Atmosfera"
		atmo.mesh = shell
		atmo.material_override = atmo_mat
		atmo.position = pos
		atmo.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		root.add_child(atmo)


static func _terrain(root: Node3D, low: Color, high: Color, height: float, freq: float, seed_value: int) -> void:
	var noise := FastNoiseLite.new()
	noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
	noise.frequency = freq
	noise.fractal_octaves = 5
	noise.seed = seed_value
	var tex := NoiseTexture2D.new()
	tex.width = 512
	tex.height = 512
	tex.seamless = true
	tex.noise = noise
	var plane := PlaneMesh.new()
	plane.size = Vector2(400, 400)
	plane.subdivide_width = 255
	plane.subdivide_depth = 255
	var m := ShaderMaterial.new()
	m.shader = TERRAIN_SHADER
	m.set_shader_parameter("height_map", tex)
	m.set_shader_parameter("height_scale", height)
	m.set_shader_parameter("plane_size", 400.0)
	m.set_shader_parameter("color_low", low)
	m.set_shader_parameter("color_high", high)
	var mi := MeshInstance3D.new()
	mi.name = "Terreno"
	mi.mesh = plane
	mi.material_override = m
	mi.extra_cull_margin = height
	root.add_child(mi)


static func _rocks(root: Node3D, color: Color, count: int, seed_value: int) -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_value
	var m := Ship.mat(color, 0.0, 0.95)
	var rocks := Node3D.new()
	rocks.name = "Rocas"
	root.add_child(rocks)
	for i in count:
		var angle := rng.randf() * TAU
		var dist := rng.randf_range(3.0, 13.0)
		var r := rng.randf_range(0.08, 0.5)
		var s := SphereMesh.new()
		s.radius = r
		s.height = r * 1.2
		s.radial_segments = 8
		s.rings = 5
		var mi := MeshInstance3D.new()
		mi.mesh = s
		mi.material_override = m
		mi.position = Vector3(cos(angle) * dist, r * 0.3, sin(angle) * dist)
		mi.rotation = Vector3(rng.randf() * TAU, rng.randf() * TAU, 0)
		rocks.add_child(mi)


static func _earth_gradient() -> Gradient:
	return _gradient([0.0, 0.48, 0.52, 0.62, 0.75, 0.9],
		[Color(0.02, 0.10, 0.35), Color(0.06, 0.25, 0.55), Color(0.80, 0.75, 0.55),
		Color(0.18, 0.42, 0.15), Color(0.40, 0.35, 0.22), Color(0.95, 0.95, 0.97)])


static func _mars_gradient() -> Gradient:
	return _gradient([0.0, 0.4, 0.7, 1.0],
		[Color(0.35, 0.12, 0.06), Color(0.70, 0.30, 0.13), Color(0.85, 0.50, 0.28), Color(0.95, 0.85, 0.75)])


static func _moon_gradient() -> Gradient:
	return _gradient([0.0, 0.5, 1.0],
		[Color(0.25, 0.25, 0.26), Color(0.55, 0.55, 0.54), Color(0.85, 0.85, 0.83)])


static func _gradient(offsets: Array, colors: Array) -> Gradient:
	var g := Gradient.new()
	g.offsets = PackedFloat32Array(offsets)
	g.colors = PackedColorArray(colors)
	return g
