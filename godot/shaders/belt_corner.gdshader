shader_type canvas_item;

// Full-cell rounded L corner (galleria / tunnel).
// SDF capsule L → outer NE quarter-arc + inner fillet.
// Dark pad in the NE pocket. Square N/E frame; SW knuckle only.
// Path rim only on the curved bend (not entry/exit arms).
// Mouth ombrette = soft multiply only (no rail-tint lips → no seam steps).
// shade_entry / shade_exit: C# zeros when neighbor is another corner (Z/U).
// Base: enter west → exit south.

uniform float half_width = 0.42;
uniform float shade_entry = 1.0;
uniform float shade_exit = 1.0;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);
// Above IsRail thresholds (R>30 or G>36) so pad ≠ rail for pixel checks.
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
	float shade = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(across));
	vec3 belt = base_color.rgb * shade;

	// Rim along SDF boundary near the bend only (outer arc + inner fillet).
	float rim = step(hw - 0.08, d) * step(d, hw);
	float groove_rim = step(hw - 0.14, d) * (1.0 - step(hw - 0.08, d));
	// Suppress on straight entry/exit arms so N/S and E/W seams stay flush.
	float on_entry_arm = step(p.x, 0.40);
	float on_exit_arm = step(0.60, p.y);
	float curved = (1.0 - on_entry_arm) * (1.0 - on_exit_arm);
	// Keep rim in the NE outer-arc quadrant even when y is mid-high.
	float ne_arc = step(0.45, p.x) * step(p.y, 0.55);
	curved = max(curved, ne_arc);
	rim *= curved;
	groove_rim *= curved;

	belt = mix(belt, groove_color.rgb, clamp(groove_rim, 0.0, 1.0));
	belt = mix(belt, rail_color.rgb, clamp(rim, 0.0, 1.0));

	// Galleria mouth ombrette — multiply only, capped so body stays non-rail.
	float entry_umbra = shade_entry * (1.0 - smoothstep(0.0, 0.30, p.x));
	float exit_umbra = shade_exit * smoothstep(0.70, 1.0, p.y);
	float umbra = min(max(entry_umbra, exit_umbra), 0.30) * in_belt;
	belt *= 1.0 - umbra;

	vec3 col = mix(pad_color.rgb, belt, in_belt);

	// Square outer frame.
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
