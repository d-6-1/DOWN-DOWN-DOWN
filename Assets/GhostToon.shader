// 三渲二（卡通）着色：分段明暗 + 边缘光 + 描边。给 low poly 幽灵用。
// 内置渲染管线（Standard 那套）可用。
Shader "DownDown/ToonGhost"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.08, 0.08, 0.10, 1)
        _ShadowColor ("Shadow Color", Color) = (0.02, 0.02, 0.03, 1)
        _HighlightColor ("Highlight Color", Color) = (0.28, 0.30, 0.36, 1)
        _HighlightThreshold ("Highlight Threshold", Range(-1, 1)) = 0.55
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = -0.05
        _RimColor ("Rim Color", Color) = (0.45, 0.55, 0.75, 1)
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.72
        _Unlit ("Unlit (eyes = 1)", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 0.015
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        // ---------- 描边：把模型沿法线放大一点，只画背面 ----------
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            fixed4 _Color;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;   // GhostFluid 把平滑法线放在这里，描边不会断开
            };

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                float3 n = dot(v.tangent.xyz, v.tangent.xyz) > 0.25 ? v.tangent.xyz : v.normal;
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex.xyz + normalize(n) * _OutlineWidth);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_OutlineColor.rgb, _OutlineColor.a * _Color.a);
            }
            ENDCG
        }

        // ---------- 主体：三段明暗 + 边缘光 ----------
        Pass
        {
            Name "TOON"
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _ShadowColor, _HighlightColor, _RimColor;
            float _HighlightThreshold, _ShadowThreshold, _RimThreshold, _Unlit;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wn : TEXCOORD0;
                float3 wpos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_Unlit > 0.5) return _Color;

                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = dot(n, l);

                fixed3 c = ndl > _HighlightThreshold ? _HighlightColor.rgb
                         : (ndl > _ShadowThreshold ? _Color.rgb : _ShadowColor.rgb);

                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float rim = 1.0 - saturate(dot(n, v));
                if (rim > _RimThreshold) c = lerp(c, _RimColor.rgb, 0.8);

                return fixed4(c, _Color.a);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
