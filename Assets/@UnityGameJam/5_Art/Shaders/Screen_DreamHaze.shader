// 몽환적이고 위험한 분위기를 한 번에 입히는 화면 효과.
//  - 비스듬히 내려오는 분홍 빛줄기(월드 공간 볼류메트릭 흉내) + 바닥에 떨어진 빛 웅덩이
//  - 떠다니는 빛 알갱이와 불씨(앞쪽 큰 보케 / 뒤쪽 작은 반짝임)
//  - 화면 가장자리 흐림(틸트 시프트) + 가장자리에서 꿈틀대는 어두운 연기
//  - 아주 약한 화면 일렁임
// PC_Renderer의 Full Screen Pass(FX_DreamHaze)가 후처리 직전에 이 머티리얼(M_FX_DreamHaze)로 그린다.
// 그래서 빛줄기·알갱이도 Bloom을 받아 번진다. 값은 머티리얼 인스펙터에서 바로 조정한다(플레이 중에도 반영된다).
Shader "UnityGameJam/Screen/Dream Haze"
{
    Properties
    {
        [Header(Light Shafts)]
        _ShaftColor("빛줄기 색", Color) = (1, 0.32, 0.78, 1)
        _ShaftStrength("빛줄기 세기", Range(0, 3)) = 0.65
        _ShaftDirection("빛이 내려오는 방향 (월드)", Vector) = (0.55, -1, 0.35, 0)
        _ShaftScale("빛줄기 촘촘함", Range(0.05, 3)) = 0.8
        _ShaftThin("빛줄기 가늘기", Range(0.2, 0.85)) = 0.56
        _ShaftCoverage("빛이 드는 영역 비율", Range(0, 1)) = 0.55
        _ShaftGround("바닥 높이 (월드 Y)", Float) = 0
        _ShaftHeight("빛줄기가 보이는 높이 (m)", Range(0.5, 10)) = 2.6
        _ShaftSpeed("빛줄기 일렁임 속도", Range(0, 2)) = 0.12
        _ShaftFloorPool("바닥 빛 웅덩이", Range(0, 3)) = 0.7
        _PoolColor("바닥 빛 웅덩이 색", Color) = (1, 0.58, 0.3, 1)

        [Header(Floating Motes)]
        _MoteColorA("떠다니는 빛 색", Color) = (1, 0.42, 0.95, 1)
        _MoteColorB("불씨 색", Color) = (1, 0.45, 0.12, 1)
        _MoteEmberRatio("불씨 비율", Range(0, 1)) = 0.4
        _MoteStrength("떠다니는 빛 세기", Range(0, 4)) = 1
        _MoteDensity("떠다니는 빛 밀도", Range(2, 40)) = 13
        _MoteAmount("알갱이가 있는 칸 비율", Range(0, 1)) = 0.16
        _MoteSize("떠다니는 빛 크기", Range(0.02, 0.3)) = 0.09
        _MoteSpeed("떠오르는 속도", Range(0, 0.5)) = 0.04

        [Header(Edge Blur)]
        _BlurStrength("가장자리 흐림", Range(0, 1)) = 0.8
        _BlurRadius("흐림 반경 (화면 비율)", Range(0, 0.03)) = 0.011
        _BlurStart("흐림 시작 (중앙 0 ~ 가장자리 1)", Range(0, 1.4)) = 0.45
        _BlurEnd("흐림 최대 (중앙 0 ~ 가장자리 1)", Range(0.1, 1.5)) = 1.1

        [Header(Edge Smoke)]
        _SmokeColor("가장자리 연기 색 (곱하기)", Color) = (0.16, 0.05, 0.22, 1)
        _SmokeStrength("가장자리 연기 세기", Range(0, 1)) = 0.55
        _SmokeStart("연기 시작 (중앙 0 ~ 가장자리 1)", Range(0, 1.4)) = 0.75
        _SmokeSpeed("연기 흐르는 속도", Range(0, 1)) = 0.06

        [Header(Dream Wobble)]
        _WobbleAmount("화면 일렁임", Range(0, 0.004)) = 0.0007
        _WobbleSpeed("일렁임 속도", Range(0, 3)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "DreamHaze"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShaftColor;
                half _ShaftStrength;
                float4 _ShaftDirection;
                float _ShaftScale;
                half _ShaftThin;
                half _ShaftCoverage;
                float _ShaftGround;
                float _ShaftHeight;
                float _ShaftSpeed;
                half _ShaftFloorPool;
                half4 _PoolColor;

                half4 _MoteColorA;
                half4 _MoteColorB;
                half _MoteEmberRatio;
                half _MoteStrength;
                float _MoteDensity;
                half _MoteAmount;
                float _MoteSize;
                float _MoteSpeed;

                half _BlurStrength;
                float _BlurRadius;
                half _BlurStart;
                half _BlurEnd;

                half4 _SmokeColor;
                half _SmokeStrength;
                half _SmokeStart;
                float _SmokeSpeed;

                float _WobbleAmount;
                float _WobbleSpeed;
            CBUFFER_END

            #define SHAFT_STEPS 4
            #define BLUR_TAPS 6

            float DreamHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float DreamNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = DreamHash(i);
                float b = DreamHash(i + float2(1, 0));
                float c = DreamHash(i + float2(0, 1));
                float d = DreamHash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float DreamFbm(float2 p)
            {
                return DreamNoise(p) * 0.55 + DreamNoise(p * 2.07 + 13.1) * 0.3 + DreamNoise(p * 4.13 + 27.7) * 0.15;
            }

            // 화소마다 다른 값. 레이마칭 계단 무늬와 흐림 무늬를 흩뜨린다.
            float DreamDither(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float3 ShaftDirection()
            {
                float3 direction = _ShaftDirection.xyz;
                direction.y = min(direction.y, -0.05);
                return normalize(direction);
            }

            // 월드 위치 p를 지나는 빛줄기의 밝기. 빛 방향을 따라 바닥에 투영해서 무늬를 찾으므로
            // 공중의 빛줄기와 바닥의 빛 웅덩이가 같은 모양으로 이어진다.
            float ShaftPattern(float3 p, float3 lightDirection)
            {
                float2 q = p.xz - lightDirection.xz * ((p.y - _ShaftGround) / lightDirection.y);
                float2 horizontal = lightDirection.xz;
                float horizontalLength = length(horizontal);
                float2 along = horizontalLength > 0.001 ? horizontal / horizontalLength : float2(0, 1);
                float2 across = float2(-along.y, along.x);

                float t = _Time.y * _ShaftSpeed;
                float s = dot(q, across) * _ShaftScale;
                float a = dot(q, along) * _ShaftScale * 0.18;
                float stripes = DreamNoise(float2(s + t, a - t * 0.5)) * 0.7 + DreamNoise(float2(s * 2.7 - t * 1.3, a * 1.9 + 3.1)) * 0.3;
                // 큰 얼룩으로 빛이 드는 곳과 안 드는 곳을 나눈다.
                float patch = DreamNoise(q * _ShaftScale * 0.11 + float2(11.7, 3.3) + t * 0.15);
                patch = smoothstep(1.0 - _ShaftCoverage - 0.15, 1.0 - _ShaftCoverage + 0.25, patch);
                return smoothstep(_ShaftThin, _ShaftThin + 0.16, stripes) * patch;
            }

            half3 Motes(float2 uv, float aspect, float edge)
            {
                half3 result = 0;
                float time = _Time.y;
                [unroll]
                for (int layer = 0; layer < 3; layer++)
                {
                    // 0: 앞쪽의 크고 흐린 보케(가장자리 위주), 1: 중간, 2: 뒤쪽의 작은 반짝임
                    float layerScale = layer == 0 ? 0.45 : (layer == 1 ? 1.0 : 1.9);
                    float speed = _MoteSpeed * (layer == 0 ? 1.6 : (layer == 1 ? 1.0 : 0.6));
                    float2 p = float2(uv.x * aspect, uv.y);
                    p.y -= time * speed;
                    p.x += sin(time * 0.21 + layer * 1.7) * 0.03;
                    p *= _MoteDensity * layerScale;

                    float2 cell = floor(p);
                    float2 local = frac(p);
                    float seed = DreamHash(cell + layer * 17.31);
                    float exists = step(1.0 - _MoteAmount, seed);
                    float2 center = float2(DreamHash(cell + 3.17), DreamHash(cell + 7.73)) * 0.5 + 0.25;
                    center.x += sin(time * (0.4 + seed) + seed * 6.2831) * 0.12;
                    center.y += cos(time * (0.3 + seed * 0.7) + seed * 4.1) * 0.08;

                    float size = _MoteSize * lerp(0.5, 1.0, DreamHash(cell + 11.1)) * (layer == 0 ? 1.8 : 1.0);
                    float d = length(local - center);
                    float glow = exp(-(d * d) / max(size * size, 0.00001));
                    float twinkle = 0.55 + 0.45 * sin(time * (1.3 + seed * 2.5) + seed * 40.0);
                    half3 tint = lerp(_MoteColorA.rgb, _MoteColorB.rgb, step(1.0 - _MoteEmberRatio, DreamHash(cell + 5.53)));
                    float weight = layer == 0 ? 0.55 * saturate(edge * 1.2) : (layer == 1 ? 0.8 : 0.5);
                    result += tint * glow * twinkle * exists * weight;
                }
                return result;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float aspect = _ScaledScreenParams.x / max(_ScaledScreenParams.y, 1.0);
                float time = _Time.y;
                float dither = DreamDither(input.positionCS.xy);

                // 중앙 0, 화면 변의 가운데 1, 모서리 약 1.4
                float edge = length((uv - 0.5) * 2.0);

                // 1. 아주 약한 일렁임
                float2 wobble = float2(DreamNoise(uv * 3.0 + float2(time * _WobbleSpeed, 0.0)),
                                       DreamNoise(uv * 3.0 + float2(5.2, time * _WobbleSpeed))) - 0.5;
                float2 sampleUv = uv + wobble * 2.0 * _WobbleAmount;

                // 2. 가장자리 흐림
                half3 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sampleUv, 0).rgb;
                float blur = smoothstep(_BlurStart, _BlurEnd, edge) * _BlurStrength;
                if (blur > 0.001)
                {
                    float radius = _BlurRadius * blur;
                    float rotation = dither * 6.2831853;
                    half3 sum = color;
                    [unroll]
                    for (int i = 0; i < BLUR_TAPS; i++)
                    {
                        float angle = i * 2.39996323 + rotation;
                        float r = sqrt((i + 0.5) / BLUR_TAPS);
                        float2 offset = float2(cos(angle), sin(angle)) * r * radius * float2(1.0 / aspect, 1.0);
                        sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sampleUv + offset, 0).rgb;
                    }
                    color = lerp(color, sum / (BLUR_TAPS + 1), saturate(blur * 1.5));
                }

                // 3. 깊이로 월드 위치 복원
                float rawDepth = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                float deviceDepth = rawDepth;
                bool isSky = rawDepth <= 0.00001;
            #else
                float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                bool isSky = rawDepth >= 0.99999;
            #endif
                float3 cameraPos = _WorldSpaceCameraPos;
                float3 positionWS = ComputeWorldSpacePosition(uv, isSky ? 0.5 : deviceDepth, UNITY_MATRIX_I_VP);
                float3 toSurface = positionWS - cameraPos;
                float surfaceDistance = isSky ? 80.0 : length(toSurface);
                float3 rayDirection = normalize(toSurface);

                // 4. 빛줄기: 시선이 바닥~_ShaftHeight 사이를 지나는 구간만 몇 걸음 걸으며 모은다.
                float3 lightDirection = ShaftDirection();
                if (_ShaftStrength > 0.001 && abs(rayDirection.y) > 0.0001)
                {
                    float tTop = (_ShaftGround + _ShaftHeight - cameraPos.y) / rayDirection.y;
                    float tBottom = (_ShaftGround - cameraPos.y) / rayDirection.y;
                    float tEnter = max(0.0, min(tTop, tBottom));
                    float tExit = min(surfaceDistance, max(tTop, tBottom));
                    if (tExit > tEnter)
                    {
                        float stepLength = (tExit - tEnter) / SHAFT_STEPS;
                        float gathered = 0.0;
                        for (int s = 0; s < SHAFT_STEPS; s++)
                        {
                            float3 p = cameraPos + rayDirection * (tEnter + (s + dither) * stepLength);
                            float h = saturate((p.y - _ShaftGround) / _ShaftHeight);
                            float heightFade = smoothstep(0.0, 0.12, h) * (1.0 - smoothstep(0.55, 1.0, h));
                            gathered += ShaftPattern(p, lightDirection) * heightFade;
                        }
                        half shaft = 1.0 - exp(-gathered * stepLength * _ShaftStrength * 0.3);
                        color += _ShaftColor.rgb * shaft;
                    }
                }

                // 바닥에 떨어진 빛 웅덩이: 보라 화면 속 따뜻한 금빛으로 바닥을 밝힌다.
                if (!isSky && _ShaftFloorPool > 0.001)
                {
                    half floorMask = 1.0 - saturate((positionWS.y - _ShaftGround) / 0.35);
                    half pool = ShaftPattern(positionWS, lightDirection) * floorMask * _ShaftFloorPool;
                    color += color * _PoolColor.rgb * pool * 1.6 + _PoolColor.rgb * pool * 0.05;
                }

                // 5. 가장자리에서 꿈틀대는 어두운 연기
                if (_SmokeStrength > 0.001)
                {
                    float2 smokeUv = float2(uv.x * aspect, uv.y) * 2.2;
                    float smoke = DreamFbm(smokeUv + float2(time * _SmokeSpeed, -time * _SmokeSpeed * 1.6));
                    smoke = DreamFbm(smokeUv * 0.8 + smoke * 1.5 - float2(0.0, time * _SmokeSpeed));
                    half smokeMask = saturate(smoothstep(_SmokeStart, 1.45, edge + (smoke - 0.5) * 0.45) * _SmokeStrength * 1.4);
                    color = lerp(color, color * _SmokeColor.rgb, smokeMask);
                }

                // 6. 떠다니는 빛 알갱이와 불씨
                color += Motes(uv, aspect, edge) * _MoteStrength;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
