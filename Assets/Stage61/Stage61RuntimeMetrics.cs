#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using SporeGobbo.Safety;

/// <summary>Opt-in, bounded evidence collector. No campaign ownership and no per-frame disk IO.</summary>
[DefaultExecutionOrder(32000)]
public sealed class Stage61RuntimeMetrics : MonoBehaviour
{
    const int MaximumFrames=72000;
    static Stage61RuntimeMetrics instance;
    static readonly string[] Names={
        "Spore.Intro.Initialize","Spore.Intro.Sequence","Spore.Intro.TerrainPlacement","Spore.Intro.FallingRock","Spore.Intro.Discovery",
        "Spore.Region.Initialize","Spore.Region.Navigation","Spore.Region.Collision","Spore.Expedition.Discovery","Spore.Expedition.Landmarks",
        "Spore.AI.Weevil","Spore.AI.Blob","Spore.AI.BuddyCombat","Spore.AI.BuddyScavenger","Spore.Projectile.TerrainSweep",
        "Spore.Stage61.Input","Spore.Stage61.Animation","Spore.Stage61.ActorPresentation","Spore.Stage61.LandmarkPresentation","Spore.Stage61.Sorting",
        "Spore.Stage61.Camera","Spore.Stage61.RegionInitialize","Spore.Stage61.CollisionPublish","Spore.Stage61.Navigation","Spore.Stage61.Digging",
        "Spore.Stage61.RenderRefresh","Spore.Stage61.MeshPublication","Spore.Stage61.SnapshotCapture","Spore.Stage61.SaveCommit","Spore.Stage61.ResidentialPlanner",
        "Spore.V1.SaveSerialize","Spore.V1.SaveValidate","Spore.V1.StorageRead","Spore.V1.StorageHash","Spore.V1.StorageValidate",
        "Spore.V1.StorageWrite","Spore.V1.StorageFlush","Spore.V1.StorageReplace","Spore.V1.StorageRecovery","Spore.V1.StorageCommit",
        "Spore.V1.GobboStart","Spore.V1.CommandWheelInit","Spore.V1.LeaderRefresh","Spore.V1.WorldInteractorInit",
        "Spore.V1.IntroRelease","Spore.V1.BindingDisplay","Spore.V1.IntroGUI","Spore.V1.IntroPromptPrepare"
    };
    struct Frame
    {
        public int index,counterFrame,gc0,gc1,gc2;
        public float wall;
        public string phase,category,scene,region;
        public long mono,allocated,reserved,gcBytes,mainNs,waitNs,collectorNs,presentWaitNs,renderThreadWaitNs,playerLoopNs;
        public ulong operationSamples;
        public bool excluded;
    }
    // Operations are drained every completed frame, not retained in a shorter native ring.
    readonly Frame[] frames=new Frame[MaximumFrames];
    readonly long[] operationValues=new long[MaximumFrames*Names.Length];
    readonly int[] operationCounts=new int[MaximumFrames*Names.Length];
    readonly List<string> warnings=new List<string>();
    ProfilerRecorder[] recorders;
    ProfilerRecorder gcRecorder,mainRecorder,waitRecorder,presentWaitRecorder,renderThreadWaitRecorder,playerLoopRecorder;
    ProfilerMarker[] markers;
    int count,droppedFrames,sampleCountOverflows,excludeThroughFrame=-1;
    bool exported;
    string directory,phase="entry",category="initialization",scene,region;
    string completedPhase="entry",completedCategory="initialization",completedScene,completedRegion;
    long mono,allocated,reserved;
    int ordinaryCount,above33,above50,above100;
    float latestMs,maximumOrdinaryMs;
    string maximumOrdinaryPhase="none";

    void Awake()
    {
        if(instance!=null)throw new InvalidOperationException("Only one runtime evidence collector may be active");
        if(Names.Length>64)throw new InvalidOperationException("Operation availability mask capacity exceeded");
        instance=this;
        string id=Environment.GetEnvironmentVariable("SPORE_STAGE61_EVIDENCE_ID");
        directory=Persistence.ExperimentPath("Evidence",id??"");Directory.CreateDirectory(directory);
        markers=new ProfilerMarker[Names.Length];recorders=new ProfilerRecorder[Names.Length];
        var options=ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame;
        for(int i=0;i<Names.Length;i++){markers[i]=new ProfilerMarker(Names[i]);recorders[i]=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,Names[i],2,options);}
        gcRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
        mainRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);waitRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"WaitForTargetFPS",1);
        // Public recorder API only. Missing/pipeline-specific counters remain explicitly unavailable.
        presentWaitRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Gfx.WaitForPresentOnGfxThread",1,options);
        renderThreadWaitRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Gfx.WaitForRenderThread",1,options);
        playerLoopRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"PlayerLoop",1,options);
        SetScene(SceneManager.GetActiveScene());completedScene=scene;completedRegion=region;
        SceneManager.activeSceneChanged+=SceneChanged;Application.logMessageReceived+=Observe;
        CampaignSnapshotStorage.BeginTimingCapture();
    }
    public static void Mark(string value)
    {if(instance==null)return;value=value??"unspecified";if(instance.phase==value)return;instance.phase=value;instance.excludeThroughFrame=Math.Max(instance.excludeThroughFrame,Time.frameCount);}
    /// <summary>Driver labels ordinary/setup/capture/save/transition explicitly; labels do not alter gameplay.</summary>
    public static void Category(string value)
    {if(instance==null)return;value=value??"unspecified";if(instance.category==value)return;instance.category=value;instance.excludeThroughFrame=Math.Max(instance.excludeThroughFrame,Time.frameCount);}
    /// <summary>Call before capture, forced GC or output. Retain these frames but exclude them from ordinary statistics.</summary>
    public static void Exclude(string reason,int followingFrames=2)
    {if(instance==null)return;instance.excludeThroughFrame=Math.Max(instance.excludeThroughFrame,Time.frameCount+Math.Max(0,followingFrames));if(reason!=null)instance.phase=reason;}
    public static string ReadSummary()
    {
        var m=instance;if(m==null)return "Performance capture disabled. No diagnostic work is running.";
        return "Frames "+m.count+" / ordinary "+m.ordinaryCount+"; latest "+m.latestMs.ToString("F2",CultureInfo.InvariantCulture)+" ms; ordinary max "+m.maximumOrdinaryMs.ToString("F2",CultureInfo.InvariantCulture)+" ms ("+m.maximumOrdinaryPhase+"); >33/50/100 ms "+m.above33+"/"+m.above50+"/"+m.above100+"; dropped "+m.droppedFrames+". Phase "+m.phase+", region "+m.region+". Inclusive attribution requires exported evidence; unavailable is not zero.";
    }
    void SceneChanged(Scene previous,Scene next)=>SetScene(next);
    void SetScene(Scene active){scene=active.name;region=scene==GameSceneCatalog.Camp?"camp":scene==GameSceneCatalog.CandidateIntro?"intro":scene==GameSceneCatalog.CandidateExpedition?"expedition":"menu-or-other";}
    void Observe(string message,string trace,LogType type){if(type==LogType.Warning&&message.Contains("ComputeBuffer")&&warnings.Count<256)warnings.Add(phase+" / "+message);}
    static long Last(ProfilerRecorder recorder)=>recorder.Valid&&recorder.Count>0?recorder.LastValue:-1;
    void LateUpdate()
    {
        long begin=System.Diagnostics.Stopwatch.GetTimestamp();
        if(count>=frames.Length){droppedFrames++;return;}
        if(count%60==0){mono=Profiler.GetMonoUsedSizeLong();allocated=Profiler.GetTotalAllocatedMemoryLong();reserved=Profiler.GetTotalReservedMemoryLong();}
        int counterFrame=Time.frameCount-1;
        var f=new Frame{index=Time.frameCount,counterFrame=counterFrame,wall=Time.unscaledDeltaTime*1000,phase=completedPhase,category=completedCategory,scene=completedScene,region=completedRegion,mono=mono,allocated=allocated,reserved=reserved,gcBytes=Last(gcRecorder),mainNs=Last(mainRecorder),waitNs=Last(waitRecorder),presentWaitNs=Last(presentWaitRecorder),renderThreadWaitNs=Last(renderThreadWaitRecorder),playerLoopNs=Last(playerLoopRecorder),gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2),excluded=counterFrame<=excludeThroughFrame};
        for(int i=0;i<recorders.Length;i++)
        {
            var recorder=recorders[i];int n=count*Names.Length+i;
            if(!recorder.Valid||recorder.Count==0){operationValues[n]=-1;operationCounts[n]=-1;continue;}
            var sample=recorder.GetSample(recorder.Count-1);f.operationSamples|=1UL<<i;operationValues[n]=sample.Value;if(sample.Count>int.MaxValue)sampleCountOverflows++;operationCounts[n]=(int)Math.Min(int.MaxValue,sample.Count);
        }
        latestMs=f.wall;
        if(!f.excluded&&f.category=="ordinary"){ordinaryCount++;if(f.wall>maximumOrdinaryMs){maximumOrdinaryMs=f.wall;maximumOrdinaryPhase=f.phase;}if(f.wall>33)above33++;if(f.wall>50)above50++;if(f.wall>100)above100++;}
        f.collectorNs=(long)((System.Diagnostics.Stopwatch.GetTimestamp()-begin)*(1000000000.0/System.Diagnostics.Stopwatch.Frequency));frames[count++]=f;
        completedPhase=phase;completedCategory=category;completedScene=scene;completedRegion=region;
    }
    static string Csv(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
    public void Export()
    {
        if(exported)return;exported=true;long exportBegin=System.Diagnostics.Stopwatch.GetTimestamp();
        using(var file=new StreamWriter(Path.Combine(directory,"frame-memory.csv")))
        {
            file.WriteLine("frame,phase,wallMs,monoBytes,totalAllocatedBytes,totalReservedBytes,gcAllocatedBytes,mainThreadNs,targetWaitNs,gc0,gc1,gc2");
            for(int i=0;i<count;i++){var f=frames[i];file.WriteLine(f.index+","+Csv(f.phase)+","+f.wall.ToString("F5",CultureInfo.InvariantCulture)+","+f.mono+","+f.allocated+","+f.reserved+","+f.gcBytes+","+f.mainNs+","+f.waitNs+","+f.gc0+","+f.gc1+","+f.gc2);}
        }
        using(var file=new StreamWriter(Path.Combine(directory,"frame-context.csv")))
        {
            file.WriteLine("frame,counterFrame,scene,region,phase,category,excluded,operationSamplesMask,collectorNs,gc0Delta,gc1Delta,gc2Delta,presentWaitNs,presentWaitAvailable,renderThreadWaitNs,renderThreadWaitAvailable,playerLoopNs,playerLoopAvailable");
            for(int i=0;i<count;i++){var f=frames[i];var prior=i>0?frames[i-1]:f;file.WriteLine(f.index+","+f.counterFrame+","+Csv(f.scene)+","+Csv(f.region)+","+Csv(f.phase)+","+Csv(f.category)+","+(f.excluded?1:0)+","+f.operationSamples.ToString("X16",CultureInfo.InvariantCulture)+","+f.collectorNs+","+(f.gc0-prior.gc0)+","+(f.gc1-prior.gc1)+","+(f.gc2-prior.gc2)+","+f.presentWaitNs+","+(f.presentWaitNs>=0?1:0)+","+f.renderThreadWaitNs+","+(f.renderThreadWaitNs>=0?1:0)+","+f.playerLoopNs+","+(f.playerLoopNs>=0?1:0));}
        }
        using(var file=new StreamWriter(Path.Combine(directory,"frame-profiler-counters.csv")))
        {
            file.WriteLine("name,requestedCategory,registeredAtExport,unit");
            CounterMetadata(file,"Gfx.WaitForPresentOnGfxThread","Render",presentWaitRecorder);
            CounterMetadata(file,"Gfx.WaitForRenderThread","Render",renderThreadWaitRecorder);
            CounterMetadata(file,"PlayerLoop","Internal",playerLoopRecorder);
            CounterMetadata(file,"Main Thread","Internal",mainRecorder);
            CounterMetadata(file,"WaitForTargetFPS","Internal",waitRecorder);
        }
        using(var legacy=new StreamWriter(Path.Combine(directory,"operation-samples.csv")))
        using(var detail=new StreamWriter(Path.Combine(directory,"operation-frame-samples.csv")))
        using(var registry=new StreamWriter(Path.Combine(directory,"operation-registry.csv")))
        {
            legacy.WriteLine("operation,unit,value,sampleCount");detail.WriteLine("frame,counterFrame,scene,region,phase,category,excluded,operationIndex,operation,available,inclusiveNanoseconds,callCount,exclusiveNanoseconds");registry.WriteLine("operationIndex,operation,availabilityRule");
            for(int marker=0;marker<Names.Length;marker++)registry.WriteLine(marker+","+Names[marker]+",frame-context operationSamplesMask bit; missing row with set bit means valid zero calls/time");
            for(int i=0;i<count;i++)for(int marker=0;marker<Names.Length;marker++)
            {
                var f=frames[i];int n=i*Names.Length+marker;long value=operationValues[n],calls=operationCounts[n];bool available=(f.operationSamples&(1UL<<marker))!=0;
                if(available&&calls==0&&value==0)continue;
                legacy.WriteLine(Names[marker]+","+(available?"TimeNanoseconds":"unavailable")+","+value+","+Math.Max(0,calls));
                detail.WriteLine(f.index+","+f.counterFrame+","+Csv(f.scene)+","+Csv(f.region)+","+Csv(f.phase)+","+Csv(f.category)+","+(f.excluded?1:0)+","+marker+","+Names[marker]+","+(available?1:0)+","+value+","+calls+",-1");
            }
        }
        using(var file=new StreamWriter(Path.Combine(directory,"save-storage-timings.csv")))CampaignSnapshotStorage.ExportTimings(file);
        File.WriteAllLines(Path.Combine(directory,"compute-buffer-warnings.txt"),warnings);
        var fallback=new System.Text.StringBuilder("Read-only pinned-package fallback probe; not an ownership or leak attribution claim.\n");
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){var type=assembly.GetType("UnityEngine.U2D.Animation.GpuDeformationSystem");if(type==null)continue;var field=type.GetField("s_FallbackBuffer",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var buffer=field?.GetValue(null) as ComputeBuffer;fallback.AppendLine(buffer==null?"Fallback absent":!buffer.IsValid()?"Fallback present but invalid":"Fallback present; valid=true count="+buffer.count+" stride="+buffer.stride);}
        File.WriteAllText(Path.Combine(directory,"package-fallback-probe.txt"),fallback.ToString());
        double exportMs=(System.Diagnostics.Stopwatch.GetTimestamp()-exportBegin)*(1000.0/System.Diagnostics.Stopwatch.Frequency);
        File.WriteAllText(Path.Combine(directory,"metrics-limits.txt"),"Opt-in Development-player evidence; no per-frame disk writes. Completed profiler counters use counterFrame=frame-1. Wall interval ends at frame; phase/scene/category retained from preceding frame. Collector overhead is current-frame work, labeled collectorNs. Memory usage sampled every60 frames; GC collection totals sampled at collector time, deltas are interval observations, not allocation-stack attribution. Inclusive scopes overlap; exclusiveNanoseconds=-1 means unavailable. Frame operationSamplesMask distinguishes no sample from valid zero calls. Legacy operation CSV now retains early active completed-frame samples, not only the last ring. Fixed capacity="+MaximumFrames+"; recorded="+count+"; droppedFrames="+droppedFrames+"; sampleCountOverflows="+sampleCountOverflows+"; overflow="+(droppedFrames>0||sampleCountOverflows>0)+"; diagnostic operation-array bytes="+(operationValues.LongLength*sizeof(long)+operationCounts.LongLength*sizeof(int))+"; exportMs="+exportMs.ToString("F3",CultureInfo.InvariantCulture)+". Export/capture/forcedGC/setup must be outside ordinary windows. No final-game capacity claim.");
    }
    void OnApplicationQuit()=>Export();
    static void CounterMetadata(TextWriter file,string name,string requested,ProfilerRecorder recorder){file.WriteLine(name+","+requested+","+(recorder.Valid?1:0)+","+(recorder.Valid?recorder.UnitType.ToString():"unavailable"));}
    void OnDestroy(){Export();SceneManager.activeSceneChanged-=SceneChanged;Application.logMessageReceived-=Observe;if(recorders!=null)for(int i=0;i<recorders.Length;i++)recorders[i].Dispose();gcRecorder.Dispose();mainRecorder.Dispose();waitRecorder.Dispose();presentWaitRecorder.Dispose();renderThreadWaitRecorder.Dispose();playerLoopRecorder.Dispose();CampaignSnapshotStorage.TimingEnabled=false;if(instance==this)instance=null;}
}
#endif
