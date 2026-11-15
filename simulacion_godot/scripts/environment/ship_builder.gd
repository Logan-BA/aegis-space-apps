extends RefCounted
## Geometría procedural de respaldo (metros, eje largo en +Y, base en y = 0).
## Usa las MISMAS medidas que la macro freecad/nave_spaceapps.py, así el modelo
## procedural y el exportado de FreeCAD son intercambiables.


static func mat(color: Color, metallic := 0.2, roughness := 0.6) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.metallic = metallic
	m.roughness = roughness
	return m


static func part(parent: Node3D, mesh: Mesh, material: Material, pos: Vector3, rot_deg := Vector3.ZERO, part_name := "") -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = material
	mi.position = pos
	mi.rotation_degrees = rot_deg
	if part_name != "":
		mi.name = part_name
	parent.add_child(mi)
	return mi


static func cyl(r_top: float, r_bottom: float, h: float, segments := 48) -> CylinderMesh:
	var c := CylinderMesh.new()
	c.top_radius = r_top
	c.bottom_radius = r_bottom
	c.height = h
	c.radial_segments = segments
	return c


static func box(size: Vector3) -> BoxMesh:
	var b := BoxMesh.new()
	b.size = size
	return b


## Nave de tránsito (Marte/Luna): motores, etapa de propulsión, hábitat con refugio,
## adaptador, módulo de servicio y cápsula. Altura total ~39 m.
static func transit_ship() -> Node3D:
	var root := Node3D.new()
	root.name = "NaveTransito"
	var white := mat(Color(0.88, 0.88, 0.86), 0.2, 0.5)
	var grey := mat(Color(0.55, 0.56, 0.6), 0.7, 0.35)
	var dark := mat(Color(0.15, 0.15, 0.17), 0.8, 0.4)
	var gold := mat(Color(0.85, 0.65, 0.25), 0.9, 0.3)
	var solar := mat(Color(0.10, 0.14, 0.38), 0.6, 0.25)
	var window := mat(Color(0.05, 0.12, 0.2), 0.9, 0.05)

	# Motores (3 toberas)
	for i in 3:
		var a := TAU * i / 3.0
		part(root, cyl(0.4, 1.2, 3.0), dark, Vector3(cos(a) * 2.5, 1.5, sin(a) * 2.5), Vector3.ZERO, "Tobera%d" % i)
	# Etapa de propulsión (tanques) y radiadores
	part(root, cyl(4.2, 4.2, 15.0), gold, Vector3(0, 10.5, 0), Vector3.ZERO, "EtapaPropulsion")
	part(root, box(Vector3(0.08, 10.0, 5.0)), white, Vector3(0, 10.5, 6.7), Vector3.ZERO, "Radiador1")
	part(root, box(Vector3(0.08, 10.0, 5.0)), white, Vector3(0, 10.5, -6.7), Vector3.ZERO, "Radiador2")
	# Hábitat (3 cubiertas, refugio interior) con franja de ventanas
	part(root, cyl(4.2, 4.2, 12.0), white, Vector3(0, 24.0, 0), Vector3.ZERO, "Habitat")
	part(root, cyl(4.22, 4.22, 0.6), window, Vector3(0, 26.0, 0), Vector3.ZERO, "Ventanas")
	# Paneles solares (2 alas de 20 x 6 m)
	for s in [-1.0, 1.0]:
		part(root, cyl(0.15, 0.15, 2.0, 12), grey, Vector3(s * 5.2, 24.0, 0), Vector3(0, 0, 90))
		part(root, box(Vector3(20.0, 0.06, 6.0)), solar, Vector3(s * 16.2, 24.0, 0), Vector3.ZERO, "PanelSolar")
	# Adaptador, módulo de servicio y cápsula
	part(root, cyl(1.0, 1.0, 2.0), grey, Vector3(0, 31.0, 0), Vector3.ZERO, "Adaptador")
	part(root, cyl(2.5, 2.5, 4.0), white, Vector3(0, 34.0, 0), Vector3.ZERO, "ModuloServicio")
	part(root, cyl(0.75, 2.5, 3.3), grey, Vector3(0, 37.65, 0), Vector3.ZERO, "Capsula")
	return root


## Cohete lanzador (tipo carga pesada): etapa central, 2 propulsores, etapa superior,
## cápsula y torre de escape. Altura total ~107 m.
static func launcher() -> Node3D:
	var root := Node3D.new()
	root.name = "CoheteLanzador"
	var orange := mat(Color(0.85, 0.45, 0.15), 0.0, 0.8)
	var white := mat(Color(0.92, 0.92, 0.9), 0.1, 0.5)
	var dark := mat(Color(0.15, 0.15, 0.17), 0.8, 0.4)
	var grey := mat(Color(0.6, 0.6, 0.64), 0.6, 0.4)

	for i in 4:
		var x := -1.5 + i * 1.0
		part(root, cyl(0.35, 0.7, 4.0), dark, Vector3(x, 2.0, 0), Vector3.ZERO, "Motor%d" % i)
	part(root, cyl(4.2, 4.2, 65.0), orange, Vector3(0, 36.5, 0), Vector3.ZERO, "EtapaCentral")
	for s in [-1.0, 1.0]:
		var x: float = s * (4.2 + 1.85 + 0.3)
		part(root, cyl(0.6, 1.3, 3.0), dark, Vector3(x, 2.5, 0), Vector3.ZERO, "ToberaBooster")
		part(root, cyl(1.85, 1.85, 54.0), white, Vector3(x, 31.0, 0), Vector3.ZERO, "Propulsor")
		part(root, cyl(0.05, 1.85, 5.0), white, Vector3(x, 60.5, 0), Vector3.ZERO, "OjivaPropulsor")
	part(root, cyl(2.5, 4.2, 4.0), orange, Vector3(0, 71.0, 0), Vector3.ZERO, "Interetapa")
	part(root, cyl(2.5, 2.5, 14.0), white, Vector3(0, 80.0, 0), Vector3.ZERO, "EtapaSuperior")
	part(root, cyl(2.5, 2.5, 2.0), grey, Vector3(0, 88.0, 0), Vector3.ZERO, "AdaptadorCarga")
	part(root, cyl(2.5, 2.5, 4.0), white, Vector3(0, 91.0, 0), Vector3.ZERO, "ModuloServicio")
	part(root, cyl(0.75, 2.5, 3.3), grey, Vector3(0, 94.65, 0), Vector3.ZERO, "Capsula")
	part(root, cyl(0.3, 0.75, 3.0), white, Vector3(0, 97.8, 0), Vector3.ZERO, "CubiertaEscape")
	part(root, cyl(0.2, 0.2, 6.0, 12), grey, Vector3(0, 102.3, 0), Vector3.ZERO, "TorreEscape")
	part(root, cyl(0.0, 0.25, 1.5, 12), dark, Vector3(0, 106.05, 0), Vector3.ZERO, "PuntaEscape")
	return root


## Estación en órbita baja (inspirada en una estación modular genérica, ~100 m de armadura).
static func station() -> Node3D:
	var root := Node3D.new()
	root.name = "Estacion"
	var white := mat(Color(0.86, 0.86, 0.84), 0.2, 0.5)
	var truss := mat(Color(0.6, 0.6, 0.62), 0.7, 0.4)
	var solar := mat(Color(0.12, 0.16, 0.38), 0.6, 0.25)
	var gold := mat(Color(0.8, 0.62, 0.3), 0.9, 0.3)
	part(root, box(Vector3(100, 2.5, 2.5)), truss, Vector3.ZERO, Vector3.ZERO, "Armadura")
	part(root, cyl(2.1, 2.1, 44.0), white, Vector3(0, -4.5, 0), Vector3(90, 0, 0), "ModulosPresurizados")
	part(root, cyl(2.1, 2.1, 16.0), white, Vector3(0, -4.5, 9.0), Vector3(0, 0, 90), "Nodo")
	part(root, cyl(1.6, 1.6, 6.0), gold, Vector3(0, -4.5, -24.0), Vector3(90, 0, 0), "ModuloServicio")
	for x in [-45.0, -33.0, 33.0, 45.0]:
		for z in [-19.0, 19.0]:
			part(root, box(Vector3(11.0, 0.08, 34.0)), solar, Vector3(x, 0, z), Vector3.ZERO, "PanelSolar")
	for x in [-16.0, 16.0]:
		part(root, box(Vector3(3.0, 0.1, 14.0)), white, Vector3(x, -1.5, 9.0), Vector3(0, 0, 30), "Radiador")
	return root


## Módulo de aterrizaje genérico (Luna).
static func lander() -> Node3D:
	var root := Node3D.new()
	root.name = "ModuloLunar"
	var gold := mat(Color(0.82, 0.62, 0.25), 0.9, 0.35)
	var grey := mat(Color(0.6, 0.6, 0.62), 0.6, 0.4)
	var white := mat(Color(0.88, 0.88, 0.86), 0.2, 0.5)
	part(root, box(Vector3(4.2, 2.0, 4.2)), gold, Vector3(0, 2.6, 0), Vector3.ZERO, "Descenso")
	part(root, cyl(1.6, 2.0, 3.0), white, Vector3(0, 5.1, 0), Vector3.ZERO, "Ascenso")
	for i in 4:
		var a := TAU * i / 4.0 + PI / 4.0
		var d := Vector3(cos(a), 0, sin(a))
		# Pata inclinada desde el pie (d*3.6, y=0) hasta el cuerpo (d*1.6, y=2.6)
		var foot := d * 3.6
		var attach := d * 1.6 + Vector3(0, 2.6, 0)
		var v := attach - foot
		var leg := part(root, cyl(0.08, 0.08, v.length(), 8), grey, (foot + attach) * 0.5, Vector3.ZERO, "Pata")
		var axis := Vector3.UP.cross(v.normalized()).normalized()
		leg.basis = Basis(axis, Vector3.UP.angle_to(v))
		part(root, cyl(0.45, 0.45, 0.1, 16), grey, d * 3.6 + Vector3(0, 0.05, 0), Vector3.ZERO, "Pie")
	return root


## Hábitat de superficie (Marte): domo + túnel + módulo cilíndrico.
static func mars_habitat() -> Node3D:
	var root := Node3D.new()
	root.name = "HabitatMarte"
	var white := mat(Color(0.9, 0.88, 0.84), 0.1, 0.6)
	var grey := mat(Color(0.55, 0.55, 0.58), 0.6, 0.4)
	var dome := SphereMesh.new()
	dome.radius = 5.0
	dome.height = 10.0
	dome.is_hemisphere = true
	part(root, dome, white, Vector3(0, 0, 0), Vector3.ZERO, "Domo")
	part(root, cyl(1.2, 1.2, 6.0), grey, Vector3(7.5, 1.2, 0), Vector3(0, 0, 90), "Tunel")
	part(root, cyl(2.4, 2.4, 9.0), white, Vector3(13.0, 2.4, 0), Vector3(90, 0, 0), "Modulo")
	return root
