shader_type canvas_item;

// Full-cell corner: opaque SQUARE covering 100% of the 1×1 tile.
// Outer silhouette is square (rails on all four edges) — never a rounded pad
// that leaves terrain in the unused quadrant. L is only a recessed groove.
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

	float rail_n = step(p.y, 0.08);
	float rail_s = step(0.92, p.y);
	float rail_w = step(p.x, 0.08);
	float rail_e = step(0.92, p.x);
	float open_w = step(abs(p.y - 0.5), half_width) * step(p.x, 0.08);
	float open_s = step(abs(p.x - 0.5), half_width) * step(0.92, p.y);
	float rail = max(max(rail_n, rail_s), max(rail_w, rail_e));
	rail *= (1.0 - open_w) * (1.0 - open_s);

	float groove_n = step(p.y, 0.14) * (1.0 - step(p.y, 0.08));
	float groove_s = step(p.y, 0.92) * (1.0 - step(p.y, 0.86));
	float groove_w = step(p.x, 0.14) * (1.0 - step(p.x, 0.08));
	float groove_e = step(p.x, 0.92) * (1.0 - step(p.x, 0.86));
	float groove = max(max(groove_n, groove_s), max(groove_w, groove_e));
	groove *= (1.0 - open_w) * (1.0 - open_s);

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
