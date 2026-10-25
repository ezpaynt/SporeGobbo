// Stage 4.5 shared GPU kernel: vertex renderer and compute parity probe include this exact file.
// Coefficients and all region parameters are supplied by FocusProjection's CPU contract.
float4 _SG_FocusOffset; // focus.xy, offset.xy
float4 _SG_CenterDistance; // half-width, half-height, side/top distance, foreground distance
float4 _SG_CurveShape; // cubic-to-quintic transition blend; 0 retains exact reference
float4 _SG_AxisDistances; // independent separable X/top/foreground transition lengths
float4 _SG_Scales; // side, top, foreground, wall height
float4 _SG_Polynomial; // smooth quadratic/cubic, integral cubic/quartic
float4 _SG_Options; // wall policy, actor mode, GPU backend flag, lip width
float4 _SG_Roles; // wall bottom, lip, actor, unused
float SG_Axis(float value,float halfSize,float negative,float positive,float distance)
{
    float u=abs(value)-halfSize;if(u<=0)return value;
    float t=saturate(u/distance);
    float integral=distance*(t*t*t*(_SG_Polynomial.z+_SG_Polynomial.w*t));
    if(_SG_CurveShape.x>0)integral=lerp(integral,distance*(2.5*t*t*t*t-3*t*t*t*t*t+t*t*t*t*t*t),_SG_CurveShape.x);
    float a=abs(value)+((value<0?negative:positive)-1)*(integral+max(0,u-distance));
    return value<0?-a:a;
}
float SG_Scale(float value,float halfSize,float negative,float positive,float distance)
{
    float t=saturate((abs(value)-halfSize)/distance);
    float curve=t*t*(_SG_Polynomial.x+_SG_Polynomial.y*t);
    if(_SG_CurveShape.x>0)curve=lerp(curve,t*t*t*(10+t*(-15+6*t)),_SG_CurveShape.x);
    return 1+((value<0?negative:positive)-1)*curve;
}
float2 SG_Project(float2 logical)
{
    float2 d=logical-_SG_FocusOffset.xy;
    return float2(SG_Axis(d.x,_SG_CenterDistance.x,_SG_Scales.x,_SG_Scales.x,_SG_AxisDistances.x),SG_Axis(d.y,_SG_CenterDistance.y,_SG_Scales.y,_SG_Scales.z,d.y>=0?_SG_AxisDistances.z:_SG_AxisDistances.y))+_SG_FocusOffset.zw;
}
float2 SG_LocalScale(float2 logical)
{
    float2 d=logical-_SG_FocusOffset.xy;
    return float2(SG_Scale(d.x,_SG_CenterDistance.x,_SG_Scales.x,_SG_Scales.x,_SG_AxisDistances.x),SG_Scale(d.y,_SG_CenterDistance.y,_SG_Scales.y,_SG_Scales.z,d.y>=0?_SG_AxisDistances.z:_SG_AxisDistances.y));
}
float2 SG_Resolve(float2 logical,float4 normalWeights,float role,float2 actorOffset)
{
    float2 top=SG_Project(logical);
    if(role<.5)return top;
    float2 scale=SG_LocalScale(logical);
    if(abs(role-_SG_Roles.z)<.1)
    {
        float2 actorScale=_SG_Options.y<.5?sqrt(scale.x*scale.y).xx:scale;
        return top+actorOffset*actorScale;
    }
    if(abs(role-_SG_Roles.x)<.1)
        return _SG_Options.x<.5?top+float2(0,_SG_Scales.w*normalWeights.z*scale.y):SG_Project(logical+float2(0,_SG_Scales.w*normalWeights.z));
    if(abs(role-_SG_Roles.y)<.1)
    {
        float2 direction=-normalWeights.xy*scale;float magnitude=length(direction);
        return top+direction/(magnitude>0?magnitude:1)*(_SG_Options.w*normalWeights.w*scale.y);
    }
    return top;
}
