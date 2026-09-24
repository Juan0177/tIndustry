shader_type canvas_item;

// Paint mockup corner — polished to pixel reference:
// dark gallery; deep NE outer-arc pocket + yellow; tiny SW fillet;
// thin west lip; N/E rails + SW knuckle. Base: enter-west → exit-south.

uniform float arc_radius = 0.43;
uniform vec4 cover_color : source_color = vec4(0.141, 0.161, 0.184, 1.0); // 36,41,47
uniform vec4 pad_color : source_color = vec4(0.192, 0.231, 0.271, 1.0);   // 49,59,69
uniform vec4 rail_color : source_color = vec4(0.098, 0.118, 0.141, 1.0);  // 25,30,36
uniform vec4 yellow_color : source_color = vec4(1.0, 0.788, 0.047, 1.0);  // 255,201,12
uniform vec4 yel_ring_color : source_color = vec4(0.212, 0.255, 0.282, 1.0); // paint ring avg

void fragment() {
	vec2 p = UV;

	vec3 col = cover_color.rgb;

	// NE outer-arc pocket (deep concave cut). Soft AA via fwidth.
	vec2 bend = vec2(0.5, 0.5);
	float d_bend = length(p - bend);
	float aa = max(fwidth(d_bend) * 1.25, 0.004);
	float ne_quad = step(0.48, p.x) * step(p.y, 0.52);
	float ne_pocket = ne_quad * smoothstep(arc_radius - aa, arc_radius + aa, d_bend);
	col = mix(col, pad_color.rgb, ne_pocket);

	// Yellow @ paint UV with thin mid-tone ring (not heavy black).
	vec2 yel_c = vec2(0.87, 0.20);
	float yel_d = length(p - yel_c);
	float yel_aa = max(fwidth(yel_d) * 1.25, 0.003);
	float in_ring = ne_pocket * (1.0 - smoothstep(0.048 - yel_aa, 0.048 + yel_aa, yel_d))
		* smoothstep(0.036 - yel_aa, 0.036 + yel_aa, yel_d);
	float in_yel = ne_pocket * (1.0 - smoothstep(0.036 - yel_aa, 0.036 + yel_aa, yel_d));
	col = mix(col, yel_ring_color.rgb, in_ring);
	col = mix(col, yellow_color.rgb, in_yel);

	// Tiny SW inner fillet — paint is a small bite, not a bulb.
	vec2 fi_c = vec2(0.09, 0.91);
	float fi_d = length(p - fi_c);
	float fi_aa = max(fwidth(fi_d) * 1.25, 0.003);
	float in_fillet = (1.0 - smoothstep(0.032 - fi_aa, 0.032 + fi_aa, fi_d))
		* step(0.065, p.x) * step(p.y, 0.94);
	col = mix(col, pad_color.rgb, in_fillet);

	// West light lip (~7% — matches paint strip width).
	float west_lip = step(p.x, 0.07) * step(0.08, p.y) * step(p.y, 0.92);
	col = mix(col, pad_color.rgb, west_lip);

	// Frame: full N + E; SW knuckle only.
	float rail_n = step(p.y, 0.08);
	float rail_e = step(0.92, p.x);
	float rail_sw = step(0.92, p.y) * step(p.x, 0.08);
	col = mix(col, rail_color.rgb, max(max(rail_n, rail_e), rail_sw));

	COLOR = vec4(col, 1.0);
}
