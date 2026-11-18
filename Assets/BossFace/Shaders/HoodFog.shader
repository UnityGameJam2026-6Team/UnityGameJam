// Black mist particles inside the hood. See ShadowHead.shader for how the three pieces fit together.
// Drawn only where DemonKing/HoodInteriorMask marked the stencil (inside the hood opening), ignoring depth,
// so the mist fills the whole opening; the head ball is drawn after and stays crisp.
Shader "DemonKing/HoodFog"
{
    Properties
    {
        _MainTex ("Puff (alpha)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+2" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            Stencil { Ref 77 Comp Equal }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; float4 _Tint;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color * _Tint; o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o; }
            float4 frag(v2f i) : SV_Target
            {
                return float4(i.color.rgb, tex2D(_MainTex, i.uv).a * i.color.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
