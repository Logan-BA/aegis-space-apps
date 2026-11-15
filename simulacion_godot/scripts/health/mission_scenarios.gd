extends RefCounted
## Datos de escenarios y misiones. Valores de referencia explicados en el informe
## "Salud del astronauta y entorno espacial".
##
## gravity       : gravedad en g (1.0 = Tierra)
## dose_rate     : dosis efectiva por radiacion cosmica (GCR), mSv/dia
## spe_exposure  : fraccion de un evento solar (SPE) que llega al lugar (0 = protegido)
## isolation     : factor de aislamiento psicologico (0 = en casa, >1 = muy lejos)
## co2           : CO2 de cabina en mmHg
## transit       : true si es un viaje (permite gravedad artificial)
## scene         : que escenario 3D se construye

const SCENARIOS := {
	"earth": {
		"label": "Tierra (recuperacion)", "gravity": 1.0, "dose_rate": 0.008,
		"spe_exposure": 0.0, "isolation": 0.0, "co2": 0.3, "transit": false, "scene": "earth",
	},
	"conditioning": {
		"label": "Acondicionamiento previo (Tierra)", "gravity": 1.0, "dose_rate": 0.008,
		"spe_exposure": 0.0, "isolation": 0.2, "co2": 0.3, "transit": false, "scene": "earth",
	},
	"leo": {
		"label": "Orbita baja / ISS", "gravity": 0.0, "dose_rate": 0.5,
		"spe_exposure": 0.05, "isolation": 0.6, "co2": 3.0, "transit": false, "scene": "leo",
	},
	"moon_transit": {
		"label": "Transito Tierra-Luna", "gravity": 0.0, "dose_rate": 1.4,
		"spe_exposure": 1.0, "isolation": 0.8, "co2": 3.0, "transit": true, "scene": "deep_space_moon",
	},
	"moon_surface": {
		"label": "Superficie lunar", "gravity": 0.165, "dose_rate": 1.4,
		"spe_exposure": 0.5, "isolation": 0.9, "co2": 2.5, "transit": false, "scene": "moon_surface",
	},
	"mars_transit": {
		"label": "Transito a Marte", "gravity": 0.0, "dose_rate": 1.8,
		"spe_exposure": 1.0, "isolation": 1.2, "co2": 3.0, "transit": true, "scene": "deep_space_mars",
	},
	"mars_surface": {
		"label": "Superficie de Marte", "gravity": 0.38, "dose_rate": 0.64,
		"spe_exposure": 0.3, "isolation": 1.3, "co2": 2.5, "transit": false, "scene": "mars_surface",
	},
}

## Orden en el que aparecen en el selector de escenario libre.
const SCENARIO_ORDER := ["earth", "leo", "moon_transit", "moon_surface", "mars_transit", "mars_surface"]

## Misiones = secuencia de tramos [escenario, dias]. El acondicionamiento se antepone solo.
const MISSIONS := [
	{"label": "Libre (elegir escenario)", "legs": []},
	{"label": "ISS - 6 meses", "legs": [["leo", 180], ["earth", 90]]},
	{"label": "ISS - 1 ano", "legs": [["leo", 365], ["earth", 120]]},
	{"label": "Artemis - Luna 40 dias", "legs": [["moon_transit", 5], ["moon_surface", 30], ["moon_transit", 5], ["earth", 60]]},
	{"label": "Marte - clase conjuncion (~920 d)", "legs": [["mars_transit", 210], ["mars_surface", 500], ["mars_transit", 210], ["earth", 180]]},
]

## Tripulacion por defecto: [nombre, rol, condicion fisica 0..1]
const DEFAULT_CREW := [
	["CMD Alfa", "Comandante", 0.85],
	["PIL Bravo", "Piloto", 0.70],
	["MED Charlie", "Oficial medico", 0.75],
	["ING Delta", "Especialista", 0.55],
]
