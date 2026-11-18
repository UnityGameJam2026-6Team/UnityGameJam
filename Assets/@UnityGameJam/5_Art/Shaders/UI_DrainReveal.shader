// Purple liquid coat over the red digits that drains downward.
// Pixels above the (wavy, streaky) liquid level are hidden, revealing the red digit underneath.
//   _Level : liquid surface height in sprite UV (1 = covers everything, 0 = fully drained)
//   _T     : clock (set by script) for the wobble
// Sprites using this material must not be packed into an atlas (UV 0..1 assumed).
Shader "UI/DemonKing/DrainReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Level ("Liquid Level", Range(0,1.2)) = 1
        _T ("Clock", Float) = 0
        _Wave ("Surface Wave", Range(0,0.08)) = 0.018
        _Streak ("Trailing Streaks", Range(0,0.2)) = 0.07
        _Soft ("Edge Softness", Range(0.001,0.05)) = 0.01

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
            Name "DrainReveal"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color; float4 _ClipRect;
            float _Level, _T, _Wave, _Streak, _Soft;

            float Hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

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
                half4 col = i.color * tex2D(_MainTex, i.uv);
                float x = i.uv.x;

                // wobbling liquid surface
                float edge = _Level + _Wave * (0.6 * sin(x * 23.0 + _T * 2.1) + 0.4 * sin(x * 57.0 - _T * 3.3));

                // thin streaks of purple that lag behind (still sliding down the red)
                float colId = floor(x * 16.0);
                float h = Hash(colId + 3.0);
                float inCol = 1.0 - smoothstep(0.12, 0.32, abs(frac(x * 16.0) - 0.5));
                float lag = _Streak * step(0.6, h) * (0.4 + 0.6 * Hash(colId + 11.0)) * (0.8 + 0.2 * sin(_T * 1.7 + h * 6.0));
                edge += lag * inCol * saturate(_Level * 4.0);

                col.a *= saturate((edge - i.uv.y) / _Soft);

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
