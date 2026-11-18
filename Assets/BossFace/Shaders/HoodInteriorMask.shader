// Shadow head: three shaders that work together through the stencil buffer (ref 77).
//
//  DemonKing/HoodInteriorMask  - skinned copy of the hood's inner surfaces. Marks "you are looking into the hood"
//                                in the stencil and lays down a near-black backdrop there. Gold rim / cream cloth
//                                pixels are skipped (read from the body texture), so the hood itself is untouched.
//  DemonKing/HoodFog           - black mist particles. Drawn only inside the hood opening (stencil), on top of the
//                                backdrop, behind the head ball.
//  DemonKing/ShadowHeadBall    - the round head inside the hood. Drawn only inside the hood opening (stencil),
//                                so it can never poke out through the hood cloth.
// Draw order: mask (Geometry+1) -> fog (Geometry+2) -> ball (Geometry+3) -> eyes (Transparent).
// Unlit, Built-in and URP.

Shader "DemonKing/HoodInteriorMask"
{
    Properties
    {
        [NoScaleOffset] _BodyTex ("Body Texture (for the rim cut)", 2D) = "black" {}
        _BaseColor ("Backdrop", Color) = (0.012, 0.006, 0.014, 1)
        [Toggle] _BodyUV0 ("Body UV In UV0 (exported FBX mesh)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+1" "RenderType" = "Opaque" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Back
            Stencil { Ref 77 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _BodyTex; float4 _BaseColor; float _BodyUV0;
            struct appdata { float4 vertex : POSITION; float2 uv0 : TEXCOORD0; float2 uvb : TEXCOORD2; };
            struct v2f { float4 pos : SV_POSITION; float2 uvb : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uvb = (_BodyUV0 > 0.5) ? v.uv0 : v.uvb; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_BodyTex, i.uvb).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                c = LinearToGammaSpace(c);
                #endif
                // only the gold rim trim stays visible. Cream cloth on these (inward-facing) triangles is the
                // INSIDE of the hood, so it gets covered too.
                float gold = step(0.6, c.r) * step(0.4, c.g) * step(c.b, 0.35);
                clip(0.5 - gold);
                return _BaseColor;
            }
            ENDCG
        }
    }
    FallBack Off
}
