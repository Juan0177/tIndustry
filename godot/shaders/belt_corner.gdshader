shader_type canvas_item;

// Full-cell static blue *platform* pad (factory plate). No arrows.
// Recessed L channel carries items; the rest of the tile is opaque deck
// so the corner reads as a junction platform, not another belt strip.
// Sprite rotation / Y-flip covers other 90° turns.

uniform float half_width = 0.39;
uniform vec4 deck_color : source_color = vec4(0.28, 0.40, 0.52, 1.0);
uniform vec4 deck_hi : source_color = vec4(0.40, 0.55, 0.68, 1.0);
uniform vec4 deck_lo : source_color = vec4(0.16, 0.22, 0.30, 1.0);
uniform vec4 rim_color : source_color = vec4(0.06, 0.08, 0.10, 1.0);
uniform vec4 channel_color : source_color = vec4(0.12, 0.15, 0.20, 1.0);
uniform vec4 bolt_color : source_color = vec4(0.55, 0.62, 0.70, 1.0);

float sd_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * h);
}

void fragment() {
	vec2 p = UV;
	float hw = half_width;

	// Full opaque cell — this is the platform footprint.
	float margin = 0.04;
	float in_tile = step(margin, p.x) * step(p.x, 1.0 - margin)
		* step(margin, p.y) * step(p.y, 1.0 - margin);

	// Soft panel gradient (reads as raised plate).
	float panel = smoothstep(0.0, 1.0, 1.0 - length(p - vec2(0.45, 0.55)));
	vec3 deck = mix(deck_lo.rgb, deck_hi.rgb, panel * 0.7);
	deck = mix(deck, deck_color.rgb, 0.45);

	// Panel inset line near the rim.
	float inset = step(margin + 0.03, p.x) * step(p.x, 1.0 - margin - 0.03)
		* step(margin + 0.03, p.y) * step(p.y, 1.0 - margin - 0.03);
	float rim_band = in_tile * (1.0 - inset);
	deck = mix(deck, rim_color.rgb, rim_band);

	// Recessed L channel (west mid → center → south mid), no marks.
	vec2 a = vec2(0.0, 0.5);
	vec2 b = vec2(0.5, 0.5);
	vec2 c = vec2(0.5, 1.0);
	float d = min(sd_segment(p, a, b), sd_segment(p, b, c));
	float in_channel = 1.0 - smoothstep(hw - 0.04, hw, d);
	float lip = smoothstep(hw - 0.08, hw - 0.03, d) * in_channel;

	deck = mix(deck, channel_color.rgb, in_channel);
	deck = mix(deck, rim_color.rgb, lip * 0.85);

	// Four corner bolts — unmistakable platform detail.
	vec2 b0 = vec2(0.14, 0.14);
	vec2 b1 = vec2(0.86, 0.14);
	vec2 b2 = vec2(0.14, 0.86);
	vec2 b3 = vec2(0.86, 0.86);
	float bolt = max(max(
		1.0 - smoothstep(0.035, 0.055, length(p - b0)),
		1.0 - smoothstep(0.035, 0.055, length(p - b1))), max(
		1.0 - smoothstep(0.035, 0.055, length(p - b2)),
		1.0 - smoothstep(0.035, 0.055, length(p - b3))));
	float bolt_core = max(max(
		1.0 - smoothstep(0.012, 0.025, length(p - b0)),
		1.0 - smoothstep(0.012, 0.025, length(p - b1))), max(
		1.0 - smoothstep(0.012, 0.025, length(p - b2)),
		1.0 - smoothstep(0.012, 0.025, length(p - b3))));

	float on_deck = in_tile * (1.0 - in_channel);
	deck = mix(deck, bolt_color.rgb, bolt * on_deck);
	deck = mix(deck, rim_color.rgb, bolt_core * on_deck);

	// Outer black frame of the whole tile.
	float frame = in_tile * (
		step(p.x, margin + 0.025) + step(1.0 - margin - 0.025, p.x)
		+ step(p.y, margin + 0.025) + step(1.0 - margin - 0.025, p.y)
	);
	deck = mix(deck, rim_color.rgb, clamp(frame, 0.0, 1.0));

	COLOR = vec4(deck, in_tile);
}
