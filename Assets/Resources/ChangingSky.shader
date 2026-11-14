Shader "LineFour/Changing Sky"
{
 Properties { _Day("Day",Range(0,1))=1 _Dusk("Dusk",Range(0,1))=0 _Cloud("Cloud",Range(0,1))=0 _Night("Night",Range(0,1))=0 }
 SubShader {
 Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
 Cull Off ZWrite Off
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct app {float4 vertex:POSITION;};struct vary {float4 pos:SV_POSITION;float3 ray:TEXCOORD0;};
 float _Day,_Dusk,_Cloud,_Night;
 vary vert(app v){vary o;o.pos=UnityObjectToClipPos(v.vertex);o.ray=v.vertex.xyz;return o;}
 float hash(float2 v){return frac(sin(dot(v,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 fixed4 frag(vary i):SV_Target {
  float3 d=normalize(i.ray);float h=saturate(d.y);
  float3 top=lerp(float3(.012,.019,.055),float3(.16,.40,.72),_Day);
  float3 horizon=lerp(float3(.07,.075,.14),float3(.65,.78,.85),_Day);
  horizon=lerp(horizon,float3(.97,.32,.10),_Dusk*.82);
  float3 color=lerp(horizon,top,pow(h,.45));
  float2 uv=d.xz/max(.13,d.y)*.44+float2(_Time.y*.003,0);
  float n=noise(uv*2)*.58+noise(uv*4.3)*.28+noise(uv*9)*.14;
  float cloud=smoothstep(.62-_Cloud*.31,.86-_Cloud*.36,n)*smoothstep(0,.2,d.y);
  color=lerp(color,lerp(float3(.12,.14,.19),float3(.85,.86,.87),_Day)*(1-.35*_Cloud),cloud);
  float star=step(.9978,hash(floor(d.xz/max(.1,abs(d.y))*750)))*_Night*smoothstep(.08,.3,d.y)*(1-cloud);
  color+=star*.6;
  return float4(color,1);
 }
 ENDCG
 }
 }
}
