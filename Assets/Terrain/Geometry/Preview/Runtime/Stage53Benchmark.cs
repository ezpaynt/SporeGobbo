using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Profiling;
namespace SporeGobbo.Geometry.Preview
{
 [DefaultExecutionOrder(1500)]public sealed class Stage53Benchmark:MonoBehaviour
 {
  Stage53Preview env;Stage47Preview live;Stage49Preview cam;string output,phase="";ProfilerRecorder alloc,main;double last;bool done;readonly Results result=new Results();
  [Serializable]public class Results{public string error="";public List<string> checks=new List<string>();public List<Frame> frames=new List<Frame>();public List<Metric> metrics=new List<Metric>();public List<Resource> resources=new List<Resource>();public List<Shot> shots=new List<Shot>();public List<long> probes=new List<long>();}
  [Serializable]public class Frame{public string phase;public double interval,host,discovery,frontier,publication,cull,main;public long allocated;public int resident,structureUploads,frontierTiles;public bool ready;}
  [Serializable]public class Metric{public string name;public double ms;public int value;}
  [Serializable]public class Resource{public string name;public long managed,unity;public int ownMeshes,labMeshes,buffers,materials,residents,pool;}
  [Serializable]public class Shot{public string name;public int anchor,reveal,residents,known,shown,visibleParts,frontierTiles;public float x,y;public double above,below,normalAbove,normalBelow,height;public string diagnostics;}
  IEnumerator Start()
  {
   var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,"-stage53Output");if(n<0)yield break;output=args[n+1];Directory.CreateDirectory(output);env=GetComponent<Stage53Preview>();env.Initialize();env.automation=true;live=env.Live;cam=env.CameraStudy;live.automation=true;env.Lab.paused=true;env.showPanel=false;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;alloc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");yield return null;yield return Settle();ResourceSample("initial");AddMetric("initialization",env.InitializationMs);AddMetric("structural-mesh-build",env.MeshBuildMs);
   for(int i=0;i<4;i++){var probe=new byte[1024*1024];probe[0]=1;yield return null;result.probes.Add(alloc.Valid?alloc.LastValue:-1);GC.KeepAlive(probe);}
   for(int a=0;a<3;a++){env.Go(0);env.SetAnchor(a);yield return Settle();yield return Capture("01-spike-hidden-anchor-"+a);yield return Frames("hidden-spike-camera-"+a,120,new Vector2(.3f,0),false);}
   // Same physical fixture, three reveal schedules. Capture while animation remains deliberately paused.
   for(int style=0;style<3;style++)
   {env.reveal=(BreakthroughStyle)style;env.ResetDemo();env.Go(2);yield return Settle();Dig("network-breakthrough-"+style,86,19);env.Process(.1,false);yield return Capture("02-network-frontier-style-"+style);yield return Frames("reveal-style-"+style,90,Vector2.zero);yield return Settle();yield return Capture("03-network-finished-style-"+style);Check(!env.State.Discovery.Known(env.State.Fixture.X,env.State.Fixture.Y-42),"Network style "+style+" remains bounded");}
   env.reveal=BreakthroughStyle.Hybrid;env.ResetDemo();env.Go(0);yield return Settle();Dig("spike-breakthrough",-45,19);yield return Frames("spike-discovery",100,Vector2.zero);yield return Settle();yield return Capture("04-spike-entrance-reveal");
   yield return Drive(env.State.Fixture.P(-45,5),"spike-physical-exploration");yield return Drive(env.State.Fixture.P(-53,-7),"spike-physical-exploration");yield return Settle();Check(StructureMesh.GroundVisible(env.State.Fixture.Landmarks[0],env.State.Discovery),"Spike lower body appears only after adjacent floor shown");Check(!live.World.Excavate(env.State.Fixture.X-45,env.State.Fixture.Y-10),"Spike base cannot be excavated");
   for(int a=0;a<3;a++){env.SetAnchor(a);yield return Settle();yield return Capture("05-spike-base-anchor-"+a);}
   Dig("excavate-spike-edge",-50,-10);yield return Settle();yield return Capture("06-spike-local-excavation");
   // Each height is rebuilt by reset, never by camera movement.
   env.SetAnchor(0);foreach(float height in new[]{12f,36f}){env.spikeHeight=height;env.ResetDemo();env.Go(0);yield return Settle();yield return Capture("07-hidden-spike-height-"+height);AddMetric("height-"+height+"-build",env.MeshBuildMs);}
   env.spikeHeight=24;env.ResetDemo();env.Go(1);yield return Settle();
   foreach(int size in new[]{1,3,5,7,9}){Check(env.TestRoute(size,true),"Root narrow approach complete alternative route size "+size);AddMetric("navigation-narrow-"+size,env.NavigationMs);Check(env.TestRoute(size,false),"Root wide full route size "+size);AddMetric("navigation-wide-"+size,env.NavigationMs);}
   for(int a=0;a<3;a++){env.Go(1);env.SetAnchor(a);yield return Settle();yield return Capture("08-root-anchor-"+a);yield return Frames("roots-camera-"+a,120,new Vector2(.1f,.2f));}
   env.Go(1);env.TestRoute(3,true,true);env.Lab.tuning.detection=0;yield return Frames("buddy-narrow-root-route",300,Vector2.zero);yield return Capture("09a-buddy-under-root");yield return Frames("buddy-narrow-root-route",300,Vector2.zero);Check(env.Lab.Actors[0].RouteIndex>=env.Lab.Actors[0].Route.Count-1,"Buddy completes actual physical route");yield return Capture("09-buddy-root-passage");
   env.Go(1);env.TestRoute(9,false,true);env.Lab.tuning.detection=0;yield return Frames("large-body-wide-root-route",300,Vector2.zero);for(int a=0;a<3;a++){env.SetAnchor(a);yield return Capture("10a-large-under-root-anchor-"+a);}yield return Frames("large-body-wide-root-route",300,Vector2.zero);Check(env.Lab.Actors[0].RouteIndex>=env.Lab.Actors[0].Route.Count-1,"Nine-cell body completes actual physical route");yield return Capture("10-large-root-passage");env.Lab.encounterCount=0;
   live.Teleport(Stage53Preview.V(env.State.Fixture.P(45,-1)));env.Process(1.0/60);yield return Settle();yield return Capture("11-overhead-cutaway-on");env.cutaway=false;yield return Capture("12-overhead-cutaway-off");env.cutaway=true;
   // Controlled downward, upward and side exploration on same geography and profile.
   env.Go(2);Dig("downward-network-entry",86,29);yield return Frames("downward-breakthrough",90,Vector2.zero);for(int a=0;a<3;a++){env.SetAnchor(a);live.Teleport(Stage53Preview.V(env.State.Fixture.P(86,29)));env.Process(1.0/60);yield return Settle();yield return Capture("13-downward-anchor-"+a);}
   yield return Drive(env.State.Fixture.P(86,51),"downward-exploration");yield return Drive(env.State.Fixture.P(65,56),"side-exploration");yield return Settle();yield return Capture("14-side-network-progression");
   // Actual shortcut and protected/no-op digging.
   live.Teleport(Stage53Preview.V(env.State.Fixture.P(-2,24)));env.Process(1.0/60);yield return Settle();for(int x=3;x<=5;x++)Dig("shortcut-"+x,x,24);Check(new LabNavigation(live.World,env.State.Fixture.P(-2,24)).TraceToRoot(env.State.Fixture.P(9,24),1,new List<Point2>()),"Excavated shortcut is connected");
   env.Go(1);yield return Settle();int builds=env.StructureBuilds,rebuilds=live.Stream.PresentationRebuilds;int uploads=0;for(int a=0;a<3;a++){env.SetAnchor(a);yield return Frames("camera-only-anchor-"+a,120,Vector2.zero,false);uploads+=env.StructureUploads;}Check(env.StructureBuilds==builds&&live.Stream.PresentationRebuilds==rebuilds,"Anchor-only movement does not rebuild terrain or structures");Check(uploads==0,"Retained structure streams not uploaded for anchor-only change");
   for(int r=0;r<5;r++){long t=System.Diagnostics.Stopwatch.GetTimestamp();env.ResetDemo();AddMetric("reset-"+r,StreamingGeometry.Elapsed(t));yield return Settle();ResourceSample("reset-"+r);Check(env.OwnedMeshes==10&&env.Lab.OwnedMeshes==46&&live.Stream.Resident.Count<=384&&live.PooledPresentations<=8,"Bounded owned resources reset "+r);}
   Finish();
  }
  void AddMetric(string name,double ms,int value=0)=>result.metrics.Add(new Metric{name=name,ms=ms,value=value});
  void Dig(string name,int x,int y){long t=System.Diagnostics.Stopwatch.GetTimestamp();int count=live.Stream.Dig(new List<DualTile>{new DualTile(env.State.Fixture.X+x,env.State.Fixture.Y+y)});env.Process(0,false);AddMetric(name,StreamingGeometry.Elapsed(t),count);Check(count==1,"Actual dig "+name);}
  IEnumerator Settle(){int n=0;do{env.Process(1.0/60,false);yield return null;if(++n>3000){Check(false,"Ready timeout");yield break;}}while(!live.Ready||live.Stream.PendingPresentation>0||env.State.Discovery.PendingReleases>0);for(int f=0;f<4;f++)yield return null;}
  IEnumerator Frames(string label,int count,Vector2 move,bool model=true){phase=label;last=Time.realtimeSinceStartupAsDouble;for(int i=0;i<count;i++){if(live.Ready&&move.sqrMagnitude>0)live.Step(move,Vector2.right,false,1f/60);if(model)env.Process(1.0/60);if(env.Lab.encounterCount>0)env.Lab.Advance(1.0/60);yield return null;double now=Time.realtimeSinceStartupAsDouble;result.frames.Add(new Frame{phase=phase,interval=(now-last)*1000,host=live.DisplayMs,discovery=env.DiscoveryMs,frontier=env.FrontierMs,publication=live.Stream.PresentationMs,cull=env.CullingMs,main=main.Valid?main.LastValue*1e-6:-1,allocated=alloc.Valid?alloc.LastValue:-1,resident=live.Stream.Resident.Count,ready=live.Ready,structureUploads=env.StructureUploads,frontierTiles=env.FrontierTiles});last=now;}}
  IEnumerator Drive(Point2 target,string label){int n=0;while(Vector2.Distance(live.Position,Stage53Preview.V(target))>.09f&&n++<2400){var delta=Stage53Preview.V(target)-live.Position;yield return Frames(label,1,Vector2.ClampMagnitude(delta/(5f/60),1));}Check(n<2400,"Physical route movement "+label);}
  IEnumerator Capture(string name){yield return null;while(!live.Ready)yield return null;for(int n=0;n<4;n++)yield return null;cam.Capture(Path.Combine(output,"captures"),name);cam.captureLabel=false;while(cam.CapturePending)yield return null;cam.captureLabel=false;var p=live.Snapshot;double w=cam.DisplayWidth,h=w/(16.0/9);var min=p.Unproject(new Point2(-w*.5,-h*.5));var max=p.Unproject(new Point2(w*.5,h*.5));result.shots.Add(new Shot{name=name,anchor=env.anchor,reveal=(int)env.reveal,residents=live.Stream.Resident.Count,known=env.State.Discovery.Remembered.Count,shown=env.State.Discovery.Visual.Count,visibleParts=env.VisibleParts,frontierTiles=env.FrontierTiles,x=live.Position.x,y=live.Position.y,above=live.Position.y-min.Y,below=max.Y-live.Position.y,normalAbove=Math.Min(p.HalfHeight,p.Offset.Y+h*.5),normalBelow=Math.Min(p.HalfHeight,h*.5-p.Offset.Y),height=env.spikeHeight,diagnostics=cam.Diagnostics()});}
  void ResourceSample(string name){GC.Collect();result.resources.Add(new Resource{name=name,managed=GC.GetTotalMemory(true),unity=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),ownMeshes=env.GetComponentsInChildren<MeshFilter>().Count(f=>f.name.StartsWith("Stage53 structure batch")),labMeshes=env.Lab.OwnedMeshes,buffers=live.Gpu.BufferCount,materials=live.Gpu.ResourceCount,residents=live.Stream.Resident.Count,pool=live.PooledPresentations});}
  void Check(bool okay,string message){result.checks.Add((okay?"PASS ":"FAIL ")+message);if(!okay){result.error=message;Finish();throw new Exception(message);}}
  void Finish(){if(done)return;done=true;File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(result,true));alloc.Dispose();main.Dispose();Application.Quit(string.IsNullOrEmpty(result.error)?0:1);}
  void OnDestroy(){if(!done){if(alloc.Valid)alloc.Dispose();if(main.Valid)main.Dispose();}}
 }
}
