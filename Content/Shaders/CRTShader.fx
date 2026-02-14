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
static const float CURVATURE = 0.1; 
static const float ZOOM = 1.01;

// Color & Contrast
static const float BLACK_LEVEL = 0.03; 

// Scanlines (Structural)
static const float SCANLINE_DENSITY = 1.0;  
static const float SCANLINE_HARDNESS = 0.6; 
static const float SCANLINE_BLOOM_CUTOFF = 0.7; 

// Halation (The Glow)
static const float HALATION_INTENSITY = 0.85; 
static const float HALATION_RADIUS = 4.0;     

// Chromatic Aberration (Color Bleed)
static const float CHROMATIC_OFFSET_CENTER = 1.0; 
static const float CHROMATIC_OFFSET_EDGE = 1.5;   

// Jitter & Noise (Signal Instability)
static const float JITTER_MICRO_INTENSITY = 0.0002; 
static const float JITTER_DESYNC_INTENSITY = 0.001;

static const float HUM_BAR_SPEED = 0.2;
static const float HUM_BAR_OPACITY = 0.05;
static const float NOISE_INTENSITY = 0.025; 
static const float VIGNETTE_INTENSITY = 0.90;
static const float VIGNETTE_ROUNDNESS = 0.25;

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
    
    // --- 1. JITTER (Signal Instability) ---
#ifdef ENABLE_JITTER
    // A. Constant Micro-Vibration (The "Hum")
    float jitterTime = Time * 113.0;
    float microShake = (rand(float2(uv.y, jitterTime)) - 0.5) * JITTER_MICRO_INTENSITY;
    
    // B. Randomized Desync (The "Tension")
    // We use a sum of sine waves with prime frequencies to create an irregular interference pattern.
    // This ensures glitches happen at unpredictable intervals and last for varying durations.
    float t = Time;
    float wave1 = sin(t * 0.5);  // Slow drift (2s period)
    float wave2 = sin(t * 1.7);  // Medium cycle (~0.6s period)
    float wave3 = sin(t * 2.9);  // Fast cycle (~0.3s period)
    
    // Combine waves. Range is roughly -3.0 to 3.0.
    float chaos = wave1 + wave2 + wave3;
    
    float desyncOffset = 0.0;
    
    // Only trigger when the chaos aligns (peaks/troughs)
    // Threshold 2.4 means it happens roughly 5-10% of the time, in clusters.
    if (abs(chaos) > 2.4) {
        // Normalize intensity (0.0 to 1.0) based on how far past threshold we are
        float glitchIntensity = (abs(chaos) - 2.4) / 0.6;
        
        // VARY THE LOOK:
        // Use the slow wave (wave1) to change the density of the tear lines over time
        // This ensures some glitches are chunky (low density) and others are fine (high density)
        float tearDensity = 20.0 + (wave1 * 15.0) + (wave2 * 5.0); 
        
        // Use the medium wave (wave2) to change the speed/direction of the tear
        float tearSpeed = 20.0 + (wave2 * 30.0);
        
        // Calculate the tear pattern
        float tear = sign(sin(uv.y * tearDensity + Time * tearSpeed));
        
        desyncOffset = tear * JITTER_DESYNC_INTENSITY * glitchIntensity;
    }

    // C. Impact Glitch (Game Logic)
    float impactOffset = 0.0;
    if (ImpactGlitchIntensity > 0.0) {
        float block = floor(uv.y * 10.0); 
        float glitchNoise = (rand(float2(Time, block)) - 0.5);
        impactOffset = glitchNoise * ImpactGlitchIntensity * 0.05;
    }

    // Apply all offsets to the X coordinate
    uv.x += microShake + desyncOffset + impactOffset;
#endif

    // --- 2. CHROMATIC ABERRATION (Distance Based) ---
    float3 color;
#ifdef ENABLE_CHROMATIC_ABERRATION
    // Calculate distance from center (0.0 to ~0.7)
    float dist = distance(uv, float2(0.5, 0.5));
    // Lerp offset based on distance: Small at center, large at edges
    float spread = lerp(CHROMATIC_OFFSET_CENTER, CHROMATIC_OFFSET_EDGE, dist * 2.0);
    
    // Add a tiny bit of jitter to the aberration itself for that "unstable" look
    spread += (rand(float2(Time, uv.y)) - 0.5) * 0.2;

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
    // Apply noise proportional to signal strength (masks noise on black)
    color += noise * color;
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