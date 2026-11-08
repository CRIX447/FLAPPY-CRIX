// The screen effects: glow (bloom), colour split, vignette, scanlines, film grain, the death
// flash and fade to grey. Built-in render pipeline (OnRenderImage), works on every platform.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).
Shader "Hidden/FlappyCrix/Post"
{
    Properties { _MainTex ("", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold, _Bloom, _Chroma, _Vignette, _Grain, _Scan, _Seed, _Desat, _Flash, _Glitch;
    float4 _FlashColor;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    half3 Box (float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                  tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25h;
    }

    half4 fragPrefilter (v2f i) : SV_Target
    {
        half3 c = Box(i.uv, 1);
        half b = max(c.r, max(c.g, c.b));
        half k = max(0, b - _Threshold) / max(b, 0.0001h);
        return half4(c * k, 1);
    }
    half4 fragDown (v2f i) : SV_Target { return half4(Box(i.uv, 1), 1); }
    half4 fragUp (v2f i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }

    float Hash (float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

    half4 fragFinal (v2f i) : SV_Target
    {
        float2 uv = i.uv;
        // glitch: a few horizontal bands slide sideways
        if (_Glitch > 0)
        {
            float band = floor(uv.y * 24 + _Seed * 7);
            float h = Hash(float2(band, floor(_Seed * 13)));
            if (h > 1 - _Glitch * 0.6) uv.x += (Hash(float2(band, _Seed)) - 0.5) * 0.08 * _Glitch;
        }
        float2 d = uv - 0.5;
        float2 off = d * (_Chroma + _Glitch * 2) * 0.012;
        half3 c;
        c.r = tex2D(_MainTex, uv + off).r;
        c.g = tex2D(_MainTex, uv).g;
        c.b = tex2D(_MainTex, uv - off).b;
        c += tex2D(_BloomTex, i.uv).rgb * _Bloom;
        half l = dot(c, half3(0.299, 0.587, 0.114));
        c = lerp(c, l.xxx, _Desat);
        c = saturate((c - 0.5) * 1.06 + 0.5);
        float v = 1 - dot(d, d) * _Vignette * 2.4;
        c *= saturate(v);
        c *= 1 - _Scan * (0.5 + 0.5 * sin(i.uv.y * _ScreenParams.y * 3.14159));
        c += (Hash(i.uv * _ScreenParams.xy + _Seed * 100) - 0.5) * _Grain;
        c = lerp(c, _FlashColor.rgb, _Flash);
        return half4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragPrefilter
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragDown
               ENDCG }
        Pass { Blend One One
               CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragUp
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragFinal
               ENDCG }
    }
}
