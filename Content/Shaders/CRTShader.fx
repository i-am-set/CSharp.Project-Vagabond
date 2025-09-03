#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// --- Uniforms (Variables from C#) ---
uniform float Time;
uniform float2 ScreenResolution;
uniform float Gamma;
uniform float3 FlashColor;
uniform float FlashIntensity;
uniform float ImpactGlitchIntensity;

// --- Effect Toggles ---
// #define ENABLE_VIGNETTE // Disabled as requested
#define ENABLE_CHROMATIC_ABERRATION
#define ENABLE_CONTRAST
#define ENABLE_IMPACT_GLITCH
#define ENABLE_SCANLINES
#define ENABLE_WOBBLE

// --- Effect Intensity Values ---
// static const float VIGNETTE_INTENSITY = 0.8; // Vignette is disabled
static const float CHROMATIC_ABERRATION_AMOUNT = 2.0;
static const float CONTRAST_AMOUNT = 1.2;
// --- Scanline Parameters ---
static const float SCANLINE_INTENSITY = 0.45;
static const float SCANLINE_FREQUENCY = 1.0f; // Set to 1.0f for 1:1 pixel scanlines
// --- Wobble Parameters ---
static const float WOBBLE_AMOUNT = 0.5;
static const float WOBBLE_FREQUENCY = 15.0;
static const float WOBBLE_VERTICAL_FREQUENCY = 250.0;
// --- Impact Glitch Parameters ---
static const float IMPACT_GLITCH_BLOCK_HEIGHT = 0.002;
static const float IMPACT_GLITCH_INTENSITY = 0.02;


// --- Shader Globals ---
Texture2D SpriteTexture;
sampler s0 = sampler_state { Texture = <SpriteTexture>; };

// A simple hash function to generate pseudo-random numbers in the shader.
float rand(float2 co)
{
    return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
}

struct PixelShaderInput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};

// --- Pixel Shader ---
float4 MainPS(PixelShaderInput input) : COLOR
{
    float2 uv = input.TexCoord - 0.5; 
    float2 sampleCoords = input.TexCoord;

#ifdef ENABLE_WOBBLE
    float wobble_offset = sin(Time * WOBBLE_FREQUENCY + sampleCoords.y * WOBBLE_VERTICAL_FREQUENCY) * (WOBBLE_AMOUNT / ScreenResolution.x);
    sampleCoords.x += wobble_offset;
#endif

#ifdef ENABLE_IMPACT_GLITCH
    if (ImpactGlitchIntensity > 0.0)
    {
        float block_id = floor(input.TexCoord.y / IMPACT_GLITCH_BLOCK_HEIGHT);
        float glitch_offset = (rand(float2(Time * 20.0, block_id)) - 0.5) * 2.0 * IMPACT_GLITCH_INTENSITY * ImpactGlitchIntensity;
        sampleCoords.x += glitch_offset;
    }
#endif

    float4 color = (float4)0;

#ifdef ENABLE_CHROMATIC_ABERRATION
    // Note: Curvature is disabled, so we calculate a simplified distance for aberration.
    float dist = dot(uv, uv);
    float2 offset = uv * dist * (CHROMATIC_ABERRATION_AMOUNT / ScreenResolution.x);
    float r = tex2D(s0, sampleCoords - offset).r;
    float g = tex2D(s0, sampleCoords).g;
    float b = tex2D(s0, sampleCoords + offset).b;
    color = float4(r, g, b, 1.0);
#else
    color = tex2D(s0, sampleCoords);
#endif

#ifdef ENABLE_VIGNETTE
    float vignette = smoothstep(0.2, 0.8, length(uv));
    color.rgb *= 1.0 - (vignette * VIGNETTE_INTENSITY);
#endif

#ifdef ENABLE_CONTRAST
    color.rgb = lerp(0.5, color.rgb, CONTRAST_AMOUNT);
#endif

#ifdef ENABLE_SCANLINES
    float scanline_phase = input.TexCoord.y * ScreenResolution.y * SCANLINE_FREQUENCY;
    // Use fmod to create sharp, alternating lines instead of a soft sine wave.
    // This creates a 1-pixel on, 1-pixel off pattern for a classic sharp scanline look.
    if (fmod(scanline_phase, 2.0) > 1.0)
    {
        color.rgb *= (1.0 - SCANLINE_INTENSITY);
    }
#endif

    color.rgb = max(color.rgb, 0.0);
    color.rgb = pow(color.rgb, 1.0 / Gamma);

    color.rgb = lerp(color.rgb, FlashColor, FlashIntensity);

	return color;
}

technique CRT
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};