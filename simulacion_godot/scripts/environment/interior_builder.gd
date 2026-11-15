extends RefCounted
## Interior de la nave: pasillo con marcos hexagonales, piso de rejilla, paredes de
## tuberías, pasarelas laterales con barandas, tiras de luz y compuerta al fondo.
## Si existe res://assets/models/interior_nave.glb se usa ese modelo en su lugar.

const Ship := preload("res://scripts/environment/ship_builder.gd")
const ModelLoader := preload("res://scripts/environment/model_loader.gd")

const LENGTH := 48.0        # largo del pasillo (m), hacia -Z
const FRAME_STEP := 8.0     # separación entre marcos
const START_Z := 6.0

# Contorno del marco (x, y): hexágono alargado con esquinas achaflanadas
const FRAME_POINTS := [
	Vector2(-1.8, 0.0), Vector2(1.8, 0.0), Vector2(3.0, 1.2), Vector2(3.0, 3.6),
	Vector2(1.8, 4.8), Vector2(-1.8, 4.8), Vector2(-3.0, 3.6), Vector2(-3.0, 1.2),
]


static func build(root: Node3D) -> void:
	_environment(root)
	var model := ModelLoader.try_load("interior_nave")
	if model != null:
		root.add_child(model)
		_lights(root)
		return

	var metal := Ship.mat(Color(0.22, 0.23, 0.25), 0.8, 0.45)
	var frame_mat := Ship.mat(Color(0.32, 0.33, 0.35), 0.85, 0.35)
	var pipe_mat := Ship.mat(Color(0.18, 0.19, 0.21), 0.9, 0.35)
	var pipe_light := Ship.mat(Color(0.45, 0.46, 0.48), 0.6, 0.5)
	var grate := Ship.mat(Color(0.3, 0.31, 0.33), 0.8, 0.5)
	grate.albedo_texture = _grid_texture()
	grate.uv1_triplanar = true
	grate.uv1_scale = Vector3(1.5, 1.5, 1.5)
	var strip := StandardMaterial3D.new()
	strip.albedo_color = Color(0.9, 0.95, 1.0)
	strip.emission_enabled = true
	strip.emission = Color(0.85, 0.92, 1.0)
	strip.emission_energy_multiplier = 6.0
	var screen := StandardMaterial3D.new()
	screen.albedo_color = Color(0.1, 0.4, 0.9)
	screen.emission_enabled = true
	screen.emission = Color(0.2, 0.55, 1.0)
	screen.emission_energy_multiplier = 3.0

	var z_end := START_Z - LENGTH
	var mid_z := (START_Z + z_end) * 0.5

	# Piso central (pasarela de rejilla) y piso lateral más bajo
	Ship.part(root, Ship.box(Vector3(3.6, 0.12, LENGTH)), grate, Vector3(0, -0.06, mid_z), Vector3.ZERO, "Pasarela")
	Ship.part(root, Ship.box(Vector3(0.12, 0.08, LENGTH)), frame_mat, Vector3(0, 0.01, mid_z), Vector3.ZERO, "Riel")
	Ship.part(root, Ship.box(Vector3(14, 0.1, LENGTH)), metal, Vector3(0, -0.8, mid_z), Vector3.ZERO, "PisoInferior")
	# Techo
	Ship.part(root, Ship.box(Vector3(14, 0.3, LENGTH)), Ship.mat(Color(0.08, 0.08, 0.09), 0.6, 0.6), Vector3(0, 7.5, mid_z), Vector3.ZERO, "Techo")

	# Marcos hexagonales con tiras de luz
	var z := START_Z - 2.0
	while z > z_end + 1.0:
		_frame(root, z, frame_mat, strip)
		z -= FRAME_STEP

	# Paredes de tuberías (ambos lados)
	var rng := RandomNumberGenerator.new()
	rng.seed = 1977
	for side in [-1.0, 1.0]:
		for i in 14:
			var y := 0.3 + i * 0.5 + rng.randf_range(-0.1, 0.1)
			var x: float = side * (4.2 + rng.randf_range(0.0, 1.6))
			var r := rng.randf_range(0.06, 0.18)
			var m: StandardMaterial3D = pipe_light if rng.randf() < 0.2 else pipe_mat
			Ship.part(root, Ship.cyl(r, r, LENGTH, 12), m, Vector3(x, y, mid_z), Vector3(90, 0, 0), "Tubo")
		# Tanques grandes en la parte alta
		Ship.part(root, Ship.cyl(0.55, 0.55, LENGTH, 20), pipe_light, Vector3(side * 5.6, 6.4, mid_z), Vector3(90, 0, 0), "Tanque")
		# Bajantes verticales y codos
		var zz := START_Z - 5.0
		while zz > z_end + 2.0:
			var xx: float = side * rng.randf_range(4.0, 5.0)
			Ship.part(root, Ship.cyl(0.08, 0.08, 6.5, 10), pipe_mat, Vector3(xx, 3.25, zz), Vector3.ZERO, "Bajante")
			zz -= rng.randf_range(2.5, 5.0)
		# Pasarela lateral elevada con baranda
		Ship.part(root, Ship.box(Vector3(1.6, 0.08, LENGTH)), grate, Vector3(side * 3.9, 2.6, mid_z), Vector3.ZERO, "PasarelaLateral")
		Ship.part(root, Ship.cyl(0.03, 0.03, LENGTH, 8), frame_mat, Vector3(side * 3.15, 3.6, mid_z), Vector3(90, 0, 0), "Baranda")
		Ship.part(root, Ship.cyl(0.02, 0.02, LENGTH, 8), frame_mat, Vector3(side * 3.15, 3.1, mid_z), Vector3(90, 0, 0), "BarandaMedia")
		var pz := START_Z - 1.0
		while pz > z_end:
			Ship.part(root, Ship.cyl(0.025, 0.025, 1.0, 8), frame_mat, Vector3(side * 3.15, 3.1, pz), Vector3.ZERO, "Poste")
			pz -= 2.0
		# Pantallas azules
		for k in 3:
			var sz := START_Z - 10.0 - k * 14.0
			Ship.part(root, Ship.box(Vector3(0.05, 0.45, 0.35)), screen, Vector3(side * 3.35, 1.5, sz), Vector3.ZERO, "Pantalla")

	_bulkhead(root, z_end, frame_mat, strip)
	_lights(root)


static func _frame(root: Node3D, z: float, frame_mat: Material, strip: Material) -> void:
	var n := FRAME_POINTS.size()
	for i in n:
		var a: Vector2 = FRAME_POINTS[i]
		var b: Vector2 = FRAME_POINTS[(i + 1) % n]
		if i == 0:
			continue  # sin viga sobre el piso
		var mid := (a + b) * 0.5
		var d := b - a
		var beam := Ship.part(root, Ship.box(Vector3(d.length() + 0.35, 0.35, 0.5)), frame_mat, Vector3(mid.x, mid.y, z), Vector3(0, 0, rad_to_deg(d.angle())), "Marco")
		beam.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		# Tiras de luz en el techo y los chaflanes superiores
		if i == 3 or i == 4 or i == 5:
			var normal := Vector2(-d.y, d.x).normalized()  # hacia el interior del marco
			var p := mid + normal * 0.19
			Ship.part(root, Ship.box(Vector3(d.length() * 0.45, 0.04, 0.08)), strip, Vector3(p.x, p.y, z + 0.26), Vector3(0, 0, rad_to_deg(d.angle())), "TiraLuz")


static func _bulkhead(root: Node3D, z: float, frame_mat: Material, strip: Material) -> void:
	var door := Ship.mat(Color(0.55, 0.56, 0.58), 0.85, 0.3)
	var inset := Ship.mat(Color(0.35, 0.36, 0.38), 0.85, 0.4)
	Ship.part(root, Ship.box(Vector3(6.2, 5.0, 0.3)), door, Vector3(0, 2.4, z), Vector3.ZERO, "Compuerta")
	Ship.part(root, Ship.box(Vector3(3.2, 0.8, 0.1)), inset, Vector3(0, 3.4, z + 0.18), Vector3.ZERO, "PanelSuperior")
	Ship.part(root, Ship.box(Vector3(3.6, 0.9, 0.1)), inset, Vector3(0, 1.3, z + 0.18), Vector3.ZERO, "PanelInferior")
	for sx in [-1.0, 1.0]:
		Ship.part(root, Ship.box(Vector3(0.9, 2.6, 0.1)), inset, Vector3(sx * 2.2, 2.4, z + 0.18), Vector3.ZERO, "PanelLateral")
	_frame(root, z + 0.6, frame_mat, strip)


static func _lights(root: Node3D) -> void:
	var z := START_Z - 2.0
	while z > START_Z - LENGTH:
		var l := OmniLight3D.new()
		l.position = Vector3(0, 4.3, z)
		l.light_color = Color(0.85, 0.9, 1.0)
		l.light_energy = 1.6
		l.omni_range = 9.0
		l.shadow_enabled = z > START_Z - 20.0
		root.add_child(l)
		z -= 8.0
	var spot := SpotLight3D.new()
	spot.position = Vector3(0, 5.5, START_Z - LENGTH + 5.0)
	spot.rotation_degrees = Vector3(-60, 0, 0)
	spot.light_energy = 6.0
	spot.spot_range = 12.0
	spot.spot_angle = 40.0
	root.add_child(spot)
	# Luz azul tenue de las pantallas/tuberías
	for sx in [-1.0, 1.0]:
		var b := OmniLight3D.new()
		b.position = Vector3(sx * 4.0, 1.0, START_Z - 20.0)
		b.light_color = Color(0.2, 0.45, 1.0)
		b.light_energy = 0.8
		b.omni_range = 14.0
		root.add_child(b)


static func _environment(root: Node3D) -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.0, 0.0, 0.0)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.5, 0.55, 0.65)
	env.ambient_light_energy = 0.15
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.glow_enabled = true
	env.glow_intensity = 0.8
	env.glow_bloom = 0.08
	env.fog_enabled = true
	env.fog_light_color = Color(0.03, 0.035, 0.05)
	env.fog_density = 0.02
	env.ssao_enabled = true
	var we := WorldEnvironment.new()
	we.environment = env
	root.add_child(we)


## Textura de rejilla generada (para el piso tipo "grating").
static func _grid_texture() -> ImageTexture:
	var img := Image.create(64, 64, false, Image.FORMAT_RGB8)
	img.fill(Color(0.22, 0.23, 0.25))
	for i in 64:
		for j in 64:
			var border := i % 16 < 2 or j % 16 < 2
			var fine := i % 4 == 0 or j % 4 == 0
			if border:
				img.set_pixel(i, j, Color(0.55, 0.56, 0.58))
			elif fine:
				img.set_pixel(i, j, Color(0.38, 0.39, 0.41))
			else:
				img.set_pixel(i, j, Color(0.06, 0.06, 0.07))
	return ImageTexture.create_from_image(img)
