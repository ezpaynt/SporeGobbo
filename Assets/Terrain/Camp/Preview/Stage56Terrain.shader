Shader "SporeGobbo/CampCandidate/Terrain"
{
 Properties{_Color("Color",Color)=(1,1,1,1) _RegionSize("Region dimensions",Vector)=(192,192,0,0) _CampSurface("Authored material identities",2D)="white" {} _Layer("Layer",Float)=0 _WallUpper("Dirt upper",Color)=(1,1,1,1) _WallLower("Dirt lower",Color)=(1,1,1,1) _Permanent("Permanent top",Color)=(1,1,1,1) _PermanentFace("Permanent face",Color)=(1,1,1,1) _Foot("Foot",Color)=(0,0,0,1)}
 SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"} Pass{Cull Back ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 #include "../../Geometry/Preview/Runtime/FocusProjectionGpu.hlsl"
 sampler2D _CampSurface;float4 _RegionSize;float4 _Color,_WallUpper,_WallLower,_Permanent,_PermanentFace,_Foot;float _Layer;
 struct A{float4 vertex:POSITION;float4 wall:TEXCOORD0;float4 role:TEXCOORD1;};struct V{float4 pos:SV_POSITION;float2 logical:TEXCOORD0;float depth:TEXCOORD1;};
 V vert(A a){V o;float2 p=SG_Resolve(a.vertex.xy,a.wall,a.role.x,a.role.yz);o.pos=mul(UNITY_MATRIX_VP,float4(p.x,-p.y,0,1));o.logical=a.vertex.xy;o.depth=abs(a.role.x-_SG_Roles.x)<.1?1:0;return o;}
 float4 frag(V i):SV_Target{float4 surface=tex2D(_CampSurface,(i.logical+_RegionSize.xy*.5)/_RegionSize.xy);float permanent=surface.r;float4 c=_Color;if(_Layer>.5&&_Layer<1.5)c=lerp(c,_Permanent,permanent);if(_Layer>1.5&&_Layer<2.5)c=lerp(lerp(_WallUpper,_PermanentFace,permanent),lerp(_WallLower,_Foot,permanent),i.depth);if(_Layer>.5&&_Layer<2.5&&surface.g>0&&permanent<.5)c.rgb=lerp(c.rgb,float3(.42,.27,.17),surface.g*.65);return c;}
 ENDHLSL
 }}
}
