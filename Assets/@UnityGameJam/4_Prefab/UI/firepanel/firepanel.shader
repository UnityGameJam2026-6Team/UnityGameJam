Shader "UI/firepanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Frame Texture", 2D) = "white" {}
        _Color ("UI Tint", Color) = (1,1,1,1)
        _OuterColor ("Outer Flame", Color) = (0.42,0.06,0.78,1)
        _BodyColor ("Crimson Flame", Color) = (0.93,0.075,0.43,1)
        _HotColor ("Hot Flame", Color) = (1,0.39,0.53,1)
        _CoreColor ("Flame Core", Color) = (1,0.91,0.77,1)
        _GlowColor ("Glow Color", Color) = (0.80,0.09,0.88,1)
        _FlameHeight ("Flame Height", Range(0.5,3.2)) = 2.4
        _FlameWidth ("Flame Width", Range(0.55,1.85)) = 1.45
        _Speed ("Flicker Speed", Range(0,3.5)) = 2
        _Glow ("Glow", Range(0,2)) = 1
        _Embers ("Embers", Range(0,1)) = 1
        _RimLight ("Bowl Reflection", Range(0,2)) = 1

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "CastleBrazierUI"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _ClipRect;
            half4 _Color, _TextureSampleAdd;
            half4 _OuterColor, _BodyColor, _HotColor, _CoreColor, _GlowColor;
            float _FlameHeight, _FlameWidth, _Speed, _Glow, _Embers, _RimLight;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float3 uv : TEXCOORD0;
                #ifdef UNITY_UI_CLIP_RECT
                float4 mask : TEXCOORD1;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                float2 texUV = TRANSFORM_TEX(v.uv, _MainTex);
                // For a full UI quad, this interpolates from 0 at its base to 1 at its top,
                // including when the RawImage extends its top UV above the artwork.
                o.uv = float3(texUV, saturate(v.uv.y));
                o.color = v.color * _Color;
                #ifdef UNITY_UI_CLIP_RECT
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                float2 projection = abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float2 pixelSize = o.vertex.w / max(projection, 0.00001);
                float2 softness = float2(_UIMaskSoftnessX, _UIMaskSoftnessY);
                o.mask = float4(v.vertex.xy * 2.0 - rect.xy - rect.zw,
                                0.25 / (0.25 * softness + abs(pixelSize)));
                #endif
                return o;
            }

            float hash21(float2 p)
            {
                float3 h = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
            }

            // Wrapping lattice rows lets the flow rise continuously through the loop seam.
            float risingNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float2 rows = frac((cell.y + float2(0.0, 1.0)) / 8.0) * 8.0;
                float a = hash21(float2(cell.x, rows.x));
                float b = hash21(float2(cell.x + 1.0, rows.x));
                float c = hash21(float2(cell.x, rows.y));
                float d = hash21(float2(cell.x + 1.0, rows.y));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y) * 2.0 - 1.0;
            }

            // Continue tapering past the tip, so moving noise cannot give it a flat cap.
            float tongue(float2 p, float root, float radius, float height, float lean, float phase)
            {
                float relativeHeight = max(0.0, p.y) / height;
                float u = saturate(relativeHeight);
                float spine = root + u * (lean + 0.15 * sin(6.8 * u - phase));
                float taper = (1.0 - relativeHeight) * (1.0 + 0.32 * u);
                return radius * taper - abs(p.x - spine);
            }

            float emberField(float2 p, float cycle, float seed, float aa)
            {
                float coverage = 0.0;
                [unroll] for (int k = 0; k < 6; k++)
                {
                    float i = (float)k;
                    float life = frac(cycle * (2.0 + fmod(i, 2.0)) + i * 0.38197 + seed * 0.13);
                    float start = hash21(float2(i + 7.0, seed));
                    float x = (start - 0.5) * 1.48 +
                        (0.08 + 0.12 * life) * sin(6.0 * life + 3.7 * i + seed);
                    float y = 0.29 + 0.12 * start + life * (0.61 + 0.31 * start);
                    float2 offset = p - float2(x, y);
                    offset.x -= offset.y * (0.32 * cos(6.0 * life + i));
                    float size = lerp(0.029, 0.014, life) * (0.8 + 0.5 * start);
                    float d = size - length(offset * float2(1.0, 0.60));
                    float fade = smoothstep(0.03, 0.22, life) * (1.0 - smoothstep(0.67, 1.0, life));
                    coverage = max(coverage, smoothstep(-aa, aa, d) * fade);
                }
                return coverage;
            }

            // Returns premultiplied RGBA. art arrives as straight RGBA from the UI texture.
            half4 shadeCastle(float3 uv, half4 art, float seconds)
            {
                const float2 frameSize = float2(1519.0, 1047.0);
                const float cupY = 147.0;
                float2 px = float2(uv.x, 1.0 - uv.y) * frameSize;
                float side = step(0.5, uv.x);
                float dx = (px.x - lerp(110.8, 1407.5, side)) * (1.0 - 2.0 * side);
                half4 frame = half4(art.rgb * art.a, art.a);
                float2 footprint = fwidth(px);
                UNITY_BRANCH
                if (px.y >= 250.0 || abs(dx) >= 156.0)
                    return frame;

                float seed = lerp(1.7, 8.9, side);
                float cycle = frac(frac(seconds * (_Speed * 0.2)) + 0.371 * side);
                float phase = cycle * 6.2831853;
                float pulse = 0.92 + 0.10 * sin(3.0 * phase + seed) + 0.055 * sin(7.0 * phase - seed);
                float2 fireSize = float2(59.0, 108.0) * max(float2(_FlameWidth, _FlameHeight), 0.1);
                // For both a standard Image and an extended RawImage, uv.y / uv.z is
                // the top edge's texture V. Keep tall tips inside the space actually available.
                float headroom = max(0.0, uv.y / max(uv.z, 0.001) - 1.0) * frameSize.y;
                float rise = cupY - px.y;
                float room = cupY + headroom - 8.0;
                float excess = max(0.0, 1.22 * fireSize.y - room);
                float tipCompression = smoothstep(0.50, 1.0, rise / room);
                float2 p = float2(dx, rise + excess * tipCompression) / fireSize;
                float y = saturate(p.y);
                float drift = y * (0.23 * sin(5.3 * y - 2.0 * phase + seed)
                                 + 0.13 * sin(9.1 * y - 3.0 * phase + 0.8 * seed));
                float2 q = float2(p.x - drift, p.y);
                float low = risingNoise(float2(q.x * 1.8 + seed, p.y * 3.5 - cycle * 8.0));
                float fine = risingNoise(float2(q.x * 4.8 + low * 0.35 + seed + 9.7,
                                                p.y * 7.0 - cycle * 16.0));
                q.x += 0.10 * low + 0.038 * fine;
                float mainH = 0.96 + 0.09 * sin(2.0 * phase + seed) + 0.035 * sin(5.0 * phase);
                float leftH = 0.74 + 0.14 * sin(3.0 * phase + 1.4 + seed);
                float rightH = 0.79 + 0.15 * sin(2.0 * phase + 0.9 - seed);
                float body = tongue(q, -0.04, 0.52, mainH, 0.07, 2.0 * phase + seed);
                // The flanking licks rise through the two gaps in each three-point crown.
                float left = tongue(q, -0.53, 0.40, leftH, 0.09, 3.0 * phase + 2.4);
                float right = tongue(q, 0.53, 0.39, rightH, -0.06, 2.0 * phase - 1.1);
                float field = max(body, max(left, right)) + 0.073 * low + 0.052 * fine;
                field = min(field, (p.y + 0.083) * 1.5 - 0.085 * p.x * p.x);
                float2 pixelInFire = footprint / fireSize;
                float aa = max(0.006, 0.60 * max(pixelInFire.x, pixelInFire.y));
                float flame = smoothstep(-aa, aa, field);
                float heat = 0.48 - 0.64 * y + 1.50 * field + 0.19 * low + 0.095 * fine;
                half3 fireColor = lerp(_OuterColor.rgb, _BodyColor.rgb, smoothstep(-0.20, 0.29, heat));
                fireColor = lerp(fireColor, _HotColor.rgb, smoothstep(0.32, 0.54, heat));
                fireColor = lerp(fireColor, _CoreColor.rgb, smoothstep(0.62, 0.87, heat));

                float spark = emberField(p, cycle, seed, aa) * _Embers;
                float topFade = smoothstep(1.0, 11.0, px.y + headroom);
                float bounds = (1.0 - smoothstep(131.0, 156.0, abs(dx)))
                             * (1.0 - smoothstep(212.0, 250.0, px.y))
                             * smoothstep(0.0, 10.0, min(px.x, frameSize.x - px.x));
                float visible = bounds * topFade;
                float2 glowXY = float2(p.x - drift * 0.45, p.y - 0.28);
                float aura = exp2(-2.8 * glowXY.x * glowXY.x - 4.6 * glowXY.y * glowXY.y);
                float fringe = exp2(-20.0 * abs(field)) * saturate(1.0 - abs(p.x) * 0.48)
                             * smoothstep(-0.12, 0.06, p.y) * (1.0 - smoothstep(0.93, 1.22, p.y));
                float glow = saturate((0.29 * aura + 0.19 * fringe) * pulse * _Glow);
                half3 effectRGB = _GlowColor.rgb * glow;
                float effectAlpha = glow;
                effectRGB = lerp(effectRGB, fireColor, flame);
                effectAlpha += flame * (1.0 - effectAlpha);
                effectRGB = lerp(effectRGB, lerp(_HotColor.rgb, _CoreColor.rgb, 0.50), spark);
                effectAlpha += spark * (1.0 - effectAlpha);
                effectRGB *= visible;
                effectAlpha *= visible;

                float2 metalXY = float2((dx - 6.0 * sin(2.0 * phase + seed)) / 88.0,
                                        (px.y - 128.0) / 68.0);
                float stoneLight = exp2(-dot(metalXY, metalXY) * 1.65) * pulse * _RimLight * bounds;
                float stoneValue = max(art.r, max(art.g, art.b));
                frame.rgb += art.a * stoneLight * stoneValue * (_HotColor.rgb * 0.43 + _GlowColor.rgb * 0.20);

                // All three crown prongs are foreground. The source alpha preserves their outlines.
                frame.rgb += effectRGB * (1.0 - art.a);
                frame.a += effectAlpha * (1.0 - art.a);
                return frame;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 art = (tex2D(_MainTex, i.uv.xy) + _TextureSampleAdd) * step(i.uv.y, 1.0);
                half4 color = shadeCastle(i.uv, art, _Time.y);
                half opacity = i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                opacity *= mask.x * mask.y;
                #endif
                color.rgb *= i.color.rgb * opacity;
                color.a *= opacity;
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
