Shader "Storybook/Spent Purification Fountain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment FountainFragment
            #include "UnitySprites.cginc"

            fixed4 FountainFragment(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                // UV regions on the supplied artwork: basin, stream and airborne sparks.
                float pool = 1 - smoothstep(.82, 1.06, length((input.texcoord - float2(.50,.421)) / float2(.139,.065)));
                float stream = 1 - smoothstep(.68, 1.03, length((input.texcoord - float2(.50,.494)) / float2(.023,.075)));
                float sparks = step(.412,input.texcoord.x) * step(input.texcoord.x,.582) *
                    step(.46,input.texcoord.y) * step(input.texcoord.y,.555);
                float light = smoothstep(.17,.46,color.g - color.r) * smoothstep(.23,.66,color.g);
                float intensity = max(max(pool,stream), sparks * light);
                float luminance = dot(color.rgb,float3(.299,.587,.114));
                color.rgb = lerp(color.rgb, float3(.035,.082,.103) + luminance * float3(.065,.10,.12), intensity);
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
