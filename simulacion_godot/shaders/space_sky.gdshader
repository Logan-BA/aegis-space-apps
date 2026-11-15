shader_type sky;
// Cielo procedural: estrellas, banda de la Vía Láctea, disco solar y atmósfera opcional.

uniform vec3 nebula_color : source_color = vec3(0.18, 0.10, 0.32);
uniform float star_threshold = 0.9965;
uniform float star_cells = 420.0;
// atmósfera: 0 = espacio, 1 = cielo opaco (Tierra / Marte)
uniform float atmosphere = 0.0;
uniform vec3 zenith_color : source_color = vec3(0.20, 0.45, 0.85);
uniform vec3 horizon_color : source_color = vec3(0.65, 0.80, 0.95);
uniform vec3 ground_color : source_color = vec3(0.05, 0.05, 0.06);

float hash(vec3 p) {
	p = fract(p * 0.3183099 + vec3(0.1, 0.2, 0.3));
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

void sky() {
	vec3 d = normalize(EYEDIR);
	vec3 cell = floor(d * star_cells);
	float h = hash(cell);
	float star = step(star_threshold, h);
	float twinkle = 0.4 + hash(cell + vec3(3.1, 1.7, 5.3));
	vec3 tint = mix(vec3(0.8, 0.85, 1.0), vec3(1.0, 0.9, 0.75), hash(cell + vec3(9.0)));

	// Banda galáctica inclinada
	float band = exp(-pow(d.y * 2.6 + d.x * 0.9, 2.0) * 2.0);
	vec3 space = nebula_color * band * 0.35 + tint * star * twinkle;

	// Atmósfera (gradiente horizonte -> cénit)
	float up = clamp(d.y, 0.0, 1.0);
	vec3 air = mix(horizon_color, zenith_color, pow(up, 0.6));
	if (d.y < 0.0) {
		air = mix(horizon_color, ground_color, clamp(-d.y * 4.0, 0.0, 1.0));
	}
	vec3 col = mix(space, air, atmosphere);

	// Disco solar
	if (LIGHT0_ENABLED) {
		float s = dot(d, LIGHT0_DIRECTION);
		col += LIGHT0_COLOR * (smoothstep(0.9992, 0.9997, s) * 12.0 + pow(max(s, 0.0), 400.0) * 0.6);
	}
	COLOR = col;
}
