Shader "SporeGobbo/GeometryPreview/Stage48TerrainArt"
{
    Properties { _Color ("Color",Color)=(1,1,1,1) _MainTex ("Preview sprite",2D)="white" {} _UseTexture("Use sprite",Float)=0 }
    HLSLINCLUDE
    #include "UnityCG.cginc"
    #include "FocusProjectionGpu.hlsl"
    sampler2D _MainTex;float4 _Color;float _UseTexture;
    struct Attributes {float4 vertex:POSITION;float4 wall:TEXCOORD0;float4 roleOffset:TEXCOORD1;float2 uv:TEXCOORD2;};
    struct Varyings {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
    Varyings vert(Attributes input)
    {
        Varyings o;
        float3 render=input.vertex.xyz;
        if(_SG_Options.z>.5){float2 p=SG_Resolve(input.vertex.xy,input.wall,input.roleOffset.x,input.roleOffset.yz);render=float3(p.x,-p.y,0);}
        o.position=mul(UNITY_MATRIX_VP,float4(render,1));o.uv=input.uv;return o;
    }
    float4 frag(Varyings i):SV_Target {return _UseTexture>.5?tex2D(_MainTex,i.uv):_Color;}
    ENDHLSL
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Back ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Cull Back ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
