extends Control
## Gráfico de líneas simple dibujado con _draw().
## series: [{name, color, values: PackedFloat32Array}]
## markers: [{x (día), label}]        -> líneas verticales (cambio de fase)
## ref_lines: [{y, label}]            -> líneas horizontales (límites)

var title := ""
var y_min := 0.0
var y_max := 100.0
var x_max := 100.0
var unit := ""
var series: Array = []
var markers: Array = []
var ref_lines: Array = []


func _draw() -> void:
	var font := ThemeDB.fallback_font
	var fs := 12
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.02, 0.03, 0.08, 0.72))
	var r := Rect2(Vector2(44, 26), size - Vector2(56, 48))
	draw_string(font, Vector2(8, 16), title, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.9, 0.93, 1.0))

	# Rejilla y eje Y
	for i in 5:
		var v := lerpf(y_min, y_max, i / 4.0)
		var y := _y(v, r)
		draw_line(Vector2(r.position.x, y), Vector2(r.end.x, y), Color(1, 1, 1, 0.08))
		draw_string(font, Vector2(4, y + 4), "%.0f" % v, HORIZONTAL_ALIGNMENT_LEFT, 38, fs, Color(0.7, 0.75, 0.85))
	draw_string(font, Vector2(r.end.x - 60, size.y - 6), "días", HORIZONTAL_ALIGNMENT_RIGHT, 60, fs, Color(0.7, 0.75, 0.85))
	draw_string(font, Vector2(r.position.x, size.y - 6), "0", HORIZONTAL_ALIGNMENT_LEFT, -1, fs, Color(0.7, 0.75, 0.85))
	draw_string(font, Vector2(r.end.x - 120, size.y - 6), "%d" % int(x_max), HORIZONTAL_ALIGNMENT_RIGHT, 50, fs, Color(0.7, 0.75, 0.85))

	# Cambios de fase
	for mk in markers:
		var x := _x(float(mk["x"]), r)
		draw_line(Vector2(x, r.position.y), Vector2(x, r.end.y), Color(1, 1, 1, 0.25), 1.0)
		draw_string(font, Vector2(x + 3, r.position.y + 10), str(mk["label"]), HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color(0.8, 0.85, 0.95, 0.8))

	# Límites
	for rl in ref_lines:
		var y := _y(float(rl["y"]), r)
		draw_dashed_line(Vector2(r.position.x, y), Vector2(r.end.x, y), Color(1.0, 0.4, 0.35, 0.9), 1.5, 6.0)
		draw_string(font, Vector2(r.end.x - 160, y - 4), str(rl["label"]), HORIZONTAL_ALIGNMENT_RIGHT, 158, 11, Color(1.0, 0.55, 0.5))

	# Series
	var legend_x := 160.0
	for s in series:
		var values: PackedFloat32Array = s["values"]
		var col: Color = s["color"]
		if values.size() >= 2:
			var pts := PackedVector2Array()
			var stride := maxi(1, values.size() / 600)
			for i in range(0, values.size(), stride):
				pts.append(Vector2(_x(i + 1, r), _y(values[i], r)))
			pts.append(Vector2(_x(values.size(), r), _y(values[values.size() - 1], r)))
			draw_polyline(pts, col, 2.0, true)
		draw_rect(Rect2(legend_x, 8, 10, 10), col)
		draw_string(font, Vector2(legend_x + 14, 17), str(s["name"]), HORIZONTAL_ALIGNMENT_LEFT, -1, 11, Color(0.85, 0.88, 0.95))
		legend_x += 14 + font.get_string_size(str(s["name"]), HORIZONTAL_ALIGNMENT_LEFT, -1, 11).x + 16


func _x(day: float, r: Rect2) -> float:
	return r.position.x + clampf(day / maxf(x_max, 1.0), 0.0, 1.0) * r.size.x


func _y(v: float, r: Rect2) -> float:
	return r.end.y - clampf((v - y_min) / maxf(y_max - y_min, 0.001), 0.0, 1.0) * r.size.y
