shader_type canvas_item;

// Paint mockup corner (galleria) — rebuilt from pixel reference:
// solid dark cover over the full cell; circular NE pad halo + yellow
// indicator; N/E rails + SW knuckle; west light lip. No umbrae.
// Base orientation: enter west → exit south.

uniform vec4 cover_color : source_color = vec4(0.141, 0.161, 0.184, 1.0); // 36,41,47
uniform vec4 pad_color : source_color = vec4(0.192, 0.231, 0.271, 1.0);   // 49,59,69
uniform vec4 rail_color : source_color = vec4(0.098, 0.118, 0.141, 1.0);  // 25,30,36
uniform vec4 yellow_color : source_color = vec4(1.0, 0.788, 0.047, 1.0);  // 255,201,12

void fragment() {
	vec2 p = UV;

	// Full-cell dark gallery plate (covers path + unused quadrants).
	vec3 col = cover_color.rgb;

	// NE pad halo around the yellow indicator (paint outer-arc pocket).
	vec2 pad_c = vec2(0.82, 0.22);
	float pad_r = 0.12;
	float in_pad = 1.0 - smoothstep(pad_r - 0.01, pad_r + 0.008, length(p - pad_c));
	col = mix(col, pad_color.rgb, in_pad);

	// Yellow indicator.
	vec2 yel_c = vec2(0.88, 0.19);
	float yel_r = 0.052;
	float in_yel = 1.0 - smoothstep(yel_r - 0.006, yel_r + 0.002, length(p - yel_c));
	col = mix(col, yellow_color.rgb, in_yel);

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
