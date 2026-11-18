// Floating glowing eyes (blink shader), built from hand-drawn (Grok) parts:
//   _ScleraTex  : the glowing yellow eye shape (the "white" of the eye) on black, glow included
//   _ScleraMask : solid body of that shape (white = inside)
//   _PupilTex   : the ring pupil (alpha)
// Blink = the sclera narrows toward the line between its two corners and vanishes.
// The pupil is never squashed; the narrowing sclera body just clips it away.
// Additive (black = invisible). Works in Built-in and URP (plain unlit pass, no lighting).
Shader "DemonKing/GlowEyes"
{
    Properties
    {
        [Header(Art (Grok parts))]
        [NoScaleOffset] _ScleraTex ("Sclera Art (on black)", 2D) = "black" {}
        [NoScaleOffset] _ScleraMask ("Sclera Body Mask", 2D) = "black" {}
        [NoScaleOffset] _PupilTex ("Pupil (rings, alpha)", 2D) = "clear" {}
        _CornerA ("Sclera Corner A (uv)", Vector) = (0.1, 0.4, 0, 0)
        _CornerB ("Sclera Corner B (uv)", Vector) = (0.9, 0.6, 0, 0)
        _PupilPos ("Pupil Center (sclera uv)", Vector) = (0.45, 0.5, 0, 0)
        _PupilSize ("Pupil Size (x sclera width)", Range(0.02, 1)) = 0.24

        [Header(Layout on the Quad)]
        [Toggle] _Pair ("Both Eyes On One Quad", Float) = 1
        _EyeSpacing ("Eye Spacing", Range(0, 3)) = 1.15
        _EyeSize ("Eye Size (width)", Range(0.1, 3)) = 1.0
        _EyeTilt ("Eye Tilt (deg)", Range(-40, 40)) = 0
        _EyeOffsetY ("Eye Offset Y", Range(-0.5, 0.5)) = 0
        [Toggle] _FlipArt ("Flip Art (outer corner side)", Float) = 0
        [Toggle] _AutoAspect ("Aspect From Object Scale (Quad)", Float) = 1
        _Aspect ("Manual Aspect (width/height)", Float) = 2.5

        [Header(Glow)]
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity (>1 for Bloom)", Range(0, 8)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.6
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.1
        _BlackCut ("Black Level Cut (jpeg noise)", Range(0, 0.2)) = 0.03

        [Header(Float)]
        _BobAmount ("Bob Height (x quad height)", Range(0, 0.3)) = 0.04
        _BobSpeed ("Bob Speed", Range(0, 6)) = 1.8

        [Header(Look)]
        _Look ("Pupil Look Offset (x,y)", Vector) = (0, 0, 0, 0)

        [Header(Blink)]
        _Blink ("Blink (0 open - 1 closed)", Range(0, 1)) = 0
        [Toggle] _AutoBlink ("Auto Blink", Float) = 1
        _BlinkInterval ("Interval (s)", Range(0.5, 10)) = 3.4
        _BlinkRandom ("Interval Randomness", Range(0, 1)) = 0.7
        _BlinkClose ("Close Time (s)", Range(0.02, 0.5)) = 0.07
        _BlinkHold ("Hold Closed (s)", Range(0, 0.5)) = 0.05
        _BlinkOpen ("Open Time (s)", Range(0.02, 0.6)) = 0.13
        _DoubleBlink ("Double Blink Chance", Range(0, 1)) = 0.25

        [Header(Corner Flicker (outer eye corner licks like hand drawn fire))]
        _CornerFlicker ("Corner Flicker (0 = off)", Range(0, 1)) = 0
        _CornerStart ("Starts At (0 inner - 1 outer corner)", Range(0, 1)) = 0.66
        _CornerReach ("Tail Stretch", Range(0, 1.5)) = 0.7
        _CornerThin ("Tail Thinning", Range(0, 3)) = 1.1
        _CornerCurl ("Tail Curl Up (hook)", Range(-0.5, 0.5)) = 0.14
        _CornerSway ("Tail Sway", Range(0, 0.4)) = 0.11
        _CornerWobble ("Tail Wobble", Range(0, 0.2)) = 0.025
        _CornerTear ("Tip Tear Off", Range(0, 1)) = 0.6
        _CornerSpeed ("Flicker Speed", Range(0, 10)) = 4.0

        [Header(Eye Fire (glowing flame streaming off the outer eye corner))]
        _EyeFire ("Eye Fire Amount (0 off, 1 on, above 1 = ignition burst)", Range(0, 2)) = 0
        [HDR] _FireCore ("Core (white hot)", Color) = (1, 0.98, 0.8, 1)
        [HDR] _FireColor ("Flame", Color) = (1, 0.8, 0.08, 1)
        [HDR] _FireGlow ("Glow", Color) = (1, 0.4, 0.04, 1)
        _FireGlowAmt ("Glow Amount", Range(0, 1.5)) = 0.55
        _FireAngle ("Direction (deg above outward)", Range(-30, 90)) = 36
        _FireLength ("Length (x eye width)", Range(0.1, 2)) = 0.95
        _FireWidth ("Base Width (x eye width)", Range(0.02, 0.5)) = 0.15
        _FireBack ("Base Starts Inside Eye (x eye width)", Range(0, 0.6)) = 0.2
        _FireBend ("Bend Up And Back", Range(-1, 1)) = 0.3
        _FireRag ("Tongue Raggedness", Range(0, 2)) = 1.4
        _FireStreak ("Tongue Thinness", Range(1, 30)) = 13
        _FireSpeed ("Flicker Speed", Range(0, 8)) = 3.4
        _FireWiggle ("Wiggle", Range(0, 1)) = 0.4
        _EyeOffsetX ("Eye Offset X (quad shifted outward)", Range(-0.5, 0.5)) = 0

        [Header(Render)]
        _OffsetFactor ("Depth Offset Factor", Float) = -1
        _OffsetUnits ("Depth Offset Units", Float) = -2
        _DepthPush ("Depth Push Toward Camera (m)", Float) = 0
        _StencilRef ("Stencil Ref (0 = off)", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Compare", Float) = 8
        _BlinkSeed ("Blink Seed (-1 = from position)", Float) = -1
        [HideInInspector] _TimeScale ("Time Scale", Float) = 1
        [HideInInspector] _TimeOffset ("Time Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            Offset [_OffsetFactor], [_OffsetUnits]
            Stencil { Ref [_StencilRef] Comp [_StencilComp] }   // default Always: no effect

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _ScleraTex; float4 _ScleraTex_TexelSize;
            sampler2D _ScleraMask;
            sampler2D _PupilTex;  float4 _PupilTex_TexelSize;
            float _BobAmount, _BobSpeed;
            float4 _CornerA, _CornerB, _PupilPos;
            float _PupilSize;
            float _Pair, _EyeSpacing, _EyeSize, _EyeTilt, _EyeOffsetY, _FlipArt, _AutoAspect, _Aspect;
            float4 _Tint;
            float _Intensity, _PulseSpeed, _PulseAmount, _BlackCut;
            float4 _Look;
            float _Blink, _AutoBlink, _BlinkInterval, _BlinkRandom, _BlinkClose, _BlinkHold, _BlinkOpen, _DoubleBlink;
            float _TimeScale, _TimeOffset;
            float _DepthPush, _BlinkSeed;
            float _CornerFlicker, _CornerStart, _CornerReach, _CornerThin, _CornerCurl, _CornerSway, _CornerWobble, _CornerTear, _CornerSpeed;
            float _EyeFire, _FireGlowAmt, _FireAngle, _FireLength, _FireWidth, _FireBack, _FireBend, _FireRag, _FireStreak, _FireSpeed, _FireWiggle, _EyeOffsetX;
            float4 _FireCore, _FireColor, _FireGlow;

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx) * 2 - 1;
            }
            float Noise3(float3 p)   // gradient noise ~[-0.5, 0.5]
            {
                float3 i = floor(p), f = frac(p);
                float3 u = f * f * f * (f * (f * 6 - 15) + 10);
                float n000 = dot(Hash33(i + float3(0,0,0)), f - float3(0,0,0));
                float n100 = dot(Hash33(i + float3(1,0,0)), f - float3(1,0,0));
                float n010 = dot(Hash33(i + float3(0,1,0)), f - float3(0,1,0));
                float n110 = dot(Hash33(i + float3(1,1,0)), f - float3(1,1,0));
                float n001 = dot(Hash33(i + float3(0,0,1)), f - float3(0,0,1));
                float n101 = dot(Hash33(i + float3(1,0,1)), f - float3(1,0,1));
                float n011 = dot(Hash33(i + float3(0,1,1)), f - float3(0,1,1));
                float n111 = dot(Hash33(i + float3(1,1,1)), f - float3(1,1,1));
                return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                            lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
            }

            // Outer eye corner licks like hand-drawn fire. From _CornerStart to the tip, the eye art is remapped into a
            // longer, thinner tail (no new art): it stretches and pulls back, curls up into a hook, a wave runs to the
            // tip, and the very tip tears into flickering bits. uv = sclera art uv. Returns the lookup uv; tear multiplies.
            float2 CornerFlicker(float2 uv, float t, float seed, float sa, out float tear)
            {
                tear = 1;
                if (_CornerFlicker <= 0) return uv;
                float2 p = float2(uv.x, uv.y / sa);                       // metric art space
                float2 pa = float2(_CornerA.x, _CornerA.y / sa), pb = float2(_CornerB.x, _CornerB.y / sa);
                float2 ax = pb - pa; float len = length(ax);
                float2 dir = ax / len, nrm = float2(-dir.y, dir.x);       // nrm = up (same for both eyes: art space)
                float along = dot(p - pa, dir) / len;                     // 0 inner corner .. 1 outer corner
                float across = dot(p - pa, nrm) / len;
                float a0 = _CornerStart;
                if (along <= a0) return uv;

                float tt = t * _CornerSpeed + seed * 7.0 + (_FlipArt > 0.5 ? 2.3 : 0.0);   // the two eyes don't move in lockstep
                float amt = _CornerFlicker;
                // tail length (1 = original corner), licking in and out
                float lick = 0.7 + 0.3 * sin(tt * 0.83) + 0.7 * Noise3(float3(tt * 0.45, 1.7, seed * 5.0));
                float Lt = 1 + _CornerReach * amt * max(lick, 0.15);
                float k = (along - a0) / (1 - a0);                        // 0 at start, 1 at the original corner
                float ka = k / Lt;                                        // where in the art this point comes from
                float kk = saturate(ka);
                // hook: the tail curls up toward the tip, with a wave running out to it
                float bend = (_CornerCurl * (0.7 + 0.35 * sin(tt * 0.6)) + _CornerSway * (sin(tt - k * 3.2) + 0.45 * sin(tt * 1.7 - k * 6.0 + 1.3))) * kk * kk * amt;
                float3 np = float3(k * 3.0 - tt * 0.8, across * 6.0, tt * 0.35);
                float2 wob = float2(Noise3(np), Noise3(np + 17.3)) * _CornerWobble * kk * amt;
                // thinner toward the tip so it stays a pointed tongue, not a bulge
                float thin = 1 + _CornerThin * amt * kk;
                float acrossArt = (across - bend * (1 - a0)) * thin;
                float alongArt = a0 + ka * (1 - a0);
                float2 q = pa + dir * (alongArt * len) + nrm * (acrossArt * len) + wob * len;

                // tip tear: bits pinch off near the end of the tail and flicker outward
                // cut bands run ACROSS the tail, so the tip piece separates whole (no holes in the middle)
                float tn = Noise3(float3(k * 3.2 - tt * 1.4, acrossArt * 0.8, tt * 0.45)) + 0.5;
                float zone = smoothstep(0.8, 1.0, ka) * _CornerTear * amt;
                float edge = tn - zone * 0.85;
                tear = lerp(1, saturate((edge - 0.05) / max(fwidth(edge), 1e-4) + 0.5), step(0.001, zone));
                return float2(q.x, q.y * sa);
            }

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 inst : TEXCOORD1; // x = aspect, y = per-object random seed
            };

            float Hash11(float p) { p = frac(p * 0.1031); p *= p + 33.33; p *= p + p; return frac(p); }

            v2f vert(appdata v)
            {
                v2f o;
                float sx = length(unity_ObjectToWorld._m00_m10_m20);
                float sy = length(unity_ObjectToWorld._m01_m11_m21);
                float3 wp = unity_ObjectToWorld._m03_m13_m23;
                float seed = frac(sin(dot(wp, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                if (_BlinkSeed >= 0) seed = frac(_BlinkSeed);   // e.g. two single-eye quads that must blink together
                float t = _Time.y * _TimeScale + _TimeOffset;
                // gentle floating bob (object space, Quad is 1 unit tall)
                float4 vtx = v.vertex;
                vtx.y += sin(t * _BobSpeed + seed * 6.2831) * _BobAmount;
                o.pos = UnityObjectToClipPos(vtx);
                // depth-only push toward the camera (screen position unchanged):
                // lets eyes that sit deep in a concave face win the depth test against the face sides
                if (_DepthPush != 0)
                {
                    float3 vp = UnityObjectToViewPos(vtx);
                    vp.z += _DepthPush;                       // view space looks down -z
                    float4 cp = mul(UNITY_MATRIX_P, float4(vp, 1));
                    o.pos.z = cp.z / cp.w * o.pos.w;
                }
                o.uv = v.uv;
                o.inst.x = (_AutoAspect > 0.5) ? sx / max(sy, 1e-5) : _Aspect;
                o.inst.y = seed;
                return o;
            }

            // 0 -> 1 -> 0 over one blink, x = seconds since the blink started
            float BlinkShape(float x)
            {
                if (x < 0) return 0;
                float c = saturate(x / _BlinkClose);
                float o = saturate((x - _BlinkClose - _BlinkHold) / _BlinkOpen);
                float closing = c * c;                    // lids accelerate shut
                float opening = 1 - (1 - o) * (1 - o);    // ease out when opening
                return min(closing, 1 - opening);
            }

            float AutoBlink(float t, float seed)
            {
                float cyc = _BlinkInterval;
                float tt = t + seed * 97.0;
                float k = floor(tt / cyc);
                float local = tt - k * cyc;
                float dur = _BlinkClose + _BlinkHold + _BlinkOpen;
                float start = Hash11(k + seed * 131.7) * _BlinkRandom * max(cyc - 2.0 * dur - 0.1, 0);
                float b = BlinkShape(local - start);
                if (Hash11(k * 1.37 + seed * 17.3 + 5.0) < _DoubleBlink)
                    b = max(b, BlinkShape(local - start - dur - 0.05));
                return b;
            }

            float InBox(float2 uv)
            {
                float2 e = min(uv, 1 - uv);
                return step(0, min(e.x, e.y));
            }

            float4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _TimeScale + _TimeOffset;
                float aspect = i.inst.x;
                float seed = i.inst.y;

                // quad space in "quad height" units, origin at the center
                float2 q = float2((i.uv.x - 0.5) * aspect - _EyeOffsetX, i.uv.y - 0.5 - _EyeOffsetY);
                float side = (_Pair > 0.5) ? (q.x >= 0 ? 1.0 : -1.0) : 1.0;
                float ex = (_Pair > 0.5) ? abs(q.x) - _EyeSpacing * 0.5 : q.x;   // +x = toward the outer corner

                float ang = radians(_EyeTilt);
                float cs = cos(ang), sn = sin(ang);
                float2 e = float2(ex * cs + q.y * sn, -ex * sn + q.y * cs);

                float scleraAspect = _ScleraTex_TexelSize.z / max(_ScleraTex_TexelSize.w, 1);
                float2 uv = float2(e.x / _EyeSize + 0.5, e.y * scleraAspect / _EyeSize + 0.5);
                float flip = (_FlipArt > 0.5) ? -1.0 : 1.0;
                uv.x = (_FlipArt > 0.5) ? 1 - uv.x : uv.x;

                // ---- blink: narrow toward the corner-to-corner line ----
                float blink = _Blink;
                if (_AutoBlink > 0.5) blink = max(blink, AutoBlink(t, seed));
                float open = 1 - blink;

                // ---- outer corner flicker (sclera only; the pupil keeps the undistorted uv) ----
                float tear;
                float2 fuv = CornerFlicker(uv, t, seed, scleraAspect, tear);

                float2 ca = _CornerA.xy, cb = _CornerB.xy;
                float lineV = lerp(ca.y, cb.y, (fuv.x - ca.x) / (cb.x - ca.x));
                float2 suv = float2(fuv.x, lineV + (fuv.y - lineV) / max(open, 1e-3));

                float inS = InBox(suv);
                float3 col = tex2D(_ScleraTex, suv).rgb * inS * tear;
                col = max(col - _BlackCut, 0) / (1 - _BlackCut);
                float body = tex2D(_ScleraMask, suv).r * inS * tear;

                // ---- pupil: fixed size, clipped by the narrowing body ----
                float pupilAspect = _PupilTex_TexelSize.z / max(_PupilTex_TexelSize.w, 1);
                float2 look = _Look.xy * float2(side * flip, 1);
                float2 pc = _PupilPos.xy + look;
                float2 puv = float2((uv.x - pc.x) / _PupilSize + 0.5,
                                    (uv.y - pc.y) * pupilAspect / (_PupilSize * scleraAspect) + 0.5);
                float4 pupil = tex2D(_PupilTex, puv) * InBox(puv);
                col = lerp(col, pupil.rgb, pupil.a * body);

                // ---- eye fire: a glowing flame streaming off the OUTER eye corner, up and outward ----
                // Flame frame anchored at the outer corner: s = distance along the stream, w = sideways.
                // Thin tongues = noise stretched along the stream and racing outward, so it flickers fast and frays at the tip.
                // _EyeFire 0..1 grows it in, above 1 is the instant ignition burst (longer, wider, brighter).
                if (_EyeFire > 0)
                {
                    float amt = _EyeFire;
                    float o = flip;                                   // +1: outer corner at +x of the quad, -1: mirrored eye
                    float Wd = _EyeSize;                              // eye width in quad units
                    float2 ec = float2((cb.x - 0.5) * Wd * o, (cb.y - 0.5) * Wd / scleraAspect);
                    float2 qc = float2(cs * ec.x - sn * ec.y, sn * ec.x + cs * ec.y);   // outer corner in quad space
                    float2 rel = float2(ex, q.y) - qc;
                    float tt = t + seed * 13.0 + (o < 0 ? 4.1 : 0.0);  // the two eyes differ

                    float fa = radians(_FireAngle);
                    float2 dir = float2(o * cos(fa), sin(fa));
                    float2 nrm = o * float2(-dir.y, dir.x);           // points up and back toward the head
                    float L = _FireLength * Wd * (0.3 + 0.7 * amt) * (1 + 0.2 * Noise3(float3(tt * 2.3, 0.5, seed * 3.0)));
                    float s = dot(rel, dir);
                    float w = dot(rel, nrm);
                    float sb = _FireBack * Wd;
                    float ss = saturate(s / L);

                    // the stream bends up/back and wags, more toward the tip
                    float bend = _FireBend * L * ss * ss
                               + Noise3(float3(s / Wd * 2.0 - tt * _FireSpeed * 0.5, 7.7, tt * 0.9)) * _FireWiggle * Wd * 0.45 * ss;
                    float prof = (s < 0) ? sqrt(saturate(1 - (s / sb) * (s / sb))) : pow(saturate(1 - ss), 0.6);
                    float hw = _FireWidth * Wd * lerp(0.4, 1.0, saturate(amt)) * (1 + 0.25 * max(amt - 1, 0)) * prof + 1e-4;
                    float F0 = 1 - abs(w - bend) / hw;

                    // tongues: noise stretched along the stream, racing outward
                    float3 np = float3(s / Wd * 3.0 - tt * _FireSpeed, (w - bend) / Wd * _FireStreak, tt * 1.3);
                    float nz = Noise3(np) + 0.45 * Noise3(np * float3(2.1, 1.7, 1.9) + 5.3);
                    // big forks: the stream splits into a few separate tongues that peel off
                    float3 fp = float3(s / Wd * 1.3 - tt * _FireSpeed * 0.6, (w - bend) / Wd * _FireStreak * 0.35, tt * 0.8 + 9.1);
                    nz += 0.8 * Noise3(fp) * smoothstep(0.1, 0.6, ss);
                    float F = F0 * (1 - 0.3 * ss) + nz * _FireRag * (0.4 + 0.9 * ss) - 0.08 - 0.35 * ss;
                    F = (s < -sb || s > L * 1.2) ? -1 : F;

                    float fwF = clamp(fwidth(F), 1e-4, 0.1);
                    float fbody = smoothstep(-fwF, fwF, F);                        // crisp flame edge
                    float fcore = smoothstep(0.22, 0.5, F);                        // white-hot core
                    float fglow = smoothstep(-0.5, 0.0, F) * (1 - fbody);          // soft glow around it
                    float bright = saturate(amt * 2) * (1 + 0.6 * max(amt - 1, 0));
                    float3 fc = (lerp(_FireColor.rgb, _FireCore.rgb, fcore) * fbody + _FireGlow.rgb * fglow * _FireGlowAmt) * bright;
                    fc *= 1 - 0.9 * saturate(pupil.a * body);                      // the pupil rings stay readable
                    col = max(col, fc) + _FireGlow.rgb * fglow * _FireGlowAmt * 0.25 * bright;
                }

                float pulse = 1 + _PulseAmount * sin(t * _PulseSpeed + seed * 6.2831);
                float fade = smoothstep(0.0, 0.12, open);
                col *= _Tint.rgb * _Intensity * pulse * fade;
                return float4(col, 0);
            }
            ENDCG
        }
    }
    FallBack Off
}
