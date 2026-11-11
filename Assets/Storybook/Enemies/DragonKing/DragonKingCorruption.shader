Shader "Storybook/Dragon King Corruption"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CorruptionIntensity ("Corruption Intensity", Float) = 1
        _Purified ("Purified", Range(0, 1)) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment CorruptionFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float4 _MainTex_TexelSize;
            float _CorruptionIntensity;
            float _Purified;

            half RedCrack(fixed4 texel)
            {
                // Red fissures are saturated in both channels relative to gold and warm skin.
                half red = texel.r;
                half separation = min(red - texel.g * 2.1h, red - texel.b * 1.45h);
                return smoothstep(.08h, .37h, separation) * step(.40h, texel.a);
            }

            fixed4 CorruptionFrag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                fixed4 original = SampleSpriteTexture(uv);
                half core = RedCrack(original);
                float2 texel = _MainTex_TexelSize.xy * 2.0;
                half nearby = max(
                    max(RedCrack(SampleSpriteTexture(uv + float2(texel.x, 0))),
                        RedCrack(SampleSpriteTexture(uv - float2(texel.x, 0)))),
                    max(RedCrack(SampleSpriteTexture(uv + float2(0, texel.y))),
                        RedCrack(SampleSpriteTexture(uv - float2(0, texel.y)))));
                half halo = max(0, nearby - core);
                half3 tint = lerp(original.rgb, half3(1, .18, .18), saturate(core * _CorruptionIntensity));
                tint = saturate(tint + half3(.34, .027, .045) * halo * _CorruptionIntensity);
                half redOnly = min(original.r - original.g * 1.9h, original.r - original.b * 1.35h);
                half stain = smoothstep(.035h, .23h, redOnly);
                half value = dot(original.rgb, half3(.2126h, .7152h, .0722h));
                half3 restored = lerp(half3(.09h, .12h, .16h), half3(.28h, .39h, .43h), saturate(value));
                tint = lerp(tint, restored, stain * _Purified);
                fixed4 color = fixed4(tint, original.a) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
