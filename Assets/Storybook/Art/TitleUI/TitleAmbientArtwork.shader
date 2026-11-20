Shader "Storybook/Title Ambient Artwork"
{
    Properties
    {
        [PerRendererData] _MainTex ("Title Artwork", 2D) = "white" {}
        _Book ("Book Motion", Vector) = (0,1,0,0)
        _Glow ("Local Glow", Vector) = (0,0,0,0)
        _Lanterns ("Lantern Flicker", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Book, _Glow, _Lanterns;
            static const float2 ArtworkSize = float2(1672, 941);

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 tag : COLOR;
            };

            Varyings vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.tag = input.color;
                return output;
            }

            float patchMask(float2 pixel, float2 lower, float2 upper, float2 feather)
            {
                float2 edge = smoothstep(lower, lower + feather, pixel)
                    * (1 - smoothstep(upper - feather, upper, pixel));
                return edge.x * edge.y;
            }

            float gold(float3 color)
            {
                return smoothstep(.38, .81, color.r) * smoothstep(.025, .29, color.g - color.b);
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                int patch = (int)floor(input.uv.x * .5);
                float2 position = float2(input.uv.x - patch * 2, 1 - input.uv.y) * ArtworkSize;
                float2 samplePosition = position;
                if (patch == 0)
                {
                    // Move the book rigidly. The outer band anchors the sampling to the unchanged artwork.
                    float strength = patchMask(position, float2(628, 402), float2(1169, 757), float2(69, 22));
                    float2 local = position - float2(899, 583) + float2(0, _Book.x);
                    float2 rotated = float2(local.x * _Book.y + local.y * _Book.z,
                                            -local.x * _Book.z + local.y * _Book.y);
                    samplePosition = lerp(position, rotated + float2(899, 583), strength);
                }

                half3 color = tex2D(_MainTex, float2(samplePosition.x, ArtworkSize.y - samplePosition.y) / ArtworkSize).rgb;
                float light = 0;
                if (patch == 0)
                {
                    float core = patchMask(position, float2(730, 410), float2(1076, 681), float2(80, 62));
                    float rings = patchMask(position, float2(634, 421), float2(1163, 742), float2(46, 39));
                    light = gold(color) * (core * _Book.w + rings * _Glow.x * (1 - core * .78));
                }
                else if (patch == 1)
                {
                    light = gold(color) * _Glow.y *
                        patchMask(position, float2(530,173), float2(1195,355), float2(36,24));
                }
                else if (patch == 2)
                {
                    float red = smoothstep(.09, .32, color.r - max(color.g, color.b));
                    float region = smoothstep(1220, 1320, position.x);
                    return fixed4(float3(1, .18, .31), red * region * _Glow.z * input.tag.a);
                }
                else if (patch == 8)
                {
                    light = gold(color) * _Glow.x * .65 *
                        patchMask(position, float2(472,763), float2(1286,869), float2(78,18));
                }
                else
                {
                    float2 center;
                    float2 extent;
                    if (patch == 3) { center = float2(124, 232); extent = float2(52, 66); }
                    else if (patch == 4) { center = float2(294, 390); extent = float2(29, 43); }
                    else if (patch == 5) { center = float2(64, 792); extent = float2(53, 64); }
                    else if (patch == 6) { center = float2(1557, 385); extent = float2(43, 46); }
                    else { center = float2(1359, 554); extent = float2(28, 40); }
                    float2 distance = abs(position - center) / extent;
                    float mask = saturate(1 - dot(distance, distance));
                    float flicker = patch == 3 || patch == 7 ? _Lanterns.x : patch == 5 ? _Lanterns.z : _Lanterns.y;
                    // Only the warm glass and immediate light change.
                    light = gold(color) * mask * flicker;
                }
                return fixed4(saturate(color + light * float3(.83, .58, .28)), input.tag.a);
            }
            ENDCG
        }
    }
}
