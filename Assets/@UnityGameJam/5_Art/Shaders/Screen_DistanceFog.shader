// 카메라에서 멀어질수록 어둑하게 덮는 화면 안개 + 바닥에 낮게 깔리는 붉은 안개.
// PC_Renderer의 Full Screen Pass(FX_DistanceFog)가 이 머티리얼(M_FX_DistanceFog)로 그린다.
// 값은 머티리얼 인스펙터에서 바로 조정한다(플레이 중에도 반영된다).
Shader "UnityGameJam/Screen/Distance Fog"
{
    Properties
    {
        _FogColor("안개 색", Color) = (0.05, 0.04, 0.07, 1)
        _FogStart("안개 시작 거리 (m)", Float) = 15
        _FogEnd("안개 최대 거리 (m)", Float) = 45
        _FogDensity("최대 농도", Range(0, 1)) = 0.7
        _FogCurve("농도 곡선 (1 = 일정, 클수록 먼 곳에 몰림)", Range(0.3, 3)) = 1.3
        _Darken("멀리 있는 물체 어둡게", Range(0, 1)) = 0.4

        [Header(Ground Mist)]
        _GroundColor("바닥 안개 색", Color) = (0.55, 0.06, 0.1, 1)
        _GroundStrength("바닥 안개 세기", Range(0, 1)) = 0.45
        _GroundHeight("바닥 높이 (월드 Y)", Float) = 0
        _GroundFade("안개 두께 (m)", Range(0.1, 5)) = 1.5
        _GroundNoiseScale("안개 무늬 크기 (작을수록 큼)", Range(0.02, 2)) = 0.35
        _GroundNoiseAmount("안개 얼룩 정도", Range(0, 1)) = 0.5
        _GroundNoiseSpeed("안개 흐르는 속도", Range(0, 2)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "DistanceFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _FogStart;
                float _FogEnd;
                half _FogDensity;
                half _FogCurve;
                half _Darken;
                half4 _GroundColor;
                half _GroundStrength;
                float _GroundHeight;
                float _GroundFade;
                float _GroundNoiseScale;
                half _GroundNoiseAmount;
                float _GroundNoiseSpeed;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 바닥 근처일수록 붉게 덮는다. 월드 좌표로 계산해서 화면이 아니라 바닥에 깔린 것처럼 보인다.
            half GroundMist(float3 positionWS)
            {
                half height = saturate((positionWS.y - _GroundHeight) / max(_GroundFade, 0.01));
                half mist = (1.0 - height) * (1.0 - height);
                float2 drift = float2(_Time.y, _Time.y * 0.7) * _GroundNoiseSpeed;
                float2 p = positionWS.xz * _GroundNoiseScale;
                float noise = ValueNoise(p + drift) * 0.65 + ValueNoise(p * 2.3 - drift * 1.4) * 0.35;
                mist *= lerp(1.0, noise * 1.4, _GroundNoiseAmount);
                return saturate(mist * _GroundStrength);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // 깊이로 월드 위치를 복원해 카메라와의 실제 거리를 쓴다.
                // 탑다운 카메라에서 화면 가장자리가 중앙보다 멀게 느껴지도록 시선 방향 깊이 대신 거리를 쓴다.
                float rawDepth = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                float deviceDepth = rawDepth;
                bool isSky = rawDepth <= 0.00001;
            #else
                float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                bool isSky = rawDepth >= 0.99999;
            #endif
                float3 positionWS = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
                float distanceToCamera = isSky ? _FogEnd : distance(positionWS, _WorldSpaceCameraPos);

                float range = max(_FogEnd - _FogStart, 0.001);
                half fog = pow(saturate((distanceToCamera - _FogStart) / range), _FogCurve);

                if (!isSky)
                {
                    half ground = GroundMist(positionWS);
                    half3 glow = color.rgb * _GroundColor.rgb * 2.2 + _GroundColor.rgb * 0.25;
                    color.rgb = lerp(color.rgb, glow, ground);
                }

                color.rgb *= 1.0 - _Darken * fog;
                color.rgb = lerp(color.rgb, _FogColor.rgb, fog * _FogDensity);
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
