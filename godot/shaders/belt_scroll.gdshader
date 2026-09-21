shader_type canvas_item;

// Mindustry-style belt: rails + scrolling chevrons along flow.
// UV.x spans the strip (0..1). cell_count converts to tile space so
// scroll (tiles) matches BeltLane Advance rate (tiles/sec).

uniform float scroll = 0.0;
uniform float cell_count = 1.0;
uniform float marks_per_tile = 2.5;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 mark_color : source_color = vec4(0.42, 0.52, 0.58, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);

void fragment() {
	float tiles = max(cell_count, 0.001);
	float u = UV.x * tiles - scroll;
	float v = UV.y;

	float rail = step(v, 0.10) + step(0.90, v);
	float groove = (step(v, 0.16) - step(v, 0.10)) + (step(v, 0.90) - step(v, 0.84));

	float wave = fract(u * marks_per_tile + (0.5 - abs(v - 0.5)) * 1.1);
	float in_band = step(0.18, v) * step(v, 0.82);
	float mark = step(0.58, wave) * step(wave, 0.92) * in_band;

	float shade = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(v - 0.5));

	vec3 col = base_color.rgb * shade;
	col = mix(col, groove_color.rgb, clamp(groove, 0.0, 1.0));
	col = mix(col, mark_color.rgb, mark);
	col = mix(col, rail_color.rgb, clamp(rail, 0.0, 1.0));

	COLOR = vec4(col, 1.0);
}
