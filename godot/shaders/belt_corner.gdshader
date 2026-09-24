shader_type canvas_item;

// Full-cell corner with a true rounded L belt (first-design look).
// SDF capsule L → outer quarter-arc in the NE pocket + inner fillet at the bend.
// Outside the belt but inside the tile is dark pad (the yellow-circle corner).
// Square N/E frame rails stay flush with the grid; no SE south-rail stub.
// Base art: enter west → exit south. half_width 0.42 matches full-tile straights.

uniform float half_width = 0.42;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);
uniform vec4 pad_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);

float sd_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * h);
}

void fragment() {
	vec2 p = UV;
	float hw = half_width;

	// L centerline: west mid → center → south mid.
	vec2 a = vec2(0.0, 0.5);
	vec2 b = vec2(0.5, 0.5);
	vec2 c = vec2(0.5, 1.0);
	float dh = sd_segment(p, a, b);
	float dv = sd_segment(p, b, c);
	float d = min(dh, dv);

	// Rounded L body (outer arc + inner fillet fall out of the SDF).
	float in_belt = step(d, hw);

	// Along/across mapping so rails follow the curve (same language as #60).
	float on_vert = step(dv, dh);
	float across_h = (p.y - 0.5) / hw;
	float across_v = (0.5 - p.x) / hw;
	float across = clamp(mix(across_h, across_v, on_vert), -1.0, 1.0);
	float v = across * 0.5 + 0.5;

	float shade = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(across));
	vec3 belt = base_color.rgb * shade;

	// Path rails / grooves travel WITH the arc (not axis-aligned only).
	float path_rail = step(v, 0.08) + step(0.92, v);
	float path_groove = (step(v, 0.14) - step(v, 0.08))
		+ (step(v, 0.92) - step(v, 0.86));
	belt = mix(belt, groove_color.rgb, clamp(path_groove, 0.0, 1.0) * in_belt);
	belt = mix(belt, rail_color.rgb, clamp(path_rail, 0.0, 1.0) * in_belt);

	// Full-tile pad outside the belt — NE pocket under the outer arc.
	vec3 col = mix(pad_color.rgb, belt, in_belt);

	// Square outer frame (flush with grid). N + E always.
	float rail_n = step(p.y, 0.08);
	float rail_e = step(0.92, p.x);
	// South / west only the SW knuckle — continues H bottom / V left into the
	// fillet without an SE stub (that was the outer bump).
	float rail_s = step(0.92, p.y) * step(p.x, 0.08);
	float rail_w = step(p.x, 0.08) * step(0.92, p.y);
	float frame = max(max(rail_n, rail_e), max(rail_s, rail_w));

	float groove_n = step(p.y, 0.14) * (1.0 - step(p.y, 0.08));
	float groove_e = step(p.x, 0.92) * (1.0 - step(p.x, 0.86));
	// Frame grooves only beside N/E (pad side); SW knuckle stays rail-clean.
	float frame_groove = max(groove_n, groove_e) * (1.0 - in_belt);

	col = mix(col, groove_color.rgb, clamp(frame_groove, 0.0, 1.0));
	col = mix(col, rail_color.rgb, clamp(frame, 0.0, 1.0));

	COLOR = vec4(col, 1.0);
}
