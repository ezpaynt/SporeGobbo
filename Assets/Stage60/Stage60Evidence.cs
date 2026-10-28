using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using Unity.Profiling;
using SporeGobbo.Input;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.CampCandidate.Preview;
using SporeGobbo.CampLifecycle;

/// <summary>Deterministic fixture controls + real semantic input and actor movement. Output is owned test storage only.</summary>
[DefaultExecutionOrder(-1400)]
public sealed class Stage60Evidence:MonoBehaviour
{
    [Serializable]public class Frame
    {public string phase,construction;public int frame,revision,beds,buddies;public double interval,main,allocation,camera,sorting,presentation,meshBuild,meshUpload,collision,navigation;public bool ready,clear;public float footError;}
    [Serializable]public class Result
    {public bool finished;public string error="",hardware,unity;public List<string> checks=new List<string>();public List<string> notes=new List<string>();public List<Frame> frames=new List<Frame>(20000);public List<string> captures=new List<string>();public List<double> rootMs=new List<double>(),planMs=new List<double>(),resetMs=new List<double>();}
    Stage60Preview camp;Stage59Preview proof;Result data=new Result();Keyboard keyboard;Mouse mouse;InputActionAsset actions;InputDevice[] priorDevices;InputSettings.BackgroundBehavior priorBackground;bool inputChanged,done,raw;string output,phase="initial";Stage58Telemetry telemetry;ProfilerRecorder main,alloc;int lastRevision;readonly WaitForEndOfFrame end=new WaitForEndOfFrame();
    IEnumerator Start()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-stage60Capture")<0)yield break;
        output=SporeGobbo.Safety.Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(output);Application.logMessageReceived+=OnLog;
        camp=GetComponent<Stage60Preview>();camp.Initialize();camp.Automated=true;camp.ShowPanel=false;camp.ShowGuide=false;proof=camp.Proof;proof.Automated=true;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;data.hardware=SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName;data.unity=Application.unityVersion;
        priorBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;inputChanged=true;
        keyboard=InputSystem.AddDevice<Keyboard>("Stage60 automated semantic keyboard");mouse=InputSystem.AddDevice<Mouse>("Stage60 automated semantic pointer");
        actions=(InputActionAsset)typeof(SporeInputReader).GetField("actions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(SporeInputReader.Instance);var old=actions.devices;priorDevices=old.HasValue?old.Value.ToArray():null;actions.devices=new InputDevice[]{keyboard,mouse};
        telemetry=new Stage58Telemetry(output);main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");alloc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
        data.notes.Add("Fixture pre-excavation, fake returns, test death and reset are simulated controls. Gobbo movement/aim/dig/Interact and Buddy follow/DigRoutine/directed construction walks are real components. No campaign GameState or saves.");
        yield return Settle();Check(GameState.Instance==null&&MapGenerator.Instance==null&&DiggableTerrainService.Active==null,"No production service registration");Check(!Stage60Layout.PotentialBypass(camp.Session.Definition),"Intact root has no hypothetical ordinary-dirt bypass");
        Check(!proof.Live.automation&&!proof.DiagnosticCamera,"Exact normal-delta reference camera remains active");
        Raw("root-and-real-movement");yield return Capture("01-intact-root-fire-view");
        yield return MovePlayer(Stage60Layout.RootFront);int rootRevision=camp.Session.Revision;yield return Press(Key.F);
        Check(!camp.RootOpened&&camp.Session.Revision==rootRevision,"Actual Interact without a Buddy cannot remove roots");
        camp.AddBuddy();yield return Press(Key.F);Check(!camp.RootOpened,"Owned but physically distant Buddy cannot authorize roots");yield return Wait(()=>camp.EligibleBuddy(),30,"Real Buddy approaches root");yield return Capture("02-root-interaction-with-buddy");
        proof.SetModal(true);yield return null;yield return Press(Key.F);Check(!camp.RootOpened,"Modal context blocks root interaction");proof.SetModal(false);yield return Press();
        proof.SetPaused(true);yield return null;yield return Press(Key.F);Check(!camp.RootOpened&&Time.timeScale==0,"Pause blocks root interaction");proof.SetPaused(false);yield return Press();
        int prior=rootRevision;yield return Press(Key.F);yield return Settle();
        Check(camp.RootOpened&&camp.RootActivations==1&&camp.Session.Revision==prior+1,"Semantic Interact opens root in exactly one revision");
        data.rootMs.Add(camp.RootMs);yield return Press(Key.F);Check(camp.RootActivations==1&&camp.Session.Revision==prior+1,"Repeated root interaction does not activate twice");
        Check(proof.Authority.PublishedRevision==camp.Session.Revision,"Root collision revision matches terrain");
        Check(!proof.Player.GetComponent<ActorTerrainBinding>().AcceptsRoute(prior),"Root transaction invalidates stale routes");
        Check(Stage60Layout.RootCells().All(c=>!proof.Authority.IsBlocked(new Vector2Int(c.X,c.Y))),"Root occupancy is open for navigation and collision");yield return Capture("03-opened-entrance");
        yield return MovePlayer(new Point2(108.5,92.5));Check(TileMover.CanOccupyBox(proof.Authority,proof.Player.GetComponent<Rigidbody2D>().position,Vector2.one*.5f),"Real player crosses the opened root with full clearance");
        yield return MovePlayer(Stage60Preview.FireWatch);yield return Settle();yield return Capture("04a-fire-before-construction");StopRaw();
        Raw("first-five-real-beds");phase="untouched-bedroom";camp.Residence.Speed=0;Check(camp.Residence.Begin(),"Untouched bed starts real construction");
        yield return Wait(()=>camp.Residence.Phase.StartsWith("Excavating"),45,"Untouched bedroom reaches Dig");
        yield return Capture("04-fire-untouched-bedroom-excavation",true);
        int pausedRevision=camp.Session.Revision;var worker=camp.Residence.Worker;Vector2 pausedPosition=worker.Actor.GetComponent<Rigidbody2D>().position;
        proof.SetPaused(true);yield return new WaitForSecondsRealtime(.3f);Check(camp.Session.Revision==pausedRevision&&Vector2.Distance(pausedPosition,worker.Actor.GetComponent<Rigidbody2D>().position)<.001f,"Pause freezes actual construction excavation and walking");proof.SetPaused(false);
        yield return Wait(()=>!camp.Residence.Busy,80,"Untouched bed completes");Check(camp.Session.Established==1&&camp.Residence.DigActions>0,"Untouched room excavated and bed owned");
        yield return Capture("05-first-bed-established-fire-view");
        // Demonstrate actual BuddyFollow navigating back to Fire, then to its owned home.
        yield return MovePlayer(camp.Session.Definition.Marker("fire").Interaction);yield return Press(Key.F);yield return Wait(()=>Vector2.Distance(worker.Actor.transform.position,proof.Player.transform.position)<1.5f,25,"Housed Buddy returns to Fire");
        Check(worker.Follow.GetComponent<ActorTerrainBinding>().LastRouteRevision==camp.Session.Revision,"Buddy follows a current-revision residential route");camp.Residence.SendHome(worker);
        var rest=camp.Residence.Terrain.Catalog.GetSlot(1).RestCell;yield return Wait(()=>Vector2.Distance(worker.Actor.transform.position,proof.Authority.CellToWorld(new Vector2Int(rest.x,rest.y)))<.9f,25,"Buddy can revisit and use its owned bed");
        camp.Residence.Speed=2;
        for(int slot=2;slot<=3;slot++){camp.AddBuddy();Check(camp.Residence.Begin(),"Start shared-pocket bed "+slot);yield return Wait(()=>!camp.Residence.Busy,80,"Complete shared-pocket bed "+slot);Check(camp.Session.Established==slot,"Unique bed ownership "+slot+" / "+camp.Residence.Failure);}
        yield return MovePlayer(new Point2(112.5,92.5));
        phase="partial-bedroom";camp.AddBuddy();camp.Residence.PreDigNext(.5);int partialBefore=camp.Residence.Terrain.GetResidentialSlotFootprint(4).Count(c=>!camp.Residence.Terrain.IsBlocked(c));int digBefore=camp.Residence.DigActions;
        Check(camp.Residence.Begin(),"Partially excavated room begins");yield return Wait(()=>camp.Residence.Phase.StartsWith("Excavating"),40,"Partial room Dig phase");yield return Capture("06-partial-bedroom-completion",true);
        yield return Wait(()=>!camp.Residence.Busy,80,"Partially excavated bed completes");Check(partialBefore>0&&camp.Session.Established==4&&camp.Residence.DigActions>digBefore,"Partial room keeps existing openings and finishes actual dirt");
        camp.AddBuddy();Check(camp.Residence.Begin(),"Start fifth bed");yield return Wait(()=>!camp.Residence.Busy,80,"Five real beds complete");Check(camp.Session.Established==5&&camp.Session.Residents.Where(r=>r.Alive&&r.Slot>0).Select(r=>r.Slot).Distinct().Count()==5,"Five real Buddies own five unique beds");yield return Capture("07-five-beds");StopRaw();
        Raw("ten-real-beds-and-returns");phase="sixth-bedroom";camp.AddBuddy();camp.Residence.Begin();yield return Wait(()=>!camp.Residence.Busy,80,"Sixth bed completes");Check(camp.Session.Established==6,"Second three-Buddy pocket complete");
        yield return MovePlayer(new Point2(115.5,76.5));phase="fully-open-bedroom";camp.AddBuddy();camp.Residence.PreDigNext(1);int zeroBefore=camp.Residence.ZeroDigPreparations;digBefore=camp.Residence.DigActions;
        Check(camp.Residence.Begin(),"Fully excavated room begins");yield return Wait(()=>camp.Residence.Phase.StartsWith("Preparing"),50,"Fully open room visible preparation");yield return Capture("08-fully-excavated-bed-preparation",true);
        yield return Wait(()=>!camp.Residence.Busy,30,"Fully open bed completes");Check(camp.Session.Established==7&&camp.Residence.ZeroDigPreparations==zeroBefore+1&&camp.Residence.DigActions==digBefore,"Fully open room performs preparation without a nonexistent-dirt Dig");
        for(int slot=8;slot<=10;slot++){phase="bed-"+slot;camp.AddBuddy();Check(camp.Residence.Begin(),"Start bed "+slot);yield return Wait(()=>!camp.Residence.Busy,80,"Complete bed "+slot);Check(camp.Session.Established==slot,"Bed established "+slot);}
        Check(camp.Session.Residents.Where(r=>r.Alive).Select(r=>r.Slot).Distinct().Count()==10,"Ten real Buddies retain unique ownership");yield return Capture("09-ten-beds");
        phase="ten-buddies-returning-to-fire";camp.Residence.CallToFire();yield return MovePlayer(camp.Session.Definition.Marker("fire").Interaction);phase="ten-buddies-returning-to-fire";double crowd=Time.realtimeSinceStartupAsDouble;while(Time.realtimeSinceStartupAsDouble-crowd<12){yield return end;Record();}
        Check(camp.Residence.Residents.Where(r=>r.Record.Alive).All(r=>proof.Authority.Route(r.Actor.GetComponent<Rigidbody2D>().position,proof.Player.transform.position,Vector2.one*.28f).Count>0),"All ten active Buddies retain a clear terrain route back to Fire");
        int established=camp.Session.Established;camp.Residence.Die();camp.AddBuddy();Check(camp.Session.Established==established&&camp.Session.Residents.Last().Slot==1,"Death vacates an established bed; new identity reuses vacancy");
        phase="fake-returns";for(int i=0;i<3;i++){camp.FakeReturn();yield return Settle();}Check(camp.Session.Returns==3&&camp.Session.Established==10,"Repeated fake returns retain isolated state and bed assignments");StopRaw();
        // Actual ordinary Gobbo dig remains bound to the same residential terrain, not renderer occupancy.
        Raw("real-gobbo-dig-and-reset");data.planMs.AddRange(camp.Residence.PlanSamples);phase="real-gobbo-dig";
        Point2 stand=default;Vector2 target=default;bool found=false;
        // Select an actual still-solid residential cell next to a reachable standing cell.
        for(int y=55;y<94&&!found;y++)for(int x=92;x<138&&!found;x++)if(camp.Session.Definition.Category(x,y)==SporeGobbo.CampCandidate.CampCell.Residential&&camp.Session.Terrain.IsSolid(x,y))
        foreach(var offset in new[]{new Vector2Int(-2,0),new Vector2Int(2,0),new Vector2Int(0,-2),new Vector2Int(0,2)})
        {var p=new Point2(x+offset.x+.5,y+offset.y+.5);if(!camp.Session.Fits(p,3))continue;var route=proof.Authority.Route(proof.Player.transform.position,proof.Authority.ToWorld(p),Vector2.one*.5f);if(route.Count==0)continue;stand=p;target=proof.Authority.CellToWorld(new Vector2Int(x,y));found=true;break;}
        Check(found,"Real residential dirt has a reachable Gobbo digging front");yield return MovePlayer(stand);int before=camp.Session.Revision;
        for(int i=0;i<60;i++){QueueMouse(target);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return end;Record();}yield return Press();
        Check(proof.Player.GetComponent<ActorTerrainBinding>().DigAttempts>0&&camp.Session.Revision>before,"Real Gobbo Dig removes actual residential earth");
        Check(proof.Authority.PublishedRevision==camp.Session.Revision,"Real Gobbo excavation shares revision-aware collision");
        // Cancellation/removal and reset use live components; no completion shortcut or teleporting worker.
        phase="repeated-reset";
        for(int i=0;i<3;i++){camp.ResetTest();yield return Settle();data.resetMs.Add(camp.ResetMs);Check(!camp.RootOpened&&camp.Session.Established==0&&camp.Session.Living==0&&proof.Views.Count==1,"Reset releases old residents and restores root "+i);}
        camp.AddBuddy();yield return MovePlayer(Stage60Layout.RootFront);yield return Wait(()=>camp.EligibleBuddy(),30,"Reset Buddy reaches entrance");yield return Press(Key.F);yield return Settle();camp.Residence.Begin();yield return Wait(()=>camp.Residence.Phase.StartsWith("Excavating"),45,"Cancellation fixture reaches actual excavation");camp.Residence.Cancel("Synthetic cancellation");Check(camp.Session.Established==0&&!camp.Residence.Busy,"Construction cancellation never claims a bed");
        camp.Residence.Begin();yield return Wait(()=>camp.Residence.Phase.StartsWith("Excavating"),45,"Worker-removal fixture reaches actual excavation");camp.Residence.Die();yield return null;Check(!camp.Residence.Busy&&camp.Session.Established==0,"Worker removal cancels construction without corrupting ownership");
        camp.ResetTest();yield return Settle();camp.ShowPanel=true;camp.ShowGuide=true;yield return Capture("10-player-test-guide");camp.ShowPanel=false;
        Check(GameState.Instance==null&&MapGenerator.Instance==null&&DiggableTerrainService.Active==null,"No production registration after construction, deaths, returns and resets");
        Check(Stage59ActorView.OwnedMeshes==2&&Stage59ActorView.OwnedMaterials==3,"Repeated resets release resident visual resources; one player remains");
        Check(camp.GetComponentsInChildren<Transform>(true).Count(t=>t.name.EndsWith("scoped home/walk target"))==0,"Reset removes owned construction targets");
        Check(data.frames.All(f=>f.clear),"Every sampled actual actor position has logical terrain clearance");Check(data.frames.All(f=>f.footError<.002f),"Projected feet agree with the unchanged camera contract");StopRaw();data.finished=true;Write();done=true;Application.Quit(data.error==""?0:1);
    }
    IEnumerator Press(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return end;Record();InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return end;Record();}
    void QueueMouse(Vector2 world){var s=proof.Live.previewCamera.WorldToScreenPoint(proof.Render(world));InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(s.x,s.y)});}
    IEnumerator MovePlayer(Point2 goal)
    {
        phase="real-player-movement";var rb=proof.Player.GetComponent<Rigidbody2D>();var path=proof.Authority.Route(rb.position,proof.Authority.ToWorld(goal),Vector2.one*.5f);Check(path.Count>0,"Player route to ("+goal.X+","+goal.Y+")");int index=0;double start=Time.realtimeSinceStartupAsDouble;
        while(index<path.Count&&Time.realtimeSinceStartupAsDouble-start<35)
        {
            Vector2 delta=path[index]-rb.position;if(delta.magnitude<.16f){index++;continue;}
            var keys=new List<Key>();if(delta.x>.06)keys.Add(Key.D);if(delta.x<-.06)keys.Add(Key.A);if(delta.y>.06)keys.Add(Key.W);if(delta.y<-.06)keys.Add(Key.S);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));QueueMouse(path[index]);yield return end;Record();
        }
        yield return Press();Check(Vector2.Distance(rb.position,proof.Authority.ToWorld(goal))<.45f,"Actual Gobbo reaches "+goal);yield return Settle();
    }
    IEnumerator Wait(Func<bool> predicate,float seconds,string label)
    {
        double start=Time.realtimeSinceStartupAsDouble;while(!predicate()&&Time.realtimeSinceStartupAsDouble-start<seconds){if(camp.Residence.Failure!="")Check(false,camp.Residence.Failure);yield return end;Record();}
        Check(predicate(),label);
    }
    IEnumerator Settle(){int guard=0;while(!proof.Live.Ready||proof.Live.Stream.Pending>0){yield return end;Record();if(++guard>1800)Check(false,"Streaming readiness timeout");}for(int i=0;i<12;i++){yield return end;Record();}}
    IEnumerator Capture(string name,bool freeze=false)
    {
        bool prior=proof.IsPauseOpen;if(freeze)proof.SetPaused(true);yield return Settle();proof.Camp.CameraStudy.Capture(output,name);while(proof.Camp.CameraStudy.CapturePending)yield return null;proof.Camp.CameraStudy.captureLabel=false;data.captures.Add(name);
        File.WriteAllText(Path.Combine(output,name+".state.txt"),"phase="+camp.Residence.Phase+"\nrevision="+camp.Session.Revision+"\nroot="+camp.RootOpened+"\nbeds="+camp.Session.Established+"\nplayer="+proof.Authority.ToLogical(proof.Player.transform.position)+"\nworker="+(camp.Residence.Worker?.Record.Id??"none")+"\nfireScreen="+proof.Camp.ScreenPoint(camp.Session.Definition.Marker("fire").Anchor)+"\nworkerScreen="+(camp.Residence.Worker?.Actor!=null?proof.Camp.ScreenPoint(proof.Authority.ToLogical(camp.Residence.Worker.Actor.transform.position)).ToString():"none"));
        if(freeze&&!prior)proof.SetPaused(false);
    }
    void Record()
    {
        if(proof.Live.Snapshot==null)return;bool clear=true;float error=0;double presentation=0;
        foreach(var view in proof.Views)
        {if(view==null)continue;var rb=view.GetComponent<Rigidbody2D>();if(rb!=null)clear&=TileMover.CanOccupyBox(proof.Authority,rb.position,TileMover.GetMapClearanceExtents(rb,.01f));var p=proof.Render(view.PhysicalFoot);error=Mathf.Max(error,Vector2.Distance(view.ProjectedFoot,p),Mathf.Abs(view.RenderedBottomY-p.y));presentation+=view.UpdateMs;}
        data.frames.Add(new Frame{phase=phase,construction=camp.Residence.Phase,frame=Time.frameCount,revision=camp.Session.Revision,beds=camp.Session.Established,buddies=camp.Session.Living,interval=Time.unscaledDeltaTime*1000,main=main.Valid?main.LastValue*1e-6:-1,allocation=alloc.Valid?alloc.LastValue:-1,camera=proof.CameraMs,sorting=proof.SortingMs,presentation=presentation,navigation=telemetry.Ms[(int)GeometryPhase.Navigation],meshBuild=telemetry.Ms[(int)GeometryPhase.MeshBuild],meshUpload=telemetry.Ms[(int)GeometryPhase.MeshUpload],collision=lastRevision!=camp.Session.Revision?proof.Authority.ColliderMs:0,ready=proof.Live.Ready,clear=clear,footError=error});lastRevision=camp.Session.Revision;telemetry.Reset();
    }
    void Raw(string name){StopRaw();Profiler.logFile=Path.Combine(output,name+".raw");Profiler.maxUsedMemory=128*1024*1024;Profiler.enableBinaryLog=true;Profiler.enabled=true;raw=true;}
    void StopRaw(){if(!raw)return;Profiler.enabled=false;Profiler.logFile="";raw=false;}
    void Check(bool ok,string label){data.checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok){data.error+=label+"; ";Write();throw new InvalidOperationException(label);}}
    void Write(){if(output!=null)File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(data,true));}
    void OnLog(string message,string stack,LogType type){if(done||type!=LogType.Error&&type!=LogType.Exception)return;data.error+=message+"\n"+stack;StopRaw();Write();done=true;Application.Quit(1);}
    void OnDestroy(){Application.logMessageReceived-=OnLog;StopRaw();if(actions!=null)actions.devices=priorDevices;if(inputChanged)InputSystem.settings.backgroundBehavior=priorBackground;if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);telemetry?.Dispose();main.Dispose();alloc.Dispose();}
}
