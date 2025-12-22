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
uniform float2 Resolution; // Target Resolution (Window Size)
uniform float2 VirtualResolution; // Virtual Game Resolution (320x180)
uniform float4 Color1;
uniform float4 Color2;
uniform float4 PaletteBlack; // The color to replace
uniform float Threshold; // Tolerance for black detection
uniform float Opacity;
uniform float Scale;
uniform float Speed;
uniform float DistortionScale;
uniform float DistortionSpeed;

// --- Globals ---
Texture2D SceneTexture;
sampler SceneSampler = sampler_state { Texture = <SceneTexture>; AddressU = Clamp; AddressV = Clamp; };

Texture2D NoiseTexture;
sampler NoiseSampler = sampler_state { Texture = <NoiseTexture>; AddressU = Wrap; AddressV = Wrap; };

struct PixelShaderInput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};

float4 MainPS(PixelShaderInput input) : COLOR
{
    float2 uv = input.TexCoord;
    
    // Sample the scene color
    float4 sceneColor = tex2D(SceneSampler, uv);
    
    // Check if the pixel is "black" (or close enough to the palette black)
    float dist = distance(sceneColor.rgb, PaletteBlack.rgb);
    
    if (dist < Threshold)
    {
        // --- Generate Noise ---
        
        // Pixelate the UVs for the noise generation to match the virtual resolution style
        // This ensures the noise looks chunky like the rest of the game
        float2 pixelUV = floor(uv * VirtualResolution) / VirtualResolution;
        
        // Wiggle generation using sine waves on the pixelated UVs
        float xOffset = sin(pixelUV.y * DistortionScale + Time * DistortionSpeed) * 0.02;
        float yOffset = cos(pixelUV.x * DistortionScale + Time * DistortionSpeed * 0.8) * 0.02;
        
        float2 distortedUV = pixelUV + float2(xOffset, yOffset);
        
        // Sample the noise texture with the distorted UVs
        float2 scroll = float2(Time * Speed, Time * Speed * 0.5);
        
        float noise1 = tex2D(NoiseSampler, (distortedUV * Scale) + scroll).r;
        float noise2 = tex2D(NoiseSampler, (distortedUV * Scale * 1.5) - scroll * 0.5).g;
        
        float finalNoise = (noise1 + noise2) * 0.5;
        
        // Soften/Contrast
        finalNoise = finalNoise * 0.5 + 0.2;
        
        float4 noiseColor = lerp(Color1, Color2, finalNoise);
        
        // Blend the noise with the original black based on opacity
        // We replace the black pixel with the noise color
        return lerp(sceneColor, noiseColor, Opacity);
    }
    
    // Return original color if not black
    return sceneColor;
}

technique BackgroundNoise
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
}