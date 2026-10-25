using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
namespace SporeGobbo.Geometry.Preview
{
 public sealed class Stage49Benchmark:MonoBehaviour
 {
  [Serializable] public struct Frame{public double intervalMs,mainMs,cpuMs,gpuMs,displayMs;public long allocated,draws;public int resident,selected,vertices,built;public bool ready,focused,presented;}
  [Serializable] public class Case{public string profile,workload,settings;public int cameraOnlyResidentRebuilds,startBuilt,endBuilt,startEdits,endEdits,digOperations;public Frame[] frames;}
  [Serializable] public class Report{public string environment,method;public long allocationProbeBytes;public bool allocationRecorderValid,mainRecorderValid,drawRecorderValid;public List<Case> cases=new List<Case>();}
  IEnumerator Start()
  {
   var args=Environment.GetCommandLineArgs();int ai=Array.IndexOf(args,"-stage49Output");if(ai<0)yield break;string output=args[ai+1];Directory.CreateDirectory(output);var study=GetComponent<Stage49Preview>();var live=study.Live;live.Initialize();live.automation=true;live.showUI=false;study.normalOverlay=false;study.freezeActors=true;
   QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;Profiler.enabled=true;Profiler.SetAreaEnabled(ProfilerArea.Rendering,true);
   var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);var alloc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Standard Draw Calls Count",16);var timing=new FrameTiming[1];ulong prior=0;var end=new WaitForEndOfFrame();
   var report=new Report{environment=SystemInfo.processorType+"; "+SystemInfo.graphicsDeviceName+"; "+SystemInfo.graphicsDeviceType+"; "+Screen.width+"x"+Screen.height,method="Visible development standalone; uncapped; 120 warm and 300 captured frames/case. EOF-to-EOF interval includes harness after the previous EOF. Native Main Thread and unique FrameTiming samples separate. Camera-only revisions compared for retained chunks; newly selected residency is allowed. Actor clocks frozen for controlled comparisons; no production combat."};
   yield return null;var probe=new byte[1024*1024];probe[0]=1;yield return end;report.allocationProbeBytes=alloc.Valid?alloc.LastValue:-1;GC.KeepAlive(probe);yield return null;
   report.allocationRecorderValid=alloc.Valid;report.mainRecorderValid=main.Valid;report.drawRecorderValid=draws.Valid;
   for(int profile=0;profile<(Array.IndexOf(args,"-stage49CaptureOnly")>=0?0:5);profile++)
   {
    study.Apply(Math.Min(profile,3));if(profile==4){study.Apply(2);live.settings.top=.05f;live.settings.side=.15f;live.settings.coverage=.35f;study.softness=.5f;}
    live.LoadOriginalForStage49();study.freezeFocus=false;study.Go(5);
    int wait=0;while((!live.Ready||live.Stream.Pending>0)&&!live.Stream.OverBudget&&wait++<2400)yield return null;
    for(int workload=0;workload<3;workload++)
    {
     var c=new Case{profile=profile<4?Stage49Preview.Names[profile]:"Extreme capacity stress",workload=new[]{"stationary","camera-only framing oscillation","moving and local digging"}[workload],frames=new Frame[300],settings=study.Numeric(),startBuilt=live.Stream.TotalBuilt,startEdits=live.World.EditCount};
     var revisions=new Dictionary<DualTile,int>();foreach(var pair in live.Stream.Resident)revisions[pair.Key]=pair.Value.Revision;
     double last=Time.realtimeSinceStartupAsDouble;float baseOffset=study.horizontalFocusOffset;
     for(int f=-120;f<300;f++)
     {
      if(workload==1)study.horizontalFocusOffset=baseOffset+2*Mathf.Sin((f+120)*.02f);
      if(workload==2&&live.Ready)live.Step(new Vector2(1,1),Vector2.up,f%6==0,1f/60);
      FrameTimingManager.CaptureFrameTimings();yield return end;double now=Time.realtimeSinceStartupAsDouble;
      if(f>=0){var m=new Frame{intervalMs=(now-last)*1000,mainMs=main.Valid?main.LastValue/1e6:0,allocated=alloc.Valid?alloc.LastValue:-1,draws=draws.Valid?draws.LastValue:-1,resident=live.Stream.Resident.Count,selected=live.Stream.Visible.Count,vertices=live.VertexCount,built=live.Stream.TotalBuilt,ready=live.Ready,displayMs=live.DisplayMs,focused=Application.isFocused};if(FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].frameStartTimestamp!=prior){m.cpuMs=timing[0].cpuFrameTime;m.gpuMs=timing[0].gpuFrameTime;m.presented=timing[0].cpuTimePresentCalled>0;prior=timing[0].frameStartTimestamp;}c.frames[f]=m;}
      last=now;yield return null;
     }
     study.horizontalFocusOffset=baseOffset;c.endBuilt=live.Stream.TotalBuilt;c.endEdits=live.World.EditCount;c.digOperations=live.Stream.DigOperations;if(workload<2)foreach(var pair in live.Stream.Resident)if(revisions.TryGetValue(pair.Key,out int revision)&&pair.Value.Revision!=revision)c.cameraOnlyResidentRebuilds++;
     report.cases.Add(c);File.WriteAllText(Path.Combine(output,"performance.json"),JsonUtility.ToJson(report,true));
    }
   }
   // Fresh world is required so all profiles and locations see identical, unexcavated occupancy.
   live.LoadOriginalForStage49();study.freezeFocus=true;live.showGrid=false;
   for(int place=0;place<6;place++)
   {
    study.Go(place);
    for(int profile=0;profile<4;profile++)
    {
     study.Apply(profile);int wait=0;live.Refresh(0);while((!live.Ready||live.Stream.Pending>0)&&!live.Stream.OverBudget&&wait++<2400)yield return null;
     study.captureLabel=true;yield return end;string name=$"place-{place}-{profile}";study.Capture(Path.Combine(output,"comparisons"),name);while(study.CapturePending)yield return null;yield return end;
    }
   }
   study.Apply(2);live.showGrid=true;study.cellOverlay=study.normalOverlay=study.combatOverlay=study.rangeOverlay=true;live.Refresh(0);int diagnosticWait=0;while((!live.Ready||live.Stream.Pending>0)&&diagnosticWait++<2400)yield return null;yield return end;study.Capture(output,"diagnostic-overlays");while(study.CapturePending)yield return null;yield return end;
   main.Dispose();alloc.Dispose();draws.Dispose();Application.Quit();
  }
 }
}
