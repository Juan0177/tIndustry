shader_type canvas_item;

// Paint mockup corner (galleria) — pixel-matched rebuild:
// solid dark cover; sharp NE pad pocket + yellow; small SW inner fillet;
// N/E rails + SW knuckle; west light lip. No umbrae / no soft halo.
// Base orientation: enter west → exit south.

uniform vec4 cover_color : source_color = vec4(0.141, 0.161, 0.184, 1.0); // 36,41,47
uniform vec4 pad_color : source_color = vec4(0.192, 0.231, 0.271, 1.0);   // 49,59,69
uniform vec4 rail_color : source_color = vec4(0.098, 0.118, 0.141, 1.0);  // 25,30,36
uniform vec4 yellow_color : source_color = vec4(1.0, 0.788, 0.047, 1.0);  // 255,201,12

void fragment() {
	vec2 p = UV;

	// Full-cell dark gallery plate.
	vec3 col = cover_color.rgb;

	// NE pad pocket — elliptical blob around the yellow (paint pixels).
	// Sharp edge (no soft halo).
	vec2 pad_c = vec2(0.80, 0.22);
	vec2 pad_ab = vec2(0.14, 0.12);
	float pad_d = length((p - pad_c) / pad_ab);
	float in_pad = step(pad_d, 1.0);
	col = mix(col, pad_color.rgb, in_pad);

	// Yellow indicator (sharp).
	float in_yel = step(length(p - vec2(0.88, 0.19)), 0.055);
	col = mix(col, yellow_color.rgb, in_yel);

	// SW inner fillet — small light quarter at the gallery knuckle.
	vec2 fi_c = vec2(0.11, 0.89);
	float in_fillet = step(length(p - fi_c), 0.08) * step(0.07, p.x) * step(p.y, 0.95);
	col = mix(col, pad_color.rgb, in_fillet);

	// West light lip — matches straight belt body at the butt joint.
	float west_lip = step(p.x, 0.07) * step(0.08, p.y) * step(p.y, 0.92);
	col = mix(col, pad_color.rgb, west_lip);

	// Square outer frame: full N + E; SW knuckle only (no SE south stub).
	float rail_n = step(p.y, 0.08);
	float rail_e = step(0.92, p.x);
	float rail_sw = step(0.92, p.y) * step(p.x, 0.08);
	float frame = max(max(rail_n, rail_e), rail_sw);
	col = mix(col, rail_color.rgb, frame);

	COLOR = vec4(col, 1.0);
}
