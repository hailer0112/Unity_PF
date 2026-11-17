Shader "Storybook/MerchantLivingIdle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        _IdlePhase ("Idle Phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment Frag
            #include "UnitySprites.cginc"
            float _IdlePhase;
            float Region(float2 p, float2 low, float2 high, float feather)
            {
                float2 inside = smoothstep(low, low + feather, p) *
                    (1.0 - smoothstep(high - feather, high, p));
                return inside.x * inside.y;
            }
            fixed4 Frag(v2f input) : SV_Target
            {
                // Coordinates in the original first sheet frame, measured from its top left.
                float2 pixel = float2(input.texcoord.x * 1536.0,
                    (1.0 - input.texcoord.y) * 1024.0);
                float robe = Region(pixel, float2(155, 300), float2(291, 379), 15);
                float ornament = Region(pixel, float2(294, 241), float2(328, 298), 6);
                float shift = robe * sin(_IdlePhase + pixel.y * .045) * 1.0 +
                    ornament * sin(_IdlePhase + .8) * 1.0;
                float2 uv = input.texcoord + float2(round(shift), 0) * float2(1.0/1536.0, 1.0/1024.0);
                fixed4 color = SampleSpriteTexture(uv);
                float staff = Region(pixel, float2(82, 125), float2(167, 204), 12);
                float purple = saturate((color.b - color.g) * 5.0);
                float flow = sin(_IdlePhase * 2.0 + pixel.y * .16 + pixel.x * .09);
                color.rgb += half3(.12, .025, .18) * staff * purple * (.5 + .5 * flow);
                float eye = Region(pixel, float2(204, 178), float2(231, 212), 4);
                float gold = saturate((color.r - color.b) * 4.0);
                color.rgb *= 1.0 + eye * gold * (.04 + .04 * sin(_IdlePhase));
                color *= input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}


