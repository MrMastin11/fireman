Shader "Skybox/Panoramic Seamless"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panoramic Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
        _SeamWidth ("Seam Blend Width", Range(0.001, 0.12)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half _Exposure;
                half _Rotation;
                half _SeamWidth;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
#if UNITY_REVERSED_Z
                output.positionCS.z = 0.0;
#else
                output.positionCS.z = output.positionCS.w;
#endif
                output.direction = input.positionOS;
                return output;
            }

            float2 PanoramaUV(float3 direction)
            {
                direction = normalize(direction);
                float2 uv;
                uv.x = atan2(direction.x, direction.z) / (2.0 * PI) + 0.5;
                uv.y = asin(clamp(direction.y, -1.0, 1.0)) / PI + 0.5;
                uv.x = frac(uv.x + _Rotation / 360.0);
                return uv;
            }

            half4 SamplePanorama(float2 uv)
            {
                float seamWidth = max(_SeamWidth, 0.001);
                float distanceToSeam = min(uv.x, 1.0 - uv.x);
                float blend = saturate((seamWidth - distanceToSeam) / seamWidth);

                if (blend <= 0.0)
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                float seamCoordinate = uv.x < 0.5 ? uv.x : uv.x - 1.0;
                float crossfade = saturate((seamCoordinate + seamWidth) / (2.0 * seamWidth));
                float leftSampleU = 1.0 - seamWidth + (seamCoordinate + seamWidth) * 0.5;
                float rightSampleU = (seamCoordinate + seamWidth) * 0.5;
                half4 leftSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(leftSampleU, uv.y));
                half4 rightSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(rightSampleU, uv.y));
                half4 blendedSeam = lerp(leftSample, rightSample, crossfade);
                half4 originalSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return lerp(originalSample, blendedSeam, blend);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SamplePanorama(PanoramaUV(input.direction));
                color.rgb *= _Tint.rgb * _Exposure;
                return color;
            }
            ENDHLSL
        }
    }
}
