shader_type canvas_item;

// Mindustry-like corner tile: L centerline (enter west → exit south in local UV),
// chevrons scroll along the bend with the same tile units as straight strips.
// Sprite rotation / Y-flip covers other turn chirality.

uniform float scroll = 0.0;
uniform float scroll_phase = 0.0;
uniform float marks_per_tile = 2.5;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 mark_color : source_color = vec4(0.42, 0.52, 0.58, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);
uniform vec4 outside_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);

float sd_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * h);
}

void fragment() {
	vec2 p = UV;
	// L centerline in local space (Godot y-down): west mid → center → south mid.
	vec2 a = vec2(0.0, 0.5);
	vec2 b = vec2(0.5, 0.5);
	vec2 c = vec2(0.5, 1.0);

	float d0 = sd_segment(p, a, b);
	float d1 = sd_segment(p, b, c);
	float d = min(d0, d1);

	float half_w = 0.39;
	float inside = 1.0 - step(half_w, d);
	if (inside < 0.5) {
		COLOR = outside_color;
		return;
	}

	// Arc length along L in tile units (corner spans 1 tile of travel).
	float along;
	float across; // signed distance perpendicular to flow for chevron V
	if (d0 <= d1) {
		along = clamp(p.x, 0.0, 0.5);
		across = (p.y - 0.5) / half_w; // -1..1
	} else {
		along = 0.5 + clamp(p.y - 0.5, 0.0, 0.5);
		across = (0.5 - p.x) / half_w;
	}

	float u = along + scroll_phase - scroll;
	float v = across * 0.5 + 0.5; // 0..1 across belt

	float rail = step(v, 0.10) + step(0.90, v);
	float groove = (step(v, 0.16) - step(v, 0.10)) + (step(v, 0.90) - step(v, 0.84));

	float wave = fract(u * marks_per_tile - (0.5 - abs(v - 0.5)) * 1.1);
	float in_band = step(0.18, v) * step(v, 0.82);
	float mark = step(0.58, wave) * step(wave, 0.92) * in_band;

	float shade = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(v - 0.5));
	vec3 col = base_color.rgb * shade;
	col = mix(col, groove_color.rgb, clamp(groove, 0.0, 1.0));
	col = mix(col, mark_color.rgb, mark);
	col = mix(col, rail_color.rgb, clamp(rail, 0.0, 1.0));

	COLOR = vec4(col, 1.0);
}
