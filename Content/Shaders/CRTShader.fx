#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// --- Uniforms ---
uniform float Time;
uniform float2 ScreenResolution;  // Physical window size (e.g., 1920x1080)
uniform float2 VirtualResolution; // Game resolution (e.g., 320x180)
uniform float Gamma;
uniform float3 FlashColor;
uniform float FlashIntensity;
uniform float ImpactGlitchIntensity;
uniform float Saturation;
uniform float Vibrance;

// --- Toggles ---
#define ENABLE_CURVATURE
#define ENABLE_SCANLINES
#define ENABLE_WOBBLE
#define ENABLE_VIGNETTE
#define ENABLE_CHROMATIC_ABERRATION
#define ENABLE_NOISE

// --- Tuning ---

// Curvature (Lens Distortion)
static const float CURVATURE = 0.45; // 0.0 = Flat, 0.15 = Standard CRT, 0.4 = Fish eye

// Scanlines
static const float SCANLINE_OPACITY_MIN = 0.65f; // The darkness of the gap (1.0 = invisible, 0.0 = black)
static const float SCANLINE_OPACITY_MAX = 1.0f;  // The brightness of the beam center

// Wobble (Distortion)
static const float WOBBLE_FREQUENCY = 1.0;
static const float WOBBLE_AMPLITUDE = 1.0; // In physical pixels (keep small for crispness)

// Chromatic Aberration
static const float CHROMATIC_OFFSET = 1.2; // In physical pixels

// Vignette
static const float VIGNETTE_INTENSITY = 0.3;
static const float VIGNETTE_ROUNDNESS = 0.25;

// Noise
static const float NOISE_INTENSITY = 0.005;

// --- Globals ---
Texture2D SpriteTexture;
sampler s0 = sampler_state { Texture = <SpriteTexture>; };

float rand(float2 co) {
    return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
}

struct PixelShaderInput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};

float4 MainPS(PixelShaderInput input) : COLOR
{
    float2 uv = input.TexCoord;

    // --- 0. CURVATURE (Lens Distortion) ---
    // This must happen first so all other effects (scanlines, wobble) follow the curve.
#ifdef ENABLE_CURVATURE
    // Transform UV to -0.5 to 0.5 range (center of screen is 0,0)
    float2 centeredUV = uv - 0.5;
    float r2 = dot(centeredUV, centeredUV); // Distance squared from center
    
    // Apply barrel distortion: uv = uv * (1 + k * r^2)
    // We adjust the formula slightly to keep the center 1:1 scale
    float2 distortedUV = centeredUV * (1.0 + CURVATURE * r2);
    
    // Transform back to 0.0 to 1.0 range
    uv = distortedUV + 0.5;

    // Bezel Mask: If the distorted UV is outside the texture, draw black
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
    {
        return float4(0.0, 0.0, 0.0, 1.0);
    }
#endif
    
    // --- 1. WOBBLE (Distortion) ---
    // We calculate the offset based on ScreenResolution so the wave stays tight and crisp
    // regardless of how big the window is.
#ifdef ENABLE_WOBBLE
    float wobble = sin(uv.y * ScreenResolution.y * 0.02 + Time * WOBBLE_FREQUENCY) * WOBBLE_AMPLITUDE;
    // Apply glitch intensity if active
    if (ImpactGlitchIntensity > 0.0) {
        float block = floor(uv.y * 20.0); // Large blocks for glitch
        wobble += (rand(float2(Time, block)) - 0.5) * 20.0 * ImpactGlitchIntensity;
    }
    uv.x += wobble / ScreenResolution.x;
#endif

    // --- 2. CHROMATIC ABERRATION ---
    // Sample colors with slight offsets (in physical pixels)
    float3 color;
#ifdef ENABLE_CHROMATIC_ABERRATION
    float2 rOffset = float2(CHROMATIC_OFFSET / ScreenResolution.x, 0.0);
    float2 bOffset = float2(-CHROMATIC_OFFSET / ScreenResolution.x, 0.0);
    
    color.r = tex2D(s0, uv + rOffset).r;
    color.g = tex2D(s0, uv).g;
    color.b = tex2D(s0, uv + bOffset).b;
#else
    color = tex2D(s0, uv).rgb;
#endif

    // --- 3. SCANLINES ---
    // We use VirtualResolution.y to align scanlines with the game's fat pixels.
    // We use a Sine wave to create a smooth "beam" profile instead of harsh black bars.
#ifdef ENABLE_SCANLINES
    // Calculate position in the virtual grid (0.0 to 1.0 within a single game pixel row)
    float scanlinePos = uv.y * VirtualResolution.y * 3.14159 * 2.0;
    
    // Create a sine wave that oscillates between -1 and 1
    float scanlineWave = sin(scanlinePos);
    
    // Map the wave to our opacity range (e.g., 0.7 to 1.0)
    // This creates the "glow" in the center of the pixel and the "dim" at the edge.
    float scanlineFactor = lerp(SCANLINE_OPACITY_MIN, SCANLINE_OPACITY_MAX, (scanlineWave * 0.5 + 0.5));
    
    color *= scanlineFactor;
#endif

    // --- 4. SATURATION & VIBRANCE ---
    // Luminance coefficient
    float3 lumaCoef = float3(0.299, 0.587, 0.114);
    float luma = dot(color, lumaCoef);

    // Standard Saturation
    color = lerp(float3(luma, luma, luma), color, Saturation);

    // Vibrance (Smart Saturation)
    // Calculate current saturation level roughly
    float max_color = max(color.r, max(color.g, color.b));
    float min_color = min(color.r, min(color.g, color.b));
    float color_sat = max_color - min_color;

    // Increase saturation based on how low the current saturation is
    // If Vibrance is positive, we boost muted colors more.
    color = lerp(float3(luma, luma, luma), color, 1.0 + (Vibrance * (1.0 - color_sat)));

    // --- 5. VIGNETTE ---
#ifdef ENABLE_VIGNETTE
    float2 vUV = uv * (1.0 - uv.yx); // Classic vignette equation
    float vig = vUV.x * vUV.y * 15.0;
    vig = pow(vig, VIGNETTE_ROUNDNESS);
    color *= float3(vig, vig, vig); // Multiply instead of mix for better color retention
#endif

    // --- 6. NOISE (Dithering) ---
#ifdef ENABLE_NOISE
    float noise = (rand(uv * Time) - 0.5) * NOISE_INTENSITY;
    color += noise;
#endif

    // --- 7. GAMMA & FLASH ---
    // Apply Gamma Correction
    color = max(color, 0.0);
    color = pow(color, 1.0 / Gamma);

    // Apply Screen Flash
    color = lerp(color, FlashColor, FlashIntensity);

    return float4(color, 1.0);
}

technique CRT
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};