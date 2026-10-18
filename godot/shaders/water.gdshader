shader_type canvas_item;

// Water ripples over the terrain art chunks (TerrainChunks / TerrainComposer). The chunk texture holds the
// still water bed in its colour and the water mask in its alpha (1 where the bed is uncovered, 0 on land and
// under walls, edges and pebbles). Ripples are the level lines of two value noises drifting in world
// coordinates, one value per art pixel (2 world units, 32 per cell), so the pattern never repeats per cell
// and stays pixel art. The caller advances `time` (seconds); it stays 0 in playtests (stable captures).

uniform float time = 0.0;
// World units per art pixel (TileSize / TerrainLook.CellPixels).
uniform float art_pixel = 2.0;

const vec3 RIPPLE = vec3(0.07, 0.09, 0.1);
const float LINE = 0.05;

varying vec2 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

// Integer hash of a lattice point, in [0, 1]: no sin(), so far-away cells keep their precision.
float lattice(vec2 p) {
	uvec2 q = uvec2(ivec2(p));
	uint v = (q.x * 1597334677u) ^ (q.y * 3812015801u);
	v = (v ^ (v >> 16u)) * 73244475u;
	v ^= v >> 16u;
	return float(v) * (1.0 / 4294967295.0);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(lattice(i), lattice(i + vec2(1.0, 0.0)), u.x),
		mix(lattice(i + vec2(0.0, 1.0)), lattice(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	vec4 c = texture(TEXTURE, UV);
	vec3 col = c.rgb;
	if (c.a > 0.0) {
		vec2 p = floor(world / art_pixel) + 0.5;
		float n = value_noise(p / 21.0 + vec2(time * 0.30, time * 0.11))
			+ value_noise(p / 14.0 + vec2(-time * 0.17, time * 0.26));
		float band = abs(fract(n * 2.5) - 0.5);
		col += step(band, LINE) * c.a * RIPPLE;
	}
	COLOR = vec4(col, 1.0);
}
