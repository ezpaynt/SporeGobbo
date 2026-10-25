using System;
using UnityEngine;
using Unity.Profiling;
namespace SporeGobbo.Geometry.Preview
{
    [Serializable] public struct Stage4Frame
    {
        public double mainMs,intervalMs,projectionMs,uploadMs,gcMs;
        public long allocation,presentNs,occupancyQueries;
        public int vertices,patches,uploads,filterBuilds,digs,gc0,gc1,gc2;
        public double[] phases;
    }
    [DefaultExecutionOrder(-32000)] public sealed class Stage4FrameCapture:MonoBehaviour
    {
        public Stage4Frame[] Frames;public int Count;public bool AllocationValidated;
        public Stage4CameraPreview view;PlayableCavePreview live;
        ProfilerRecorder main,allocation,gc,present;long queries;int filters,digs,g0,g1,g2;double time;bool running,previous;
        void OnEnable(){NativeGeometryProfiling.Enable();main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);allocation=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);gc=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"GC.Collect",1);present=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Gfx.PresentFrame",1);}
        public void Begin(int count){live=view.GetComponent<PlayableCavePreview>();Frames=new Stage4Frame[count];for(int i=0;i<count;i++)Frames[i].phases=new double[(int)GeometryPhase.Count];Count=0;previous=false;running=true;NativeGeometryProfiling.CollectTimings=true;}
        void Update()
        {
            if(Read(allocation)>=65536)AllocationValidated=true;if(!running)return;
            if(previous)
            {
                ref var f=ref Frames[Count++];f.mainMs=Read(main)/1e6;f.intervalMs=(Time.realtimeSinceStartupAsDouble-time)*1000;f.allocation=AllocationValidated?Read(allocation):-1;f.gcMs=Read(gc)/1e6;f.presentNs=Read(present);
                f.projectionMs=view.ProjectionMs;f.uploadMs=view.UploadMs;f.vertices=view.ProjectedVertices;f.patches=view.ActivePatches;f.uploads=view.Uploads;
                f.occupancyQueries=live.Session.Terrain.OccupancyQueries-queries;f.filterBuilds=live.Session.OrientationEvaluations-filters;f.digs=live.Session.DigCount-digs;
                f.gc0=GC.CollectionCount(0)-g0;f.gc1=GC.CollectionCount(1)-g1;f.gc2=GC.CollectionCount(2)-g2;Array.Copy(NativeGeometryProfiling.Milliseconds,f.phases,f.phases.Length);
                if(Count==Frames.Length){running=false;NativeGeometryProfiling.CollectTimings=false;return;}
            }
            NativeGeometryProfiling.ResetFrame();queries=live.Session.Terrain.OccupancyQueries;filters=live.Session.OrientationEvaluations;digs=live.Session.DigCount;g0=GC.CollectionCount(0);g1=GC.CollectionCount(1);g2=GC.CollectionCount(2);time=Time.realtimeSinceStartupAsDouble;previous=true;
        }
        static long Read(ProfilerRecorder r)=>r.Valid&&r.Count>0?r.LastValue:-1;
        void OnDisable(){main.Dispose();allocation.Dispose();gc.Dispose();present.Dispose();NativeGeometryProfiling.CollectTimings=false;}
    }
}
