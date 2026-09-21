shader_type canvas_item;

// Mindustry-like corner tile: solid L elbow (enter west → exit south).
// Outside is transparent. Arrows follow the bend and scroll with flow.
// Sprite rotation / Y-flip covers other 90° turns.

uniform float scroll = 0.0;
uniform float scroll_phase = 0.0;
uniform float marks_per_tile = 2.0;
// Match straight strip thickness (0.78 tile → half 0.39).
uniform float half_width = 0.39;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 mark_color : source_color = vec4(0.55, 0.68, 0.74, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);

float arrow_mark(float px, float py) {
	float tip = 0.78;
	float from_tip = tip - px;
	float half_w = from_tip * 1.15;
	float in_body = step(0.0, from_tip) * step(from_tip, 0.42);
	float in_width = step(abs(py), max(half_w, 0.0));
	return in_body * in_width;
}

void fragment() {
	vec2 p = UV;
	float hw = half_width;

	// Solid L = horizontal arm (west → center) ∪ vertical arm (center → south).
	// Arms meet with a square overlap at the bend so there is no seam gap.
	float in_h = step(0.0, p.x) * step(p.x, 0.5 + hw)
		* step(0.5 - hw, p.y) * step(p.y, 0.5 + hw);
	float in_v = step(0.5 - hw, p.x) * step(p.x, 0.5 + hw)
		* step(0.5 - hw, p.y) * step(p.y, 1.0);
	float inside = max(in_h, in_v);

	// Prefer vertical mapping once past the bend so arrows turn with the L.
	float on_vert = step(0.5, p.x) * step(0.5, p.y);
	// Also treat the vertical arm above the bend center as vertical when clearly in it.
	on_vert = max(on_vert, in_v * (1.0 - in_h));
	// When in the overlap square, choose by which edge is nearer the exit path.
	float in_overlap = in_h * in_v;
	on_vert = mix(on_vert, step(0.5, p.y), in_overlap);

	float along_h = clamp(p.x, 0.0, 0.5);
	float along_v = 0.5 + clamp(p.y - 0.5, 0.0, 0.5);
	float along = mix(along_h, along_v, on_vert);

	float across_h = (p.y - 0.5) / hw;
	float across_v = (0.5 - p.x) / hw;
	float across = mix(across_h, across_v, on_vert);
	across = clamp(across, -1.0, 1.0);

	float u = along + scroll_phase - scroll;
	float v = across * 0.5 + 0.5;

	float rail = step(v, 0.10) + step(0.90, v);
	float groove = (step(v, 0.16) - step(v, 0.10)) + (step(v, 0.90) - step(v, 0.84));

	float px = fract(u * marks_per_tile);
	float py = (v - 0.5) * 2.2;
	float in_band = step(0.14, v) * step(v, 0.86);
	float mark = arrow_mark(px, py) * in_band;

	float shade = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(v - 0.5));
	vec3 belt = base_color.rgb * shade;
	belt = mix(belt, groove_color.rgb, clamp(groove, 0.0, 1.0));
	belt = mix(belt, mark_color.rgb, mark);
	belt = mix(belt, rail_color.rgb, clamp(rail, 0.0, 1.0));

	COLOR = vec4(belt, inside);
}
