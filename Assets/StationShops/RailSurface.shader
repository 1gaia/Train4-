Shader "LineFour/Rail surface"
{
    Properties
    {
        _MainTex ("Aggregate or grain", 2D) = "white" {}
        _Density ("Repeats per metre", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        struct Input {float3 worldPos;};
        sampler2D _MainTex;
        float _Density;
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv=(IN.worldPos.xz+float2(.14,.31)*IN.worldPos.y)*_Density;
            o.Albedo=tex2D(_MainTex,uv).rgb;
            o.Smoothness=.12;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
