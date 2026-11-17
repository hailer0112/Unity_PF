Shader "Storybook/Purification Fountain Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _WaterFlow ("Water Flow", Range(0,1)) = 1
        [PerRendererData] _WaterSparks ("Water Sparks", Range(0,1)) = 1
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

            float _WaterFlow;
            float _WaterSparks;

            float Ripple(float radius, float phase)
            {
                float ring = 1 - smoothstep(.04, .11, abs(radius - lerp(.12, .90, phase)));
                return ring * smoothstep(0,.16,phase) * (1 - smoothstep(.38,1,phase));
            }

            float Spark(float2 pixel, float2 origin, float phase)
            {
                float2 center = floor(origin + float2(0, 4 * phase));
                float2 distance = abs(pixel - center);
                float shape = step(distance.x + distance.y, 1);
                return shape * smoothstep(.09,.34,phase) * (1 - smoothstep(.47,.79,phase));
            }

            fixed4 FountainFragment(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                // UV masks are aligned to the unmodified fountain sprite.
                float2 uv = input.texcoord;
                float2 pixel = floor(uv * 220);
                float2 gridUv = (pixel + .5) / 220;
                float water = smoothstep(.08,.28,color.g - color.r);
                float pool = (1 - smoothstep(.84,1.00,length((uv - float2(.50,.424)) / float2(.133,.051)))) * water;
                float stream = (1 - smoothstep(.010,.017,abs(uv.x - .50))) *
                    smoothstep(.427,.448,uv.y) * (1 - smoothstep(.546,.554,uv.y)) * water;
                float sparkRegion = step(.421,uv.x) * step(uv.x,.582) * step(.454,uv.y) * step(uv.y,.534);
                float paintedSparks = sparkRegion * (1 - stream) * smoothstep(.015,.11,color.g - color.r) *
                    smoothstep(.46,.70,color.g);

                color.rgb = lerp(color.rgb, float3(.017,.054,.066), paintedSparks * lerp(1,.64,_WaterSparks));
                if (_WaterFlow < 1)
                {
                    // Reveal the shadow tones beside the stream as the water stops.
                    float3 leftBank = SampleSpriteTexture(float2(.480,uv.y)).rgb;
                    float3 rightBank = SampleSpriteTexture(float2(.520,uv.y)).rgb;
                    float3 behindStream = lerp(leftBank, rightBank, smoothstep(.487,.513,uv.x));
                    color.rgb = lerp(color.rgb, behindStream * input.color.rgb, stream * (1 - _WaterFlow));
                    color.rgb *= 1 - pool * .80 * (1 - _WaterFlow);
                }

                if (_WaterFlow > 0)
                {
                    float cycle = _Time.y / .85;
                    // Highlights travel down inside the current silhouette without distorting its edge.
                    float band = .5 + .5 * cos(6.283185 * ((.55 - gridUv.y) / .062 - cycle));
                    color.rgb += float3(.12,.30,.29) * (band * band - .33) * stream * _WaterFlow;

                    float radius = length((gridUv - float2(.50,.438)) / float2(.090,.030));
                    float ring = Ripple(radius, frac(cycle)) + .34 * Ripple(radius, frac(cycle + .47));
                    color.rgb += float3(.13,.27,.23) * ring * pool * _WaterFlow;
                }

                if (_WaterSparks > 0)
                {
                    float sparkCycle = _Time.y / 2.4;
                    float sparks = Spark(pixel, float2(99,108), frac(sparkCycle)) +
                        Spark(pixel, float2(103,100), frac(sparkCycle + .69)) +
                        Spark(pixel, float2(119,104), frac(sparkCycle + .34));
                    color.rgb += float3(.21,.59,.51) * sparks * _WaterSparks;
                }

                color.rgb = saturate(color.rgb);
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
