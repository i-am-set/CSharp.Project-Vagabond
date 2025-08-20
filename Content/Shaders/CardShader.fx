#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// --- Uniforms (Variables from C#) ---
uniform float IsTemporary; // CHANGED from bool to float
uniform float Time;      // A continuously increasing value for animation

// --- TWEAKABLE CONSTANTS FOR GHOSTLY EFFECT ---
#define WAVE_AMPLITUDE 0.015
#define WAVE_FREQUENCY 15.0
#define WAVE_SPEED 2.5
#define ABERRATION_AMOUNT 0.01
#define GLOW_INTENSITY 0.4
#define GLOW_SIZE 0.4
#define GLOW_PULSE_SPEED 2.0
#define NOISE_INTENSITY 0.15
#define NOISE_SCALE 3.0
#define NOISE_SCROLL_SPEED_X 0.1
#define NOISE_SCROLL_SPEED_Y 0.3

// --- SHADER CODE ---

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

// A simple random function for generating noise
float random(float2 p)
{
    return frac(sin(dot(p.xy, float2(12.9898, 78.233))) * 43758.5453123);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 sampleCoords = input.TextureCoordinates;
    float4 finalColor = float4(0,0,0,0);

	// Check if the temporary flag is set (use > 0.5 for a clear boolean check)
	if (IsTemporary > 0.5)
	{
        // --- 1. WAVY DISTORTION ---
		float waveOffset = sin(sampleCoords.y * WAVE_FREQUENCY + Time * WAVE_SPEED) * WAVE_AMPLITUDE;
		sampleCoords.x += waveOffset;

        // --- 2. CHROMATIC ABERRATION ---
        float4 redChannel = tex2D(s0, sampleCoords + float2(ABERRATION_AMOUNT, 0));
        float4 greenChannel = tex2D(s0, sampleCoords);
        float4 blueChannel = tex2D(s0, sampleCoords - float2(ABERRATION_AMOUNT, 0));
        finalColor = float4(redChannel.r, greenChannel.g, blueChannel.b, greenChannel.a);

        // --- 3. SCROLLING NOISE ---
        float2 noiseCoords = input.TextureCoordinates * NOISE_SCALE + Time * float2(NOISE_SCROLL_SPEED_X, NOISE_SCROLL_SPEED_Y);
        float noise = random(noiseCoords) * NOISE_INTENSITY;
        finalColor.rgb += noise;

        // --- 4. GRAYSCALE ---
		finalColor.gb = finalColor.r;

        // --- 5. PULSING INNER GLOW ---
        float2 distFromCenter = abs(input.TextureCoordinates - 0.5) * 2.0;
        float glowMask = 1.0 - saturate(length(distFromCenter) / GLOW_SIZE);
        float pulse = (sin(Time * GLOW_PULSE_SPEED) + 1.0) / 2.0;
        float currentGlow = glowMask * GLOW_INTENSITY * pulse;
        finalColor.rgb += currentGlow;
	}
    else
    {
        // If not temporary, just sample the texture normally.
        finalColor = tex2D(s0, sampleCoords);
    }

    // Apply tinting and alpha from C# code.
	return finalColor * input.Color;
}

technique SpriteDrawing
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
