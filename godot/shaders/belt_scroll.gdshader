shader_type canvas_item;

// Mindustry-style regular belt: FULL TILE — rails sit on UV edges (tile edges).
// Thin spanning look is for bridges only — never shrink the strip in C#.
// Rails + scrolling arrows tip toward +UV.x (flow); scroll rises with transport.

uniform float scroll = 0.0;
uniform float scroll_phase = 0.0;
uniform float cell_count = 1.0;
uniform float marks_per_tile = 2.0;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 mark_color : source_color = vec4(0.55, 0.68, 0.74, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);

// Explicit arrow pointing +u (flow). px in period, py across belt (-1..1).
float arrow_mark(float px, float py) {
	float tip = 0.78;
	float from_tip = tip - px;
	float half_w = from_tip * 1.15;
	float in_body = step(0.0, from_tip) * step(from_tip, 0.42);
	float in_width = step(abs(py), max(half_w, 0.0));
	return in_body * in_width;
}

void fragment() {
	float tiles = max(cell_count, 0.001);
	float u = UV.x * tiles + scroll_phase - scroll;
	float v = UV.y;

	// Rails on the absolute edges of the tile (flush with grid).
	float rail = step(v, 0.08) + step(0.92, v);
	float groove = (step(v, 0.14) - step(v, 0.08)) + (step(v, 0.92) - step(v, 0.86));

	float px = fract(u * marks_per_tile);
	float py = (v - 0.5) * 2.2;
	float in_band = step(0.16, v) * step(v, 0.84);
	float mark = arrow_mark(px, py) * in_band;

	float shade = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(v - 0.5));

	vec3 col = base_color.rgb * shade;
	col = mix(col, groove_color.rgb, clamp(groove, 0.0, 1.0));
	col = mix(col, mark_color.rgb, mark);
	col = mix(col, rail_color.rgb, clamp(rail, 0.0, 1.0));

	// Always opaque — never punch holes that show terrain inside the tile.
	COLOR = vec4(col, 1.0);
}
