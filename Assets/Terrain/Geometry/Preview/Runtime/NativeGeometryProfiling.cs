using System;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace SporeGobbo.Geometry.Preview
{
    /// <summary>Native Unity markers, plus optional allocation-free elapsed counters for automated Play captures.
    /// Durations are inclusive: e.g. Contour includes WallFilter; MeshBuild includes both.</summary>
    public sealed class NativeGeometryProfiling : IGeometryTrace
    {
        static readonly NativeGeometryProfiling instance=new NativeGeometryProfiling();
        readonly ProfilerMarker[] markers;
        readonly long[] starts=new long[(int)GeometryPhase.Count];
        public static readonly double[] Milliseconds=new double[(int)GeometryPhase.Count];
        public static readonly int[] Calls=new int[(int)GeometryPhase.Count];
        public static bool CollectTimings;

        NativeGeometryProfiling()
        {
            markers=new ProfilerMarker[(int)GeometryPhase.Count];
            for(int i=0;i<markers.Length;i++)markers[i]=new ProfilerMarker("SporeGobbo.Geometry."+(GeometryPhase)i);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Enable()=>GeometryTrace.Sink=instance;
        public static void ResetFrame(){Array.Clear(Milliseconds,0,Milliseconds.Length);Array.Clear(Calls,0,Calls.Length);}
        public void Begin(GeometryPhase phase)
        {
            int index=(int)phase;markers[index].Begin();
            if(CollectTimings)starts[index]=Stopwatch.GetTimestamp();
        }
        public void End(GeometryPhase phase)
        {
            int index=(int)phase;
            if(CollectTimings){Milliseconds[index]+=(Stopwatch.GetTimestamp()-starts[index])*1000.0/Stopwatch.Frequency;Calls[index]++;}
            markers[index].End();
        }
    }
}
