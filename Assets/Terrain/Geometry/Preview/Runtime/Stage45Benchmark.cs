using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using System.Reflection;
using System.Text;
namespace SporeGobbo.Geometry.Preview
{
    /// <summary>Opt-in standalone benchmark. No code runs unless -stage45BenchmarkOutput is supplied.</summary>
    [DefaultExecutionOrder(950)] public sealed class Stage45Benchmark:MonoBehaviour
    {
        [Serializable] public struct Frame
        {
            public double intervalMs,mainMs,cpuFrameMs,gpuMs,displayMs,projectionMs,uploadMs,controlMs,buildMs;
            public long allocated,drawCalls,sourceQueries;public int terrainUploads,uploads,visibleVertices,patches,filterBuilds,digs,gc0,gc1,gc2;
            public bool timingValid,uniqueTiming,presentCalled,focused;public ulong timingStamp;
        }
        [Serializable] public sealed class Case
        {public string name,backend;public Frame[] frames;public bool allocationValidated;public int meshes,objects,materials,buffers;public long managedUsedBytes,nativeAllocatedBytes,meshBytes;}
        [Serializable] public sealed class Report
        {public string environment,method;public bool development,frameTimingEnabled;public double gpuParityMaxError;public List<Case> cases=new List<Case>();}
        readonly WaitForEndOfFrame endOfFrame=new WaitForEndOfFrame();
        ProfilerRecorder main,allocation,draws;readonly FrameTiming[] timings=new FrameTiming[1];ulong previousTiming;
        static string OutputArgument(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-stage45BenchmarkOutput");return i<0?null:args[i+1];}
        IEnumerator Start()
        {
            string output=OutputArgument();if(output==null)yield break;Directory.CreateDirectory(output);
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Time.captureDeltaTime=1f/60;Profiler.enabled=true;Profiler.SetAreaEnabled(ProfilerArea.Rendering,true);
            var view=GetComponent<Stage4CameraPreview>();var gpu=GetComponent<Stage45GpuProjection>();var demo=GetComponent<Stage45MovingDemo>();var live=GetComponent<PlayableCavePreview>();
            view.Refresh(0);demo.Initialize();live.enabled=demo.enabled=false;view.ShowDebug=false;
            var report=new Report{environment=SystemInfo.processorType+"; "+SystemInfo.graphicsDeviceName+"; "+SystemInfo.graphicsDeviceType+"; Unity "+Application.unityVersion+"; "+Screen.width+"x"+Screen.height,development=Debug.isDebugBuild,frameTimingEnabled=FrameTimingManager.IsFeatureEnabled(),method="Visible standalone Development Player, 1920x1080 window requested, vSync off, uncapped target; fixed 1/60 logical simulation. 60 warm-up + 240 captured engine frames per backend/scenario. Native counters can lag current CPU phase clocks; FrameTiming GPU results are asynchronous, unique timestamp flag identifies fresh results. Debug UI hidden. Native last-frame and interval timing include rendering/engine work. No claim of optical screen presentation."};
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-stage45DiagnosticsOnly")>=0){yield return Diagnostics(output);Application.Quit(0);yield break;}
            if(SystemInfo.supportsComputeShaders){report.gpuParityMaxError=Stage45GpuValidation.Probe(gpu.parityShader);if(report.gpuParityMaxError>.0002){File.WriteAllText(Path.Combine(output,"parity-failed.json"),JsonUtility.ToJson(report,true));Debug.LogError("GPU parity exceeds tolerance.");Application.Quit(2);yield break;}}
            main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);allocation=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Standard Draw Calls Count",16);
            foreach(string name in new[]{"stationary","camera-only","follow-gentle","follow-balanced","follow-strong","dig-follow","moving-actors","projectiles-range","dense-strong","switch-presets"})foreach(bool gpuMode in new[]{false,true})
            {
                live.ResetFixture();view.settings=new Stage4Settings{coverage=.55f};view.settings.smoothing=0;view.FreezeCamera=false;view.ExperimentFocus=null;live.followPlayer=true;gpu.useGpu=gpuMode;
                demo.paused=name!="moving-actors"&&name!="projectiles-range";demo.showProjectiles=name=="projectiles-range";demo.showTrajectories=name=="projectiles-range";demo.showRange=name=="projectiles-range";demo.speedFactor=1;demo.Restart();
                if(name=="follow-gentle")view.Preset(0);if(name=="follow-strong"||name=="dense-strong")view.Preset(2);
                if(name=="dense-strong"){view.settings.coverage=.4f;view.settings.centerWidth=60;view.settings.centerHeight=32;}
                live.Predict(live.Position+live.Aim*(live.footprint*.5f+.55f));live.DrawOverlay();view.Refresh(1);
                if(name=="stationary"||name=="moving-actors"||name=="projectiles-range"||name=="switch-presets")view.FreezeCamera=true;
                var current=new Case{name=name,backend=gpuMode?"GPU":"CPU",frames=new Frame[240]};
                GC.KeepAlive(new byte[65536]);bool allocationValid=false;
                for(int warm=0;warm<60;warm++){if(Read(allocation)>=65536)allocationValid=true;FrameTimingManager.CaptureFrameTimings();yield return null;}
                current.allocationValidated=allocationValid;
                double last=Time.realtimeSinceStartupAsDouble;
                for(int i=0;i<current.frames.Length;i++)
                {
                    long queries=live.Session.Terrain.OccupancyQueries;int filters=live.Session.OrientationEvaluations,digs=live.Session.DigCount,g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
                    if(name=="camera-only"||name=="dense-strong")view.ExperimentFocus=new Vector2(50.5f+Mathf.Sin(i*.04f)*12,39.5f+Mathf.Cos(i*.04f)*14);
                    if(name.StartsWith("follow-"))live.Step(Vector2.right,new Vector2(0,-1),false,1f/60);
                    if(name=="dig-follow")live.Step(new Vector2(0,-1),new Vector2(0,-1),true,1f/60);
                    if(name=="switch-presets")view.Preset((i/15)%3);
                    demo.Advance(1.0/60);live.DrawOverlay();FrameTimingManager.CaptureFrameTimings();
                    yield return endOfFrame;
                    double now=Time.realtimeSinceStartupAsDouble;var f=new Frame{intervalMs=(now-last)*1000,mainMs=Read(main)/1e6,allocated=allocationValid?Read(allocation):-1,drawCalls=ReadDraws(draws),displayMs=view.DisplayFrameMs,projectionMs=view.ProjectionMs,uploadMs=view.UploadMs,controlMs=gpu.ControlMs,buildMs=demo.BuildMs,terrainUploads=gpuMode?gpu.TerrainUploads:0,uploads=view.Uploads,visibleVertices=gpu.VisibleVertices,patches=view.ActivePatches,sourceQueries=live.Session.Terrain.OccupancyQueries-queries,filterBuilds=live.Session.OrientationEvaluations-filters,digs=live.Session.DigCount-digs,gc0=GC.CollectionCount(0)-g0,gc1=GC.CollectionCount(1)-g1,gc2=GC.CollectionCount(2)-g2,focused=Application.isFocused};
                    if(FrameTimingManager.GetLatestTimings(1,timings)>0){var t=timings[0];f.timingValid=true;f.timingStamp=t.frameStartTimestamp;f.uniqueTiming=t.frameStartTimestamp!=previousTiming;previousTiming=t.frameStartTimestamp;f.gpuMs=t.gpuFrameTime;f.cpuFrameMs=t.cpuFrameTime;f.presentCalled=t.cpuTimePresentCalled>0;}
                    current.frames[i]=f;last=now;
                    yield return null;
                }
                current.meshes=live.RuntimeMeshCount+demo.MeshCount+3;current.objects=live.RuntimeObjectCount+demo.MeshCount+3;current.materials=gpu.ResourceCount;current.buffers=gpu.BufferCount;
                current.managedUsedBytes=Profiler.GetMonoUsedSizeLong();current.nativeAllocatedBytes=Profiler.GetTotalAllocatedMemoryLong();var uniqueMeshes=new HashSet<Mesh>();foreach(var filter in view.GetComponentsInChildren<MeshFilter>()){var mesh=filter.sharedMesh;if(mesh!=null&&uniqueMeshes.Add(mesh))current.meshBytes+=Profiler.GetRuntimeMemorySizeLong(mesh);}
                report.cases.Add(current);File.WriteAllText(Path.Combine(output,"standalone.json"),JsonUtility.ToJson(report,true));
                Debug.Log("Stage45 benchmark "+name+" "+current.backend+" complete.");
            }
            demo.paused=false;demo.showRange=demo.showProjectiles=demo.showTrajectories=true;demo.Restart();demo.Advance(17);demo.paused=true;view.ExperimentFocus=new Vector2(50.5f,39.5f);view.FreezeCamera=false;view.settings=new Stage4Settings{coverage=.55f};view.Preset(2);view.Refresh(1);yield return null;
            var camera=GetComponent<CaveGeometryPreview>().previewCamera;
            foreach(bool gpuMode in new[]{false,true}){gpu.useGpu=gpuMode;view.Refresh(0);Capture(camera,Path.Combine(output,gpuMode?"strong-gpu.png":"strong-cpu.png"));}
            view.settings.actorMode=1;view.Refresh(0);Capture(camera,Path.Combine(output,"strong-grid-actors.png"));
            main.Dispose();allocation.Dispose();draws.Dispose();Debug.Log("Stage45 benchmark finished: "+output);Application.Quit(0);
        }
        static long ReadDraws(ProfilerRecorder r)=>!r.Valid?-1:(r.Count>0||r.WrappedAround)?r.LastValue:r.CurrentValue>0?r.CurrentValue:-1;
        IEnumerator Diagnostics(string output)
        {
            var text=new StringBuilder();text.AppendLine("No preview compute parity buffers are created in this diagnostic path.");
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type=assembly.GetType("UnityEngine.U2D.Animation.GpuDeformationSystem");if(type==null)continue;
                var field=type.GetField("s_FallbackBuffer",BindingFlags.Static|BindingFlags.NonPublic);var buffer=field==null?null:field.GetValue(null) as ComputeBuffer;
                text.AppendLine("Installed 2D Animation fallback buffer: "+(buffer==null?"absent":("valid="+buffer.IsValid()+", count="+buffer.count+", stride="+buffer.stride)));
            }
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);var recorders=new List<ProfilerRecorder>();var names=new List<string>();
            foreach(var handle in handles)
            {
                var description=ProfilerRecorderHandle.GetDescription(handle);
                if(description.Name.IndexOf("Draw",StringComparison.OrdinalIgnoreCase)<0&&description.Name.IndexOf("Batch",StringComparison.OrdinalIgnoreCase)<0)continue;
                if(description.Name.IndexOf("Count",StringComparison.OrdinalIgnoreCase)<0)continue;
                names.Add(description.Category.Name+" / "+description.Name);recorders.Add(ProfilerRecorder.StartNew(description.Category,description.Name,16));
            }
            for(int i=0;i<120;i++)yield return null;
            for(int i=0;i<recorders.Count;i++){var recorder=recorders[i];text.AppendLine(names[i]+": valid="+recorder.Valid+", count="+recorder.Count+", current="+(recorder.Valid?recorder.CurrentValue:-1)+", last="+(recorder.Count>0?recorder.LastValue:-1));recorder.Dispose();}
            File.WriteAllText(Path.Combine(output,"diagnostics.txt"),text.ToString());
        }
        static long Read(ProfilerRecorder r)=>r.Valid&&(r.Count>0||r.WrappedAround)?r.LastValue:-1;
        public static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1920,1080,24);var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);var old=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            try{camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=16f/9;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.rect=rect;RenderTexture.active=active;Destroy(target);Destroy(texture);}
        }
    }
}
