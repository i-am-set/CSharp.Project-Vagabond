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
#define ENABLE_JITTER       
#define ENABLE_HUM_BAR      
#define ENABLE_VIGNETTE
#define ENABLE_CHROMATIC_ABERRATION
#define ENABLE_NOISE

// --- Tuning ---

// Distortion
static const float CURVATURE = 0.15;        // 0.0 = Flat, 0.15 = Standard CRT, 0.4 = Fish-eye
static const float ZOOM = 1.02;             // 1.0 = No Zoom, 1.03 = Crop Bezels, 0.9 = Shrink Image

// Color & Contrast
static const float BLACK_LEVEL = 0.03;      // 0.0 = Pure Black, 0.03 = Phosphor Glow, 0.1 = Washed Out

// Scanlines
static const float SCANLINE_OPACITY_MIN = 0.75f; // 0.0 = Black Lines, 0.5 = Soft Lines, 1.0 = Invisible
static const float SCANLINE_OPACITY_MAX = 1.0f;  // 1.0 = Full Brightness (Standard)
static const float SCANLINE_CRAWL_SPEED = 0.0f;  // 0.0 = Static (Authentic), 1.0 = Slow Roll, 5.0 = Fast Roll

// Horizontal Sync Jitter (Shaking)
static const float HORIZONTAL_JITTER = 0.0002;   // 0.0 = Perfect, 0.0001 = Micro-Jitter, 0.005 = Broken TV
static const float JITTER_FREQUENCY = 0.000001;      // Speed of the shake

// AC Hum Bar (Rolling Shadow)
static const float HUM_BAR_SPEED = 1.0;          // Speed of the rolling bar
static const float HUM_BAR_OPACITY = 0.15;       // 0.0 = Invisible, 0.05 = Subtle, 0.2 = Bad Interference

// Chromatic Aberration (Color Bleed)
static const float CHROMATIC_OFFSET = 1.0;       // 0.0 = Sharp, 1.0 = Consumer TV, 3.0 = Broken Convergence

// Vignette (Corner Darkening)
static const float VIGNETTE_INTENSITY = 0.25;    // 0.0 = Off, 0.25 = Subtle, 0.8 = Heavy Spotlight
static const float VIGNETTE_ROUNDNESS = 0.25;    // 0.0 = Oval, 1.0 = Circle

// Noise (Static/Snow)
static const float NOISE_INTENSITY = 0.02;      // 0.0 = Clean, 0.004 = RF Fuzz, 0.1 = Heavy Snow

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

    // --- 0. CURVATURE & ZOOM ---
#ifdef ENABLE_CURVATURE
    float2 centeredUV = uv - 0.5;
    float r2 = dot(centeredUV, centeredUV);
    
    // Apply Barrel Distortion
    float2 distortedUV = centeredUV * (1.0 + CURVATURE * r2);
    
    // Apply Zoom
    distortedUV *= (1.0 / ZOOM);

    uv = distortedUV + 0.5;

    // Bezel Mask
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
    {
        return float4(0.0, 0.0, 0.0, 1.0);
    }
#endif
    
    // --- 1. HORIZONTAL SYNC JITTER ---
#ifdef ENABLE_JITTER
    float jitterTime = Time * JITTER_FREQUENCY;
    float jitterOffset = (rand(float2(uv.y, jitterTime)) - 0.5) * 2.0; 
    float currentJitter = HORIZONTAL_JITTER;

    if (ImpactGlitchIntensity > 0.0) {
        float block = floor(uv.y * 10.0); 
        float glitchNoise = (rand(float2(Time, block)) - 0.5);
        currentJitter += glitchNoise * ImpactGlitchIntensity * 0.05;
    }

    uv.x += jitterOffset * currentJitter;
#endif

    // --- 2. CHROMATIC ABERRATION ---
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

    // --- 2.5. BLACK LEVEL LIFT ---
    color = max(color, BLACK_LEVEL);

    // --- 3. SCANLINES ---
#ifdef ENABLE_SCANLINES
    float scanlinePos = (uv.y * VirtualResolution.y) + (Time * SCANLINE_CRAWL_SPEED);
    float scanlineWave = sin(scanlinePos * 3.14159 * 2.0);
    float scanlineFactor = lerp(SCANLINE_OPACITY_MIN, SCANLINE_OPACITY_MAX, (scanlineWave * 0.5 + 0.5));
    color *= scanlineFactor;
#endif

    // --- 3.5 HUM BAR ---
#ifdef ENABLE_HUM_BAR
    float humWave = sin((uv.y * 2.0) + (Time * HUM_BAR_SPEED));
    float humFactor = 1.0 - (((humWave + 1.0) / 2.0) * HUM_BAR_OPACITY);
    color *= humFactor;
#endif

    // --- 4. SATURATION & VIBRANCE ---
    float3 lumaCoef = float3(0.299, 0.587, 0.114);
    float luma = dot(color, lumaCoef);

    color = lerp(float3(luma, luma, luma), color, Saturation);

    float max_color = max(color.r, max(color.g, color.b));
    float min_color = min(color.r, min(color.g, color.b));
    float color_sat = max_color - min_color;
    color = lerp(float3(luma, luma, luma), color, 1.0 + (Vibrance * (1.0 - color_sat)));

    // --- 5. VIGNETTE ---
#ifdef ENABLE_VIGNETTE
    float2 vUV = uv * (1.0 - uv.yx);
    float vig = vUV.x * vUV.y * 15.0;
    vig = pow(vig, VIGNETTE_ROUNDNESS);
    color *= float3(vig, vig, vig); 
#endif

    // --- 6. NOISE ---
#ifdef ENABLE_NOISE
    float noise = (rand(uv * Time) - 0.5) * NOISE_INTENSITY;
    color += noise;
#endif

    // --- 7. GAMMA & FLASH ---
    color = max(color, 0.0);
    color = pow(color, 1.0 / Gamma);
    color = lerp(color, FlashColor, FlashIntensity);

    return float4(color, 1.0);
}

technique CRT
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
}