shader_type canvas_item;

// Full-cell corner: opaque SQUARE. Rails on all four sides for the square
// silhouette, but entry (west) / exit (south) rails are truncated so they
// never invade the N/S (or E/W) border bands — that invasion was the
// "rientranza" step at the straight↔corner seam.
// Base art: enter west → exit south.

uniform float half_width = 0.32;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);
uniform vec4 channel_color : source_color = vec4(0.15, 0.18, 0.22, 1.0);

float sd_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * h);
}

void fragment() {
	vec2 p = UV;
	vec3 col = base_color.rgb;

	// Continuous borders shared with abutting straights (same 8% bands).
	float rail_n = step(p.y, 0.08);
	float rail_e = step(0.92, p.x);

	// South rail continues the horizontal strip's bottom border, open at exit.
	float open_s = step(abs(p.x - 0.5), half_width);
	float rail_s = step(0.92, p.y) * (1.0 - open_s);

	// West rail continues the vertical strip's left border BELOW the channel,
	// and never enters the N/S rail bands (that caused the seam notch).
	float open_w = step(abs(p.y - 0.5), half_width);
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

	vec2 a = vec2(0.0, 0.5);
	vec2 b = vec2(0.5, 0.5);
	vec2 c = vec2(0.5, 1.0);
	float d = min(sd_segment(p, a, b), sd_segment(p, b, c));
	float in_channel = step(d, half_width);

	col = mix(col, groove_color.rgb, clamp(groove, 0.0, 1.0));
	col = mix(col, channel_color.rgb, in_channel);
	col = mix(col, rail_color.rgb, clamp(rail, 0.0, 1.0));

	COLOR = vec4(col, 1.0);
}
