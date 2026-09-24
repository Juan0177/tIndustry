shader_type canvas_item;

// Full-cell corner, seamless with belt_scroll.
// Axis-aligned L (no SDF round inner corner). Outer SE has NO south-rail
// stub (that was the bump on the vertical join). Base: enter west → exit south.
//
// Inner SW is a hard ┘ knuckle: south rail only in the west rail column,
// west rail only in the south rail band. No groove stub on the bottom edge
// beside the knuckle (that read as a stepped notch).

uniform float half_width = 0.42;
uniform vec4 base_color : source_color = vec4(0.20, 0.24, 0.28, 1.0);
uniform vec4 rail_color : source_color = vec4(0.10, 0.12, 0.14, 1.0);
uniform vec4 groove_color : source_color = vec4(0.14, 0.16, 0.19, 1.0);

void fragment() {
	vec2 p = UV;

	// Shade matches belt_scroll on each arm.
	float shade_h = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(p.y - 0.5));
	float shade_v = 0.92 + 0.08 * smoothstep(0.0, 0.5, abs(p.x - 0.5));
	// Prefer horizontal shade on the west half, vertical on the south half.
	float use_v = step(0.5, p.y) * step(0.5 - half_width, p.x);
	float shade = mix(shade_h, shade_v, use_v);
	vec3 col = base_color.rgb * shade;

	// Rail bands match belt_scroll (8% / 92%).
	float rail_n = step(p.y, 0.08);
	float rail_e = step(0.92, p.x);

	// South: ONLY west rail column — continues H bottom border into the ┘.
	// No SE stub (avoids outer bump on the vertical join).
	float rail_s = step(0.92, p.y) * step(p.x, 0.08);

	// West: ONLY south rail band — continues V left border into the ┘.
	// Never invades N/S bands at the west entry.
	float rail_w = step(p.x, 0.08) * step(0.92, p.y);

	float rail = max(max(rail_n, rail_e), max(rail_s, rail_w));

	// Grooves: N/E full (match strips). South groove only above the SW knuckle
	// (west column). No west-groove on the bottom edge — that stub read as a
	// stepped notch beside the ┘. Vertical strip owns its own left groove below.
	float groove_n = step(p.y, 0.14) * (1.0 - step(p.y, 0.08));
	float groove_e = step(p.x, 0.92) * (1.0 - step(p.x, 0.86));
	float groove_s = step(p.y, 0.92) * (1.0 - step(p.y, 0.86)) * step(p.x, 0.08);
	float groove = max(max(groove_n, groove_e), groove_s);

	col = mix(col, groove_color.rgb, clamp(groove, 0.0, 1.0));
	col = mix(col, rail_color.rgb, clamp(rail, 0.0, 1.0));

	COLOR = vec4(col, 1.0);
}
