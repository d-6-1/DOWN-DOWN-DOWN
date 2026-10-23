// DogToon.shader —— 小狗用的三渲二（卡通）着色器
// 一个 Pass，内置渲染管线和 URP 都能用。
// 普通零件：两种颜色的明暗（亮面 + 暗面），面和面之间是硬边，看起来像低多边形。
// 描边零件：Outline Width > 0 且 Cull = Front，沿法线往外撑一圈，只显示纯色 → 黑色描边。
// 光的方向由 DogScoutBuilder 从场景里的 Directional Light 读取（全局变量 _DogToonLightDir）。

Shader "DogScout/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tint (暗面颜色倍数)", Color) = (0.72, 0.62, 0.68, 1)
        _ShadowThreshold ("Shadow Threshold (明暗分界)", Range(-1, 1)) = 0.05
        _ShadowSoftness ("Shadow Softness (分界软硬)", Range(0.001, 0.5)) = 0.01
        _OutlineWidth ("Outline Width (描边粗细，普通零件为 0)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _ShadowColor;
            float _ShadowThreshold;
            float _ShadowSoftness;
            float _OutlineWidth;
            float4 _DogToonLightDir;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 n = UnityObjectToWorldNormal(v.normal);
                float3 p = mul(unity_ObjectToWorld, v.vertex).xyz + n * _OutlineWidth;
                o.pos = mul(UNITY_MATRIX_VP, float4(p, 1));
                o.normalWS = n;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = _Color;
                if (_OutlineWidth <= 0)
                {
                    float3 L = _DogToonLightDir.xyz;
                    if (dot(L, L) < 0.0001) L = float3(0.4, 0.8, -0.45);
                    L = normalize(L);
                    float ndl = dot(normalize(i.normalWS), L);
                    float lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl);
                    c.rgb = lerp(c.rgb * _ShadowColor.rgb, c.rgb, lit);
                }
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }

    // 借用内置着色器的 ShadowCaster，让小狗能在雪地上投影子
    Fallback "Legacy Shaders/VertexLit"
}
