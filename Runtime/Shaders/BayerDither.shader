Shader "Hidden/PostFX/BayerDither"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "BayerDither"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ColorDepth;
            float _DitherStrength;
            float _PixelSize;
            float _Desaturation;
            float _Darkness;
            float _WarmthShift;
            float _NoiseAmount;
            float _VignetteStrength;

            static const float kBayer4x4[16] =
            {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };

            float SampleBayer(float2 pixelPos)
            {
                int x = (int)fmod(abs(pixelPos.x), 4.0);
                int y = (int)fmod(abs(pixelPos.y), 4.0);
                return kBayer4x4[y * 4 + x];
            }

            float Hash12(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 resolution = _ScreenParams.xy;

                float2 sampleUV = uv;
                if (_PixelSize > 1.0)
                {
                    float2 lowRes = resolution / _PixelSize;
                    sampleUV = floor(uv * lowRes) / lowRes;
                }

                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);

                color.r = lerp(color.r, color.r * 1.15, _WarmthShift);
                color.g = lerp(color.g, color.g * 0.95, _WarmthShift);
                color.b = lerp(color.b, color.b * 0.75, _WarmthShift);

                float luma = dot(color.rgb, float3(0.299, 0.587, 0.114));
                color.rgb = lerp(color.rgb, luma.xxx, _Desaturation);

                color.rgb *= (1.0 - _Darkness);

                if (_NoiseAmount > 0.001)
                {
                    float noise = Hash12(uv + frac(_Time.y * 0.1)) * 2.0 - 1.0;
                    color.rgb += noise * _NoiseAmount;
                }

                float2 ditherCoord = (_PixelSize > 1.0)
                    ? uv * (resolution / _PixelSize)
                    : uv * resolution;
                float bayer = SampleBayer(ditherCoord) - 0.5;
                float levels = max(_ColorDepth, 2.0);
                color.rgb = floor((color.rgb + bayer * _DitherStrength / levels) * levels) / levels;

                if (_VignetteStrength > 0.001)
                {
                    float2 centered = uv - 0.5;
                    color.rgb *= saturate(1.0 - dot(centered, centered) * _VignetteStrength * 3.0);
                }

                return float4(saturate(color.rgb), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
