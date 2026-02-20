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
#define ENABLE_LCD_GRID
#define ENABLE_JITTER       
#define ENABLE_HUM_BAR      
#define ENABLE_VIGNETTE
// #define ENABLE_CHROMATIC_ABERRATION
#define ENABLE_NOISE
#define ENABLE_HALATION

// --- Tuning ---
static const float CURVATURE = 0.1; 
static const float ZOOM = 1.01;
static const float BLACK_LEVEL = 0.03; 
static const float LCD_GAP_SIZE = 0.04;      // Fraction of each virtual pixel used for the gap (0 = no gap, 0.2 = thick gap)
static const float LCD_GAP_SOFTNESS = 0.2;  // Edge softness of the gap (lower = sharper grid lines)
static const float LCD_GAP_DARKNESS = 0.35;  // How dark the gaps are (0 = black, 1 = no effect)

// Halation Tuning
static const float HALATION_INTENSITY = 0.35;
static const float HALATION_RADIUS = 7.0;

static const float CHROMATIC_OFFSET_CENTER = 1.0; 
static const float CHROMATIC_OFFSET_EDGE = 1.5;   
static const float JITTER_FREQUENCY          = 0.01;  // [0.0 - 1.0]
static const float JITTER_BAND_COUNT_MIN     = 0.5;   // bands on screen
static const float JITTER_BAND_COUNT_MAX     = 3.0;
static const float JITTER_BAND_SPEED_MIN     = 0.1;   // lower = calmer, not a shake
static const float JITTER_BAND_SPEED_MAX     = 1.5;
static const float JITTER_DESYNC_PIXELS_MIN  = 1.0;   // virtual pixels
static const float JITTER_DESYNC_PIXELS_MAX  = 8.0;
static const float JITTER_SMOOTHNESS_MIN     = 20.0;  // ramp sharpness
static const float JITTER_SMOOTHNESS_MAX     = 60.0;
static const float HUM_BAR_SPEED = 0.2;
static const float HUM_BAR_OPACITY = 0.05;
static const float NOISE_INTENSITY = 0.025; 
static const float VIGNETTE_INTENSITY = 1.20;
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
    
    // --- 1. VIRTUAL PIXEL SNAP ---
    // Snap texture reads to the center of each virtual pixel cell so that every
    // LCD square shows one clean, unambiguous color with no subpixel bleed.
    // uv is kept for all positional math (grid mask, vignette, dist, etc.);
    // snappedUV is used exclusively for texture samples.
    float2 snappedUV = (floor(uv * VirtualResolution) + 0.5) / VirtualResolution;

    // --- 2. LCD GLITCH / DESYNC ---
    // Applied ONLY to snappedUV.x so it never touches the curvature mask, LCD grid,
    // vignette, or any other geometry. The shift is quantized to whole virtual pixels,
    // which is physically correct for an LCD (pixel data corruption, not a bent beam).
#ifdef ENABLE_JITTER
    {
        float t = Time;
        float wave1 = sin(t * 0.5);
        float wave2 = sin(t * 1.7);
        float wave3 = sin(t * 2.9);
        float chaos = wave1 + wave2 + wave3; // range approx [-3, 3]

        // JITTER_FREQUENCY maps [0,1] -> chaos threshold [2.95, 1.2].
        // At 0 the threshold is nearly unreachable; at 1 it fires very often.
        float chaosThreshold = lerp(2.95, 1.2, JITTER_FREQUENCY);

        if (abs(chaos) > chaosThreshold) {
            // Per-event seed: quantize time coarsely so it picks a new random set of
            // parameters each time a fresh glitch window opens, but stays stable across
            // all pixels for the duration of that window (no per-pixel variance).
            float eventSeed = floor(t * 0.4);

            // Pick each parameter independently from its min/max range.
            float r0 = rand(float2(eventSeed, 0.0));
            float r1 = rand(float2(eventSeed, 1.0));
            float r2 = rand(float2(eventSeed, 2.0));
            float r3 = rand(float2(eventSeed, 3.0));

            // Band count steps in 0.5 increments between min and max.
            float bandSteps = round(r0 * (JITTER_BAND_COUNT_MAX - JITTER_BAND_COUNT_MIN) / 0.5);
            float bandCount = JITTER_BAND_COUNT_MIN + bandSteps * 0.5;

            float bandSpeed   = lerp(JITTER_BAND_SPEED_MIN,    JITTER_BAND_SPEED_MAX,    r1);
            float desyncPx    = lerp(JITTER_DESYNC_PIXELS_MIN,  JITTER_DESYNC_PIXELS_MAX,  r2);
            float smoothness  = lerp(JITTER_SMOOTHNESS_MIN,     JITTER_SMOOTHNESS_MAX,     r3);

            // How deep past the threshold we are, normalized 0->1.
            // smoothness controls how gradually full intensity is reached.
            float rawIntensity   = (abs(chaos) - chaosThreshold) / (3.0 - chaosThreshold);
            float glitchIntensity = pow(saturate(rawIntensity), 1.0 / max(smoothness, 0.01));

            // Bands are keyed to virtual pixel rows so they never split an LCD pixel.
            // Low band count + low speed = big calm tears instead of a screenshake.
            float pixelRow = floor(uv.y * VirtualResolution.y);
            float band = sign(sin(
                (pixelRow / VirtualResolution.y) * bandCount * 6.28318
                + Time * bandSpeed
            ));

            // Quantize shift to whole virtual pixels — LCD data corruption, not analog drift.
            float shiftPixels = round(band * desyncPx * glitchIntensity);
            snappedUV.x += shiftPixels / VirtualResolution.x;
        }
    }
#endif

    // --- 3. CHROMATIC ABERRATION ---
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
    color.r = Tex2DQuantized(s0, snappedUV + rOffset).r;
    color.g = Tex2DQuantized(s0, snappedUV + gOffset).g;
    color.b = Tex2DQuantized(s0, snappedUV + bOffset).b;
#else
    float2 glitchOffset = float2(tearNoise, 0.0);
    color = Tex2DQuantized(s0, snappedUV + glitchOffset).rgb;
#endif

// --- 4. HALATION (IMPROVED) ---
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
    glow += tex2D(s0, snappedUV + float2(off.x, 0.0)).rgb;
    glow += tex2D(s0, snappedUV - float2(off.x, 0.0)).rgb;
    glow += tex2D(s0, snappedUV + float2(0.0, off.y)).rgb;
    glow += tex2D(s0, snappedUV - float2(0.0, off.y)).rgb;

    glow += tex2D(s0, snappedUV + float2(offDiag.x, offDiag.y)).rgb;
    glow += tex2D(s0, snappedUV + float2(-offDiag.x, offDiag.y)).rgb;
    glow += tex2D(s0, snappedUV + float2(offDiag.x, -offDiag.y)).rgb;
    glow += tex2D(s0, snappedUV + float2(-offDiag.x, -offDiag.y)).rgb;

    glow *= 0.125; // Average
    
    // D. Additive Blending
    // We add the glow to the base color. This creates a true light effect.
    color += glow * HALATION_INTENSITY;
#endif

    // --- 4. LCD PIXEL GRID ---
#ifdef ENABLE_LCD_GRID
    // Find where we are within each virtual pixel (0..1 in both axes)
    float2 pixelCell = frac(uv * VirtualResolution);

    // Build a soft mask that is 1.0 in the pixel center and falls to 0.0 at the edges.
    // smoothstep from 0 -> LCD_GAP_SIZE ramps up, smoothstep from 1 -> 1-LCD_GAP_SIZE ramps down.
    float2 edgeMask;
    edgeMask.x = smoothstep(0.0, LCD_GAP_SIZE + LCD_GAP_SOFTNESS, pixelCell.x) *
                 smoothstep(1.0, 1.0 - (LCD_GAP_SIZE + LCD_GAP_SOFTNESS), pixelCell.x);
    edgeMask.y = smoothstep(0.0, LCD_GAP_SIZE + LCD_GAP_SOFTNESS, pixelCell.y) *
                 smoothstep(1.0, 1.0 - (LCD_GAP_SIZE + LCD_GAP_SOFTNESS), pixelCell.y);

    // Combine axes: full brightness inside the pixel, LCD_GAP_DARKNESS in the gaps
    float gridMask = edgeMask.x * edgeMask.y;
    float gridMultiplier = lerp(LCD_GAP_DARKNESS, 1.0, gridMask);
    color *= gridMultiplier;
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