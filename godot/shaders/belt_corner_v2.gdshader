shader_type canvas_item;

// Paint mockup corner — pixel-matched:
// dark gallery plate; NE concave outer-arc pocket (light) + yellow;
// tiny SW inner fillet; N/E rails + SW knuckle; west light lip.
// Base: enter west → exit south. No umbrae.

uniform float arc_radius = 0.36;
uniform vec4 cover_color : source_color = vec4(0.141, 0.161, 0.184, 1.0); // 36,41,47
uniform vec4 pad_color : source_color = vec4(0.192, 0.231, 0.271, 1.0);   // 49,59,69
uniform vec4 rail_color : source_color = vec4(0.098, 0.118, 0.141, 1.0);  // 25,30,36
uniform vec4 yellow_color : source_color = vec4(1.0, 0.788, 0.047, 1.0);  // 255,201,12

void fragment() {
	vec2 p = UV;

	// Full-cell dark gallery.
	vec3 col = cover_color.rgb;

	// NE concave outer-arc pocket: outside quarter-circle from bend.
	vec2 bend = vec2(0.5, 0.5);
	float d_bend = length(p - bend);
	float ne_pocket = step(0.5, p.x) * step(p.y, 0.5) * step(arc_radius, d_bend);
	col = mix(col, pad_color.rgb, ne_pocket);

	// Yellow indicator fully inside the pocket (clear of east rail).
	vec2 yel_c = vec2(0.84, 0.20);
	float yel_d = length(p - yel_c);
	// Thin dark ring (paint outline), then yellow fill.
	float in_ring = ne_pocket * step(yel_d, 0.055) * step(0.042, yel_d);
	float in_yel = ne_pocket * step(yel_d, 0.042);
	col = mix(col, rail_color.rgb, in_ring);
	col = mix(col, yellow_color.rgb, in_yel);

	// Tiny SW inner fillet (paint: small light bite near knuckle).
	float in_fillet = step(length(p - vec2(0.10, 0.90)), 0.045)
		* step(0.07, p.x) * step(p.y, 0.93);
	col = mix(col, pad_color.rgb, in_fillet);

	// West light lip — flush with straight body.
	float west_lip = step(p.x, 0.07) * step(0.08, p.y) * step(p.y, 0.92);
	col = mix(col, pad_color.rgb, west_lip);

	// Frame: full N + E; SW knuckle only.
	float rail_n = step(p.y, 0.08);
	float rail_e = step(0.92, p.x);
	float rail_sw = step(0.92, p.y) * step(p.x, 0.08);
	col = mix(col, rail_color.rgb, max(max(rail_n, rail_e), rail_sw));

	COLOR = vec4(col, 1.0);
}
