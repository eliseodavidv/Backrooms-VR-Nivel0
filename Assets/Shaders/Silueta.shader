// Silueta negra plana para el menu: sin luces, sin niebla, con profundidad normal (las paredes la tapan).
// Una sola pasada, casi sin costo. Se ve solo como una mancha mas oscura que el fondo.
Shader "Backrooms/Silueta"
{
    Properties
    {
        _Color ("Color", Color) = (0.0, 0.0, 0.0, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+5" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, 1);
            }
            ENDCG
        }
    }
}
