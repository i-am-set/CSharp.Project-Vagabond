#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// This shader does not require any external uniforms.
// It derives all necessary data from the vertex color.

Texture2D SpriteTexture;
sampler s0 = sampler_state
{
	Texture = <SpriteTexture>;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0; // R = lifeRatio, A = alpha
	float2 TextureCoordinates : TEXCOORD0;
};

// --- HEATMAP GRADIENT COLORS ---
// These define the color progression from the hot center to the cool edge.
static const float4 COLOR_WHITE = float4(1.0, 1.0, 1.0, 1.0);
static const float4 COLOR_YELLOW = float4(1.0, 1.0, 0.0, 1.0);
static const float4 COLOR_ORANGE = float4(1.0, 0.5, 0.0, 1.0);
static const float4 COLOR_RED = float4(1.0, 0.0, 0.0, 1.0);

float4 MainPS(VertexShaderOutput input) : COLOR
{
    // Sample the particle's texture (the ember sprite).
    // texColor.rgb will be (1,1,1) for the white parts of the ember and (0,0,0) for transparent parts.
    // texColor.a will be 1.0 for the ember shape and 0.0 for transparent parts.
    float4 texColor = tex2D(s0, input.TextureCoordinates);

    // Unpack data from the vertex color attribute.
    // 'lifeRatio' is the particle's age / lifetime (0.0 at spawn, 1.0 at death).
    // 'calculatedAlpha' is the final transparency calculated in C# over the particle's lifetime.
	float lifeRatio = input.Color.r;
	float calculatedAlpha = input.Color.a;

	float4 heatmapColor;

    // Calculate the heatmap color based on the particle's life ratio.
	if (lifeRatio < 0.15)
	{
		// Stage 1: White to Yellow (0% to 15% of life)
		heatmapColor = lerp(COLOR_WHITE, COLOR_YELLOW, lifeRatio / 0.15);
	}
	else if (lifeRatio < 0.4)
	{
		// Stage 2: Yellow to Orange (15% to 40% of life)
		heatmapColor = lerp(COLOR_YELLOW, COLOR_ORANGE, (lifeRatio - 0.15) / 0.25);
	}
	else
	{
		// Stage 3: Orange to Red (40% to 100% of life)
		heatmapColor = lerp(COLOR_ORANGE, COLOR_RED, (lifeRatio - 0.4) / 0.6);
	}

    // The final color is the heatmap color, masked by the texture's color.
    // This applies the heatmap only to the white parts of the ember sprite.
    float3 finalRgb = heatmapColor.rgb * texColor.rgb;

    // The final alpha is the particle's lifetime alpha combined with the texture's alpha (shape).
    // This cuts out the shape of the ember from the particle square.
    float finalAlpha = calculatedAlpha * texColor.a;

	return float4(finalRgb, finalAlpha);
}

technique Fireball
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};