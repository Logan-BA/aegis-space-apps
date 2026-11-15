extends Node3D
## Escena principal: une el modelo de salud, los escenarios 3D, el astronauta y el panel.
## Controles: clic derecho + arrastrar = girar cámara · rueda = zoom · Espacio = iniciar/pausar

const HealthModel := preload("res://scripts/health/health_model.gd")
const Data := preload("res://scripts/health/mission_scenarios.gd")
const ScenarioBuilder := preload("res://scripts/environment/scenario_builder.gd")
const ModelLoader := preload("res://scripts/environment/model_loader.gd")
const AstronautBody := preload("res://scripts/astronaut/astronaut_body.gd")
const OrbitCamera := preload("res://scripts/camera/orbit_camera.gd")
const LineChart := preload("res://scripts/ui/line_chart.gd")

const INTERIOR_CAPABLE := ["leo", "moon_transit", "mars_transit"]

# --- Estado de la simulación ---
var crew: Array = []                 # Array de HealthModel
var selected := 0
var cm := {
	"exercise": 0.8, "shielding": 0.5, "shelter": true,
	"co2_scrubber": 0.3, "support": 0.6, "artificial_g": 0.0,
}
var solar_activity := 0.5
var conditioning_days := 14
var mission_index := 4
var free_scenario := "leo"
var interior_view := false
var legs: Array = []                 # [[clave_escenario, días], ...]
var leg_index := 0
var day_in_leg := 0
var mission_day := 0
var running := false
var finished := false
var days_per_second := 8.0
var _accum := 0.0
var _dirty := true
var rng := RandomNumberGenerator.new()

# --- Nodos ---
var current_scene_key := ""
var scenario_node: Node3D
var astronaut                        # AstronautBody (Node3D con set_health)
var reference_suit: Node3D
var camera: Camera3D
var spe_flash := 0.0
var ui := {}
var log_lines: Array[String] = []


func _ready() -> void:
	rng.randomize()
	for c in Data.DEFAULT_CREW:
		crew.append(HealthModel.new(c[0], c[1], c[2], conditioning_days))
	camera = OrbitCamera.new()
	add_child(camera)
	camera.current = true
	astronaut = AstronautBody.new()
	astronaut.name = "Astronauta"
	add_child(astronaut)
	reference_suit = ModelLoader.try_load("astronauta", 1.85)
	if reference_suit:
		reference_suit.position = Vector3(1.3, 0, 0)
		add_child(reference_suit)
	_build_ui()
	_apply_mission(mission_index)


# ================================================================ simulación

func _apply_mission(idx: int) -> void:
	mission_index = idx
	for h in crew:
		h.conditioning_days = conditioning_days
		h.reset()
	legs.clear()
	var mission_legs: Array = Data.MISSIONS[idx]["legs"]
	if not mission_legs.is_empty():
		if conditioning_days > 0:
			legs.append(["conditioning", conditioning_days])
		for l in mission_legs:
			legs.append(l)
	leg_index = 0
	day_in_leg = 0
	mission_day = 0
	finished = false
	running = false
	_accum = 0.0
	log_lines.clear()
	_log("Misión: %s" % Data.MISSIONS[idx]["label"])
	if conditioning_days < 14:
		_log("[color=orange]Advertencia: acondicionamiento menor a 14 días[/color]")
	ui["scen_opt"].disabled = not legs.is_empty()
	_update_scene()
	_update_play_button()
	_dirty = true


func _current_env_key() -> String:
	if legs.is_empty():
		return free_scenario
	return legs[mini(leg_index, legs.size() - 1)][0]


func _current_env() -> Dictionary:
	return Data.SCENARIOS[_current_env_key()]


func _sim_day() -> void:
	var env := _current_env()
	# Evento de partículas solares (afecta a toda la tripulación el mismo día)
	var spe_raw := 0.0
	if rng.randf() < 0.002 + 0.006 * solar_activity:
		spe_raw = rng.randf_range(30.0, 400.0)
		if rng.randf() < 0.1:
			spe_raw = rng.randf_range(400.0, 1200.0)
		if float(env["spe_exposure"]) > 0.0:
			spe_flash = 1.0
			_log("[color=#ff9a4a]Día %d: tormenta solar (%.0f mSv sin blindaje)%s[/color]" % [
				mission_day + 1, spe_raw, " - tripulación en el refugio" if cm["shelter"] else ""])
	for h in crew:
		for a in h.step_day(env, cm, spe_raw):
			_log("Día %d: %s" % [h.day, a])
	mission_day += 1

	if legs.is_empty():
		_dirty = true
		return
	day_in_leg += 1
	if day_in_leg >= int(legs[leg_index][1]):
		leg_index += 1
		day_in_leg = 0
		if leg_index >= legs.size():
			leg_index = legs.size() - 1
			finished = true
			running = false
			_update_play_button()
			_log("[b]Misión completada en %d días[/b]" % mission_day)
		else:
			_log("[b]Nueva fase: %s[/b]" % Data.SCENARIOS[legs[leg_index][0]]["label"])
			_update_scene()
	_dirty = true


func _total_days() -> float:
	if legs.is_empty():
		return maxf(365.0, mission_day * 1.1)
	var t := 0
	for l in legs:
		t += int(l[1])
	return float(t)


# ================================================================ escena 3D

func _update_scene() -> void:
	var key: String = _current_env()["scene"]
	if interior_view and _current_env_key() in INTERIOR_CAPABLE:
		key = "ship_interior"
	if key == current_scene_key:
		return
	current_scene_key = key
	if scenario_node:
		scenario_node.queue_free()
	scenario_node = ScenarioBuilder.build(key)
	add_child(scenario_node)
	if ui.has("scene_label"):
		ui["scene_label"].text = ScenarioBuilder.SCENE_LABELS.get(key, key)


func _process(delta: float) -> void:
	if running and not finished:
		_accum += delta * days_per_second
		var steps := 0
		while _accum >= 1.0 and steps < 60 and not finished:
			_accum -= 1.0
			steps += 1
			_sim_day()

	var h = crew[selected]
	var env := _current_env()
	var g: float = env["gravity"]
	if env.get("transit", false):
		g += float(cm["artificial_g"])
	astronaut.set_health(h.region_severity(), h.muscle, h.plasma_deficit, g < 0.05)

	if scenario_node:
		for n in get_tree().get_nodes_in_group("spin"):
			(n as Node3D).rotate_y(delta * 0.01)

	spe_flash = maxf(0.0, spe_flash - delta * 0.8)
	ui["flash"].color = Color(1.0, 0.45, 0.1, spe_flash * 0.28)

	if _dirty:
		_dirty = false
		_refresh_ui()


func _unhandled_key_input(event: InputEvent) -> void:
	var k := event as InputEventKey
	if k and k.pressed and not k.echo and k.keycode == KEY_SPACE:
		_on_play_pressed()


# ================================================================ interfaz

func _build_ui() -> void:
	var layer := CanvasLayer.new()
	add_child(layer)
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(root)

	var flash := ColorRect.new()
	flash.set_anchors_preset(Control.PRESET_FULL_RECT)
	flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	flash.color = Color(1, 0.45, 0.1, 0)
	root.add_child(flash)
	ui["flash"] = flash

	# ---------- Barra superior
	var top := _panel(root)
	top.set_anchors_preset(Control.PRESET_TOP_WIDE)
	top.offset_bottom = 48
	var hb := HBoxContainer.new()
	hb.add_theme_constant_override("separation", 10)
	top.add_child(hb)
	hb.add_child(_label("Misión:"))
	var mission_opt := OptionButton.new()
	for i in Data.MISSIONS.size():
		mission_opt.add_item(Data.MISSIONS[i]["label"], i)
	mission_opt.select(mission_index)
	mission_opt.item_selected.connect(func(i: int) -> void: _apply_mission(i))
	hb.add_child(mission_opt)
	hb.add_child(_label("Escenario:"))
	var scen_opt := OptionButton.new()
	for i in Data.SCENARIO_ORDER.size():
		scen_opt.add_item(Data.SCENARIOS[Data.SCENARIO_ORDER[i]]["label"], i)
	scen_opt.select(Data.SCENARIO_ORDER.find(free_scenario))
	scen_opt.item_selected.connect(_on_free_scenario)
	hb.add_child(scen_opt)
	ui["scen_opt"] = scen_opt
	var view_btn := Button.new()
	view_btn.text = "Vista: Exterior"
	view_btn.tooltip_text = "En órbita y en tránsito, alterna entre la nave por fuera y el pasillo interior"
	view_btn.pressed.connect(func() -> void:
		interior_view = not interior_view
		view_btn.text = "Vista: Interior" if interior_view else "Vista: Exterior"
		_update_scene())
	hb.add_child(view_btn)
	var play := Button.new()
	play.custom_minimum_size.x = 110
	play.pressed.connect(_on_play_pressed)
	hb.add_child(play)
	ui["play"] = play
	var reset := Button.new()
	reset.text = "Reiniciar"
	reset.pressed.connect(func() -> void: _apply_mission(mission_index))
	hb.add_child(reset)
	hb.add_child(_label("Días/seg:"))
	var speed := HSlider.new()
	speed.min_value = 1
	speed.max_value = 60
	speed.value = days_per_second
	speed.custom_minimum_size = Vector2(120, 0)
	speed.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	speed.value_changed.connect(func(v: float) -> void: days_per_second = v)
	hb.add_child(speed)
	var csv := Button.new()
	csv.text = "Exportar CSV"
	csv.pressed.connect(_export_csv)
	hb.add_child(csv)
	var day_label := _label("")
	day_label.add_theme_font_size_override("font_size", 16)
	hb.add_child(day_label)
	ui["day_label"] = day_label

	var scene_label := _label("")
	scene_label.add_theme_font_size_override("font_size", 20)
	scene_label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.9))
	scene_label.set_anchors_preset(Control.PRESET_CENTER_TOP)
	scene_label.offset_top = 56
	scene_label.offset_left = -300
	scene_label.offset_right = 300
	scene_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root.add_child(scene_label)
	ui["scene_label"] = scene_label
	var env_label := _label("")
	env_label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.9))
	env_label.set_anchors_preset(Control.PRESET_CENTER_TOP)
	env_label.offset_top = 84
	env_label.offset_left = -300
	env_label.offset_right = 300
	env_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root.add_child(env_label)
	ui["env_label"] = env_label

	# ---------- Panel izquierdo: tripulación y contramedidas
	var left := _panel(root)
	left.set_anchors_preset(Control.PRESET_LEFT_WIDE)
	left.offset_top = 56
	left.offset_right = 300
	left.offset_bottom = -8
	var ls := ScrollContainer.new()
	ls.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	left.add_child(ls)
	var lv := VBoxContainer.new()
	lv.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	ls.add_child(lv)

	lv.add_child(_title("Tripulación"))
	var crew_opt := OptionButton.new()
	for i in crew.size():
		crew_opt.add_item("%s (%s)" % [crew[i].crew_name, crew[i].role], i)
	crew_opt.item_selected.connect(func(i: int) -> void:
		selected = i
		(ui["fitness"] as HSlider).set_value_no_signal(crew[i].fitness)
		(ui["fitness_lbl"] as Label).text = "Condición física: %.2f" % crew[i].fitness
		_dirty = true)
	lv.add_child(crew_opt)
	_slider(lv, "Condición física", 0.0, 1.0, 0.05, crew[selected].fitness, "fitness", func(v: float) -> void:
		crew[selected].fitness = v
		_dirty = true)
	lv.add_child(_label("Acondicionamiento previo (días, mín. 14):"))
	var cond := SpinBox.new()
	cond.min_value = 0
	cond.max_value = 60
	cond.value = conditioning_days
	cond.value_changed.connect(func(v: float) -> void:
		conditioning_days = int(v)
		_apply_mission(mission_index))
	lv.add_child(cond)

	lv.add_child(_title("Contramedidas"))
	_slider(lv, "Ejercicio diario", 0.0, 1.0, 0.05, cm["exercise"], "", func(v: float) -> void: cm["exercise"] = v)
	_slider(lv, "Blindaje de la nave", 0.0, 1.0, 0.05, cm["shielding"], "", func(v: float) -> void: cm["shielding"] = v)
	var shelter := CheckBox.new()
	shelter.text = "Refugio para tormentas solares"
	shelter.button_pressed = cm["shelter"]
	shelter.toggled.connect(func(b: bool) -> void: cm["shelter"] = b)
	lv.add_child(shelter)
	_slider(lv, "Depurador de CO₂", 0.0, 1.0, 0.05, cm["co2_scrubber"], "", func(v: float) -> void: cm["co2_scrubber"] = v)
	_slider(lv, "Apoyo psicológico", 0.0, 1.0, 0.05, cm["support"], "", func(v: float) -> void: cm["support"] = v)
	_slider(lv, "Gravedad artificial en tránsito (g)", 0.0, 0.5, 0.05, cm["artificial_g"], "", func(v: float) -> void: cm["artificial_g"] = v)
	lv.add_child(_title("Entorno"))
	_slider(lv, "Actividad solar", 0.0, 1.0, 0.05, solar_activity, "", func(v: float) -> void: solar_activity = v)
	var help := _label("Clic derecho + arrastrar: girar · Rueda: zoom · Espacio: iniciar/pausar")
	help.autowrap_mode = TextServer.AUTOWRAP_WORD
	help.add_theme_color_override("font_color", Color(0.6, 0.65, 0.75))
	lv.add_child(help)

	# ---------- Panel derecho: indicadores y síntomas
	var right := _panel(root)
	right.set_anchors_preset(Control.PRESET_RIGHT_WIDE)
	right.offset_left = -340
	right.offset_top = 56
	right.offset_bottom = -8
	var rs := ScrollContainer.new()
	rs.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	right.add_child(rs)
	var rv := VBoxContainer.new()
	rv.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	rs.add_child(rv)
	var who := _title("")
	rv.add_child(who)
	ui["who"] = who
	var hi := _label("")
	hi.add_theme_font_size_override("font_size", 22)
	rv.add_child(hi)
	ui["health_label"] = hi
	ui["bars"] = {}
	for spec in [
		["bone", "Densidad ósea (%)", 70.0, 100.0],
		["muscle", "Masa muscular (%)", 60.0, 100.0],
		["vo2", "VO₂ máx (%)", 60.0, 100.0],
		["plasma", "Déficit de plasma (%)", 0.0, 15.0],
		["sans", "Índice SANS (visión)", 0.0, 1.0],
		["dose", "Dosis acumulada (mSv)", 0.0, 1000.0],
		["cancer", "Riesgo de cáncer adicional (%)", 0.0, 6.0],
		["stress", "Estrés (0-100)", 0.0, 100.0],
	]:
		var l := _label(spec[1])
		rv.add_child(l)
		var bar := ProgressBar.new()
		bar.min_value = spec[2]
		bar.max_value = spec[3]
		bar.show_percentage = false
		bar.custom_minimum_size = Vector2(0, 10)
		rv.add_child(bar)
		ui["bars"][spec[0]] = [bar, l, spec[1]]
	rv.add_child(_title("Síntomas"))
	var sym := RichTextLabel.new()
	sym.bbcode_enabled = true
	sym.fit_content = true
	sym.custom_minimum_size = Vector2(0, 60)
	rv.add_child(sym)
	ui["symptoms"] = sym
	rv.add_child(_title("Bitácora"))
	var logbox := RichTextLabel.new()
	logbox.bbcode_enabled = true
	logbox.scroll_following = true
	logbox.custom_minimum_size = Vector2(0, 180)
	rv.add_child(logbox)
	ui["log"] = logbox

	# ---------- Gráficos inferiores
	var charts := HBoxContainer.new()
	charts.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	charts.offset_left = 308
	charts.offset_right = -348
	charts.offset_top = -230
	charts.offset_bottom = -8
	charts.add_theme_constant_override("separation", 8)
	root.add_child(charts)
	var c1 = LineChart.new()
	c1.title = "Fisiología (% del valor inicial)"
	c1.y_min = 60.0
	c1.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	charts.add_child(c1)
	var c2 = LineChart.new()
	c2.title = "Radiación (mSv) y estrés"
	c2.y_max = 1200.0
	c2.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	c2.ref_lines = [{"y": 600.0, "label": "Límite NASA 600 mSv"}]
	charts.add_child(c2)
	ui["chart_phys"] = c1
	ui["chart_rad"] = c2


func _refresh_ui() -> void:
	var h = crew[selected]
	var env := _current_env()
	var phase := ""
	if legs.is_empty():
		phase = "modo libre"
	else:
		phase = "fase %d/%d, día %d de %d" % [leg_index + 1, legs.size(), day_in_leg, int(legs[leg_index][1])]
	ui["day_label"].text = "Día %d · %s" % [mission_day, phase]
	var g: float = env["gravity"]
	var g_note := ""
	if env.get("transit", false) and float(cm["artificial_g"]) > 0.0:
		g_note = " (+%.2f g artificial)" % cm["artificial_g"]
	ui["env_label"].text = "%s · %.3f g%s · %.2f mSv/día · CO₂ %.1f mmHg" % [env["label"], g, g_note, env["dose_rate"], env["co2"]]
	ui["who"].text = "%s - %s" % [h.crew_name, h.role]
	var hidx: float = h.health_index()
	var hcol := Color(0.3, 0.9, 0.45) if hidx > 75.0 else (Color(1.0, 0.8, 0.2) if hidx > 50.0 else Color(1.0, 0.3, 0.25))
	ui["health_label"].text = "Salud global: %.0f / 100" % hidx
	ui["health_label"].add_theme_color_override("font_color", hcol)

	var values := {
		"bone": h.bone, "muscle": h.muscle, "vo2": h.vo2, "plasma": h.plasma_deficit,
		"sans": h.sans, "dose": h.dose_msv, "cancer": h.cancer_risk_percent(), "stress": h.stress,
	}
	for key in values.keys():
		var entry: Array = ui["bars"][key]
		(entry[0] as ProgressBar).value = values[key]
		var fmt := "%.2f" if key == "sans" else "%.1f"
		(entry[1] as Label).text = ("%s: " + fmt) % [entry[2], values[key]]

	var txt := ""
	for s in h.symptoms():
		var c := "#7ee08a" if s["sev"] < 0.4 else ("#ffd24a" if s["sev"] < 0.7 else "#ff5a4a")
		txt += "[color=%s]●[/color] [b]%s:[/b] %s\n" % [c, s["zona"], s["texto"]]
	ui["symptoms"].text = txt if txt != "" else "[color=#7ee08a]Sin síntomas relevantes[/color]"
	ui["log"].text = "\n".join(log_lines)
	_update_charts(h)


func _update_charts(h) -> void:
	var bone := PackedFloat32Array()
	var muscle := PackedFloat32Array()
	var vo2 := PackedFloat32Array()
	var health := PackedFloat32Array()
	var dose := PackedFloat32Array()
	var stress := PackedFloat32Array()
	for e in h.history:
		bone.append(e["bone"])
		muscle.append(e["muscle"])
		vo2.append(e["vo2"])
		health.append(e["health"])
		dose.append(e["dose"])
		stress.append(e["stress"] * 10.0)
	var markers: Array = []
	var acc := 0
	for l in legs:
		markers.append({"x": acc, "label": Data.SCENARIOS[l[0]]["label"].split(" ")[0]})
		acc += int(l[1])
	var c1 = ui["chart_phys"]
	c1.x_max = _total_days()
	c1.markers = markers
	c1.series = [
		{"name": "Hueso", "color": Color(0.95, 0.9, 0.75), "values": bone},
		{"name": "Músculo", "color": Color(1.0, 0.5, 0.4), "values": muscle},
		{"name": "VO₂ máx", "color": Color(0.4, 0.75, 1.0), "values": vo2},
		{"name": "Salud global", "color": Color(0.4, 1.0, 0.55), "values": health},
	]
	c1.queue_redraw()
	var c2 = ui["chart_rad"]
	c2.x_max = _total_days()
	c2.markers = markers
	c2.y_max = maxf(1200.0, h.dose_msv * 1.1)
	c2.series = [
		{"name": "Dosis (mSv)", "color": Color(1.0, 0.35, 0.6), "values": dose},
		{"name": "Estrés x10", "color": Color(0.75, 0.6, 1.0), "values": stress},
	]
	c2.queue_redraw()


func _on_free_scenario(i: int) -> void:
	free_scenario = Data.SCENARIO_ORDER[i]
	_update_scene()
	_dirty = true


func _on_play_pressed() -> void:
	if finished:
		_apply_mission(mission_index)
	running = not running
	_update_play_button()


func _update_play_button() -> void:
	if ui.has("play"):
		ui["play"].text = "Pausa" if running else ("Repetir" if finished else "Iniciar")


func _log(text: String) -> void:
	log_lines.append(text)
	if log_lines.size() > 200:
		log_lines.remove_at(0)
	_dirty = true


func _export_csv() -> void:
	var path := "user://simulacion_salud.csv"
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		_log("[color=red]No se pudo escribir el CSV[/color]")
		return
	f.store_line("astronauta,dia,densidad_osea,masa_muscular,vo2max,deficit_plasma,sans,dosis_msv,estres,salud_global")
	for h in crew:
		for e in h.history:
			f.store_line("%s,%d,%.3f,%.3f,%.3f,%.3f,%.4f,%.2f,%.2f,%.2f" % [
				h.crew_name, e["day"], e["bone"], e["muscle"], e["vo2"], e["plasma"],
				e["sans"], e["dose"], e["stress"], e["health"]])
	f.close()
	_log("CSV guardado en: %s" % ProjectSettings.globalize_path(path))


# ---------------------------------------------------------------- helpers de UI

func _panel(parent: Control) -> PanelContainer:
	var p := PanelContainer.new()
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.03, 0.05, 0.12, 0.78)
	sb.border_color = Color(0.3, 0.5, 0.9, 0.35)
	sb.set_border_width_all(1)
	sb.set_corner_radius_all(6)
	sb.set_content_margin_all(10)
	p.add_theme_stylebox_override("panel", sb)
	parent.add_child(p)
	return p


func _label(text: String) -> Label:
	var l := Label.new()
	l.text = text
	return l


func _title(text: String) -> Label:
	var l := _label(text)
	l.add_theme_font_size_override("font_size", 17)
	l.add_theme_color_override("font_color", Color(0.55, 0.78, 1.0))
	return l


func _slider(parent: Control, title: String, min_v: float, max_v: float, step: float, value: float, key: String, cb: Callable) -> void:
	var l := _label("%s: %.2f" % [title, value])
	parent.add_child(l)
	var s := HSlider.new()
	s.min_value = min_v
	s.max_value = max_v
	s.step = step
	s.value = value
	s.value_changed.connect(func(v: float) -> void:
		l.text = "%s: %.2f" % [title, v]
		cb.call(v))
	parent.add_child(s)
	if key != "":
		ui[key] = s
		ui[key + "_lbl"] = l
