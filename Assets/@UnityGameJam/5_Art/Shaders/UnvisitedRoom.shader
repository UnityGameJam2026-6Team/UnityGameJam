Shader "UnityGameJam/Unvisited Room"
{
    Properties
    {
        [MainColor] _BaseColor("Room Color", Color) = (0.12, 0.13, 0.17, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+499" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            float4 Vert(float3 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS);
            }

            half4 Frag(float4 positionCS : SV_POSITION) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(positionCS);
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif
                // 비스듬히 내려다봐도 문 밖의 플레이어와 로비 소품은 원래 색을 유지한다.
                float3 sceneWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 sceneOS = TransformWorldToObject(sceneWS);
                clip(0.5 - max(abs(sceneOS.x), abs(sceneOS.z)));
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
