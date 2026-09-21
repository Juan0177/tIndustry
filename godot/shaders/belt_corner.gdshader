shader_type canvas_item;

// Mindustry-like corner: L belt only (enter west → exit south). Outside is transparent
// so the unused quadrant does not draw a dark pad block. Arrows scroll along the bend.

uniform float scroll = 0.0;
uniform float scroll_phase = 0.0;
uniform float marks_per_tile = 2.0;
uniform float half_width = 0.39;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 mark_color : source_color = vec4(0.55, 0.68, 0.74, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);

float sd_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * h);
}

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
	// L centerline: west mid → center → south mid (Godot y-down).
	vec2 a = vec2(0.0, 0.5);
	vec2 b = vec2(0.5, 0.5);
	vec2 c = vec2(0.5, 1.0);

	float d0 = sd_segment(p, a, b);
	float d1 = sd_segment(p, b, c);
	float d = min(d0, d1);
	float inside = 1.0 - smoothstep(half_width - 0.02, half_width, d);

	float on_vert = step(d1, d0);
	float along_h = clamp(p.x, 0.0, 0.5);
	float along_v = 0.5 + clamp(p.y - 0.5, 0.0, 0.5);
	float along = mix(along_h, along_v, on_vert);

	float across_h = (p.y - 0.5) / half_width;
	float across_v = (0.5 - p.x) / half_width;
	float across = mix(across_h, across_v, on_vert);

	float u = along + scroll_phase - scroll;
	float v = across * 0.5 + 0.5;

	float rail = step(v, 0.10) + step(0.90, v);
	float groove = (step(v, 0.16) - step(v, 0.10)) + (step(v, 0.90) - step(v, 0.84));

	float px = fract(u * marks_per_tile);
	float py = (v - 0.5) * 2.2;
	float in_band = step(0.16, v) * step(v, 0.84);
	float mark = arrow_mark(px, py) * in_band;

	float shade = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(v - 0.5));
	vec3 belt = base_color.rgb * shade;
	belt = mix(belt, groove_color.rgb, clamp(groove, 0.0, 1.0) * inside);
	belt = mix(belt, mark_color.rgb, mark * inside);
	belt = mix(belt, rail_color.rgb, clamp(rail, 0.0, 1.0) * inside);

	// Transparent outside the L — no opaque pad square in the unused quadrant.
	COLOR = vec4(belt, inside);
}
