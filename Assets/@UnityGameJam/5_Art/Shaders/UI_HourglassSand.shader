// Procedural flowing sand for the hourglass, drawn inside the glass mask (sprite alpha = glass interior).
// Sits BEHIND the hourglass frame sprite (whose glass area is transparent).
//   _Progress 0 = full top bulb, 1 = all sand fallen.   _T = animation clock (set by script).
// Glass geometry in sprite UV: _NeckY (narrowest point), _TopY (top of upper bulb), _BottomY (bottom of lower bulb),
// _CenterX / _HalfWidth (horizontal center and half width of the bulbs).
// Sprites using this material must not be packed into an atlas (UV 0..1 assumed).
Shader "UI/DemonKing/HourglassSand"
{
    Properties
    {
        [PerRendererData] _MainTex ("Glass Mask", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Progress ("Progress", Range(0,1)) = 0
        _T ("Clock", Float) = 0

        _NeckY ("Neck Y", Range(0,1)) = 0.5
        _TopY ("Top Y", Range(0,1)) = 0.9
        _BottomY ("Bottom Y", Range(0,1)) = 0.1
        _CenterX ("Center X", Range(0,1)) = 0.5
        _HalfWidth ("Half Width", Range(0.01,0.5)) = 0.3

        _SandColor ("Sand", Color) = (0.86,0.13,0.25,1)
        _SandShade ("Sand Shade", Color) = (0.45,0.05,0.16,1)
        _SandLight ("Sand Light", Color) = (1,0.55,0.62,1)
        _GlassColor ("Glass", Color) = (0.16,0.08,0.22,0.55)
        _Highlight ("Glass Highlight", Color) = (0.95,0.88,1,0.35)
        _StreamWidth ("Stream Width", Range(0.005,0.2)) = 0.045
        _FallSpeed ("Fall Speed", Float) = 2.2

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "HourglassSand"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color; float4 _ClipRect;
            float _Progress, _T, _NeckY, _TopY, _BottomY, _CenterX, _HalfWidth, _StreamWidth, _FallSpeed;
            fixed4 _SandColor, _SandShade, _SandLight, _GlassColor, _Highlight;

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float mask = tex2D(_MainTex, i.uv).a;
                if (mask < 0.003) return 0;

                float p = saturate(_Progress);
                float x = (i.uv.x - _CenterX) / _HalfWidth;   // -1..1 across the bulbs
                float ax = abs(x);
                float y = i.uv.y;
                float topH = _TopY - _NeckY;
                float botH = _NeckY - _BottomY;

                // ---- upper bulb: level drops, a funnel forms in the middle
                float level = _NeckY + topH * 0.8 * (1.0 - p);
                float funnel = topH * 0.22 * saturate(p * 4.0) * saturate(1.0 - p) * 2.0 * (1.0 - saturate(ax * 1.6));
                float topSurface = level - funnel;
                float inTop = step(_NeckY - 0.01, y) * step(y, topSurface) * step(0.002, 1.0 - p);

                // ---- lower bulb: mound grows
                float h = botH * 0.8 * p;
                float botSurface = _BottomY + h * (0.5 + 0.5 * (1.0 - saturate(ax))) + 0.004 * step(0.001, p);
                float inBot = step(y, botSurface);

                // ---- falling stream with broken grains
                float flowing = step(0.002, p) * step(p, 0.998);
                float wob = sin(y * 90.0 + _T * 13.0) * 0.006;
                float inCol = step(abs(i.uv.x - _CenterX + wob), _StreamWidth * _HalfWidth);
                float grainRow = floor((y + _T * _FallSpeed * 0.25) * 140.0);
                float grains = step(0.28, Hash(float2(grainRow, floor(_T * 30.0) * 0.0)));
                float inStream = flowing * inCol * step(botSurface, y) * step(y, _NeckY + 0.02) * grains;

                float sand = saturate(inTop + inBot + inStream);

                // ---- sand color: speckle + edge shade + bright crust on top surfaces
                float speck = Hash(floor(i.uv * float2(90.0, 140.0)));
                fixed3 sc = lerp(_SandShade.rgb, _SandColor.rgb, 0.55 + 0.45 * speck);
                sc = lerp(sc, _SandShade.rgb, saturate(ax - 0.35) * 0.8);
                float crustTop = inTop * saturate(1.0 - (topSurface - y) / 0.012);
                float crustBot = inBot * saturate(1.0 - (botSurface - y) / 0.012);
                sc = lerp(sc, _SandLight.rgb, saturate(crustTop + crustBot) * 0.75);
                float glitter = step(0.992, Hash(floor(i.uv * 120.0) + floor(_T * 7.0))) * sand;
                sc += glitter * 0.6;

                // ---- glass where there is no sand + a vertical highlight
                fixed4 glass = _GlassColor;
                float hl = saturate(1.0 - abs(x + 0.55) / 0.12) * 0.9 + saturate(1.0 - abs(x - 0.62) / 0.05) * 0.4;
                glass.rgb = lerp(glass.rgb, _Highlight.rgb, hl * _Highlight.a);
                glass.a = saturate(glass.a + hl * _Highlight.a);

                fixed4 col = lerp(glass, fixed4(sc, 1.0), sand);
                col *= i.color;
                col.a *= mask;
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                col.rgb *= col.a;
                return col;
            }
        ENDCG
        }
    }
}
