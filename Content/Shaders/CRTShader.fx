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
uniform float2 ScreenResolution;  // Physical window size
uniform float2 VirtualResolution; // Game resolution
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
#define ENABLE_HALATION

// --- Tuning ---

// Distortion
static const float CURVATURE = 0.15;
static const float ZOOM = 1.02;

// Color & Contrast
static const float BLACK_LEVEL = 0.03;

// Scanlines (Structural)
static const float SCANLINE_DENSITY = 1.0;  // Multiplier for line count
static const float SCANLINE_HARDNESS = 0.6; // How sharp the lines are
static const float SCANLINE_BLOOM_CUTOFF = 0.7; // Brightness level where scanlines disappear

// Halation (The Glow)
static const float HALATION_INTENSITY = 0.75; // How much light bleeds
static const float HALATION_RADIUS = 3.5;    // Size of the bleed in pixels

// Chromatic Aberration (Color Bleed)
static const float CHROMATIC_OFFSET_CENTER = 1.5; // Bleed at center
static const float CHROMATIC_OFFSET_EDGE = 1.5;   // Bleed at corners

// Jitter & Noise
static const float HORIZONTAL_JITTER = 0.0002;
static const float JITTER_FREQUENCY = 0.000001;
static const float HUM_BAR_SPEED = 0.5;
static const float HUM_BAR_OPACITY = 0.08;
static const float NOISE_INTENSITY = 0.015;
static const float VIGNETTE_INTENSITY = 0.95;
static const float VIGNETTE_ROUNDNESS = 0.30;

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
    float2 distortedUV = centeredUV * (1.0 + CURVATURE * r2);
    distortedUV *= (1.0 / ZOOM);
    uv = distortedUV + 0.5;

    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return float4(0.0, 0.0, 0.0, 1.0);
#endif
    
    // --- 1. JITTER ---
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

    // --- 2. CHROMATIC ABERRATION (Distance Based) ---
    float3 color;
#ifdef ENABLE_CHROMATIC_ABERRATION
    // Calculate distance from center (0.0 to ~0.7)
    float dist = distance(uv, float2(0.5, 0.5));
    // Lerp offset based on distance: Small at center, large at edges
    float spread = lerp(CHROMATIC_OFFSET_CENTER, CHROMATIC_OFFSET_EDGE, dist * 2.0);
    
    float2 rOffset = float2(spread / ScreenResolution.x, 0.0);
    float2 bOffset = float2(-spread / ScreenResolution.x, 0.0);
    
    color.r = tex2D(s0, uv + rOffset).r;
    color.g = tex2D(s0, uv).g;
    color.b = tex2D(s0, uv + bOffset).b;
#else
    color = tex2D(s0, uv).rgb;
#endif

// --- 3. HALATION (Phosphor Glow) ---
#ifdef ENABLE_HALATION
    // 8-tap blur for a smoother, rounder glow (Octagon shape)
    float2 pixelSize = 1.0 / ScreenResolution;
    float2 off = pixelSize * HALATION_RADIUS;
    float2 offDiag = off * 0.707; // 1/sqrt(2) for circular distance

    float3 glow = float3(0.0, 0.0, 0.0);

    // Cardinals
    glow += tex2D(s0, uv + float2(off.x, 0.0)).rgb;
    glow += tex2D(s0, uv - float2(off.x, 0.0)).rgb;
    glow += tex2D(s0, uv + float2(0.0, off.y)).rgb;
    glow += tex2D(s0, uv - float2(0.0, off.y)).rgb;

    // Diagonals
    glow += tex2D(s0, uv + float2(offDiag.x, offDiag.y)).rgb;
    glow += tex2D(s0, uv + float2(-offDiag.x, offDiag.y)).rgb;
    glow += tex2D(s0, uv + float2(offDiag.x, -offDiag.y)).rgb;
    glow += tex2D(s0, uv + float2(-offDiag.x, -offDiag.y)).rgb;

    glow *= 0.125; // Average of 8 samples
    
    // Blend glow into original color (Screen blend for light addition)
    color = max(color, glow * HALATION_INTENSITY);
#endif

    // --- 4. SCANLINES (Structural) ---
#ifdef ENABLE_SCANLINES
    // Calculate Luma (Brightness)
    float luma = dot(color, float3(0.299, 0.587, 0.114));
    
    // Generate Scanline Pattern (Sine wave)
    float scanlinePos = (uv.y * VirtualResolution.y * SCANLINE_DENSITY);
    float scanline = sin(scanlinePos * 3.14159 * 2.0);
    
    // Normalize sine to 0..1 range
    scanline = (scanline * 0.5) + 0.5;
    
    // Calculate Visibility:
    // If Luma is high (bright), visibility approaches 0 (lines disappear/bloom out).
    // If Luma is low (dark), visibility approaches 1 (lines are distinct).
    float bloomFactor = smoothstep(0.0, SCANLINE_BLOOM_CUTOFF, luma);
    float lineVisibility = (1.0 - bloomFactor) * SCANLINE_HARDNESS;
    
    // Apply scanline darkening
    float lineMultiplier = 1.0 - (scanline * lineVisibility);
    color *= lineMultiplier;
#endif

    // --- 5. HUM BAR ---
#ifdef ENABLE_HUM_BAR
    float humWave = sin((uv.y * 2.0) + (Time * HUM_BAR_SPEED));
    float humFactor = 1.0 - (((humWave + 1.0) / 2.0) * HUM_BAR_OPACITY);
    color *= humFactor;
#endif

    // --- 6. COLOR GRADING ---
    color = max(color, BLACK_LEVEL); // Lift blacks for phosphor look

    // Saturation/Vibrance
    float lumaVal = dot(color, float3(0.299, 0.587, 0.114));
    color = lerp(float3(lumaVal, lumaVal, lumaVal), color, Saturation);
    
    float max_c = max(color.r, max(color.g, color.b));
    float min_c = min(color.r, min(color.g, color.b));
    color = lerp(float3(lumaVal, lumaVal, lumaVal), color, 1.0 + (Vibrance * (1.0 - (max_c - min_c))));

    // --- 7. VIGNETTE ---
#ifdef ENABLE_VIGNETTE
    float2 vUV = uv * (1.0 - uv.yx);
    float vig = vUV.x * vUV.y * 15.0;
    vig = pow(vig, VIGNETTE_ROUNDNESS);
    color *= lerp(1.0, vig, VIGNETTE_INTENSITY);
#endif

    // --- 8. NOISE ---
#ifdef ENABLE_NOISE
    float noise = (rand(uv * Time) - 0.5) * NOISE_INTENSITY;
    // Noise is more visible in dark areas
    color += noise * (1.0 - lumaVal);
#endif

    // --- 9. GAMMA & FLASH ---
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