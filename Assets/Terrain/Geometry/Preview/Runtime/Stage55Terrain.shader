Shader "SporeGobbo/GeometryPreview/Stage55Terrain"
{
 Properties{_Color("Color",Color)=(1,1,1,1) _MainTex("Preview sprite",2D)="white" {} _UseTexture("Use sprite",Float)=0 _Layer("Semantic layer",Float)=0 _WallUpper("Wall upper",Color)=(1,1,1,1) _WallLower("Wall lower",Color)=(1,1,1,1) _Foot("Wall foot",Color)=(0,0,0,1) _Fracture("Fresh fracture",Color)=(1,1,1,1) _Variation("Restrained variation",Color)=(1,1,1,1)}
 SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"} Pass{
 Cull Back ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 #include "FocusProjectionGpu.hlsl"
 sampler2D _MainTex;float _UseTexture,_Layer;float4 _Color,_WallUpper,_WallLower,_Foot,_Fracture,_Variation;
 float4 _SG55_ContactBounds[5];float4 _SG55_StoneUpper,_SG55_StoneLower,_SG55_RootUpper,_SG55_RootLower,_SG55_BoneUpper,_SG55_BoneLower,_SG55_Ink;
 struct A{float4 vertex:POSITION;float4 wall:TEXCOORD0;float4 role:TEXCOORD1;float2 uv:TEXCOORD2;};
 struct V{float4 pos:SV_POSITION;float2 logical:TEXCOORD0;float depth:TEXCOORD1;float2 uv:TEXCOORD2;};
 V vert(A a){V o;float2 p=SG_Resolve(a.vertex.xy,a.wall,a.role.x,a.role.yz);o.pos=mul(UNITY_MATRIX_VP,float4(p.x,-p.y,0,1));o.logical=a.vertex.xy;o.depth=abs(a.role.x-_SG_Roles.x)<.1?1:0;o.uv=a.uv;return o;}
 float4 frag(V i):SV_Target{float4 c=_Color;float patch=frac(sin(dot(floor(i.logical*.33),float2(12.9898,78.233)))*43758.5453);if(_Layer<.5)c=lerp(c,_Variation,patch>.86?.12:0);else if(_Layer<1.5)c=lerp(c,_Variation,patch>.82?.16:0);else if(_Layer<2.5){float4 upper=_WallUpper,lower=_WallLower,foot=_Foot;for(int contact=0;contact<5;contact++){float4 b=_SG55_ContactBounds[contact];if(all(i.logical>=b.xy)&&all(i.logical<=b.zw)){upper=contact==0?_SG55_StoneUpper:contact==4?_SG55_BoneUpper:_SG55_RootUpper;lower=contact==0?_SG55_StoneLower:contact==4?_SG55_BoneLower:_SG55_RootLower;foot=_SG55_Ink;}}c=lerp(upper,lower,saturate(i.depth*1.4));c=lerp(c,foot,smoothstep(.78,1,i.depth));}else if(_Layer>3.5&&_Layer<4.5){float along=frac(i.logical.x*.63+i.logical.y*.79);if(along<.18&&patch>.35)c=_Fracture;}return c*(_UseTexture>.5?tex2D(_MainTex,i.uv):float4(1,1,1,1));}
 ENDHLSL
 }}
}
