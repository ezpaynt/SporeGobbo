Shader "SporeGobbo/GeometryPreview/Stage55Structure"
{
 Properties{_Color("Tint",Color)=(1,1,1,1) _Alpha("Cutaway",Float)=1 _Legacy("Reference projection",Float)=0}
 SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"} Pass{
 Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 #include "FocusProjectionGpu.hlsl"
 float4 _Color;float _Alpha,_Legacy;
 struct A{float4 vertex:POSITION;float2 height:TEXCOORD0;float4 color:COLOR;};
 struct V{float4 position:SV_POSITION;float4 color:COLOR;};
 V vert(A a){V o;float2 s=SG_LocalScale(a.vertex.xy);float2 p=SG_Project(a.vertex.xy);p.y-=a.height.x*sqrt(s.x*s.y);if(_Legacy>.5)p=SG_Project(a.vertex.xy-float2(0,a.height.x));o.position=mul(UNITY_MATRIX_VP,float4(p.x,-p.y,0,1));o.color=a.color*_Color;o.color.a*=_Alpha;return o;}
 float4 frag(V v):SV_Target{return v.color;}
 ENDHLSL
 }}
}
