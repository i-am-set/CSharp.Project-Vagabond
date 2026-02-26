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

// --- Color Grading Uniforms ---
uniform float Saturation; // 1.0 is neutral
uniform float Vibrance;   // 0.0 is neutral

// Toggles passed from Core
uniform float EnableJitter; 
uniform float EnableLcdGrid; 

// Layout Uniforms
uniform float TargetScale;      
uniform float2 TargetOffset;    

// Palette (Max 16 for loop unrolling)
uniform float3 Palette[16];
uniform int PaletteCount;

// --- Toggles (Compile Time) ---
#define ENABLE_CURVATURE
#define ENABLE_LCD_GRID
#define ENABLE_HUM_BAR      
#define ENABLE_VIGNETTE
#define ENABLE_HALATION

// --- Tuning ---
static const float CURVATURE = 0.15; 
static const float ZOOM = 1.03;
static const float BLACK_LEVEL = 0.01; 
static const float LCD_GAP_SIZE = 0.04;      
static const float LCD_GAP_SOFTNESS = 0.2;  
static const float LCD_GAP_DARKNESS = 0.55;  
static const float LCD_VARIANCE_INTENSITY = 0.025;
static const float LCD_VARIANCE_SPEED = 10;

static const float DITHER_SPREAD = 0.025; 

static const float HALATION_INTENSITY = 0.35;
static const float HALATION_RADIUS = 7.0;
static const float HALATION_BREATH_SPEED = 2.5; 
static const float HALATION_BREATH_AMOUNT = 1.5; 

static const float JITTER_FREQUENCY          = 0.01;  
static const float JITTER_BAND_COUNT_MIN     = 0.5;   
static const float JITTER_BAND_COUNT_MAX     = 3.0;
static const float JITTER_BAND_SPEED_MIN     = 0.1;   
static const float JITTER_BAND_SPEED_MAX     = 1.5;
static const float JITTER_DESYNC_PIXELS_MIN  = 1.0;   
static const float JITTER_DESYNC_PIXELS_MAX  = 4.0;
static const float JITTER_SMOOTHNESS_MIN     = 20.0;  
static const float JITTER_SMOOTHNESS_MAX     = 60.0;
static const float HUM_BAR_SPEED = 0.2;
static const float HUM_BAR_OPACITY = 0.05;
static const float VIGNETTE_INTENSITY = 1.40;

// --- Color Tuning Defaults ---
// These are applied ON TOP of the Uniforms.
// Use these to set a "Base" look if Uniforms are left at default.
static const float TUNING_SATURATION_BASE = 1.15; 
static const float TUNING_VIBRANCE_BASE = 0.15;   

// --- Globals ---
Texture2D SpriteTexture;
sampler s0 = sampler_state { Texture = <SpriteTexture>; };

// --- Bayer Matrix 4x4 ---
static const float4x4 Bayer4x4 = {
    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
};

// Optimized Random
float rand(float2 co) {
    return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
}

// Helper: Quantizes colors to the palette with Dithering
float4 Tex2DQuantized(sampler s, float2 uv, float2 pixelPos)
{
    float4 rawColor = tex2D(s, uv);
    
    int x = (int)fmod(abs(pixelPos.x), 4.0);
    int y = (int)fmod(abs(pixelPos.y), 4.0);
    
    float threshold = Bayer4x4[y][x] - 0.5;
    float3 ditheredColor = rawColor.rgb + (threshold * DITHER_SPREAD);

    float3 closest = rawColor.rgb;
    float minDist = 1000.0;

    [unroll]
    for(int i = 0; i < 16; i++)
    {
        if (i >= PaletteCount) break;

        float3 pColor = Palette[i];
        float3 diff = ditheredColor - pColor;
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

    if (abs(centeredUV.x) > 0.5 || abs(centeredUV.y) > 0.5)
        return float4(0.0, 0.0, 0.0, 1.0);
#endif
    
    // --- 1. COORDINATE MAPPING ---
    float2 screenPos = uv * ScreenResolution;
    float2 virtualPos = (screenPos - TargetOffset) / TargetScale;
    float2 staticVirtualPos = virtualPos;

    // --- 2. LCD GLITCH / DESYNC ---
    if (EnableJitter > 0.5)
    {
        float t = Time;
        float chaos = sin(t * 0.5) + sin(t * 1.7) + sin(t * 2.9); 
        float chaosThreshold = lerp(2.95, 1.2, JITTER_FREQUENCY);

        if (abs(chaos) > chaosThreshold) {
            float eventSeed = floor(t * 0.4);
            float3 r = float3(
                rand(float2(eventSeed, 0.0)),
                rand(float2(eventSeed, 1.0)),
                rand(float2(eventSeed, 2.0))
            );

            float bandSteps = floor((r.x * (JITTER_BAND_COUNT_MAX - JITTER_BAND_COUNT_MIN) / 0.5) + 0.5);
            float bandCount = JITTER_BAND_COUNT_MIN + bandSteps * 0.5;

            float bandSpeed   = lerp(JITTER_BAND_SPEED_MIN,    JITTER_BAND_SPEED_MAX,    r.y);
            float desyncPx    = lerp(JITTER_DESYNC_PIXELS_MIN,  JITTER_DESYNC_PIXELS_MAX,  r.z);
            
            float smoothness = 40.0; 

            float rawIntensity   = (abs(chaos) - chaosThreshold) / (3.0 - chaosThreshold);
            float glitchIntensity = pow(saturate(rawIntensity), 1.0 / smoothness);

            float pixelRow = floor(virtualPos.y);
            float band = sign(sin(
                (pixelRow / VirtualResolution.y) * bandCount * 6.28318
                + Time * bandSpeed
            ));

            virtualPos.x += floor((band * desyncPx * glitchIntensity) + 0.5);
        }
    }

    // --- 3. VIRTUAL PIXEL SNAP ---
    float2 snappedVirtualPos = floor(virtualPos) + 0.5;
    float2 snappedScreenPos = snappedVirtualPos * TargetScale + TargetOffset;
    float2 snappedUV = snappedScreenPos / ScreenResolution;

    // --- 4. SAMPLING & QUANTIZATION ---
    float tearNoise = 0.0;
    
    if (ImpactGlitchIntensity > 0.0) {
        float highFreq = floor(uv.y * 300.0); 
        float noise = rand(float2(Time * 50.0, highFreq)) - 0.5;
        if (abs(noise) > 0.2) {
            tearNoise = noise * ImpactGlitchIntensity * 0.05; 
        }
    }

    float3 color = Tex2DQuantized(s0, snappedUV + float2(tearNoise, 0.0), staticVirtualPos).rgb;

    // --- 5. HALATION (FULL COVERAGE + BREATHING) ---
#ifdef ENABLE_HALATION
    float breath = sin(Time * HALATION_BREATH_SPEED);
    float currentRadius = HALATION_RADIUS + (breath * HALATION_BREATH_AMOUNT);
    float currentIntensity = HALATION_INTENSITY + (breath * 0.05);

    float2 pixelSize = 1.0 / ScreenResolution;
    float2 offAxis = pixelSize * currentRadius;
    float2 offDiag = offAxis * 0.707; 

    float3 glow = float3(0.0, 0.0, 0.0);

    glow += tex2D(s0, snappedUV + float2(offDiag.x, offDiag.y)).rgb;
    glow += tex2D(s0, snappedUV + float2(-offDiag.x, offDiag.y)).rgb;
    glow += tex2D(s0, snappedUV + float2(offDiag.x, -offDiag.y)).rgb;
    glow += tex2D(s0, snappedUV + float2(-offDiag.x, -offDiag.y)).rgb;

    glow += tex2D(s0, snappedUV + float2(offAxis.x, 0.0)).rgb;
    glow += tex2D(s0, snappedUV + float2(-offAxis.x, 0.0)).rgb;
    glow += tex2D(s0, snappedUV + float2(0.0, offAxis.y)).rgb;
    glow += tex2D(s0, snappedUV + float2(0.0, -offAxis.y)).rgb;

    glow *= 0.125; 
    color += glow * currentIntensity;
#endif

    // --- LCD VARIANCE ---
    float2 cellIndex = floor(staticVirtualPos);
    float baseSeed = rand(cellIndex);
    float3 cellSeed = float3(baseSeed, baseSeed + 0.33, baseSeed + 0.66);

    float t = Time * LCD_VARIANCE_SPEED;
    float3 phase = cellSeed * 6.283; 
    float3 smoothNoise = sin(t + phase); 
    float3 variance = 1.0 + (smoothNoise * LCD_VARIANCE_INTENSITY);
    color *= variance;

    // --- 6. LCD PIXEL GRID ---
#ifdef ENABLE_LCD_GRID
    if (EnableLcdGrid > 0.5)
    {
        float2 pixelCell = frac(staticVirtualPos);
        float low = LCD_GAP_SIZE + LCD_GAP_SOFTNESS;
        float high = 1.0 - low;
        float2 edgeMask = smoothstep(0.0, low, pixelCell) * smoothstep(1.0, high, pixelCell);
        float gridMask = edgeMask.x * edgeMask.y;
        color *= lerp(LCD_GAP_DARKNESS, 1.0, gridMask);
    }
#endif

    // --- 7. HUM BAR ---
#ifdef ENABLE_HUM_BAR
    float humWave = sin((uv.y * 2.0) + (Time * HUM_BAR_SPEED));
    color *= (1.0 - (((humWave + 1.0) * 0.5) * HUM_BAR_OPACITY));
#endif

    // --- 8. COLOR GRADING (SATURATION & VIBRANCE) ---
    color = max(color, BLACK_LEVEL); 

    // Calculate Luminance
    float lumaVal = dot(color, float3(0.299, 0.587, 0.114));
    float3 lumaVec = float3(lumaVal, lumaVal, lumaVal);

    // Combine Uniforms with Tuning Constants
    float finalSat = Saturation * TUNING_SATURATION_BASE;
    float finalVib = Vibrance + TUNING_VIBRANCE_BASE;

    // Apply Saturation
    color = lerp(lumaVec, color, finalSat);
    
    // Apply Vibrance (Boosts saturation of less saturated colors)
    float max_c = max(color.r, max(color.g, color.b));
    float min_c = min(color.r, min(color.g, color.b));
    float satMask = 1.0 - (max_c - min_c); // 1.0 for gray, 0.0 for pure color
    
    // Aggressive Vibrance Curve
    color = lerp(lumaVec, color, 1.0 + (finalVib * satMask * 2.0));

    // --- 9. VIGNETTE ---
#ifdef ENABLE_VIGNETTE
    float2 vUV = uv * (1.0 - uv.yx);
    float vig = vUV.x * vUV.y * 15.0;
    vig = sqrt(sqrt(vig));
    color *= lerp(1.0, vig, VIGNETTE_INTENSITY);
#endif

    // --- 10. GAMMA & FLASH ---
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