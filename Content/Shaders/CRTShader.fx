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

//------------------------------------------------------------------------------------
// HOW TO TWEAK THE CRT EFFECTS
//------------------------------------------------------------------------------------
// To enable or disable an effect, add or remove the "//" before a #define line.
// To change the intensity of an effect, modify the 'static const float' values below.
// After making changes, you must REBUILD your Content.mgcb project for them to apply.
//
// JITTER_FREQUENCY: How often the jitter effect happens (in seconds).
//   - 5.0 = A jitter event will occur once every 5 seconds.
//
// JITTER_DURATION: How long the jitter event lasts (in seconds).
//   - 0.2 = The screen will shake for a brief 0.2 seconds.
//
// JITTER_SPEED: How many times the screen "jolts" to a new position per second
// during a jitter event.
//   - 10.0 = The screen will jump 10 times per second. A lower value is slower.
//
// JITTER_INTENSITY: How far the screen shakes. Measured in virtual pixels.
//   - 1.0 = A very subtle, 1-pixel shake.
//   - 3.0+ = A much more noticeable, glitchy shake.
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
#define ENABLE_JITTER // <-- NEW EFFECT TOGGLE

// --- Effect Intensity Values ---
static const float CURVATURE_AMOUNT = 0.1;
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
// --- NEW: Jitter Parameters ---
static const float JITTER_FREQUENCY = 4.0; // Jitter will happen once every 4 seconds.
static const float JITTER_DURATION = 0.2;  // It will last for 0.2 seconds.
static const float JITTER_SPEED = 12.0;    // It will jolt 12 times per second during the event.
static const float JITTER_INTENSITY = 1.5; // It will shake by a maximum of 1.5 pixels.


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

#ifdef ENABLE_JITTER
    // Check if we are inside the sporadic jitter event window
    if (fmod(Time, JITTER_FREQUENCY) < JITTER_DURATION)
    {
        // Create a "stepped" time to make the jitter change slower than every frame
        float steppedTime = floor(Time * JITTER_SPEED);
        // Generate two random numbers based on the stepped time for X and Y offsets
        float offsetX = (rand(float2(steppedTime, steppedTime * 0.5)) - 0.5) * (JITTER_INTENSITY / ScreenResolution.x);
        float offsetY = (rand(float2(steppedTime * 0.8, steppedTime)) - 0.5) * (JITTER_INTENSITY / ScreenResolution.y);
        // Apply the offset
        sampleCoords += float2(offsetX, offsetY);
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

	return color;
}

technique CRT
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
