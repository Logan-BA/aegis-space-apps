// Holograma aditivo para el rostro de ASTRA: color por vértice, brillo de borde (fresnel)
// y líneas de barrido que se desplazan. _Fresnel = 0 para líneas y puntos (sin normales).
Shader "SpaceApps/Holograma"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.8, 1, 1)
        _Intensity ("Intensidad", Float) = 1
        _Fresnel ("Fresnel", Float) = 0
        _Scan ("Barrido", Float) = 0.15
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _Intensity, _Fresnel, _Scan;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 col : COLOR; float3 n : TEXCOORD0; float3 v : TEXCOORD1; float y : TEXCOORD2; };

            v2f vert (appdata i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.col = i.color;
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = normalize(WorldSpaceViewDir(i.vertex));
                o.y = i.vertex.y;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float f = 1;
                if (_Fresnel > 0)
                    f = pow(1 - saturate(abs(dot(normalize(i.n), i.v))), _Fresnel) * 1.4 + 0.08;
                float scan = 1 - _Scan + _Scan * sin(i.y * 140 - _Time.y * 5);
                return _Color * i.col * _Intensity * f * scan;
            }
            ENDCG
        }
    }
}
