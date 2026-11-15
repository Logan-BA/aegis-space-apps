extends Camera3D
## Cámara orbital: clic derecho (o central) + arrastrar para girar, rueda para acercar.

var target := Vector3(0, 1.2, 0)
var distance := 4.5
var yaw := 0.45
var pitch := -0.12
var _dragging := false


func _ready() -> void:
	far = 30000.0
	near = 0.05
	fov = 60.0
	_update()


func _unhandled_input(event: InputEvent) -> void:
	var mb := event as InputEventMouseButton
	if mb:
		if mb.button_index == MOUSE_BUTTON_RIGHT or mb.button_index == MOUSE_BUTTON_MIDDLE:
			_dragging = mb.pressed
		elif mb.button_index == MOUSE_BUTTON_WHEEL_UP and mb.pressed:
			distance = maxf(1.5, distance * 0.9)
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN and mb.pressed:
			distance = minf(120.0, distance * 1.1)
		_update()
		return
	var mm := event as InputEventMouseMotion
	if mm and _dragging:
		yaw -= mm.relative.x * 0.006
		pitch = clampf(pitch - mm.relative.y * 0.006, -1.35, 1.35)
		_update()


func _update() -> void:
	var offset := Vector3(cos(pitch) * sin(yaw), sin(pitch), cos(pitch) * cos(yaw)) * distance
	if is_inside_tree():
		look_at_from_position(target + offset, target, Vector3.UP)
	else:
		position = target + offset
