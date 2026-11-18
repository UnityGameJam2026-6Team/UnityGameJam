// The round shadow head inside the hood. See ShadowHead.shader for how the three pieces fit together.
// Near-black with a faint cool rim so it reads as a ball (a head) inside the black mist.
// Drawn only inside the hood opening (stencil), so it never pokes out through the hood cloth.
Shader "DemonKing/ShadowHeadBall"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.0, 0.0, 0.0, 1)
        _RimColor ("Rim", Color) = (0.24, 0.1, 0.3, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.6
        _TopLight ("Top Light", Range(0, 0.2)) = 0.02
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+3" "RenderType" = "Opaque" }
        Pass
        {
            ZWrite On
            ZTest Always      // the whole ball shows inside the hood opening (the stencil keeps it inside the hood)
            Cull Back
            Stencil { Ref 77 Comp Equal }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _BaseColor, _RimColor; float _RimPower, _TopLight;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; };
            v2f vert(appdata i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = normalize(WorldSpaceViewDir(i.vertex));
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float rim = pow(1 - saturate(dot(n, normalize(i.v))), _RimPower);
                float top = saturate(n.y) * _TopLight;
                return float4(_BaseColor.rgb + _RimColor.rgb * rim + top, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
