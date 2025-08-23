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

//------------------------------------------------------------------------------------
// HOW TO TWEAK THE CRT EFFECTS
//------------------------------------------------------------------------------------
// To enable or disable an effect, add or remove the "//" before a #define line.
// To change the intensity of an effect, modify the 'static const float' values below.
// After making changes, you must REBUILD your Content.mgcb project for them to apply.
//
// GLITCH_FREQUENCY: How often a glitch event *might* happen (in seconds).
//   - 5.0 = A new glitch has a chance to appear every 5 seconds.
//
// GLITCH_PROBABILITY: The chance (0.0 to 1.0) that a glitch will actually occur
// during its frequency window.
//   - 0.4 = There is a 40% chance of a glitch happening in that window.
//
// GLITCH_BLOCK_HEIGHT: The vertical size of the tearing blocks as a percentage of screen height.
//   - 0.05 = Many small, thin tear lines.
//   - 0.2 = A few large, chunky tear blocks.
//
// GLITCH_INTENSITY: How far the screen tears horizontally as a percentage of screen width.
//   - 0.01 = A subtle, 1% screen width tear.
//   - 0.1+ = A very strong, jarring tear that shifts a large portion of the screen.
//------------------------------------------------------------------------------------

// --- Effect Toggles ---
#define ENABLE_CURVATURE
#define ENABLE_VIGNETTE
#define ENABLE_SCANLINES
#define ENABLE_SHADOW_MASK
#define ENABLE_CHROMATIC_ABERRATION
#define ENABLE_DITHERING
#define ENABLE_ROLLING_SCANLINE
#define ENABLE_CONTRAST
#define ENABLE_FILM_GRAIN
#define ENABLE_GLITCH

// --- Effect Intensity Values ---
static const float CURVATURE_AMOUNT = 0.15;
static const float VIGNETTE_INTENSITY = 0.8;
static const float SCANLINE_INTENSITY = 0.4;
static const float SHADOW_MASK_INTENSITY = 0.15;
static const float CHROMATIC_ABERRATION_AMOUNT = 2.0;
static const float DITHER_THRESHOLD = 1.0 / 255.0;
static const float CONTRAST_AMOUNT = 1.2;
// --- Rolling Scanline Parameters ---
static const float ROLLING_SCANLINE_SPEED = 0.15;
static const float ROLLING_SCANLINE_HEIGHT = 0.02;
static const float ROLLING_SCANLINE_DISTORTION = 0.002;
static const float ROLLING_SCANLINE_FREQUENCY = 4.0;
// --- Film Grain Parameters ---
static const float FILM_GRAIN_INTENSITY = 0.03; // MODIFIED: Changed from 0.05
// --- Glitch Parameters ---
static const float GLITCH_FREQUENCY = 5.0;
static const float GLITCH_PROBABILITY = 0.4;
static const float GLITCH_DURATION = 0.2;      // MODIFIED: Changed from 0.1
static const float GLITCH_BLOCK_HEIGHT = 0.08;
static const float GLITCH_INTENSITY = 0.02;     // MODIFIED: Changed from 0.01


// --- Shader Globals ---
Texture2D SpriteTexture;
sampler s0 = sampler_state { Texture = <SpriteTexture>; };

// A 4x4 Bayer matrix for ordered dithering
static const float DITHER_MATRIX[16] = {
     0.0,  8.0,  2.0, 10.0,
    12.0,  4.0, 14.0,  6.0,
     3.0, 11.0,  1.0,  9.0,
    15.0,  7.0, 13.0,  5.0
};

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
    float2 screenCoords = input.TexCoord * ScreenResolution;

#ifdef ENABLE_CURVATURE
    float dist = dot(uv, uv);
    sampleCoords = input.TexCoord + uv * dist * CURVATURE_AMOUNT;
    if (sampleCoords.x < 0 || sampleCoords.x > 1 || sampleCoords.y < 0 || sampleCoords.y > 1)
    {
        return float4(0, 0, 0, 1);
    }
#endif

#ifdef ENABLE_ROLLING_SCANLINE
    float wrappedTime = fmod(Time * ROLLING_SCANLINE_SPEED, ROLLING_SCANLINE_FREQUENCY);
    if (wrappedTime < 1.0)
    {
        float lineY = wrappedTime;
        float distFromLine = abs(input.TexCoord.y - lineY);
        if (distFromLine < ROLLING_SCANLINE_HEIGHT / 2.0)
        {
            float falloff = 1.0 - (distFromLine / (ROLLING_SCANLINE_HEIGHT / 2.0));
            sampleCoords.x += ROLLING_SCANLINE_DISTORTION * falloff;
        }
    }
#endif

#ifdef ENABLE_GLITCH
    // Create a unique ID for each time cycle. This will be our random seed.
    float cycle_id = floor(Time / GLITCH_FREQUENCY);
    
    // Generate a random number to decide if a glitch happens in this cycle.
    float glitch_trigger = rand(float2(cycle_id, cycle_id));

    if (glitch_trigger < GLITCH_PROBABILITY)
    {
        float time_in_cycle = fmod(Time, GLITCH_FREQUENCY);

        // Only apply the effect during its short duration.
        if (time_in_cycle < GLITCH_DURATION)
        {
            // Determine which horizontal block this pixel is in.
            float block_id = floor(input.TexCoord.y / GLITCH_BLOCK_HEIGHT);
            // Generate a random offset for this specific block during this specific glitch event.
            float glitch_offset = (rand(float2(cycle_id, block_id)) - 0.5) * 2.0 * GLITCH_INTENSITY;
            // Apply the offset.
            sampleCoords.x += glitch_offset;
        }
    }
#endif

    float4 color = (float4)0;

#ifdef ENABLE_CHROMATIC_ABERRATION
    float2 offset = uv * dist * (CHROMATIC_ABERRATION_AMOUNT / ScreenResolution.x);
    float r = tex2D(s0, sampleCoords - offset).r;
    float g = tex2D(s0, sampleCoords).g;
    float b = tex2D(s0, sampleCoords + offset).b;
    color = float4(r, g, b, 1.0);
#else
    color = tex2D(s0, sampleCoords);
#endif

#ifdef ENABLE_SCANLINES
    if (fmod(screenCoords.y, 2.0) < 1.0)
    {
        color.rgb -= SCANLINE_INTENSITY;
    }
#endif

#ifdef ENABLE_SHADOW_MASK
    float mask = sin(screenCoords.x * 3.14159) * sin(screenCoords.y * 3.14159);
    color.rgb *= 1.0 - (mask * SHADOW_MASK_INTENSITY);
#endif

#ifdef ENABLE_VIGNETTE
    float vignette = smoothstep(0.2, 0.8, length(uv));
    color.rgb *= 1.0 - (vignette * VIGNETTE_INTENSITY);
#endif

#ifdef ENABLE_DITHERING
    int matrix_index = (int)(fmod(screenCoords.x, 4)) + (int)(fmod(screenCoords.y, 4)) * 4;
    float dither_val = (DITHER_MATRIX[matrix_index] / 16.0 - 0.5) * DITHER_THRESHOLD;
    color.rgb += dither_val;
#endif

#ifdef ENABLE_CONTRAST
    color.rgb = lerp(0.5, color.rgb, CONTRAST_AMOUNT);
#endif

#ifdef ENABLE_FILM_GRAIN
    float grain = (rand(input.TexCoord + Time) - 0.5) * FILM_GRAIN_INTENSITY;
    color.rgb += grain;
#endif

    // Apply gamma correction as the final step before returning.
    color.rgb = max(color.rgb, 0.0);
    color.rgb = pow(color.rgb, 1.0 / Gamma);

	return color;
}

technique CRT
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
