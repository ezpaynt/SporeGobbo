using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
namespace SporeGobbo.Geometry.Preview
{
 [DefaultExecutionOrder(1400)] public sealed class Stage50Benchmark:MonoBehaviour
 {
  [Serializable] public struct Frame{public double intervalMs,mainMs,cpuMs,gpuMs,simMs,visualMs,terrainMs;public long allocated,draws,managedBytes,unityBytes;public int active,dormant,presented,residents,actorVertices,indicatorVertices,ownedMeshes;public bool ready,focused;}
  [Serializable] public class Case{public string name;public int profile,mode,size,actors,retainedTopologyChanges,startEdits,endEdits;public Frame[] frames;}
  [Serializable] public class Report{public string environment,method;public long[] allocationProbe;public List<Case> cases=new List<Case>();}
  [Serializable] public class ActorSnapshot{public int id,size,kind;public string state,attack,visibility;public double x,y,distance,timer,attacks,logicalSpeed,pixelWidth,pixelHeight,pixelsPerLabSecond;public bool fits,persistent;}
  [Serializable] public class Snapshot{public LabTuning tuning;public float bodyScale,safetyMargin;public bool decorations,xray;public string scenario,camera,visibility;public int mode,profile,residents,active,dormant,ownedMeshes;public double clock,rootX,rootY;public ActorSnapshot[] actors;}
  [Serializable] public class Clearance{public string location;public int footprint;public double x,y;public bool collisionFits,navigationFits,visualBoundsTouchesDirt;}
  [Serializable] public class ClearanceReport{public List<Clearance> rows=new List<Clearance>();}
  Stage50Preview lab;Stage47Preview live;Stage49Preview camera;string output;
  IEnumerator Start()
  {
   var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-stage50Output");if(arg<0)yield break;output=args[arg+1];Directory.CreateDirectory(output);lab=GetComponent<Stage50Preview>();lab.Initialize();live=lab.Live;camera=lab.CameraStudy;live.automation=lab.automation=true;live.showUI=lab.showPanel=false;camera.normalOverlay=camera.combatOverlay=false;lab.showLabels=false;lab.showSafety=false;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;Profiler.enabled=true;Profiler.SetAreaEnabled(ProfilerArea.Rendering,true);
   if(Array.IndexOf(args,"-stage50ClearanceOnly")>=0){ExportClearance();Application.Quit();yield break;}
   var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);var alloc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Standard Draw Calls Count",16);var timing=new FrameTiming[1];ulong stamp=0;var end=new WaitForEndOfFrame();
   var report=new Report{environment=SystemInfo.processorType+"; "+SystemInfo.graphicsDeviceName+"; "+SystemInfo.graphicsDeviceType+"; "+Screen.width+"x"+Screen.height,method="Visible Development Mono D3D11, uncapped, vSync off; fixed logical dt1/60, 120 warm +360 measured frames/case; EOF-to-EOF includes harness. Whole-frame allocations, native main/unique positive GPU samples; memory sampled every60 frames; no modest-hardware certification.",allocationProbe=new long[4]};
   for(int n=0;n<4;n++){var bytes=new byte[1024*1024];bytes[0]=1;yield return null;yield return end;report.allocationProbe[n]=alloc.Valid?alloc.LastValue:-1;GC.KeepAlive(bytes);yield return null;}
   if(Array.IndexOf(args,"-stage50CaptureOnly")<0)for(int scenario=0;scenario<7;scenario++)
   {
    int profile=scenario<4?scenario:2;camera.Apply(profile);camera.freezeFocus=scenario!=4;live.LoadOriginalForStage49();live.Teleport(new Vector2(512.5f,512.5f));lab.encounterCount=scenario==4||scenario==6?32:8;lab.footprint=scenario==5?9:1;lab.scaleMode=scenario==5?1:0;lab.paused=false;lab.Restart();for(int n=0;n<1800;n++)lab.Advance(1.0/60);if(scenario==6)lab.paused=true;
    live.Refresh(0);int settle=0;while((!live.Ready||live.Stream.Pending>0)&&settle++<2500)yield return null;
    string name=scenario<4?"eight simultaneous encounters":scenario==4?"32 encounters, camera follow and local digging":scenario==5?"eight 9x9 bodies, grid-matched":"32 paused actors, camera-only pan";
    var c=new Case{name=name,profile=profile,mode=lab.scaleMode,size=lab.footprint,actors=lab.encounterCount,frames=new Frame[360],startEdits=live.World.EditCount};var versions=new Dictionary<DualTile,(StreamedChunk,int)>();foreach(var pair in live.Stream.Resident)versions[pair.Key]=(pair.Value,pair.Value.Revision);double last=Time.realtimeSinceStartupAsDouble;
    for(int frame=-120;frame<360;frame++)
    {
     if(scenario==4&&live.Ready)live.Step(new Vector2(1,1),Vector2.up,frame%6==0,1f/60);if(scenario==6)camera.horizontalFocusOffset=3+2*Mathf.Sin(frame*.03f);lab.Advance(1.0/60);FrameTimingManager.CaptureFrameTimings();yield return end;double now=Time.realtimeSinceStartupAsDouble;
     if(frame>=0){var f=new Frame{intervalMs=(now-last)*1000,mainMs=main.Valid?main.LastValue/1e6:0,allocated=alloc.Valid?alloc.LastValue:-1,draws=draws.Valid?draws.LastValue:-1,simMs=lab.SimulationMs,visualMs=lab.VisualMs,terrainMs=live.DisplayMs,active=lab.Active,dormant=lab.Dormant,presented=lab.Visible,residents=live.Stream.Resident.Count,actorVertices=lab.ActorVertices,indicatorVertices=lab.IndicatorVertices,ownedMeshes=lab.OwnedMeshes,ready=live.Ready,focused=Application.isFocused};if(frame%60==0){f.managedBytes=GC.GetTotalMemory(false);f.unityBytes=Profiler.GetTotalAllocatedMemoryLong();}if(FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].frameStartTimestamp!=stamp){f.cpuMs=timing[0].cpuFrameTime;f.gpuMs=timing[0].gpuFrameTime;stamp=timing[0].frameStartTimestamp;}c.frames[frame]=f;}
     last=now;yield return null;
    }
    c.endEdits=live.World.EditCount;foreach(var pair in live.Stream.Resident)if(versions.TryGetValue(pair.Key,out var old)&&ReferenceEquals(pair.Value,old.Item1)&&pair.Value.Revision!=old.Item2)c.retainedTopologyChanges++;report.cases.Add(c);File.WriteAllText(Path.Combine(output,"performance.json"),JsonUtility.ToJson(report,true));
   }
   live.LoadOriginalForStage49();live.Teleport(new Vector2(512.5f,512.5f));lab.encounterCount=8;lab.footprint=1;lab.showLabels=lab.showFootprints=lab.showSafety=true;camera.normalOverlay=camera.combatOverlay=true;camera.freezeFocus=true;camera.captureLabel=true;
   for(int profile=0;profile<4;profile++)for(int mode=0;mode<2;mode++)for(int pose=0;pose<2;pose++)
   {
    camera.Apply(profile);lab.scaleMode=mode;lab.paused=false;lab.Restart();int frames=pose==0?480:1200;for(int n=0;n<frames;n++)lab.Advance(1.0/60);lab.paused=true;yield return Capture($"approach-p{profile}-m{mode}-t{(pose==0?8:20)}",pose==0?"Approaches at8 logical seconds":"Pursuit at20 logical seconds");
   }
   for(int profile=0;profile<4;profile++)for(int mode=0;mode<2;mode++){camera.Apply(profile);lab.scaleMode=mode;lab.Restart();lab.paused=true;lab.xray=true;lab.showVisualBounds=true;yield return Capture($"corners-p{profile}-m{mode}","Explicit XRAY upper-corner scale inspection, not line-of-sight visibility");lab.xray=false;lab.showVisualBounds=false;}
   for(int profile=0;profile<4;profile++)
   {
    camera.Apply(profile);lab.scaleMode=0;lab.Restart();lab.encounterCount=4;
    for(int i=0;i<4;i++){var a=lab.Actors[i];double r=lab.tuning.Reach((LabAttack)i)*.8;var p=lab.LabRoot;a.Position=i==0?new Point2(p.X-r,p.Y):i==1?new Point2(p.X+r,p.Y):i==2?new Point2(p.X,p.Y-r):new Point2(p.X,p.Y+r);a.Previous=a.Position;a.Size=i==3?7:1;a.Kind=i; a.Attack=(LabAttack)i;a.State=LabState.Telegraphing;a.Timer=.9;double d=LabNavigation.Distance(a.Position,p);a.AttackDirection=new Point2((p.X-a.Position.X)/d,(p.Y-a.Position.Y)/d);a.Distance=d;a.Active=true;}
    lab.Actors[2].Shot=new PreviewLogicalProjectile(lab.Actors[2].Position,lab.Actors[2].AttackDirection);lab.Actors[2].Shot.Advance(.4,10,18,live.World);lab.paused=true;yield return Capture($"telegraphs-p{profile}","Staged logical attacks; boss has actual7x7 footprint");if(profile==2){lab.onlySelected=true;for(int attack=0;attack<4;attack++){lab.selected=attack;yield return Capture($"attack-isolated-{attack}","Isolated "+((LabAttack)attack).ToString()+"; no damage");}lab.onlySelected=false;lab.selected=0;}lab.encounterCount=8;
   }
   camera.Apply(2);lab.encounterCount=8;lab.footprint=9;lab.scaleMode=1;lab.Restart();lab.paused=true;yield return Capture("foreground-large-body", "9x9 foreground body remains visible before its ground anchor enters");lab.footprint=1;lab.Restart();
   camera.Apply(2);var spots=ProbeLocations();for(int location=0;location<3;location++)for(int mode=0;mode<2;mode++)
   {
    live.Teleport(new Vector2((float)spots[location].X,(float)spots[location].Y));lab.scaleMode=mode;lab.Restart();lab.paused=true;lab.showSizes=true;lab.showLabels=false;yield return Capture($"sizes-l{location}-m{mode}",new[]{"3-cell tunnel size probes","9-cell passage size probes","Chamber size probes"}[location]);lab.showSizes=false;
   }
   live.Teleport(new Vector2(512.5f,512.5f));lab.footprint=1;lab.scaleMode=0;lab.Restart();lab.paused=false;for(int n=0;n<480;n++)lab.Advance(1.0/60);lab.paused=true;
   for(int objective=0;objective<4;objective++)for(int mode=0;mode<3;mode++){lab.objective=objective;lab.guidanceMode=mode;yield return Capture($"guidance-o{objective}-m{mode}",Stage50Preview.ObjectiveNames[objective]+" / guidance"+mode);}
   ExportClearance();main.Dispose();alloc.Dispose();draws.Dispose();Application.Quit();
  }
  IEnumerator Capture(string name,string scenario)
  {
   lab.status=scenario;camera.bookmark=scenario;live.Refresh(0);int wait=0;while((!live.Ready||live.Stream.Pending>0)&&wait++<2500)yield return null;lab.Draw();yield return new WaitForEndOfFrame();string folder=Path.Combine(output,"captures");camera.Capture(folder,name);while(camera.CapturePending)yield return null;File.WriteAllText(Path.Combine(folder,name+".lab.json"),JsonUtility.ToJson(SnapshotFor(lab,scenario),true));yield return null;
  }
  public static Snapshot SnapshotFor(Stage50Preview lab,string scenario)
  {
   var live=lab.Live;var camera=lab.CameraStudy;
   var actors=new ActorSnapshot[lab.encounterCount];for(int i=0;i<actors.Length;i++){var a=lab.Actors[i];actors[i]=new ActorSnapshot{id=i,size=a.Size,kind=a.Kind,state=a.State.ToString(),attack=a.Attack.ToString(),x=a.Position.X,y=a.Position.Y,distance=a.Distance,timer=a.Timer,attacks=a.Attacks,logicalSpeed=a.LogicalSpeed,pixelWidth=lab.PixelSizes[i].x,pixelHeight=lab.PixelSizes[i].y,pixelsPerLabSecond=lab.ProjectedSpeeds[i],fits=PreviewMotion.Fits(live.World,new Vector2((float)a.Position.X,(float)a.Position.Y),a.Size),persistent=a.Persistent,visibility=LabPresentation.Visibility(live.Snapshot,new Point2(live.Position.x,live.Position.y),a.Position,camera.DisplayWidth,camera.DisplayWidth/(16.0/9),live.World).ToString()};}
   return new Snapshot{tuning=lab.tuning,bodyScale=lab.bodyScale,safetyMargin=lab.safetyMargin,decorations=lab.decorations,xray=lab.xray,scenario=scenario,camera=camera.Numeric(),mode=lab.scaleMode,profile=camera.profile,clock=lab.Clock,rootX=lab.LabRoot.X,rootY=lab.LabRoot.Y,actors=actors,active=lab.Active,dormant=lab.Dormant,residents=live.Stream.Resident.Count,ownedMeshes=lab.OwnedMeshes,visibility=lab.TargetVisibility.ToString()};
  }
  Point2[] ProbeLocations()
  {
   var world=live.World;Point2 narrow=new Point2(542.5,256.5);for(int x=world.Width/2+64;x<world.Width*3/4-48;x++){var p=new Vector2(x+.5f,world.Height/4+.5f);if(PreviewMotion.Fits(world,p,3)&&!PreviewMotion.Fits(world,p,5)){narrow=new Point2(p.x,p.y);break;}}
   var chamber=camera.Bookmarks()[0];var nav=new LabNavigation(world,new Point2(512.5,512.5));var winding=new List<Point2>();nav.Winding(1,winding);var turn=narrow;for(int i=1;i<winding.Count-1;i++){var a=winding[i-1];var b=winding[i];var c=winding[i+1];if(Math.Abs((b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X))>.1&&!nav.Fits(b,5)){turn=b;break;}}var entrance=new Point2(world.Width*.5+5.5,world.Height*.25+.5);return new[]{narrow,new Point2(532.5,512.5),new Point2(chamber.x,chamber.y),turn,entrance,new Point2(512.5,467.5),new Point2(512.5,529.5)};
  }
  void ExportClearance()
  {
   var report=new ClearanceReport();var locations=ProbeLocations();string[] names={"Three-cell tunnel","Nine-cell passage","Chamber near pillar","Winding turn route","Narrow entrance probe","Compressed upper passage","Enlarged foreground"};for(int n=0;n<locations.Length;n++){var p=locations[n];var nav=new LabNavigation(live.World,p);foreach(int size in lab.Sizes){var bounds=Stage50Preview.VisualBounds(p,size,3);bool overlap=false;for(int y=(int)Math.Floor(bounds.Min.Y);y<=Math.Ceiling(bounds.Max.Y);y++)for(int x=(int)Math.Floor(bounds.Min.X);x<=Math.Ceiling(bounds.Max.X);x++)overlap|=live.World.IsSolid(x,y);report.rows.Add(new Clearance{location=names[n],footprint=size,x=p.X,y=p.Y,collisionFits=PreviewMotion.Fits(live.World,new Vector2((float)p.X,(float)p.Y),size),navigationFits=nav.Fits(p,size),visualBoundsTouchesDirt=overlap});}}File.WriteAllText(Path.Combine(output,"clearance.json"),JsonUtility.ToJson(report,true));
  }
 }
}
