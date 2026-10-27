#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using SporeGobbo.Safety;
/// <summary>Read-only bounded diagnostics. No game-state ownership or scheduling decisions.</summary>
[DefaultExecutionOrder(32000)]
public sealed class Stage61RuntimeMetrics:MonoBehaviour
{
    static Stage61RuntimeMetrics instance;
    static readonly string[] Names={"Spore.Intro.Initialize","Spore.Intro.Sequence","Spore.Intro.TerrainPlacement","Spore.Intro.FallingRock","Spore.Intro.Discovery","Spore.Region.Initialize","Spore.Region.Navigation","Spore.Region.Collision","Spore.Expedition.Discovery","Spore.Expedition.Landmarks","Spore.AI.Weevil","Spore.AI.Blob","Spore.AI.BuddyCombat","Spore.AI.BuddyScavenger","Spore.Projectile.TerrainSweep","Spore.Stage61.Input","Spore.Stage61.Animation","Spore.Stage61.ActorPresentation","Spore.Stage61.LandmarkPresentation","Spore.Stage61.Sorting","Spore.Stage61.Camera","Spore.Stage61.RegionInitialize","Spore.Stage61.CollisionPublish","Spore.Stage61.Navigation","Spore.Stage61.Digging","Spore.Stage61.RenderRefresh","Spore.Stage61.MeshPublication","Spore.Stage61.SnapshotCapture","Spore.Stage61.SaveCommit","Spore.Stage61.ResidentialPlanner"};
    struct Frame{public int index;public float wall;public string phase;public long mono,allocated,reserved,gcBytes,mainNs,waitNs;public int gc0,gc1,gc2;}
    readonly Frame[] frames=new Frame[72000];readonly List<string> warnings=new List<string>();
    ProfilerRecorder[] recorders;ProfilerRecorder gcRecorder,mainRecorder,waitRecorder;ProfilerMarker[] markers;int count;bool exported;string phase="entry",directory;long mono,allocated,reserved;
    void Awake()
    {
        instance=this;string id=Environment.GetEnvironmentVariable("SPORE_STAGE61_EVIDENCE_ID");
        directory=Persistence.ExperimentPath("Evidence",id??"");Directory.CreateDirectory(directory);
        markers=Names.Select(name=>new ProfilerMarker(name)).ToArray();
        recorders=Names.Select(name=>ProfilerRecorder.StartNew(ProfilerCategory.Scripts,name,16384,ProfilerRecorderOptions.Default|ProfilerRecorderOptions.SumAllSamplesInFrame)).ToArray();
        gcRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
        mainRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);waitRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"WaitForTargetFPS",1);
        Application.logMessageReceived+=Observe;
    }
    public static void Mark(string value){if(instance!=null)instance.phase=value;}
    void Observe(string message,string trace,LogType type){if(type==LogType.Warning&&message.Contains("ComputeBuffer"))warnings.Add(phase+" / "+message);}
    void LateUpdate()
    {
        if(count>=frames.Length)return;
        if(count%60==0){mono=Profiler.GetMonoUsedSizeLong();allocated=Profiler.GetTotalAllocatedMemoryLong();reserved=Profiler.GetTotalReservedMemoryLong();}
        frames[count++]=new Frame{index=Time.frameCount,wall=Time.unscaledDeltaTime*1000,phase=phase,mono=mono,allocated=allocated,reserved=reserved,gcBytes=gcRecorder.Valid&&gcRecorder.Count>0?gcRecorder.LastValue:-1,mainNs=mainRecorder.Valid&&mainRecorder.Count>0?mainRecorder.LastValue:-1,waitNs=waitRecorder.Valid&&waitRecorder.Count>0?waitRecorder.LastValue:-1,gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2)};
    }
    public void Export()
    {
        if(exported)return;exported=true;
        using(var file=new StreamWriter(Path.Combine(directory,"frame-memory.csv")))
        {file.WriteLine("frame,phase,wallMs,monoBytes,totalAllocatedBytes,totalReservedBytes,gcAllocatedBytes,mainThreadNs,targetWaitNs,gc0,gc1,gc2");for(int i=0;i<count;i++){var f=frames[i];file.WriteLine(f.index+","+f.phase.Replace(","," ")+","+f.wall.ToString("F5",CultureInfo.InvariantCulture)+","+f.mono+","+f.allocated+","+f.reserved+","+f.gcBytes+","+f.mainNs+","+f.waitNs+","+f.gc0+","+f.gc1+","+f.gc2);}}
        using(var file=new StreamWriter(Path.Combine(directory,"operation-samples.csv")))
        {file.WriteLine("operation,unit,value,sampleCount");for(int i=0;i<recorders.Length;i++){var recorder=recorders[i];if(!recorder.Valid){file.WriteLine(Names[i]+",unavailable,-1,0");continue;}foreach(var sample in recorder.ToArray())file.WriteLine(Names[i]+","+recorder.UnitType+","+sample.Value+","+sample.Count);}}
        File.WriteAllLines(Path.Combine(directory,"compute-buffer-warnings.txt"),warnings);
        var fallback=new System.Text.StringBuilder("Read-only pinned-package fallback probe; not an ownership or leak attribution claim.\n");foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){var type=assembly.GetType("UnityEngine.U2D.Animation.GpuDeformationSystem");if(type==null)continue;var field=type.GetField("s_FallbackBuffer",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var buffer=field?.GetValue(null) as ComputeBuffer;fallback.AppendLine(buffer==null?"Fallback absent":!buffer.IsValid()?"Fallback present but invalid":"Fallback present; valid=true count="+buffer.count+" stride="+buffer.stride);}File.WriteAllText(Path.Combine(directory,"package-fallback-probe.txt"),fallback.ToString());
        File.WriteAllText(Path.Combine(directory,"metrics-limits.txt"),"Development player instrumentation; wall frame time includes 60 FPS target/presentation waits. Marker samples are inclusive and may overlap; do not add parent/child costs. Profiler marker aggregation is grouped per recorded frame, sampleCount records invocation count. Memory is engine allocated/reserved/managed usage sampled every 60 frames, not a complete GPU/native allocation audit. GC allocated counter is the previous recorded frame, -1 means unavailable. Bounded 36000-frame/16384-operation-sample buffers; frames recorded="+count+". No final-game capacity claim.");
    }
    void OnApplicationQuit()=>Export();
    void OnDestroy(){Export();Application.logMessageReceived-=Observe;if(recorders!=null)for(int i=0;i<recorders.Length;i++)recorders[i].Dispose();gcRecorder.Dispose();mainRecorder.Dispose();waitRecorder.Dispose();if(instance==this)instance=null;}
}
#endif
