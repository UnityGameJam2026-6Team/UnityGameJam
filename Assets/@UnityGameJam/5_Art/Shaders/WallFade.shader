// 카메라와 플레이어 사이를 가리는 벽에 잠깐 씌우는 반투명 셰이더.
// WallOcclusionFader가 가리는 렌더러만 이 머티리얼(M_FX_WallFade)의 복사본으로 바꾸고, 다시 보이면 원래 머티리얼로 되돌린다.
// 벽 전체가 아니라 카메라→플레이어 시선 둘레만 원뿔 모양으로 비워서, 나머지 벽은 그대로 보인다.
// 구멍 크기·투명도는 카메라의 WallOcclusionFader 컴포넌트에서 조정한다(전역 값으로 넘긴다).
Shader "UnityGameJam/Wall Fade"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _HoleTint("구멍 테두리 빛 색", Color) = (1, 0.35, 0.85, 1)
        _HoleTintStrength("구멍 테두리 빛 세기", Range(0, 3)) = 0.2
        [HideInInspector] _WallFadeAmount("Fade Amount", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "WallFadeForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _HoleTint;
                half _HoleTintStrength;
                float _WallFadeAmount;
            CBUFFER_END

            // WallOcclusionFader가 매 프레임 넣는 전역 값.
            float4 _WallFadeTarget; // xyz = 비울 중심(플레이어 몸통)
            float4 _WallFadeParams; // x = 플레이어 위치의 구멍 반경(m), y = 테두리 부드러움, z = 가장 투명할 때 불투명도

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);

                // 원래 Lit 머티리얼과 크게 달라 보이지 않도록 주광 + 그림자 + 주변광만 계산한다.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (ndl * mainLight.shadowAttenuation * mainLight.distanceAttenuation) + SampleSH(normalWS);
                half3 color = albedo.rgb * lighting;

                // 카메라→플레이어 시선에서 가까운 부분만 비운다. 반경이 카메라 쪽으로 좁아지는 원뿔이라 화면에서는 일정한 원으로 보인다.
                float3 cameraPos = _WorldSpaceCameraPos;
                float3 sight = _WallFadeTarget.xyz - cameraPos;
                float along = saturate(dot(input.positionWS - cameraPos, sight) / max(dot(sight, sight), 0.0001));
                float3 closest = cameraPos + sight * along;
                float radius = max(_WallFadeParams.x * along, 0.05);
                float softness = clamp(_WallFadeParams.y, 0.01, 0.95);
                float dist = distance(input.positionWS, closest);
                half hole = 1.0 - smoothstep(radius * (1.0 - softness), radius * (1.0 + softness), dist);
                hole *= saturate(_WallFadeAmount);

                half alpha = lerp(1.0, _WallFadeParams.z, hole);
                half rim = saturate(hole * (1.0 - hole) * 4.0);
                color += _HoleTint.rgb * rim * _HoleTintStrength;
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _HoleTint;
                half _HoleTintStrength;
                float _WallFadeAmount;
            CBUFFER_END

            // 그림자는 투명해져도 원래 벽처럼 그대로 드리운다(바닥 밝기가 갑자기 바뀌지 않게).
            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
