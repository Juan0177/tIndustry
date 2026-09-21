shader_type canvas_item;

// Corner as a static blue metal *platform* (no scrolling arrows).
// L deck connects west → south; unused quadrant stays transparent.
// Sprite rotation / Y-flip covers other 90° turns.

uniform float half_width = 0.39;
uniform vec4 deck_color : source_color = vec4(0.22, 0.30, 0.38, 1.0);
uniform vec4 deck_hi : source_color = vec4(0.32, 0.42, 0.52, 1.0);
uniform vec4 deck_lo : source_color = vec4(0.14, 0.18, 0.24, 1.0);
uniform vec4 rim_color : source_color = vec4(0.08, 0.10, 0.13, 1.0);
uniform vec4 channel_color : source_color = vec4(0.16, 0.20, 0.26, 1.0);

float sd_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * h);
}

void fragment() {
	vec2 p = UV;
	float hw = half_width;

	// Platform footprint: L arms, slightly wider than the belt channel for a "pad" look.
	float pad = hw + 0.08;
	float in_h = step(0.0, p.x) * step(p.x, 0.5 + pad)
		* step(0.5 - pad, p.y) * step(p.y, 0.5 + pad);
	float in_v = step(0.5 - pad, p.x) * step(p.x, 0.5 + pad)
		* step(0.5 - pad, p.y) * step(p.y, 1.0);
	float inside = max(in_h, in_v);

	// Soft bevel: brighter toward inner bend, darker on outer rim.
	float to_inner = length(p - vec2(0.35, 0.65));
	float bevel = smoothstep(0.55, 0.05, to_inner);
	vec3 deck = mix(deck_lo.rgb, deck_hi.rgb, bevel * 0.55 + 0.25);
	deck = mix(deck, deck_color.rgb, 0.55);

	// Static L channel (belt path) — groove only, no arrows.
	vec2 a = vec2(0.0, 0.5);
	vec2 b = vec2(0.5, 0.5);
	vec2 c = vec2(0.5, 1.0);
	float d = min(sd_segment(p, a, b), sd_segment(p, b, c));
	float in_channel = 1.0 - smoothstep(hw - 0.03, hw, d);
	float channel_edge = smoothstep(hw - 0.06, hw - 0.02, d) * in_channel;

	deck = mix(deck, channel_color.rgb, in_channel * 0.85);
	deck = mix(deck, rim_color.rgb, channel_edge);

	// Outer platform rim.
	float on_h_edge = in_h * (step(p.y, 0.5 - pad + 0.05) + step(0.5 + pad - 0.05, p.y));
	float on_v_edge = in_v * (step(p.x, 0.5 - pad + 0.05) + step(0.5 + pad - 0.05, p.x));
	float outer_rim = clamp(on_h_edge + on_v_edge, 0.0, 1.0);
	// Cap ends (west / south openings stay open for strip join).
	float west_cap = step(p.x, 0.04) * in_h;
	float south_cap = step(0.96, p.y) * in_v;
	outer_rim *= (1.0 - max(west_cap, south_cap));
	deck = mix(deck, rim_color.rgb, outer_rim * 0.9);

	// Tiny rivet dots on the pad (static platform detail).
	vec2 rivet_uv = fract((p - vec2(0.12, 0.12)) * 6.5);
	float rivet = step(length(rivet_uv - vec2(0.5)), 0.12);
	float on_pad = inside * (1.0 - in_channel);
	deck = mix(deck, deck_lo.rgb, rivet * on_pad * 0.65);

	COLOR = vec4(deck, inside);
}
