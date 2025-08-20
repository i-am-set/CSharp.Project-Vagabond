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

// --- Tweakable Constants (Corrected with 'static const') ---
#define ENABLE_CURVATURE
#define ENABLE_VIGNETTE
#define ENABLE_SCANLINES
#define ENABLE_SHADOW_MASK
#define ENABLE_CHROMATIC_ABERRATION
#define ENABLE_DITHERING

static const float CURVATURE_AMOUNT = 0.03;
static const float VIGNETTE_INTENSITY = 0.6;
static const float SCANLINE_INTENSITY = 0.15;
static const float SHADOW_MASK_INTENSITY = 0.1;
static const float CHROMATIC_ABERRATION_AMOUNT = 2.0;
static const float DITHER_THRESHOLD = 1.0 / 255.0;

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

// Input from the application to the vertex shader.
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

// Output from the vertex shader, passed to the pixel shader.
struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
    // We will pass the screen position here using a compatible semantic.
    float4 ScreenPosition : TEXCOORD1; 
};

// --- Vertex Shader ---
VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;
    output.Position = input.Position;
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    // Copy the final screen position to our new variable.
    output.ScreenPosition = input.Position; 
    return output;
}

// --- Pixel Shader ---
float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TexCoord - 0.5; 
    float2 sampleCoords = input.TexCoord;

#ifdef ENABLE_CURVATURE
    float dist = dot(uv, uv);
    sampleCoords = input.TexCoord + uv * dist * CURVATURE_AMOUNT;
    if (sampleCoords.x < 0 || sampleCoords.x > 1 || sampleCoords.y < 0 || sampleCoords.y > 1)
    {
        return float4(0, 0, 0, 1);
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
    float scanline = sin(input.TexCoord.y * ScreenResolution.y * 3.14159);
    color.rgb *= 1.0 - (scanline * SCANLINE_INTENSITY);
#endif

#ifdef ENABLE_SHADOW_MASK
    float mask = sin(input.TexCoord.x * ScreenResolution.x * 3.14159) * sin(input.TexCoord.y * ScreenResolution.y * 3.14159);
    color.rgb *= 1.0 - (mask * SHADOW_MASK_INTENSITY);
#endif

#ifdef ENABLE_VIGNETTE
    float vignette = smoothstep(0.4, 1.0, length(uv));
    color.rgb *= 1.0 - (vignette * VIGNETTE_INTENSITY);
#endif

#ifdef ENABLE_DITHERING
    // Use the compatible ScreenPosition variable instead of the problematic input.Position
    int matrix_index = (int)(fmod(input.ScreenPosition.x, 4)) + (int)(fmod(input.ScreenPosition.y, 4)) * 4;
    float dither_val = (DITHER_MATRIX[matrix_index] / 16.0 - 0.5) * DITHER_THRESHOLD;
    color.rgb += dither_val;
#endif

	return color;
}

technique CRT
{
	pass P0
	{
        // Tell the technique to use our new vertex shader.
        VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
