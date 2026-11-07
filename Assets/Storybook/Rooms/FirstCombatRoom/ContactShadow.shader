Shader "Storybook/Contact Shadow"
{
    Properties { _Tint ("Tint", Color) = (0.035, 0.047, 0.072, 0.54) }
    SubShader
    {
        Tags { "Queue"="Transparent-30" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Vertex
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Tint;

            Varyings Vert(Vertex vertex)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(vertex.position);
                output.uv = vertex.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2 - 1);
                return fixed4(_Tint.rgb, _Tint.a * (1 - smoothstep(.25, 1, radius)));
            }
            ENDCG
        }
    }
}
