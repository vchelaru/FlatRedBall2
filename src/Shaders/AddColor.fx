#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;

sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

// Per-channel offset added to the sampled texture's RGB before it is multiplied by the vertex
// color. Range matches the AnimationEditor's authored Red/Green/Blue (-255..255 / 255).
float3 ColorOffset;

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 tex = tex2D(SpriteTextureSampler, input.TextureCoordinates);
    // MonoGame's default texture pipeline premultiplies alpha into RGB, so a fully transparent
    // texel already has rgb == 0 - scale the offset by tex.a too, or it would leak a flat color
    // into transparent pixels regardless of alpha (SpriteBatch's premultiplied blend state adds
    // this rgb directly, unscaled by alpha, unlike the vertex-color multiply below).
    tex.rgb = saturate(tex.rgb + ColorOffset * tex.a);
    return tex * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
