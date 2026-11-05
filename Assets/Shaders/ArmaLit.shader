// Sombreado barato para armas y mesas en Quest 2: un solo pass, sin luces en tiempo real.
// La escena no tiene light probes y el ambiente es plano y oscuro, asi que un shader iluminado deja los objetos dinamicos casi negros.
// Aqui la "luz" es fija (clave arriba-atras + relleno + brillo especular tipo metal), con color y niebla de la escena.
Shader "Backrooms/ArmaLit"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.5, 0.5, 1)
        _Ambient ("Luz base", Range(0, 1)) = 0.50
        _Key ("Luz clave", Range(0, 1)) = 0.60
        _SpecColor ("Brillo (color)", Color) = (0.6, 0.6, 0.6, 1)
        _Shininess ("Brillo (dureza)", Range(2, 128)) = 32
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 nrm : TEXCOORD0;
                half3 vdir : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _SpecColor;
            half _Ambient;
            half _Key;
            half _Shininess;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.nrm = UnityObjectToWorldNormal(v.normal);
                float3 wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.vdir = normalize(_WorldSpaceCameraPos - wpos);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half3 n = normalize(i.nrm);
                half3 L = normalize(half3(0.35, 0.85, 0.40));     // clave: arriba y algo al frente
                half3 F = normalize(half3(-0.60, 0.15, -0.55));   // relleno suave del otro lado
                half key = saturate(dot(n, L));
                half fill = saturate(dot(n, F)) * 0.22;
                half3 col = _Color.rgb * (_Ambient + _Key * key + fill);
                half3 h = normalize(L + normalize(i.vdir));
                half spec = pow(saturate(dot(n, h)), _Shininess);
                col += _SpecColor.rgb * spec * key;
                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    Fallback "Mobile/Diffuse"
}
