Shader "SporeGobbo/GeometryPreview/Flat"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Back
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct Input { float4 vertex : POSITION; };
            float4 vert(Input value) : SV_POSITION { return UnityObjectToClipPos(value.vertex); }
            fixed4 frag() : SV_Target { return _Color; }
            ENDCG
        }
    }
}
