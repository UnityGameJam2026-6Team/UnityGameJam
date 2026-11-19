// 화면 전체를 살짝 붉게 물들이는 화면 효과.
// 가장자리 붉은 그라데이션은 피격 효과처럼 보여서 뺐다. 바닥의 붉은 기운은 Distance Fog의 바닥 안개가 맡는다.
// PC_FacilityDreamHaze_Renderer의 Full Screen Pass(FX_RedTint)가 이 머티리얼(M_FX_RedTint)로 그린다.
// 값은 머티리얼 인스펙터에서 바로 조정한다(플레이 중에도 반영된다).
Shader "UnityGameJam/Screen/Red Tint"
{
    Properties
    {
        _TintColor("전체 색 (곱하기, 흰색 = 효과 없음)", Color) = (1, 0.8, 0.78, 1)
        _TintStrength("전체 강도", Range(0, 1)) = 0.5

    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend DstColor Zero
        ColorMask RGB

        Pass
        {
            Name "RedTint"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _TintStrength;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(lerp(half3(1, 1, 1), _TintColor.rgb, _TintStrength), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
