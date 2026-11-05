// Armas y criaturas con texturas (Base Color + Normal + Metallic/Smoothness) para Quest 2, Built-in, un solo pass, sin luces en tiempo real.
// Como la escena no tiene light probes ni skybox, el shader Standard deja los metales casi negros (no tienen que reflejar).
// Aqui la luz es fija (clave + relleno), el metal refleja un degradado de "ambiente" falso, y hay brillo especular segun smoothness.
// _MetallicGlossMap es opcional: sin el, se usan _Metallic y _Glossiness constantes. _BumpMap es opcional (UNITY normal map).
Shader "Backrooms/ArmaLitTex"
{
    Properties
    {
        _Color ("Tinte", Color) = (1, 1, 1, 1)
        _MainTex ("Base Color", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Fuerza normal", Range(0, 2)) = 1
        _MetallicGlossMap ("Metallic (R) / Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic (si no hay mapa)", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.6
        _Ambient ("Luz base", Range(0, 1.5)) = 0.55
        _Key ("Luz clave", Range(0, 1.5)) = 0.65
        _EnvSky ("Ambiente falso (arriba)", Color) = (0.62, 0.58, 0.42, 1)
        _EnvGround ("Ambiente falso (abajo)", Color) = (0.14, 0.12, 0.08, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BumpMap;
            sampler2D _MetallicGlossMap;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _EnvSky;
            fixed4 _EnvGround;
            half _BumpScale;
            half _Metallic;
            half _Glossiness;
            half _Ambient;
            half _Key;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 vdir : TEXCOORD1;
                half3 tspace0 : TEXCOORD2;
                half3 tspace1 : TEXCOORD3;
                half3 tspace2 : TEXCOORD4;
                UNITY_FOG_COORDS(5)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.vdir = normalize(_WorldSpaceCameraPos - wpos);
                half3 wn = UnityObjectToWorldNormal(v.normal);
                half3 wt = UnityObjectToWorldDir(v.tangent.xyz);
                half3 wb = cross(wn, wt) * v.tangent.w * unity_WorldTransformParams.w;
                o.tspace0 = half3(wt.x, wb.x, wn.x);
                o.tspace1 = half3(wt.y, wb.y, wn.y);
                o.tspace2 = half3(wt.z, wb.z, wn.z);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 albedoT = tex2D(_MainTex, i.uv) * _Color;
                half3 tn = UnpackNormal(tex2D(_BumpMap, i.uv));
                tn.xy *= _BumpScale;
                tn = normalize(tn);
                half3 n;
                n.x = dot(i.tspace0, tn);
                n.y = dot(i.tspace1, tn);
                n.z = dot(i.tspace2, tn);
                n = normalize(n);

                fixed4 mg = tex2D(_MetallicGlossMap, i.uv);
                half metal = _Metallic * mg.r;      // con mapa: _Metallic = 1; sin mapa (blanco): _Metallic constante
                half smooth = _Glossiness * mg.a;

                half3 L = normalize(half3(0.35, 0.85, 0.40));
                half3 F = normalize(half3(-0.60, 0.15, -0.55));
                half key = saturate(dot(n, L));
                half fill = saturate(dot(n, F)) * 0.22;
                half3 albedo = albedoT.rgb;
                half3 diffuse = albedo * (1 - metal * 0.75) * (_Ambient + _Key * key + fill);

                // Reflejo falso: degradado cielo/suelo segun la normal; los metales lo toman con su color.
                half3 env = lerp(_EnvGround.rgb, _EnvSky.rgb, n.y * 0.5 + 0.5);
                half3 viewDir = normalize(i.vdir);
                half fres = pow(1 - saturate(dot(n, viewDir)), 3);
                half3 specCol = lerp(half3(0.04, 0.04, 0.04), albedo, metal);
                half3 refl = env * specCol * (0.35 + smooth * 0.9) * (0.55 + fres);

                half3 h = normalize(L + viewDir);
                half shin = exp2(smooth * 9 + 2);
                half spec = pow(saturate(dot(n, h)), shin) * smooth * key;
                half3 col = diffuse + refl * (0.4 + 0.6 * metal) + specCol * spec * 1.2;

                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    Fallback "Mobile/Diffuse"
}
