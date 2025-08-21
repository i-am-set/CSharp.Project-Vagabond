#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// --- Uniforms (Variables from C#) ---
uniform float IsTemporary; // MODIFIED: Changed from bool to float for compatibility
uniform float Time; 

// --- Tweakable Constants ---
#define WAVE_AMPLITUDE 0.015
#define WAVE_FREQUENCY 15.0
#define WAVE_SPEED 2.5

Texture2D SpriteTexture;
sampler s0 = sampler_state
{
	Texture = <SpriteTexture>;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 sampleCoords = input.TextureCoordinates;

	// --- Ghostly Wave Effect ---
    // MODIFIED: Check if the float is greater than 0.5 to treat it as true
	if (IsTemporary > 0.5f)
	{
		float waveOffset = sin(sampleCoords.y * WAVE_FREQUENCY + Time * WAVE_SPEED) * WAVE_AMPLITUDE;
		sampleCoords.x += waveOffset;
	}

	float4 color = tex2D(s0, sampleCoords);
	
    // --- Grayscale Effect ---
    // MODIFIED: Check if the float is greater than 0.5 to treat it as true
	if (IsTemporary > 0.5f)
	{
		color.gb = color.r;
	}

	return color * input.Color;
}

technique SpriteDrawing
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
