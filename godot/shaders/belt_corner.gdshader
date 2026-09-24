shader_type canvas_item;

// Full-cell corner, seamless with belt_scroll.
// Soft SDF L path edge (the inner arc) is kept as a groove-tint cue.
// Bottom borders only: no SE south-rail stub, solid SW ┘ knuckle,
// no groove stub on the bottom edge beside the knuckle.
// Base art: enter west → exit south.

uniform float half_width = 0.40;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);

float sd_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * h);
}

void fragment() {
	vec2 p = UV;

	// Soft L path (west → south) — same shade blend as before so the arc
	// reads as a continuation of the abutting straights.
	vec2 a = vec2(0.0, 0.5);
	vec2 b = vec2(0.5, 0.5);
	vec2 c = vec2(0.5, 1.0);
	float dh = sd_segment(p, a, b);
	float dv = sd_segment(p, b, c);
	float d = min(dh, dv);
	float shade_h = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(p.y - 0.5));
	float shade_v = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(p.x - 0.5));
	float arm = dh / max(dh + dv, 1e-5);
	float shade = mix(shade_h, shade_v, arm);

	vec3 col = base_color.rgb * shade;

	// Outer rails — N and E always (outer elbow).
	float rail_n = step(p.y, 0.08);
	float rail_e = step(0.92, p.x);

	// South: ONLY west rail column (absolute 8%) — continues H bottom into the ┘.
	// Do NOT use (0.5 - half_width): with half_width 0.40 that spills past the
	// knuckle and reads as a bottom-edge step. No SE stub either.
	float rail_s = step(0.92, p.y) * step(p.x, 0.08);

	// West: mid-band below the entry (same as before), plus SW knuckle so the
	// inner ┘ meets the south rail cleanly (no notch).
	float open_w = step(abs(p.y - 0.5), half_width) * step(p.x, 0.14);
	float rail_w = step(p.x, 0.08)
		* step(0.14, p.y) * step(p.y, 0.86)
		* (1.0 - open_w);
	float knuckle = step(p.x, 0.08) * step(0.92, p.y);
	float rail = max(max(rail_n, rail_e), max(max(rail_s, rail_w), knuckle));

	float groove_n = step(p.y, 0.14) * (1.0 - step(p.y, 0.08));
	float groove_e = step(p.x, 0.92) * (1.0 - step(p.x, 0.86));
	// South groove only west of channel (matches rail_s); never a SE stub.
	float groove_s = step(p.y, 0.92) * (1.0 - step(p.y, 0.86))
		* step(p.x, 0.5 - half_width);
	// West groove in mid-band only — NOT on the bottom edge (y>=0.92), where
	// a stub beside the knuckle read as a stepped notch.
	float groove_w = step(p.x, 0.14) * (1.0 - step(p.x, 0.08))
		* step(0.14, p.y) * step(p.y, 0.86) * (1.0 - open_w);
	float groove = max(max(groove_n, groove_e), max(groove_s, groove_w));

	// Soft L path edge (the arc) — groove tint, not a darker filled channel.
	float path_edge = step(half_width - 0.04, d) * step(d, half_width);

	col = mix(col, groove_color.rgb, clamp(groove, 0.0, 1.0));
	col = mix(col, groove_color.rgb, path_edge * 0.55);
	col = mix(col, rail_color.rgb, clamp(rail, 0.0, 1.0));

	COLOR = vec4(col, 1.0);
}
