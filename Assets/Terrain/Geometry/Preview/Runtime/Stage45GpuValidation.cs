using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
    public static class Stage45GpuValidation
    {
        [StructLayout(LayoutKind.Sequential)] public struct Sample
        {
            public Vector4 logicalRole,wall,offset;
            public Sample(Point2 logical,float role,WallSample? wallSample=null,Point2? actorOffset=null)
            {logicalRole=new Vector4((float)logical.X,(float)logical.Y,role,0);wall=wallSample.HasValue?new Vector4((float)wallSample.Value.SolidNormal.X,(float)wallSample.Value.SolidNormal.Y,(float)wallSample.Value.BackWeight,(float)wallSample.Value.FrontWeight):Vector4.zero;offset=actorOffset.HasValue?new Vector4((float)actorOffset.Value.X,(float)actorOffset.Value.Y,0,0):Vector4.zero;}
        }
        public static Vector2[] Evaluate(ComputeShader shader,FocusProjection projection,Stage4Settings settings,Sample[] samples)
        {
            if(shader==null||!SystemInfo.supportsComputeShaders)throw new InvalidOperationException("GPU compute parity probe unavailable.");
            using(var input=new ComputeBuffer(samples.Length,48))using(var output=new ComputeBuffer(samples.Length,8))
            {
                input.SetData(samples);ProjectionShaderContract.Apply(projection,settings,true,shader);int kernel=shader.FindKernel("Evaluate");shader.SetInt("SampleCount",samples.Length);shader.SetBuffer(kernel,"Input",input);shader.SetBuffer(kernel,"Output",output);shader.Dispatch(kernel,(samples.Length+63)/64,1,1);var values=new Vector2[samples.Length];output.GetData(values);return values;
            }
        }
        public static Point2 Cpu(FocusProjection projection,Stage4Settings settings,Sample value)
        {
            var logical=new Point2(value.logicalRole.x,value.logicalRole.y);int role=(int)value.logicalRole.z;
            if(role==8){var p=projection.Project(logical);var s=Stage4CameraPreview.ActorScale(projection,logical,settings.actorMode);return new Point2(p.X+value.offset.x*s.x,p.Y+value.offset.y*s.y);}
            if(role==0)return projection.Project(logical);
            // Build from original supplied weights: tests generate the same CPU WallSample before conversion.
            var wall=new WallSample(logical,new Point2(value.wall.x,value.wall.y));
            var vertex=WallGeometry.Project(wall,projection,new WallSettings(settings.stage3Comparison?2.15:settings.wallHeight,.22),settings.stage3Comparison?WallHeightPolicy.ReferenceLocalScale:settings.heightPolicy);
            return role==(int)GeometryVertexRole.WallBottom?vertex.Bottom:role==(int)GeometryVertexRole.ForegroundLip?vertex.Lip:vertex.Top;
        }
        public static double Probe(ComputeShader shader)
        {
            double worst=0;
            foreach(int policy in new[]{0,1})foreach(int actorMode in new[]{0,1})
            {
                var settings=new Stage4Settings{side=.15f,top=.1f,foreground=2.5f,wallHeight=4,heightPolicy=(WallHeightPolicy)policy,actorMode=actorMode};
                var projection=new FocusProjection(new Point2(49.3725,38.81625),48,28,7,.15,.1,2.5,new Point2(0,3.271));
                var samples=new Sample[512];
                for(int i=0;i<samples.Length;i++){var point=new Point2((i*17)%105+.125,(i*31)%75+.375);int role=i%7==0?8:i%5;var normal=new Point2(Math.Cos(i*.7),Math.Sin(i*.7));samples[i]=new Sample(point,role,new WallSample(point,normal),new Point2(.65,-1.3));}
                var actual=Evaluate(shader,projection,settings,samples);
                for(int i=0;i<samples.Length;i++){var expected=Cpu(projection,settings,samples[i]);worst=Math.Max(worst,Math.Max(Math.Abs(actual[i].x-expected.X),Math.Abs(actual[i].y-expected.Y)));}
            }
            return worst;
        }
    }
}
