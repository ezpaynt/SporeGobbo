using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using Unity.Profiling;
using SporeGobbo.Input;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.CampLifecycle;

/// <summary>Feeds real input devices/actions and real components, never a replacement movement brain.</summary>
[DefaultExecutionOrder(-1400)]
public sealed class Stage59Benchmark:MonoBehaviour
{
    [Serializable]public class Frame
    {
        public string phase,scheme,animation,context;public bool ready;public float bodySpeed;public int frame,counterFrame,revision,collisionRevision,spriteChanges;public float size;public bool diagnostic,clear;
        public double time,dt;public double meshBuild,meshUpload;public double interval,main,gpu,allocation,camera,sorting,presentation,terrain,collider;
        public Vector2 player,buddy,aim,focus;public Vector2[] points;public float speed,acceleration,footError,pointerError;
    }
    [Serializable]public class Result
    {public string error="",hardware,unity;public List<Frame> frames=new List<Frame>(12000);public List<string> checks=new List<string>();public List<string> removedComponents=new List<string>();public List<string> events=new List<string>();}
    SporeGobbo.CampCandidate.Preview.Stage58Telemetry telemetry;int previousRevision;
    InputActionAsset runtimeActions;InputDevice[] priorDevices;InputSettings.BackgroundBehavior priorBackground;bool changedInputSetup;
    Stage59Preview proof;Result data=new Result();string output;Keyboard keyboard;Mouse mouse;Gamepad gamepad;
    ProfilerRecorder main,alloc;readonly FrameTiming[] timing=new FrameTiming[1];readonly WaitForEndOfFrame end=new WaitForEndOfFrame();Vector2[] previous;Vector2 velocity;bool capturing,finishing;
    readonly Point2[] fixedPoints={new Point2(84.5,111.5),new Point2(35,65),new Point2(140,65),new Point2(84,145)};
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-stage59Output");if(arg<0)yield break;
        output=args[arg+1];Directory.CreateDirectory(output);Application.logMessageReceived+=OnLog;
        telemetry=new SporeGobbo.CampCandidate.Preview.Stage58Telemetry(output);
        proof=GetComponent<Stage59Preview>();proof.Initialize();proof.Automated=true;proof.ShowPanel=false;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        data.hardware=SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName;data.unity=Application.unityVersion;
        priorBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;changedInputSetup=true;
        keyboard=InputSystem.AddDevice<Keyboard>("Stage59 automated keyboard");mouse=InputSystem.AddDevice<Mouse>("Stage59 automated pointer");gamepad=InputSystem.AddDevice<Gamepad>("Stage59 automated controller");
        runtimeActions=(InputActionAsset)typeof(SporeInputReader).GetField("actions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(SporeInputReader.Instance);var devices=runtimeActions.devices;priorDevices=devices.HasValue?devices.Value.ToArray():null;runtimeActions.devices=new InputDevice[]{keyboard,mouse,gamepad};data.events.Add("Automation only: IgnoreFocus and virtual-device filter; production binding/context logic unchanged; restored on teardown.");
        main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");alloc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
        yield return Settle();
        Check(!proof.Live.automation,"Reference camera uses normal frame delta, with placeholder input blocked separately");
        Check(proof.Player.GetComponent<GobboController>()!=null&&proof.Buddy.GetComponent<BuddyFollow>()!=null,"Actual production controllers and prefab instances");
        Check(proof.Player.GetComponent<Rigidbody2D>()!=null&&proof.Buddy.GetComponent<Rigidbody2D>()!=null,"Actual Rigidbody2D bodies");
        Check(GameState.Instance==null&&DiggableTerrainService.Active==null&&MapGenerator.Instance==null,"No campaign, global terrain or MapGenerator registration");
        data.events.Add("Initialization "+proof.InitializationMs+" ms; collision "+proof.InitialCollisionMs+" ms; actor spawn "+proof.ActorSpawnMs+" ms (direct scopes, no cold raw capture)");
        yield return Capture("01-real-actors-reference");proof.ShowOutlines=false;yield return end;yield return Capture("02-real-actors-clean");proof.ShowOutlines=true;
        foreach(bool diagnostic in new[]{false,true})foreach(float size in new[]{.75f,1f,1.5f})
        {
            proof.DiagnosticCamera=diagnostic;proof.SetSize(size);ResetActors();yield return Settle();StartRaw((diagnostic?"comfort":"reference")+"-"+size.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
            foreach(string phase in new[]{"straight","rapid-reversal","circling-controller","dash","stopping","landmark-approach"})
            {
                ResetActors();yield return Settle();
                if(phase=="stopping"){double warm=Time.realtimeSinceStartupAsDouble;while(Time.realtimeSinceStartupAsDouble-warm<.75){Feed("straight",0);yield return end;}ReleaseInput();}
                bool moved=false,controller=false;previous=null;velocity=Vector2.zero;double start=Time.realtimeSinceStartupAsDouble;
                while(Time.realtimeSinceStartupAsDouble-start<2)
                {
                    double t=Time.realtimeSinceStartupAsDouble,elapsed=t-start;Feed(phase,elapsed);yield return end;Record(phase,t,size,diagnostic);var recorded=data.frames[data.frames.Count-1];moved|=recorded.bodySpeed>.5f;controller|=recorded.scheme=="Gamepad"&&Vector2.Dot(recorded.aim,Vector2.right)>.99f;
                }
                Check(phase=="stopping"?data.frames[data.frames.Count-1].bodySpeed<.01f:moved,"Actual motion "+phase+" / size "+size+" / diagnostic "+diagnostic);if(phase=="circling-controller")Check(controller,"Actual controller aim / size "+size+" / diagnostic "+diagnostic);
            }
            ReleaseInput();yield return Settle();StopRaw();yield return Capture((diagnostic?"comfort":"reference")+"-body-"+size.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
        }
        proof.DiagnosticCamera=false;proof.SetSize(1);ResetActors();yield return Settle();
        // Actual pointer -> inverse mapper -> GobboController aim and production target resolver.
        var target=proof.Player.transform.position+Vector3.left*2;
        QueueMouse(target);yield return null;yield return end;
        Check(Vector2.Dot(proof.Player.CurrentAimDirection,Vector2.left)>.995f,"Actual mouse aim agrees with inverse projected target");
        var targetObject=new GameObject("Isolated target resolver probe");targetObject.transform.position=target;targetObject.AddComponent<CircleCollider2D>().radius=.2f;Physics2D.SyncTransforms();
        var request=new AbilityTargetRequest{Mode=AbilityTargetingMode.PrecisePointer,Source=proof.Player.transform.position,PointerWorldPosition=proof.InversePointer(mouse.position.ReadValue()),MaxRange=4,ResolveEligibleTarget=c=>c.gameObject==targetObject?c.transform:null,HasLineOfSight=(a,b)=>proof.Player.GetComponent<ActorTerrainBinding>().Sight(a,b)};
        Check(AbilityTargetResolver.Resolve(request).Target==targetObject.transform,"Actual precise-pointer target resolver uses logical collider");request.Mode=AbilityTargetingMode.DirectionalCone;request.AimDirection=Vector2.left;request.FullConeAngle=90;request.AlignmentWeight=1;request.DistanceWeight=1;
        Check(AbilityTargetResolver.Resolve(request).Target==targetObject.transform,"Actual controller-cone target resolver");Destroy(targetObject);
        // Real Buddy DigAbility at a nearby ordinary excavation front; no residential authorization.
        proof.Buddy.GetComponent<Rigidbody2D>().position=proof.Authority.ToWorld(new Point2(84.5,123.5));yield return new WaitForFixedUpdate();proof.Buddy.brainAllowsMovement=false;
        int old=proof.Authority.Revision;var buddyDig=proof.Buddy.GetComponent<BuddyDigAbility>().Dig(proof.Authority.ToWorld(new Point2(84.5,125.5)),TerrainDigAuthority.Buddy);
        Check(buddyDig.Changed&&proof.Authority.Revision>old,"Actual BuddyDigAbility commits ordinary future-bedroom earth");
        int protectedRevision=proof.Authority.Revision;proof.Authority.DigCircle(proof.Authority.ToWorld(new Point2(84.5,111.5)),.1f);
        Check(proof.Authority.Revision==protectedRevision,"Protected station backing rejects excavation");
        var binding=proof.Buddy.GetComponent<ActorTerrainBinding>();int stale=proof.Authority.Revision;
        proof.Authority.DigCircle(proof.Authority.ToWorld(new Point2(84.5,127.5)),.2f);
        Check(proof.Authority.Revision>stale&&!binding.AcceptsRoute(stale),"Stale route revision rejected after edit");
        // All movement/digging below runs through keyboard actions and real GobboController.
        proof.Player.GetComponent<Rigidbody2D>().position=proof.Authority.ToWorld(new Point2(84.5,123.5));proof.Buddy.GetComponent<Rigidbody2D>().position=proof.Authority.ToWorld(new Point2(84.5,120.5));proof.Buddy.brainAllowsMovement=true;
        Vector2 goal=proof.Authority.ToWorld(new Point2(84.5,132.5));bool before=TileMover.CanOccupyBox(proof.Authority,goal,new Vector2(.5f,.5f));int revisionBefore=proof.Authority.Revision;
        StartRaw("real-dig-and-follow");previous=null;double digStart=Time.realtimeSinceStartupAsDouble;
        while(Time.realtimeSinceStartupAsDouble-digStart<3){double t=Time.realtimeSinceStartupAsDouble;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S,Key.LeftShift));QueueMouse(proof.Player.transform.position+Vector3.down*2);yield return end;Record("real-player-dig-follow",t,1,false);}
        ReleaseInput();yield return Settle();StopRaw();
        bool after=TileMover.CanOccupyBox(proof.Authority,goal,new Vector2(.5f,.5f));
        Check(!before&&after&&proof.Authority.Revision>revisionBefore,"Actual player digging opens previously solid passage");
        Check(proof.Authority.PublishedRevision==proof.Authority.Revision,"Physics publication agrees with committed terrain revision");
        yield return Capture("08-player-dig-buddy-follow");double followStart=Time.realtimeSinceStartupAsDouble;while(Time.realtimeSinceStartupAsDouble-followStart<5){yield return null;}
        Check(Vector2.Distance(proof.Player.transform.position,proof.Buddy.transform.position)<2.5f,"Real Buddy follows through newly excavated passage without teleport");
        yield return Capture("09-buddy-through-passage");
        proof.Player.GetComponent<Rigidbody2D>().position=proof.Authority.ToWorld(new Point2(75.5,111.5));proof.Buddy.GetComponent<Rigidbody2D>().position=proof.Authority.ToWorld(new Point2(94.5,111.5));proof.Player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;proof.Buddy.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
        Check(!binding.Sight(proof.Buddy.GetComponent<Rigidbody2D>().position,proof.Player.GetComponent<Rigidbody2D>().position),"Protected Fire mass forces a real non-direct route");
        StartRaw("buddy-obstacle-navigation");double routeStart=Time.realtimeSinceStartupAsDouble;previous=null;while(Time.realtimeSinceStartupAsDouble-routeStart<6){double t=Time.realtimeSinceStartupAsDouble;ReleaseInput();yield return end;Record("buddy-obstacle-navigation",t,1,false);}StopRaw();
        Check(binding.LastRouteRevision==proof.Authority.Revision&&Vector2.Distance(proof.Player.transform.position,proof.Buddy.transform.position)<2.5f,"Actual BuddyFollow uses bound navigation around permanent obstacle");yield return Capture("10-buddy-obstacle-route");
        var solid=Physics2D.OverlapPoint(proof.Authority.ToWorld(new Point2(84.5,111.5)),1);var open=Physics2D.OverlapPoint(proof.Authority.ToWorld(new Point2(84.5,117.5)),1);
        Check(solid!=null&&solid.transform.IsChildOf(proof.Authority.CollisionRoot.transform)&&open==null,"Physics2D solid/open probes agree with authority");
        // Real UI coordinator context and pause owner, not a fake input boolean.
        StartRaw("input-pause-boundaries");proof.SetModal(true);yield return null;int attempts=proof.Player.GetComponent<ActorTerrainBinding>().DigAttempts;int modalRevision=proof.Authority.Revision;Vector2 modalPosition=proof.Player.GetComponent<Rigidbody2D>().position;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift,Key.D));yield return new WaitForSecondsRealtime(.25f);
        Check(SporeInputReader.Instance.Context==SporeInputContext.Modal&&proof.Player.GetComponent<ActorTerrainBinding>().DigAttempts==attempts&&proof.Authority.Revision==modalRevision&&Vector2.Distance(modalPosition,proof.Player.GetComponent<Rigidbody2D>().position)<.001f,"Actual UI modal blocks digging and movement");
        Vector2 pausedPlayer=proof.Player.GetComponent<Rigidbody2D>().position,pausedBuddy=proof.Buddy.GetComponent<Rigidbody2D>().position;proof.SetPaused(true);yield return new WaitForSecondsRealtime(.3f);
        Check(Time.timeScale==0&&Vector2.Distance(pausedPlayer,proof.Player.GetComponent<Rigidbody2D>().position)<.0001f&&Vector2.Distance(pausedBuddy,proof.Buddy.GetComponent<Rigidbody2D>().position)<.0001f,"Pause owner freezes both real actors");
        proof.SetPaused(false);proof.SetModal(false);yield return null;yield return new WaitForSecondsRealtime(.15f);
        Check(proof.Player.GetComponent<ActorTerrainBinding>().DigAttempts==attempts&&proof.Authority.Revision==modalRevision,"Held dig suppressed after modal until release");ReleaseInput();yield return null;
        proof.SetPaused(true);yield return null;Check(SporeInputReader.Instance.Context==SporeInputContext.Pause,"Isolated pause screen uses actual production pause context");
        int pausedAttempts=proof.Player.GetComponent<ActorTerrainBinding>().DigAttempts,pausedRevision=proof.Authority.Revision;Vector2 purePausePosition=proof.Player.GetComponent<Rigidbody2D>().position;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift,Key.Space,Key.D));yield return new WaitForSecondsRealtime(.3f);
        Check(proof.Player.GetComponent<ActorTerrainBinding>().DigAttempts==pausedAttempts&&proof.Authority.Revision==pausedRevision&&Vector2.Distance(purePausePosition,proof.Player.GetComponent<Rigidbody2D>().position)<.0001f,"Pause without a modal blocks actual world actions and movement");
        proof.SetPaused(false);yield return null;yield return new WaitForSecondsRealtime(.15f);Check(proof.Player.GetComponent<ActorTerrainBinding>().DigAttempts==pausedAttempts,"Pause resume suppresses held dig until release");ReleaseInput();yield return null;StopRaw();
        proof.ShowPanel=true;yield return end;yield return Capture("11-real-actor-controls");proof.ShowPanel=false;
        Check(GameState.Instance==null&&DiggableTerrainService.Active==null,"No production registration after real actor exercise");
        Check(data.frames.TrueForAll(f=>f.clear),"All sampled Rigidbody positions agree with terrain clearance");
        Check(data.frames.TrueForAll(f=>f.footError<.001f&&f.pointerError<.001f),"Projected feet and inverse pointer roundtrip accuracy");
        Check(proof.Views.TrueForAll(v=>v.SpriteChanges>1),"Production sprite animation changes observed on player and Buddy");
        // Destroy a separate derived collision owner to test unsubscribe/native object cleanup without ending this benchmark coroutine.
        var cleanupParent=new GameObject("cleanup test");var cleanup=new Stage59Authority(new SporeGobbo.CampCandidate.CampSession(SporeGobbo.CampCandidate.Stage57Layout.Create()));cleanup.CreateCollision(cleanupParent.transform);var collision=cleanup.CollisionRoot;cleanup.Dispose();cleanup.Dispose();Destroy(cleanupParent);yield return null;yield return null;Check(collision==null&&cleanup.CollisionRoot==null,"Collision cleanup destroys native patches and is idempotent");
        proof.ReleaseActors();yield return null;yield return null;Check(Stage59ActorView.OwnedMeshes==0&&Stage59ActorView.OwnedMaterials==0&&Array.FindAll(Resources.FindObjectsOfTypeAll<Mesh>(),m=>m.name=="Projected foot shadow"||m.name=="Real player animated projection"||m.name=="Real following Buddy animated projection").Length==0,"All owned actor meshes/materials released after actor teardown");
        StopRaw();Write();finishing=true;Application.Quit(data.error==""?0:1);
    }
    void ResetActors(){ReleaseInput();proof.Player.GetComponent<Rigidbody2D>().position=proof.Authority.ToWorld(new Point2(84.5,117.5));proof.Player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;proof.Buddy.GetComponent<Rigidbody2D>().position=proof.Authority.ToWorld(new Point2(88.5,119.5));proof.Buddy.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();var logical=proof.Authority.ToLogical(proof.Player.GetComponent<Rigidbody2D>().position);proof.Live.Teleport(new Vector2((float)logical.X,(float)logical.Y));proof.ResetCameraStudy();}
    void Feed(string phase,double elapsed)
    {
        if(phase=="circling-controller")
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=new Vector2(Mathf.Cos((float)elapsed*3),Mathf.Sin((float)elapsed*3)),rightStick=Vector2.right});return;}
        InputSystem.QueueStateEvent(gamepad,new GamepadState());
        Key move=phase=="landmark-approach"?Key.W:phase=="rapid-reversal"?(((int)(elapsed*10)%2)==0?Key.A:Key.D):Key.D;
        if(phase=="stopping")InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        else if(phase=="dash"&&elapsed%.8<.07)InputSystem.QueueStateEvent(keyboard,new KeyboardState(move,Key.Space));
        else InputSystem.QueueStateEvent(keyboard,new KeyboardState(move));
        QueueMouse(proof.Player.transform.position+Vector3.right*2);
    }
    void QueueMouse(Vector3 world){var screen=proof.Live.previewCamera.WorldToScreenPoint(proof.Render(world));InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});}
    void ReleaseInput(){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState());if(gamepad!=null)InputSystem.QueueStateEvent(gamepad,new GamepadState());}
    void Record(string phase,double start,float size,bool diagnostic)
    {
        var points=new Vector2[fixedPoints.Length];for(int i=0;i<points.Length;i++){var p=proof.Live.Snapshot.Project(fixedPoints[i]);var s=proof.Live.previewCamera.WorldToScreenPoint(new Vector3((float)p.X,(float)-p.Y));points[i]=new Vector2(s.x,s.y);}
        float dt=Time.unscaledDeltaTime,speed=0,acceleration=0;if(previous!=null&&dt>0){var v=(points[1]-previous[1])/dt;speed=v.magnitude;acceleration=(v-velocity).magnitude/dt;velocity=v;}previous=points;
        var body=proof.Player.GetComponent<Rigidbody2D>();var foot=proof.Views.Find(v=>v.gameObject==proof.Player.gameObject);Vector2 expected=proof.Render(foot.PhysicalFoot);float error=Mathf.Max(Vector2.Distance(foot.ProjectedFoot,expected),Mathf.Abs(foot.RenderedBottomY-expected.y));var screen=proof.Live.previewCamera.WorldToScreenPoint(proof.Render(body.position));float pointer=Vector2.Distance(body.position,proof.InversePointer(new Vector2(screen.x,screen.y)));
        FrameTimingManager.CaptureFrameTimings();double gpu=FrameTimingManager.GetLatestTimings(1,timing)>0?timing[0].gpuFrameTime:-1;
        bool clear=TileMover.CanOccupyBox(proof.Authority,body.position,TileMover.GetMapClearanceExtents(body,.01f))&&TileMover.CanOccupyBox(proof.Authority,proof.Buddy.GetComponent<Rigidbody2D>().position,TileMover.GetMapClearanceExtents(proof.Buddy.GetComponent<Rigidbody2D>(),.01f));
        int sprites=0;double presentation=0;foreach(var v in proof.Views){sprites+=v.SpriteChanges;presentation+=v.UpdateMs;}
        data.frames.Add(new Frame{time=Time.realtimeSinceStartupAsDouble,dt=Time.unscaledDeltaTime,phase=phase,context=SporeInputReader.Instance.Context.ToString(),ready=proof.Live.Ready,scheme=SporeInputReader.Instance.ActiveControlScheme.ToString(),animation=proof.Player.GetComponentInChildren<GobboVisualController>().currentState.ToString(),bodySpeed=body.linearVelocity.magnitude,frame=Time.frameCount,counterFrame=Time.frameCount-1,meshBuild=telemetry.Ms[(int)GeometryPhase.MeshBuild],meshUpload=telemetry.Ms[(int)GeometryPhase.MeshUpload],revision=proof.Authority.Revision,collisionRevision=proof.Authority.PublishedRevision,size=size,diagnostic=diagnostic,interval=(Time.realtimeSinceStartupAsDouble-start)*1000,main=main.Valid?main.LastValue*1e-6:-1,allocation=alloc.Valid?alloc.LastValue:-1,gpu=gpu,camera=proof.CameraMs,sorting=proof.SortingMs,presentation=presentation,terrain=proof.Live.DisplayMs,collider=previousRevision!=proof.Authority.Revision?proof.Authority.ColliderMs:0,player=body.position,buddy=proof.Buddy.GetComponent<Rigidbody2D>().position,aim=proof.Player.CurrentAimDirection,focus=new Vector2((float)proof.Live.Snapshot.Focus.X,(float)proof.Live.Snapshot.Focus.Y),points=points,speed=speed,acceleration=acceleration,footError=error,pointerError=pointer,clear=clear,spriteChanges=sprites});previousRevision=proof.Authority.Revision;telemetry.Reset();
    }
    IEnumerator Settle(){ReleaseInput();int guard=0;do{yield return null;if(++guard>5000){Check(false,"Readiness timeout");break;}}while(!proof.Live.Ready||proof.Live.Stream.Pending>0);for(int i=0;i<12;i++)yield return null;}
    IEnumerator Capture(string name){proof.Camp.CameraStudy.Capture(output,name);while(proof.Camp.CameraStudy.CapturePending)yield return null;proof.Camp.CameraStudy.captureLabel=false;}
    void StartRaw(string name){StopRaw();Profiler.logFile=Path.Combine(output,name+".raw");Profiler.maxUsedMemory=256*1024*1024;Profiler.enableBinaryLog=true;Profiler.enabled=true;capturing=true;}
    void StopRaw(){if(!capturing)return;Profiler.enabled=false;Profiler.logFile="";capturing=false;}
    void Check(bool ok,string label){data.checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok)data.error+=label+";";}
    void Write(){if(output!=null)File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(data,true));}
    void OnLog(string message,string stack,LogType type){if(finishing||type!=LogType.Exception&&type!=LogType.Error)return;data.error+=message+"\n"+stack;Write();finishing=true;Application.Quit(1);}
    void OnDestroy(){Application.logMessageReceived-=OnLog;StopRaw();if(runtimeActions!=null)runtimeActions.devices=priorDevices;if(changedInputSetup)InputSystem.settings.backgroundBehavior=priorBackground;telemetry?.Dispose();main.Dispose();alloc.Dispose();if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);if(gamepad!=null&&gamepad.added)InputSystem.RemoveDevice(gamepad);}
}
