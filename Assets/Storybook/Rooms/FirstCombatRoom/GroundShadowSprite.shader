Shader "Storybook/Ground Shadow Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Shadow Mask", 2D) = "white" {}
        _Color ("Tint", Color) = (0.035, 0.047, 0.072, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-30" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SpriteFrag
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"
            ENDCG
        }
    }
}

