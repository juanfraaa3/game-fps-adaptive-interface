Shader "Unlit/PeripheralEdgeMask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _InnerRadius ("Inner Radius", Range(0,1)) = 0.35
        _Feather ("Feather", Range(0.001,0.5)) = 0.18
        _Opacity ("Opacity", Range(0,1)) = 0.85
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _InnerRadius;
            float _Feather;
            float _Opacity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Distancia desde el centro de pantalla
                float2 centered = i.uv - float2(0.5, 0.5);
                float d = length(centered); // 0 centro, ~0.707 esquinas

                // Alpha = 0 en el centro, 1 en bordes (con transición suave)
                float edge = smoothstep(_InnerRadius, _InnerRadius + _Feather, d);

                fixed4 col = tex2D(_MainTex, i.uv);
                col.a = edge * _Opacity;
                return col;
            }
            ENDCG
        }
    }
}
