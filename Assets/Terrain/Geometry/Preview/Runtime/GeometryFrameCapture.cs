using System;
using Unity.Profiling;
using UnityEngine;

namespace SporeGobbo.Geometry.Preview
{
    [Serializable]
    public struct GeometryFrameSample
    {
        public double intervalMs,mainThreadMs,gcMs;
        public long allocatedBytes,drawCalls,presentNs;
        public int changedCells,patches,tileVisits,uploads,gc0,gc1,gc2;
        public bool successfulDig;
        public double[] phases,nativePhases;
    }

    /// <summary>Captures completed gameplay frames before the next preview Update. Arrays are preallocated.
    /// Native recorder samples describe the previous engine frame; phase clocks include its IMGUI callbacks.</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class GeometryFrameCapture : MonoBehaviour
    {
        public GeometryFrameSample[] Samples {get;private set;}
        public int Count {get;private set;}
        public bool AllocationCounterValidated {get;private set;}
        public bool MainThreadAvailable=>mainThread.Valid;
        public bool GCMarkerAvailable=>gc.Valid;
        public bool DrawCounterAvailable=>draws.Valid;
        public bool PresentMarkerAvailable=>present.Valid;
        ProfilerRecorder allocation,mainThread,gc,draws,present;
        readonly ProfilerRecorder[] phaseRecorders=new ProfilerRecorder[(int)GeometryPhase.Count];
        PlayableCavePreview preview;
        bool running,hasPrevious;
        int digBefore,previousFrame,c0,c1,c2;
        double previousTime;

        void OnEnable()
        {
            NativeGeometryProfiling.Enable();
            for(int i=0;i<phaseRecorders.Length;i++)phaseRecorders[i]=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"SporeGobbo.Geometry."+(GeometryPhase)i,1);
            allocation=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            mainThread=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);
            gc=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"GC.Collect",1);
            draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);
            present=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Gfx.PresentFrame",1);
        }
        public void Begin(PlayableCavePreview target,int frameCount)
        {
            preview=target;Samples=new GeometryFrameSample[frameCount];Count=0;
            for(int i=0;i<frameCount;i++){Samples[i].phases=new double[(int)GeometryPhase.Count];Samples[i].nativePhases=new double[(int)GeometryPhase.Count];}
            running=true;hasPrevious=false;NativeGeometryProfiling.CollectTimings=true;
        }
        void Update()
        {
            if(allocation.Valid&&allocation.Count>0&&allocation.LastValue>=65536)AllocationCounterValidated=true;
            if(!running)return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(hasPrevious&&Time.frameCount!=previousFrame)
            {
                ref var sample=ref Samples[Count];
                sample.intervalMs=(now-previousTime)*1000;
                sample.mainThreadMs=Read(mainThread)/1e6;sample.gcMs=Read(gc)/1e6;
                sample.allocatedBytes=AllocationCounterValidated?Read(allocation):-1;
                sample.drawCalls=Read(draws);sample.presentNs=Read(present);
                sample.successfulDig=preview.Session.DigCount!=digBefore;
                sample.changedCells=sample.successfulDig?preview.Session.Changed.Count:0;
                sample.patches=sample.successfulDig?preview.Session.LastPatchCount:0;
                sample.tileVisits=0;
                if(sample.successfulDig)foreach(var key in preview.Session.LastDirty)
                    sample.tileVisits+=Math.Min(preview.Session.PatchSize,preview.Session.Terrain.Width-1-key.X)*Math.Min(preview.Session.PatchSize,preview.Session.Terrain.Height-1-key.Y);
                sample.uploads=NativeGeometryProfiling.Calls[(int)GeometryPhase.MeshUpload];
                sample.gc0=GC.CollectionCount(0)-c0;sample.gc1=GC.CollectionCount(1)-c1;sample.gc2=GC.CollectionCount(2)-c2;
                for(int i=0;i<phaseRecorders.Length;i++)sample.nativePhases[i]=Read(phaseRecorders[i])/1e6;
                Array.Copy(NativeGeometryProfiling.Milliseconds,sample.phases,sample.phases.Length);
                Count++;
                if(Count==Samples.Length){running=false;NativeGeometryProfiling.CollectTimings=false;return;}
            }
            NativeGeometryProfiling.ResetFrame();digBefore=preview.Session.DigCount;
            c0=GC.CollectionCount(0);c1=GC.CollectionCount(1);c2=GC.CollectionCount(2);
            previousTime=now;previousFrame=Time.frameCount;hasPrevious=true;
        }
        static long Read(ProfilerRecorder recorder)=>recorder.Valid&&recorder.Count>0?recorder.LastValue:-1;
        void OnDisable()
        {
            running=false;NativeGeometryProfiling.CollectTimings=false;
            for(int i=0;i<phaseRecorders.Length;i++)phaseRecorders[i].Dispose();
            allocation.Dispose();mainThread.Dispose();gc.Dispose();draws.Dispose();present.Dispose();
        }
    }
}
