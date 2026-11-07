Shader "Line4/Passenger Fabric" {
 SubShader {
  Tags { "RenderType"="Opaque" }
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   struct Input { float4 vertex:POSITION; float3 normal:NORMAL; fixed4 color:COLOR; };
   struct Varying { float4 pos:SV_POSITION; fixed4 color:COLOR; UNITY_FOG_COORDS(1) };
   Varying vert(Input v) {
    Varying o; o.pos=UnityObjectToClipPos(v.vertex);
    o.color=v.color*(.62+.38*saturate(dot(UnityObjectToWorldNormal(v.normal),normalize(float3(-.3,.8,.25)))));
    UNITY_TRANSFER_FOG(o,o.pos); return o;
   }
   fixed4 frag(Varying i):SV_Target { fixed4 c=i.color; UNITY_APPLY_FOG(i.fogCoord,c); return c; }
   ENDCG
  }
 }
}
