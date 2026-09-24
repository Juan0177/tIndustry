shader_type canvas_item;

// Corner recipe (base: enter side 1 / west → exit side 4 / south):
//  1=W  2=N  3=E  4=S
// Blu1 #191E24 — outer border on sides opposite the curve (2+3) + knuckle at focus (1∩4)
// Blu2 #24292F — second border on those same outer sides (2+3)
// Blu3 #323C46 — exterior of the arc (opposite the focus)
// Blu4 #86A7B8 — tube arc from focus; interior is Blu4; covers Blu2 in part; stays inside Blu1

uniform float border1 = 0.08;
uniform float border2 = 0.14;
uniform float tube_radius = 0.88;
uniform vec4 blu1 : source_color = vec4(0.098, 0.118, 0.141, 1.0); // #191E24
uniform vec4 blu2 : source_color = vec4(0.141, 0.161, 0.184, 1.0); // #24292F
uniform vec4 blu3 : source_color = vec4(0.196, 0.235, 0.275, 1.0); // #323C46
uniform vec4 blu4 : source_color = vec4(0.525, 0.655, 0.722, 1.0); // #86A7B8

void fragment() {
	vec2 p = UV;

	// 1) Exterior fill (outside the arc, opposite the focus).
	vec3 col = blu3.rgb;

	// 2) Second border Blu2 on outer sides of the curve (N=2, E=3).
	float b2_n = step(p.y, border2);
	float b2_e = step(1.0 - border2, p.x);
	col = mix(col, blu2.rgb, max(b2_n, b2_e));

	// 3) Tube: filled arc from focus at inner corner (1∩4 = SW).
	vec2 focus = vec2(0.0, 1.0);
	float d = length(p - focus);
	float aa = max(fwidth(d) * 1.25, 0.004);
	float in_tube = 1.0 - smoothstep(tube_radius - aa, tube_radius + aa, d);
	col = mix(col, blu4.rgb, in_tube);

	// Exterior of the arc in the cell interior must stay Blu3
	// (Blu2 bands on N/E remain where the tube does not cover them).
	float interior = step(border2, p.y) * step(p.x, 1.0 - border2);
	col = mix(col, blu3.rgb, (1.0 - in_tube) * interior);

	// 4) Outer border Blu1 on sides 2+3, plus focus knuckle square at 1∩4.
	float b1_n = step(p.y, border1);
	float b1_e = step(1.0 - border1, p.x);
	float knuckle = step(p.x, border1) * step(1.0 - border1, p.y);
	col = mix(col, blu1.rgb, max(max(b1_n, b1_e), knuckle));

	COLOR = vec4(col, 1.0);
}
