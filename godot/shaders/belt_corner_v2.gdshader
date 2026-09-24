shader_type canvas_item;

// Paint-matched rounded L corner (galleria).
// Thick dark OUTER ARC (the yellow-dot pocket sits outside it).
// Small INNER FILLET. Arms flush with full-tile straights (hw=0.42).
// Flat colors, no mouth umbrae. Base: enter west → exit south.

uniform float half_width = 0.42;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);
// NE pocket under the outer arc (same family as rail, slightly lifted for asserts).
uniform vec4 pad_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);

float sd_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * h);
}

void fragment() {
	vec2 p = UV;
	float hw = half_width;

	vec2 a = vec2(0.0, 0.5);
	vec2 b = vec2(0.5, 0.5);
	vec2 c = vec2(0.5, 1.0);
	float dh = sd_segment(p, a, b);
	float dv = sd_segment(p, b, c);
	float d = min(dh, dv);
	float in_belt = step(d, hw);

	float on_vert = step(dv, dh);
	float across_h = (p.y - 0.5) / hw;
	float across_v = (0.5 - p.x) / hw;
	float across = clamp(mix(across_h, across_v, on_vert), -1.0, 1.0);

	// Flat body (paint-like), tiny shade only.
	float shade = 0.96 + 0.04 * smoothstep(0.0, 0.5, abs(across));
	vec3 belt = base_color.rgb * shade;

	// Outer side of the L (toward NE) / inner side (toward SW fillet).
	float on_outer = step(across, 0.0);       // across <= 0
	float on_inner = 1.0 - on_outer;

	// Bend only — keep entry/exit arms flush with straight rails.
	float on_entry_arm = step(p.x, 0.42);
	float on_exit_arm = step(0.58, p.y);
	float curved = (1.0 - on_entry_arm) * (1.0 - on_exit_arm);
	float ne_quad = step(0.42, p.x) * step(p.y, 0.58);
	curved = max(curved, ne_quad);

	// THICK dark outer arc (paint "arco") — ~22% of half-width on the outer lip.
	float arco = step(hw - 0.22, d) * step(d, hw) * on_outer * curved;
	// Thin inner fillet rim (small rounded shoulder in the paint).
	float fillet = step(hw - 0.10, d) * step(d, hw) * on_inner * curved;
	// Keep N/E frame thin.
	float away_frame = step(0.14, p.y) * step(p.x, 0.86);
	arco *= away_frame;
	fillet *= away_frame;

	belt = mix(belt, rail_color.rgb, clamp(max(arco, fillet), 0.0, 1.0));

	// NE pocket outside the belt, under the arc.
	vec3 col = mix(pad_color.rgb, belt, in_belt);

	// Square outer frame — N/E full; SW knuckle only.
	float rail_n = step(p.y, 0.08);
	float rail_e = step(0.92, p.x);
	float rail_s = step(0.92, p.y) * step(p.x, 0.08);
	float rail_w = step(p.x, 0.08) * step(0.92, p.y);
	float frame = max(max(rail_n, rail_e), max(rail_s, rail_w));

	float groove_n = step(p.y, 0.14) * (1.0 - step(p.y, 0.08));
	float groove_e = step(p.x, 0.92) * (1.0 - step(p.x, 0.86));
	float frame_groove = max(groove_n, groove_e) * (1.0 - in_belt);

	col = mix(col, groove_color.rgb, clamp(frame_groove, 0.0, 1.0));
	col = mix(col, rail_color.rgb, clamp(frame, 0.0, 1.0));

	COLOR = vec4(col, 1.0);
}
