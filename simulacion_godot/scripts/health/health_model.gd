extends RefCounted
## Modelo fisiológico simplificado de UN astronauta. Cada llamada a step_day() avanza 1 día.
## Las ecuaciones y su justificación están en el informe (sección "Modelo de la simulación").
##
## Símbolos:
##   u = 1 - g          descarga gravitatoria (0 en la Tierra, 1 en microgravedad)
##   E = ejercicio      cumplimiento del programa de ejercicio (0..1)
##   M = multiplicador  1.25 - 0.4 * preparación   (preparación = condición física + acondicionamiento)

# --- Tasas base (por día, en microgravedad, sin contramedidas) ---
const BONE_RATE := 0.04        # % de densidad ósea / día  (~1.2 %/mes)
const MUSCLE_RATE := 0.12      # % de masa muscular / día
const VO2_RATE := 0.10         # % de VO2 máx / día
const PLASMA_TARGET := 12.0    # % de déficit de plasma en microgravedad
const PLASMA_TAU := 2.0        # días (constante de tiempo)
const SANS_RATE := 0.0035      # índice SANS / día
const GCR_SHIELD_MAX := 0.35   # el blindaje reduce como máximo un 35 % los rayos cósmicos
const SHELTER_FACTOR := 0.10   # el refugio deja pasar solo el 10 % de un evento solar
const CANCER_PER_SV := 5.0     # % de riesgo adicional por Sv (ICRP 103)

# --- Recuperación en la Tierra (g >= 0.9) ---
const BONE_RECOVERY := 0.015
const SOFT_RECOVERY := 0.3

# --- Identidad y preparación ---
var crew_name := "Astronauta"
var role := ""
var fitness := 0.7             # 0..1  (0.5 adulto sano entrenado, 1 atleta de élite)
var conditioning_days := 14    # mínimo recomendado: 14 (2 semanas)

# --- Estado fisiológico ---
var bone := 100.0              # % de la densidad ósea inicial
var muscle := 100.0            # % de la masa muscular inicial
var vo2 := 100.0               # % del VO2 máx inicial
var plasma_deficit := 0.0      # % de volumen plasmático perdido
var sans := 0.0                # 0..1 índice de síndrome neuro-ocular
var dose_msv := 0.0            # dosis acumulada total (mSv)
var spe_dose_msv := 0.0        # parte de la dosis que vino de eventos solares
var last_acute_msv := 0.0      # dosis recibida el último día
var stress := 10.0             # 0..100
var day := 0

var history: Array[Dictionary] = []
var _flags := {}               # umbrales ya avisados


func _init(p_name := "Astronauta", p_role := "", p_fitness := 0.7, p_conditioning := 14) -> void:
	crew_name = p_name
	role = p_role
	fitness = p_fitness
	conditioning_days = p_conditioning


func reset() -> void:
	bone = 100.0
	muscle = 100.0
	vo2 = 100.0
	plasma_deficit = 0.0
	sans = 0.0
	dose_msv = 0.0
	spe_dose_msv = 0.0
	last_acute_msv = 0.0
	stress = 10.0
	day = 0
	history.clear()
	_flags.clear()


func preparation() -> float:
	return 0.5 * fitness + 0.5 * clampf(conditioning_days / 14.0, 0.0, 1.0)


func decline_multiplier() -> float:
	return 1.25 - 0.4 * preparation()


## env: diccionario del escenario (ver mission_scenarios.gd)
## cm : contramedidas {exercise, shielding, shelter, co2_scrubber, support, artificial_g}
## spe_raw_msv: dosis de un evento solar ese día en espacio abierto sin blindaje (0 si no hubo)
## Devuelve la lista de alertas nuevas de ese día.
func step_day(env: Dictionary, cm: Dictionary, spe_raw_msv: float) -> Array[String]:
	var alerts: Array[String] = []
	var g: float = env["gravity"]
	if env.get("transit", false):
		g += float(cm.get("artificial_g", 0.0))
	var u := clampf(1.0 - g, 0.0, 1.0)
	var m := decline_multiplier()
	var e: float = cm.get("exercise", 0.0)

	# --- Gravedad: hueso, músculo, cardiovascular ---
	if g >= 0.9:
		bone = minf(100.0, bone + BONE_RECOVERY)
		muscle = minf(100.0, muscle + SOFT_RECOVERY)
		vo2 = minf(100.0, vo2 + SOFT_RECOVERY)
		sans = maxf(0.0, sans - 0.002)
	else:
		bone -= BONE_RATE * u * (1.0 - 0.6 * e) * m
		muscle -= MUSCLE_RATE * u * (1.0 - 0.7 * e) * m
		vo2 -= VO2_RATE * u * (1.0 - 0.75 * e) * m
	bone = maxf(bone, 50.0)
	muscle = maxf(muscle, 50.0)
	vo2 = maxf(vo2, 60.0)

	# --- Fluidos: déficit de plasma (respuesta rápida, días) ---
	var target := PLASMA_TARGET * u
	plasma_deficit += (target - plasma_deficit) * (1.0 - exp(-1.0 / PLASMA_TAU))

	# --- Visión (SANS): microgravedad + CO2 ---
	var co2: float = float(env["co2"]) * (1.0 - 0.6 * float(cm.get("co2_scrubber", 0.0)))
	if u > 0.0:
		sans = clampf(sans + SANS_RATE * u * (1.0 + maxf(0.0, (co2 - 2.0) / 4.0)), 0.0, 1.0)

	# --- Radiación ---
	var shield: float = cm.get("shielding", 0.0)
	var gcr: float = float(env["dose_rate"]) * (1.0 - GCR_SHIELD_MAX * shield)
	var spe := 0.0
	if spe_raw_msv > 0.0 and float(env["spe_exposure"]) > 0.0:
		var protection: float = SHELTER_FACTOR if cm.get("shelter", false) else (1.0 - GCR_SHIELD_MAX * shield)
		spe = spe_raw_msv * float(env["spe_exposure"]) * protection
	last_acute_msv = gcr + spe
	dose_msv += last_acute_msv
	spe_dose_msv += spe

	# --- Psicológico ---
	var support: float = cm.get("support", 0.0)
	if float(env["isolation"]) <= 0.01:
		stress -= 0.5
	else:
		stress += 0.08 * float(env["isolation"]) - 0.05 * support
	if spe > 1.0:
		stress += 5.0
	stress = clampf(stress, 0.0, 100.0)

	day += 1
	_record()
	_check_thresholds(alerts, spe)
	return alerts


func cancer_risk_percent() -> float:
	return CANCER_PER_SV * dose_msv / 1000.0


## Puntajes 0..100 por sistema (100 = sano)
func scores() -> Dictionary:
	return {
		"hueso": clampf((bone - 75.0) / 25.0, 0.0, 1.0) * 100.0,
		"musculo": clampf((muscle - 70.0) / 30.0, 0.0, 1.0) * 100.0,
		"cardio": clampf((vo2 - 70.0) / 30.0, 0.0, 1.0) * (1.0 - plasma_deficit / 24.0) * 100.0,
		"vision": (1.0 - sans) * 100.0,
		"radiacion": clampf(1.0 - dose_msv / 1000.0, 0.0, 1.0) * 100.0,
		"psicologico": 100.0 - stress,
	}


func health_index() -> float:
	var s := scores()
	return (0.20 * s["hueso"] + 0.15 * s["musculo"] + 0.15 * s["cardio"]
		+ 0.10 * s["vision"] + 0.25 * s["radiacion"] + 0.15 * s["psicologico"])


## Severidad 0..1 por zona del cuerpo, usada para colorear el astronauta 3D.
func region_severity() -> Dictionary:
	var bone_sev := clampf((100.0 - bone) / 20.0, 0.0, 1.0)
	var muscle_sev := clampf((100.0 - muscle) / 30.0, 0.0, 1.0)
	var cardio_sev := maxf(clampf((100.0 - vo2) / 30.0, 0.0, 1.0), plasma_deficit / 24.0)
	return {
		"legs": maxf(bone_sev, muscle_sev),
		"spine": bone_sev,
		"pelvis": bone_sev,
		"arms": muscle_sev * 0.7,
		"chest": cardio_sev,
		"eyes": sans,
		"head": maxf(stress / 100.0, plasma_deficit / 12.0 * 0.5),
		"radiation": clampf(dose_msv / 1000.0, 0.0, 1.0),
	}


## Lista de síntomas legibles: [{zona, texto, severidad}]
func symptoms() -> Array[Dictionary]:
	var out: Array[Dictionary] = []
	if plasma_deficit > 4.0:
		out.append({"zona": "Cabeza", "texto": "Desplazamiento de fluidos: cara hinchada, congestión nasal", "sev": plasma_deficit / 12.0})
	if plasma_deficit > 8.0:
		out.append({"zona": "Corazón", "texto": "Riesgo de mareo/desmayo al volver a gravedad (intolerancia ortostática)", "sev": plasma_deficit / 12.0})
	if bone < 95.0:
		out.append({"zona": "Cadera/columna", "texto": "Pérdida ósea %.1f %%" % (100.0 - bone), "sev": (100.0 - bone) / 20.0})
	if bone < 90.0:
		out.append({"zona": "Riñón", "texto": "Calcio en orina elevado: riesgo de cálculos renales", "sev": 0.6})
	if muscle < 92.0:
		out.append({"zona": "Piernas", "texto": "Atrofia muscular %.1f %%" % (100.0 - muscle), "sev": (100.0 - muscle) / 30.0})
	if vo2 < 92.0:
		out.append({"zona": "Corazón/pulmón", "texto": "Capacidad aeróbica reducida %.1f %%" % (100.0 - vo2), "sev": (100.0 - vo2) / 30.0})
	if sans > 0.2:
		out.append({"zona": "Ojos", "texto": "SANS: edema de disco óptico, visión cercana borrosa", "sev": sans})
	if last_acute_msv > 100.0:
		out.append({"zona": "Todo el cuerpo", "texto": "Dosis aguda alta hoy (%.0f mSv): náuseas, fatiga" % last_acute_msv, "sev": clampf(last_acute_msv / 1000.0, 0.3, 1.0)})
	if dose_msv > 300.0:
		out.append({"zona": "Todo el cuerpo", "texto": "Riesgo de cáncer +%.1f %% (dosis %.0f mSv)" % [cancer_risk_percent(), dose_msv], "sev": dose_msv / 1000.0})
	if stress > 50.0:
		out.append({"zona": "Mente", "texto": "Estrés por aislamiento, alteración del sueño", "sev": stress / 100.0})
	return out


func _record() -> void:
	history.append({
		"day": day, "bone": bone, "muscle": muscle, "vo2": vo2, "plasma": plasma_deficit,
		"sans": sans, "dose": dose_msv, "stress": stress, "health": health_index(),
	})


func _check_thresholds(alerts: Array[String], spe: float) -> void:
	if spe > 1.0:
		alerts.append("%s: evento solar, +%.0f mSv" % [crew_name, spe])
	_flag(alerts, "osteo", bone < 90.0, "%s: osteopenia (hueso < 90 %%)" % crew_name)
	_flag(alerts, "fract", bone < 80.0, "%s: riesgo ALTO de fractura (hueso < 80 %%)" % crew_name)
	_flag(alerts, "musc", muscle < 85.0, "%s: atrofia muscular > 15 %%" % crew_name)
	_flag(alerts, "vo2", vo2 < 85.0, "%s: VO2 máx < 85 %%" % crew_name)
	_flag(alerts, "sans", sans > 0.5, "%s: SANS afecta la visión" % crew_name)
	_flag(alerts, "dose", dose_msv > 600.0, "%s: superó el límite de carrera de 600 mSv" % crew_name)
	_flag(alerts, "ars", last_acute_msv > 500.0, "%s: síndrome agudo de radiación probable" % crew_name)
	_flag(alerts, "stress", stress > 70.0, "%s: estrés crítico" % crew_name)


func _flag(alerts: Array[String], key: String, cond: bool, text: String) -> void:
	if cond and not _flags.has(key):
		_flags[key] = true
		alerts.append(text)
