// Holograma "realista" para el rostro de ASTRA: conserva el detalle de la textura (ojos, cejas,
// labios, pelo) como luminancia, con tinte azul, luz suave desde la cámara, borde luminoso (fresnel)
// y líneas de escaneo. Primer pase: profundidad (así la cara se ve sólida y sin interiores).
Shader "SpaceApps/HoloAvatar"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Tinte", Color) = (0.35, 0.72, 1, 1)
        _Rim ("Borde", Color) = (0.55, 0.88, 1, 1)
        _Detail ("Detalle de textura", Range(0, 2)) = 1.1
        _Alpha ("Opacidad", Range(0, 1)) = 0.94
        _Cutoff ("Recorte alfa", Range(0, 1)) = 0.02
        _Scan ("Líneas de escaneo", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex; float4 _MainTex_ST;
        float4 _Color, _Rim;
        float _Detail, _Alpha, _Cutoff, _Scan;
        struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 wp : TEXCOORD2; };
        v2f vert (appdata v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            o.n = UnityObjectToWorldNormal(v.normal);
            o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            return o;
        }
        ENDCG

        Pass
        {
            ZWrite On ColorMask 0 Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            fixed4 fragDepth (v2f i) : SV_Target { clip(tex2D(_MainTex, i.uv).a - max(_Cutoff, 0.35)); return 0; }
            ENDCG
        }

        Pass
        {
            ZWrite Off ZTest LEqual Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                clip(t.a - _Cutoff);
                float luma = dot(t.rgb, float3(0.30, 0.59, 0.11));
                float3 n = normalize(i.n);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = saturate(dot(n, v));
                float fres = pow(1.0 - ndv, 2.4);
                float3 l = normalize(v + float3(0.35, 0.55, 0.0));
                float light = 0.45 + 0.55 * saturate(dot(n, l));
                float3 col = _Color.rgb * (0.22 + _Detail * luma * 1.25) * light + _Rim.rgb * fres * 1.3;
                float scan = 0.86 + 0.14 * sin(i.pos.y * 1.7 + _Time.y * 9.0);
                col *= lerp(1.0, scan, _Scan);
                col *= 0.97 + 0.03 * sin(_Time.y * 41.0);
                float a = _Alpha * saturate(0.6 + fres + luma * 0.25) * t.a;
                return fixed4(col, a);
            }
            ENDCG
        }
    }
}
