using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
namespace SporeGobbo.Geometry.Preview
{
    /// <summary>Explicit opt-in visible standalone run. Captures loading and steady frames separately.</summary>
    public sealed class Stage47Benchmark:MonoBehaviour
    {
        [Serializable] public struct Frame
        {
            public double intervalMs,mainMs,gpuMs,cpuMs,buildMs,unloadMs,digMs,displayMs;
            public long drawCalls,allocated,managedBytes,nativeBytes,meshBytes,queries;
            public int digEvents,builds,evictions,resident,visible,pending,cacheTiles,cacheSamples,meshes,objects,active,visibleDormant,simulationChunks,uploads,terrainUploads,vertices,buffers,materials,gc0,gc1,gc2;
            public bool ready,focused,timingValid,uniqueTiming,presentCalled;public ulong stamp;
        }
        [Serializable] public sealed class Case{public string name;public List<Frame> frames=new List<Frame>();public int settleFrames;public double settleMs;}
        [Serializable] public sealed class Report
        {
            public string environment,method;public double creationMs,firstActivationMs,firstReadyMs,ordinaryBuildMs,denseBuildMs;public int occupancyBytes,denseVertices,totalBuilt,totalEvicted;public bool persistencePassed,saveReloadPassed,allocationValidated;public List<Case> cases=new List<Case>();
        }
        ProfilerRecorder main,alloc,draws;readonly FrameTiming[] timing=new FrameTiming[1];readonly WaitForEndOfFrame end=new WaitForEndOfFrame();ulong previousStamp;Stage47Preview live;Report report;string output;double last;int g0,g1,g2,digs,builds,evictions;long queries;
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-stage47BenchmarkOutput");if(index<0)yield break;bool quick=Array.IndexOf(args,"-stage47QuickProof")>=0;output=args[index+1];Directory.CreateDirectory(output);
            live=GetComponent<Stage47Preview>();live.Initialize();live.automation=true;live.showUI=false;live.settings.smoothing=0;
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Time.captureDeltaTime=1f/60;Profiler.enabled=true;Profiler.SetAreaEnabled(ProfilerArea.Rendering,true);
            main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);alloc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Standard Draw Calls Count",16);
            report=new Report{environment=SystemInfo.processorType+"; "+SystemInfo.graphicsDeviceName+"; "+SystemInfo.graphicsDeviceType+"; Unity "+Application.unityVersion+"; "+Screen.width+"x"+Screen.height,creationMs=live.CreationMs,occupancyBytes=live.World.OccupancyBytes,method="Visible standalone Development Player, vSync off; ordinary cases uncapped, long revisit case targets 60 fps for 18000 frames (~5 minutes); logical dt 1/60; actual engine frames. Loading frames retained separately; asynchronous FrameTiming unique/positive GPU samples only. Memory sampled every 60 frames includes reporting lists and Unity caches. No optical monitor timing claim."};
            if(quick)report.method+=" Quick proof override: long revisit case is 360 frames, not 18000.";
            long initial=System.Diagnostics.Stopwatch.GetTimestamp();var first=new Case{name="initial-loading"};report.cases.Add(first);
            GC.KeepAlive(new byte[65536]);bool firstChunk=false;
            for(int i=0;i<1500&&!live.Ready;i++)
            {Begin();FrameTimingManager.CaptureFrameTimings();yield return end;first.frames.Add(Read(i));if(!firstChunk&&live.Stream.Resident.Count>0){report.firstActivationMs=StreamingGeometry.Elapsed(initial);firstChunk=true;}if(ReadCounter(alloc)>=65536)report.allocationValidated=true;yield return null;}
            report.firstReadyMs=StreamingGeometry.Elapsed(initial);if(!live.Ready)throw new Exception("Initial viewport did not load");Write();
            // Dig a reproducible solid test cell adjacent to the guaranteed travel corridor, then force complete eviction.
            int editX=live.World.Width/2+12,editY=live.World.Height/2+12;
            while(editY<live.World.Height-8&&(!live.World.IsSolid(editX,editY)||live.World.Locked(editX,editY)))editY++;
            live.Stream.Dig(new[]{new DualTile(editX,editY)});
            using(var memory=new MemoryStream()){live.World.Save(memory);memory.Position=0;var restored=StreamingWorld.Load(memory);report.saveReloadPassed=!restored.IsSolid(editX,editY)&&restored.EditCount==live.World.EditCount;}
            foreach(string name in new[]{"stationary-strong","slow-travel","rapid-travel","continuous-digging","preset-switches","west-edge","east-edge","return-excavation","long-travel-revisits"})
            {
                var current=new Case{name=name};report.cases.Add(current);live.Preset(2);live.speed=name=="rapid-travel"?50:5;
                if(name=="west-edge")live.Teleport(new Vector2(8.5f,live.World.Height*.5f+.5f));
                if(name=="east-edge")live.Teleport(new Vector2(live.World.Width-8.5f,live.World.Height*.5f+.5f));
                if(name=="return-excavation")live.Teleport(new Vector2(live.World.Width*.5f+.5f,live.World.Height*.5f+.5f));
                long settle=System.Diagnostics.Stopwatch.GetTimestamp();for(int i=0;i<1500&&(!live.Ready||live.Stream.Pending>0);i++){yield return null;current.settleFrames++;}current.settleMs=StreamingGeometry.Elapsed(settle);
                int frames=name=="long-travel-revisits"?(quick?360:18000):360;Application.targetFrameRate=name=="long-travel-revisits"?60:-1;last=Time.realtimeSinceStartupAsDouble;
                for(int i=0;i<frames;i++)
                {
                    Begin();if(name=="slow-travel"||name=="rapid-travel")if(live.Ready)live.Step(Vector2.right,Vector2.right,false,1f/60);
                    if(name=="continuous-digging"&&live.Ready)live.Step(Vector2.up,Vector2.up,true,1f/60);
                    if(name=="preset-switches"&&i%60==0)live.Preset((i/60)%3);
                    if(name=="long-travel-revisits"&&i%300==0){int visit=(i/300)%6;live.Teleport(new Vector2(new[]{.024f,.219f,.414f,.610f,.805f,.977f}[visit]*live.World.Width+.5f,live.World.Height*.5f+.5f));live.Preset((i/300)%3);}
                    FrameTimingManager.CaptureFrameTimings();yield return end;current.frames.Add(Read(i));if(name=="long-travel-revisits"&&i%1800==0)Debug.Log($"Stage47 long revisit progress {i}/{frames}; resident {live.Stream.Resident.Count}; built {live.Stream.TotalBuilt}; evicted {live.Stream.TotalEvicted}");yield return null;
                }
                if(name=="return-excavation")report.persistencePassed=!live.World.IsSolid(editX,editY)&&live.Stream.TotalEvicted>0;
                Write();Debug.Log("Stage47 case complete: "+name);
            }
            long build=System.Diagnostics.Stopwatch.GetTimestamp();var ordinary=new ContourTerrainCache(live.World);PreviewMeshBuilder.BuildPatch(live.World,ordinary,live.World.Width/2,live.World.Height/2,16);report.ordinaryBuildMs=StreamingGeometry.Elapsed(build);ordinary.Clear();
            var dense=new Dense();var cache=new ContourTerrainCache(dense);build=System.Diagnostics.Stopwatch.GetTimestamp();var layers=PreviewMeshBuilder.BuildPatch(dense,cache,0,0,16);report.denseBuildMs=StreamingGeometry.Elapsed(build);foreach(var layer in layers)report.denseVertices+=layer.Vertices.Count;cache.Clear();
            live.Teleport(new Vector2(live.World.Width*.5f+.5f,live.World.Height*.5f+.5f));live.Preset(2);for(int i=0;i<1500;i++){yield return null;if(live.Ready&&live.Stream.Pending==0)break;}
            Capture("large-world-strong.png");live.showGrid=true;yield return null;Capture("large-world-grid.png");report.totalBuilt=live.Stream.TotalBuilt;report.totalEvicted=live.Stream.TotalEvicted;Write();main.Dispose();alloc.Dispose();draws.Dispose();Debug.Log("Stage47 benchmark complete: "+output);Application.Quit();
        }
        void Begin(){if(last==0)last=Time.realtimeSinceStartupAsDouble;g0=GC.CollectionCount(0);g1=GC.CollectionCount(1);g2=GC.CollectionCount(2);queries=live.World.Queries;digs=live.Stream.DigOperations;builds=live.Stream.TotalBuilt;evictions=live.Stream.TotalEvicted;}
        Frame Read(int i)
        {
            var f=new Frame{intervalMs=(Time.realtimeSinceStartupAsDouble-last)*1000,mainMs=ReadCounter(main)/1e6,drawCalls=ReadCounter(draws),allocated=ReadCounter(alloc),buildMs=live.Stream.BuildMs,unloadMs=live.Stream.UnloadMs,digMs=live.Stream.DigOperations>digs?live.Stream.DigMs:0,digEvents=live.Stream.DigOperations-digs,builds=live.Stream.TotalBuilt-builds,evictions=live.Stream.TotalEvicted-evictions,displayMs=live.DisplayMs,resident=live.Stream.Resident.Count,visible=live.Stream.Visible.Count,pending=live.Stream.Pending,cacheSamples=live.Stream.CacheSamples,cacheTiles=live.Stream.CacheTiles,meshes=live.MeshCount,objects=live.ObjectCount,active=live.ActiveActors,visibleDormant=live.VisibleDormant,simulationChunks=live.SimulationChunks,uploads=live.Gpu.TotalUploads,terrainUploads=live.Gpu.TerrainUploads,vertices=live.VertexCount,buffers=live.Gpu.BufferCount,materials=live.Gpu.ResourceCount,ready=live.Ready,focused=Application.isFocused,queries=live.World.Queries-queries,gc0=GC.CollectionCount(0)-g0,gc1=GC.CollectionCount(1)-g1,gc2=GC.CollectionCount(2)-g2};
            if(i%60==0){f.managedBytes=Profiler.GetMonoUsedSizeLong();f.nativeBytes=Profiler.GetTotalAllocatedMemoryLong();foreach(var filter in live.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh!=null)f.meshBytes+=Profiler.GetRuntimeMemorySizeLong(filter.sharedMesh);}
            if(FrameTimingManager.GetLatestTimings(1,timing)>0){var t=timing[0];f.timingValid=true;f.uniqueTiming=t.frameStartTimestamp!=previousStamp;previousStamp=t.frameStartTimestamp;f.stamp=t.frameStartTimestamp;f.gpuMs=t.gpuFrameTime;f.cpuMs=t.cpuFrameTime;f.presentCalled=t.cpuTimePresentCalled>0;}last=Time.realtimeSinceStartupAsDouble;return f;
        }
        static long ReadCounter(ProfilerRecorder r)=>r.Valid&&(r.Count>0||r.WrappedAround)?r.LastValue:0;
        void Write()=>File.WriteAllText(Path.Combine(output,"standalone.json"),JsonUtility.ToJson(report,true));
        void Capture(string name){var camera=live.previewCamera;var texture=new RenderTexture(1920,1080,24);var old=camera.targetTexture;camera.targetTexture=texture;camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=previous;Destroy(pixels);texture.Release();Destroy(texture);}
        sealed class Dense:IOccupancySource{public int Width=>33;public int Height=>33;public bool IsSolid(int x,int y)=>(x+y)%2==0;}
    }
}





