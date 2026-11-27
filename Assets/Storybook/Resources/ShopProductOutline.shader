Shader "Storybook/Shop Product Outline"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            fixed4 _RendererColor;

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.vertex = UnityObjectToClipPos(input.vertex);
                o.uv = input.uv;
                o.color = input.color * _RendererColor;
                return o;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                return fixed4(input.color.rgb, tex2D(_MainTex, input.uv).a * input.color.a);
            }
            ENDCG
        }
    }
}
