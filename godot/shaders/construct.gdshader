shader_type canvas_item;

// Construction site reveal, after Mindustry's `build` shader (ConstructBlock).
// The block sprite fills in from the bottom up to `progress` (0-1). Above the front the block is a dark
// see-through silhouette with only its outline in `color`; a 1-2 px scan line in `color` sits on the
// front and breathes with sin(time). Deconstruction uses the same shader with the remove colour while
// `progress` falls back to 0. A stalled site gets `time` advanced at half rate by the caller.

uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform vec4 color : source_color = vec4(1.0, 0.827, 0.498, 1.0); // Mindustry Pal.accent #FFD37F
uniform float time = 0.0;

const float ALPHA_CUT = 0.1;
const float SHADOW_ALPHA = 0.25;

// True when the texel at uv is transparent or outside the sprite.
bool empty_at(sampler2D tex, vec2 uv) {
	if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) {
		return true;
	}
	return texture(tex, uv).a < ALPHA_CUT;
}

// Opaque texel with a transparent (or out-of-sprite) neighbour: the block's outline.
bool on_outline(sampler2D tex, vec2 uv, vec2 texel) {
	return empty_at(tex, uv + vec2(0.0, texel.y)) || empty_at(tex, uv - vec2(0.0, texel.y))
		|| empty_at(tex, uv + vec2(texel.x, 0.0)) || empty_at(tex, uv - vec2(texel.x, 0.0));
}

// World "up" expressed in UV space, so a rotated sprite (a belt) still rises from the bottom of the screen.
varying vec2 up_uv;

void vertex() {
	up_uv = normalize(vec2(-MODEL_MATRIX[0].y, -MODEL_MATRIX[1].y));
}

void fragment() {
	vec4 base = texture(TEXTURE, UV);
	vec2 texel = TEXTURE_PIXEL_SIZE;
	// 0 at the bottom edge, 1 at the top: the block rises from the ground.
	float height = dot(UV - 0.5, up_uv) + 0.5;
	// Scan line about 1-2 screen pixels thick at any zoom (never thinner than one texel).
	float line_half = max(texel.y, fwidth(height)) * 0.75;

	if (base.a < ALPHA_CUT) {
		COLOR = vec4(0.0);
	} else if (abs(height - progress) <= line_half && progress > 0.0 && progress < 1.0) {
		// Scan line on the front, pulsing.
		float pulse = 0.65 + 0.35 * sin(time * 6.0);
		COLOR = vec4(color.rgb, color.a * pulse);
	} else if (height < progress) {
		// Built part: the sprite itself.
		COLOR = base;
	} else if (on_outline(TEXTURE, UV, texel)) {
		COLOR = color;
	} else {
		// Not yet built: dark silhouette.
		COLOR = vec4(base.rgb * 0.15, SHADOW_ALPHA * base.a);
	}
}
