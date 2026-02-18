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
uniform float2 ScreenResolution;
uniform float2 VirtualResolution;
uniform float Gamma;
uniform float3 FlashColor;
uniform float FlashIntensity;
uniform float ImpactGlitchIntensity;
uniform float Saturation;
uniform float Vibrance;

uniform float3 Palette[16];
uniform int PaletteCount;

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
static const float CURVATURE = 0.1; 
static const float ZOOM = 1.01;
static const float BLACK_LEVEL = 0.03; 
static const float SCANLINE_DENSITY = 1.0;  
static const float SCANLINE_HARDNESS = 0.4; 
static const float SCANLINE_BLOOM_CUTOFF = 0.7; 

// Halation Tuning
static const float HALATION_INTENSITY = 0.35;
static const float HALATION_RADIUS = 7.0;

static const float CHROMATIC_OFFSET_CENTER = 1.0; 
static const float CHROMATIC_OFFSET_EDGE = 1.5;   
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

// Helper: Quantizes colors to the palette (Retro Console Look)
float4 Tex2DQuantized(sampler s, float2 uv)
{
    float4 rawColor = tex2D(s, uv);
    float3 closest = rawColor.rgb;
    float minDist = 1000.0;

    for(int i = 0; i < 15; i++)
    {
        float3 pColor = Palette[i];
        float3 diff = rawColor.rgb - pColor;
        float distSq = dot(diff, diff);
        
        if(distSq < minDist)
        {
            minDist = distSq;
            closest = pColor;
        }
    }
    return float4(closest, rawColor.a);
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
    float jitterTime = Time * 113.0;
    float microShake = (rand(float2(uv.y, jitterTime)) - 0.5) * JITTER_MICRO_INTENSITY;
    
    float t = Time;
    float wave1 = sin(t * 0.5);
    float wave2 = sin(t * 1.7);
    float wave3 = sin(t * 2.9);
    float chaos = wave1 + wave2 + wave3;
    float desyncOffset = 0.0;
    
    if (abs(chaos) > 2.4) {
        float glitchIntensity = (abs(chaos) - 2.4) / 0.6;
        float tearDensity = 20.0 + (wave1 * 15.0) + (wave2 * 5.0); 
        float tearSpeed = 20.0 + (wave2 * 30.0);
        float tear = sign(sin(uv.y * tearDensity + Time * tearSpeed));
        desyncOffset = tear * JITTER_DESYNC_INTENSITY * glitchIntensity;
    }

    uv.x += microShake + desyncOffset;
#endif

    // --- 2. CHROMATIC ABERRATION ---
    float3 color;
    float tearNoise = 0.0;
    
    if (ImpactGlitchIntensity > 0.0) {
        float highFreq = floor(uv.y * 300.0); 
        float noise = rand(float2(Time * 50.0, highFreq)) - 0.5;
        if (abs(noise) > 0.2) {
            tearNoise = noise * ImpactGlitchIntensity * 0.05; 
        }
    }

#ifdef ENABLE_CHROMATIC_ABERRATION
    float dist = distance(uv, float2(0.5, 0.5));
    float spread = lerp(CHROMATIC_OFFSET_CENTER, CHROMATIC_OFFSET_EDGE, dist * 2.0);
    spread += (rand(float2(Time, uv.y)) - 0.5) * 0.2;

    float2 rOffset = float2((spread / ScreenResolution.x) + tearNoise, 0.0);
    float2 gOffset = float2(tearNoise * 0.5, 0.0); 
    float2 bOffset = float2((-spread / ScreenResolution.x) - tearNoise, 0.0);
    
    // Keep Quantization here for the "Source" signal
    color.r = Tex2DQuantized(s0, uv + rOffset).r;
    color.g = Tex2DQuantized(s0, uv + gOffset).g;
    color.b = Tex2DQuantized(s0, uv + bOffset).b;
#else
    float2 glitchOffset = float2(tearNoise, 0.0);
    color = Tex2DQuantized(s0, uv + glitchOffset).rgb;
#endif

// --- 3. HALATION (IMPROVED) ---
#ifdef ENABLE_HALATION
    // A. Generate Noise for Dithering
    // We use a high frequency noise based on UV and Time to randomize the blur radius per pixel.
    // This breaks the "8-tap ring" into a diffuse, noisy fog.
    float dither = rand(uv * 97.0 + Time); 
    
    // B. Jitter the Radius
    // Vary radius between 70% and 130% of the target size
    float currentRadius = HALATION_RADIUS * (0.7 + 0.6 * dither);
    
    float2 pixelSize = 1.0 / ScreenResolution;
    float2 off = pixelSize * currentRadius;
    float2 offDiag = off * 0.707; // 1/sqrt(2)

    float3 glow = float3(0.0, 0.0, 0.0);

    // C. Sample Raw Texture (Optimization & Smoothness)
    // We use 'tex2D' (raw) instead of 'Tex2DQuantized'. 
    // This is 15x faster and allows the glow to be a smooth gradient.
    glow += tex2D(s0, uv + float2(off.x, 0.0)).rgb;
    glow += tex2D(s0, uv - float2(off.x, 0.0)).rgb;
    glow += tex2D(s0, uv + float2(0.0, off.y)).rgb;
    glow += tex2D(s0, uv - float2(0.0, off.y)).rgb;

    glow += tex2D(s0, uv + float2(offDiag.x, offDiag.y)).rgb;
    glow += tex2D(s0, uv + float2(-offDiag.x, offDiag.y)).rgb;
    glow += tex2D(s0, uv + float2(offDiag.x, -offDiag.y)).rgb;
    glow += tex2D(s0, uv + float2(-offDiag.x, -offDiag.y)).rgb;

    glow *= 0.125; // Average
    
    // D. Additive Blending
    // We add the glow to the base color. This creates a true light effect.
    color += glow * HALATION_INTENSITY;
#endif

    // --- 4. SCANLINES ---
#ifdef ENABLE_SCANLINES
    float luma = dot(color, float3(0.299, 0.587, 0.114));
    float scanlinePos = (uv.y * VirtualResolution.y * SCANLINE_DENSITY);
    float scanline = sin(scanlinePos * 3.14159 * 2.0);
    scanline = (scanline * 0.5) + 0.5;
    float bloomFactor = smoothstep(0.0, SCANLINE_BLOOM_CUTOFF, luma);
    float lineVisibility = (1.0 - bloomFactor) * SCANLINE_HARDNESS;
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
    color = max(color, BLACK_LEVEL); 

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