shader_type canvas_item;

// Full-cell corner that READS as a seamless continuation of belt_scroll.
// Same base/rail/groove/shade language — no darker "channel blotch" at the join.
// Outer square fill; L is only a subtle path cue (same body color).
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

	// Same shade formula as belt_scroll, blended across the L so each arm
	// matches the abutting straight (no mid-band color jump at the seam).
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

	// Outer rails — N and E always (outer elbow). S continues bottom of
	// horizontal run except at south exit opening; W only below channel
	// (never thickens N/S bands → no rientranza).
	float rail_n = step(p.y, 0.08);
	float rail_e = step(0.92, p.x);
	float open_s = step(abs(p.x - 0.5), half_width) * step(0.86, p.y);
	float rail_s = step(0.92, p.y) * (1.0 - open_s);
	float open_w = step(abs(p.y - 0.5), half_width) * step(p.x, 0.14);
	float rail_w = step(p.x, 0.08)
		* step(0.14, p.y) * step(p.y, 0.86)
		* (1.0 - open_w);
	float rail = max(max(rail_n, rail_e), max(rail_s, rail_w));

	float groove_n = step(p.y, 0.14) * (1.0 - step(p.y, 0.08));
	float groove_e = step(p.x, 0.92) * (1.0 - step(p.x, 0.86));
	float groove_s = step(p.y, 0.92) * (1.0 - step(p.y, 0.86)) * (1.0 - open_s);
	float groove_w = step(p.x, 0.14) * (1.0 - step(p.x, 0.08))
		* step(0.14, p.y) * step(p.y, 0.86) * (1.0 - open_w);
	float groove = max(max(groove_n, groove_e), max(groove_s, groove_w));

	// Soft L path edge (same groove color) — not a darker filled channel.
	float path_edge = step(half_width - 0.04, d) * step(d, half_width);

	col = mix(col, groove_color.rgb, clamp(groove, 0.0, 1.0));
	col = mix(col, groove_color.rgb, path_edge * 0.55);
	col = mix(col, rail_color.rgb, clamp(rail, 0.0, 1.0));

	COLOR = vec4(col, 1.0);
}
