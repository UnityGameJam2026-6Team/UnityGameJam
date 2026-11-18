// 2D 애니메이션(셀) 느낌을 내기 위한 범용 URP 툰 셰이더.
// - 단단한 명암 경계(1~4단), 단색 그림자 톤
// - 화면 기준 두께가 일정한 Inverted Hull 외곽선
// - 하드 스펙큘러 / 림 라이트 / 노멀맵 / 이미션 / 알파 클립
// - Forward, Forward+ 모두 지원
Shader "UnityGameJam/Toon Cel"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

        [Header(Normal)]
        [Toggle(_NORMALMAP)] _UseNormalMap("Use Normal Map", Float) = 0
        [Normal][NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1

        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.55,0.5,0.7,1)
        _ShadowThreshold("Shadow Threshold", Range(-1,1)) = 0.1
        _ShadowSmoothness("Shadow Edge Smoothness", Range(0,0.5)) = 0.01
        [IntRange] _ShadeSteps("Shade Steps", Range(1,4)) = 1
        _ShadeStepSpacing("Shade Step Spacing", Range(0.05,1)) = 0.35
        _ShadowColor2("Deep Shadow Color (Steps >= 2)", Color) = (0.35,0.3,0.5,1)
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1
        _ReceivedShadowStrength("Received Shadow Strength", Range(0,1)) = 1
        _LightColorInfluence("Light Color Influence", Range(0,1)) = 1
        _AmbientStrength("Flat Ambient Strength", Range(0,1)) = 0.15
        _AdditionalLightStrength("Additional Light Strength", Range(0,2)) = 1

        [Header(Specular)]
        [HDR] _SpecColor("Specular Color", Color) = (1,1,1,1)
        _SpecSize("Specular Size (0 = Off)", Range(0,1)) = 0
        _SpecSmoothness("Specular Edge Smoothness", Range(0,0.5)) = 0.01

        [Header(Rim Light)]
        [HDR] _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimStrength("Rim Strength (0 = Off)", Range(0,1)) = 0.35
        _RimWidth("Rim Width", Range(0,1)) = 0.25
        _RimSmoothness("Rim Edge Smoothness", Range(0,0.5)) = 0.02
        [ToggleUI] _RimLitSideOnly("Rim On Lit Side Only", Float) = 1

        [Header(Emission)]
        [Toggle(_EMISSION)] _UseEmission("Use Emission", Float) = 0
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.08,0.05,0.1,1)
        _OutlineWidth("Outline Width (0 = Off)", Range(0,10)) = 2
        _OutlineBaseColorBlend("Outline Base Color Blend", Range(0,1)) = 0.3
        _OutlineMaxDistance("Outline Fade Distance", Float) = 60
        _OutlineZOffset("Outline Z Offset", Range(0,1)) = 0.0005
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "UniversalMaterialType"="Unlit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);     SAMPLER(sampler_BumpMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        // SRP Batcher 호환을 위해 모든 패스가 동일한 CBUFFER를 공유한다.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _BumpScale;
            half4 _ShadowColor;
            half4 _ShadowColor2;
            half _ShadowThreshold;
            half _ShadowSmoothness;
            half _ShadeSteps;
            half _ShadeStepSpacing;
            half _ReceiveShadows;
            half _ReceivedShadowStrength;
            half _LightColorInfluence;
            half _AmbientStrength;
            half _AdditionalLightStrength;
            half4 _SpecColor;
            half _SpecSize;
            half _SpecSmoothness;
            half4 _RimColor;
            half _RimStrength;
            half _RimWidth;
            half _RimSmoothness;
            half _RimLitSideOnly;
            half4 _EmissionColor;
            half4 _OutlineColor;
            half _OutlineWidth;
            half _OutlineBaseColorBlend;
            float _OutlineMaxDistance;
            half _OutlineZOffset;
        CBUFFER_END

        half4 SampleAlbedo(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }

        void ApplyAlphaClip(half alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
        }
        ENDHLSL

        // ---------------------------------------------------------------
        // 1. 셀 셰이딩 메인 패스
        // ---------------------------------------------------------------
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  tangentWS  : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = nrm.normalWS;
                output.tangentWS = half4(nrm.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half StepCount() { return clamp(round(_ShadeSteps), 1.0h, 4.0h); }

            // 명암을 1~4단계로 끊어서 셀 애니메이션 같은 면 분할을 만든다.
            // 반환값: 1 = 밝은 면, (N-1)/N = 1차 그림자, ... 0 = 가장 깊은 그림자
            half CelStep(half nDotL)
            {
                half steps = StepCount();
                half w = max(_ShadowSmoothness, 0.0001h);
                half sum = 0.0h;
                // 첫 경계는 _ShadowThreshold, 이후 경계는 _ShadeStepSpacing 간격으로 그림자 쪽에 배치
                UNITY_UNROLL
                for (int k = 0; k < 4; k++)
                {
                    if (k < (int)steps)
                        sum += smoothstep(-w, w, nDotL - (_ShadowThreshold - k * _ShadeStepSpacing));
                }
                return sum / steps;
            }

            half FirstShadowLevel() { half n = StepCount(); return (n - 1.0h) / n; }

            half3 ShadeColor(half cel)
            {
                half firstShadow = FirstShadowLevel();
                if (cel >= firstShadow)
                    return lerp(_ShadowColor.rgb, half3(1,1,1), saturate((cel - firstShadow) / max(1.0h - firstShadow, 0.001h)));
                return lerp(_ShadowColor2.rgb, _ShadowColor.rgb, saturate(cel / max(firstShadow, 0.001h)));
            }

            half3 LightTint(half3 lightColor)
            {
                return lerp(half3(1,1,1), lightColor, _LightColorInfluence);
            }

            half3 AdditionalLightCel(Light light, half3 normalWS)
            {
                half nDotL = dot(normalWS, light.direction);
                half w = max(_ShadowSmoothness, 0.0001h);
                half atten = light.distanceAttenuation * lerp(1.0h, light.shadowAttenuation, _ReceiveShadows);
                // 거리 감쇠도 단계적으로 끊어 2D 조명처럼 표현
                half stepped = smoothstep(0.05h - w, 0.05h + w, atten) * saturate(atten * 4.0h);
                return smoothstep(-w, w, nDotL - _ShadowThreshold) * stepped * light.color * _AdditionalLightStrength;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SampleAlbedo(input.uv);
                ApplyAlphaClip(albedo.a);

                half3 normalWS = normalize(input.normalWS);
                #if defined(_NORMALMAP)
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                    normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                #endif
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float4 shadowCoord;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);

                // 받은 그림자도 단단한 경계로 처리
                half w = max(_ShadowSmoothness, 0.0001h);
                half receivedShadow = smoothstep(0.5h - w, 0.5h + w, mainLight.shadowAttenuation);
                receivedShadow = lerp(1.0h, receivedShadow, _ReceiveShadows * _ReceivedShadowStrength);

                half nDotL = dot(normalWS, mainLight.direction);
                half rawCel = CelStep(nDotL);
                half cel = lerp(min(rawCel, FirstShadowLevel()), rawCel, receivedShadow);
                half litMask = step(0.999h, cel);
                half3 lightTint = LightTint(mainLight.color) * mainLight.distanceAttenuation;

                half3 color = albedo.rgb * ShadeColor(cel) * lightTint;

                // 평평한 앰비언트: 법선 방향과 무관하게 균일하게 더해 입체감을 줄인다.
                half3 flatAmbient = max(SampleSH(half3(0,1,0)), half3(0,0,0));
                color += albedo.rgb * flatAmbient * _AmbientStrength;

                // 하드 스펙큘러
                if (_SpecSize > 0.0h)
                {
                    half3 h = normalize(mainLight.direction + viewDirWS);
                    half nDotH = saturate(dot(normalWS, h));
                    half specEdge = 1.0h - _SpecSize * _SpecSize;
                    half spec = smoothstep(specEdge - _SpecSmoothness, specEdge + _SpecSmoothness, nDotH);
                    color += spec * litMask * _SpecColor.rgb * lightTint;
                }

                // 림 라이트 (외곽선 안쪽의 얇은 밝은 테두리)
                if (_RimStrength > 0.0h)
                {
                    half fresnel = 1.0h - saturate(dot(normalWS, viewDirWS));
                    half rimEdge = 1.0h - _RimWidth;
                    half rim = smoothstep(rimEdge - _RimSmoothness, rimEdge + _RimSmoothness, fresnel);
                    rim *= lerp(1.0h, litMask, _RimLitSideOnly);
                    color += rim * _RimStrength * _RimColor.rgb * albedo.rgb * lightTint;
                }

                // 추가 라이트 (포인트/스팟)
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalWS = normalWS;
                    inputData.viewDirectionWS = viewDirWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    half4 shadowMask = half4(1,1,1,1);
                    half3 addLight = 0;

                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            Light dirLight = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                            addLight += AdditionalLightCel(dirLight, normalWS);
                        }
                    #endif

                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                        addLight += AdditionalLightCel(light, normalWS);
                    LIGHT_LOOP_END

                    color += albedo.rgb * addLight;
                #endif

                #if defined(_EMISSION)
                    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // 2. 외곽선 (Inverted Hull, 화면 기준 일정 두께)
        // ---------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   fogFactor  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);

                // 클립 공간에서 법선 방향으로 밀어 화면상 두께를 픽셀 단위로 일정하게 유지
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS);
                float2 dir = normalCS.xy;
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float2(0, 0);

                float dist = distance(positionWS, GetCameraPositionWS());
                float fade = saturate(1.0 - dist / max(_OutlineMaxDistance, 0.001));
                float widthPx = _OutlineWidth * fade;

                float2 offset = dir * widthPx * 2.0 / _ScreenParams.xy;
                positionCS.xy += offset * positionCS.w;

                // 본체와 겹치는 영역의 Z-fighting 방지
                #if UNITY_REVERSED_Z
                    positionCS.z -= _OutlineZOffset * positionCS.w;
                #else
                    positionCS.z += _OutlineZOffset * positionCS.w;
                #endif

                // 두께가 0이면 외곽선 삼각형을 퇴화시켜 그리지 않음
                if (widthPx <= 0.0001)
                    positionCS = float4(0, 0, 0, 0);

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SampleAlbedo(input.uv);
                ApplyAlphaClip(albedo.a);
                half3 color = lerp(_OutlineColor.rgb, _OutlineColor.rgb * albedo.rgb, _OutlineBaseColorBlend);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // 3. 그림자 캐스터
        // ---------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                positionCS = ApplyShadowClamping(positionCS);
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyAlphaClip(SampleAlbedo(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // 4. Depth / DepthNormals (SSAO, 후처리, Depth Texture 호환)
        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyAlphaClip(SampleAlbedo(input.uv).a);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyAlphaClip(SampleAlbedo(input.uv).a);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
