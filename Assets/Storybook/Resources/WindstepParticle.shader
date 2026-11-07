Shader "Storybook/Windstep Particle"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
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
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Vertex vertex)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(vertex.position);
                output.color = vertex.color;
                output.uv = vertex.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float edge = 1 - smoothstep(.15, 1, length(input.uv * 2 - 1));
                return fixed4(input.color.rgb, input.color.a * edge);
            }
            ENDCG
        }
    }
}
