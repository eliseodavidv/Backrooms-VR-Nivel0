// Borde rojo de persecucion: quad pegado a la camara, alfa radial (transparente al centro, rojo en el borde).
Shader "Backrooms/BordeRojo"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 0.04, 0.03, 1)
        _Intensidad ("Intensidad", Range(0, 1)) = 0
        _Inicio ("Inicio del borde", Range(0, 1)) = 0.35
        _Fin ("Fin del borde", Range(0, 1.5)) = 1.0
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _Intensidad;
            float _Inicio;
            float _Fin;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length((i.uv - 0.5) * 2.0);
                float a = smoothstep(_Inicio, _Fin, r) * _Intensidad;
                return fixed4(_Color.rgb, a * _Color.a);
            }
            ENDCG
        }
    }
}
