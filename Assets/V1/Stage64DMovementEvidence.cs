#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.Collections;using System.Collections.Generic;using System.Globalization;using System.IO;using System.Linq;
using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using SporeGobbo.Safety;using SporeGobbo.Input;

/// <summary>Owned automation over the real existing cave/actors. Fixture setup and input automation
/// are adapters, not natural recruitment or a large-world capacity claim.</summary>
public sealed class Stage64DMovementEvidence:MonoBehaviour
{
    readonly List<string> checks=new List<string>(),errors=new List<string>();
    struct BodyRow{public float time,x,y,speed,halfX,halfY,desiredX,desiredY,goalX,goalY,waypointX,waypointY;public int actor,phase,type,age,nodes;public bool waiting;public string id,task,status;}
    struct NavigationRow{public float time;public int phase,queue,pending;public long requests,searches,coalesced,work,visited,completed,unreachable,latencySamples;public double latencyTotalMs,maximumLatencyMs;}
    readonly NavigationRow[] navigation=new NavigationRow[6000];int navigationCount;RegionRoutePump routePump;
    struct PairRow{public float time,gap;public int a,b,phase;public bool ignored;}
    readonly BodyRow[] bodies=new BodyRow[30000];readonly PairRow[] pairs=new PairRow[100000];
    struct DecisionRow { public RegionMovementCoordinator.DecisionTrace trace; public float time; public int phase; }
    readonly DecisionRow[] decisions=new DecisionRow[400000];int decisionCount,decisionDropped;
    int bodyCount,pairCount,dropped,phase;float deadline,nextSample;bool finished;string directory;Keyboard keyboard;
    Stage64DNativeMotionEvidence nativeEvidence;
    BuddyUnit[] sampled=Array.Empty<BuddyUnit>();
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    static bool Camp()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true;
    void Awake()
    {
        if(!Persistence.Isolated||!V1RuntimeProfile.DevelopmentStorage||!V1RuntimeProfile.DevelopmentToolsEnabled||Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE")!="movement")throw new InvalidOperationException("Owned movement evidence only");
        directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);deadline=Time.realtimeSinceStartup+300;Application.logMessageReceived+=Observe;
        nativeEvidence=gameObject.AddComponent<Stage64DNativeMotionEvidence>();
        Stage64DRouteObservation.Begin();Stage64DReciprocalObservation.Begin();
        RegionMovementCoordinator.DiagnosticObserver+=CaptureDecision;
    }
    void CaptureDecision(RegionMovementCoordinator.DecisionTrace trace)
    {
        if(finished)return;
        nativeEvidence?.Applied(trace);
        if(decisionCount<decisions.Length)decisions[decisionCount++]=new DecisionRow{trace=trace,time=Time.realtimeSinceStartup,phase=phase};else decisionDropped++;
    }
    void Observe(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    void Update()
    {
        if(finished)return;
        if(errors.Count>0||Time.realtimeSinceStartup>deadline){Finish();return;}
        if(Time.realtimeSinceStartup<nextSample)return;nextSample=Time.realtimeSinceStartup+.1f;
        if(routePump==null)routePump=Find<RegionRoutePump>();
        if(routePump!=null&&routePump.Queue!=null&&navigationCount<navigation.Length)
        {var q=routePump.Queue;navigation[navigationCount++]=new NavigationRow{time=Time.realtimeSinceStartup,phase=phase,queue=routePump.GetInstanceID(),pending=q.PendingCount,requests=q.Requests,searches=q.Searches,coalesced=q.Coalesced,work=q.Work,visited=q.Visited,completed=q.Completed,unreachable=q.Unreachable,latencySamples=q.LatencySamples,latencyTotalMs=q.LatencyTotalMs,maximumLatencyMs=q.MaximumLatencyMs};}
        for(int i=0;i<sampled.Length;i++)
        {
            var a=sampled[i];if(a==null)continue;var body=a.GetComponent<Rigidbody2D>();if(body==null)continue;
            var binding=a.GetComponent<ActorTerrainBinding>();var motion=a.GetComponent<ActorMotionIntent>();var follow=a.GetComponent<BuddyFollow>();var extents=TileMover.GetMapClearanceExtents(body,binding?.MinimumBodyRadius??.1f);var desired=motion?.RequestedVelocity??Vector2.zero;var goal=binding?.CurrentMovementGoal??body.position;var waypoint=follow!=null?follow.RouteWaypoint:body.position;
            if(bodyCount<bodies.Length)bodies[bodyCount++]=new BodyRow{time=Time.realtimeSinceStartup,x=body.position.x,y=body.position.y,speed=body.linearVelocity.magnitude,actor=a.GetInstanceID(),phase=phase,waiting=motion?.WaitingForFriendlySpace??false,type=a.unitData!=null?(int)a.unitData.gobboType:-1,age=a.unitData!=null?(int)a.unitData.ageStage:-1,halfX=extents.x,halfY=extents.y,desiredX=desired.x,desiredY=desired.y,goalX=goal.x,goalY=goal.y,waypointX=waypoint.x,waypointY=waypoint.y,nodes=follow?.RouteNodeCount??0,id=a.unitData?.uniqueId??"missing",task=motion?.ActiveTaskName??"None",status=binding?.RouteStatus.ToString()??"unbound"};else dropped++;
            var ca=a.GetComponent<Collider2D>();if(ca==null||!ca.enabled||ca.isTrigger)continue;
            for(int j=i+1;j<sampled.Length;j++)
            {
                var b=sampled[j];var cb=b!=null?b.GetComponent<Collider2D>():null;if(cb==null||!cb.enabled||cb.isTrigger)continue;
                var distance=ca.Distance(cb);if(!distance.isValid)continue;
                if(pairCount<pairs.Length)pairs[pairCount++]=new PairRow{time=Time.realtimeSinceStartup,gap=distance.distance,a=a.GetInstanceID(),b=b.GetInstanceID(),phase=phase,ignored=Physics2D.GetIgnoreCollision(ca,cb)};else dropped++;
            }
        }
    }
    void Check(string label,bool pass){checks.Add((pass?"PASS ":"FAIL ")+label);if(!pass)Finish();}
    IEnumerator Frames(int count){for(int i=0;i<count&&!finished;i++)yield return null;}
    IEnumerator Wait(Func<bool> condition,string label,float seconds=60)
    {float end=Time.realtimeSinceStartup+seconds;while(!finished&&!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(label,!finished&&condition());}
    IEnumerator Shot(string name)
    {Stage61RuntimeMetrics.Category("capture");yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());}finally{Destroy(texture);}yield return null;}
    IEnumerator Start()
    {
        yield return Frames(2);keyboard=InputSystem.AddDevice<Keyboard>("64D owned movement keyboard");keyboard.MakeCurrent();
        DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);yield return Wait(Camp,"real established Camp ready");if(finished)yield break;
        sampled=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);phase=1;Stage61RuntimeMetrics.Mark("movement Camp wandering");Stage61RuntimeMetrics.Category("ordinary");yield return new WaitForSeconds(12);yield return Shot("movement-camp");if(finished)yield break;
        var state=GameState.Instance;foreach(var data in state.ownedGobbos)if(state.activeSquadIds.Count<state.maxActiveSquad&&!state.activeSquadIds.Contains(data.uniqueId))BuddyRosterService.MoveBuddyToActiveSquad(state,data.uniqueId);
        Check("real squad capacity preserved",state.maxActiveSquad==5&&state.activeSquadIds.Count==5);if(finished)yield break;
        Stage61RuntimeMetrics.Category("lifecycle");var portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);yield return null;portal.goButton.onClick.Invoke();
        yield return Wait(()=>Find<ExpeditionRegion>()?.Player!=null&&Find<ExpeditionRegion>().PresentationReady&&UnityEngine.Object.FindObjectsByType<BuddyBrain>(FindObjectsSortMode.None).Count(b=>b.SquadScoped)==5,"real expedition with five Buddies ready");if(finished)yield break;
        var region=Find<ExpeditionRegion>();var leader=region.Player;
        var comparison=Environment.GetEnvironmentVariable("SPORE_STAGE64D_CONTACT_COMPARISON");
        if(!string.IsNullOrEmpty(comparison))
        {
            if(comparison!="strict"&&comparison!="cooperative")throw new InvalidOperationException("Unreviewed contact comparison");
            leader.GetComponent<ActorTerrainBinding>().Movement.SetDiagnosticCooperativeContact(comparison=="cooperative");
            File.WriteAllText(Path.Combine(directory,"contact-comparison-policy.txt"),comparison+"\nSame existing driver, deadlines, destination and body sizes. Temporary isolated policy only.\n");
        }
        sampled=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Where(b=>b.GetComponent<BuddyBrain>()?.SquadScoped==true).ToArray();
        leader.IssueBuddyCommand(BuddyCommand.Passive);leader.IssueBuddyCommand(BuddyCommand.Follow);yield return Frames(30);yield return Shot("movement-follow-start");
        // Automation plans outside the ordinary window. Player movement itself remains direct keyboard input.
        var playerBody=leader.GetComponent<Rigidbody2D>();var route=region.Route(playerBody.position,region.World(region.Definition.Anchors["nursery"]),TileMover.GetMapClearanceExtents(playerBody,.25f));
        Check("existing authored winding route is physically available",route.Count>15);if(finished)yield break;
        phase=2;Stage61RuntimeMetrics.Mark("movement real full-squad winding travel");Stage61RuntimeMetrics.Category("ordinary");float end=Time.realtimeSinceStartup+45;int cursor=0;
        while(!finished&&cursor<route.Count&&Time.realtimeSinceStartup<end)
        {
            Vector2 delta=route[cursor]-playerBody.position;
            if(delta.magnitude<.14f){cursor++;continue;}
            Key key=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?delta.x>0?Key.D:Key.A:delta.y>0?Key.W:Key.S;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());Check("real keyboard completes authored winding route",cursor==route.Count);if(finished)yield break;
        phase=3;Stage61RuntimeMetrics.Mark("movement full-squad settling");Stage61RuntimeMetrics.Category("ordinary");yield return new WaitForSeconds(15);
        yield return Shot("movement-follow-settled");Check("all five real Buddies converge through the cave",sampled.All(b=>b!=null&&b.CombatAlive&&Vector2.Distance(b.transform.position,leader.transform.position)<4));if(finished)yield break;
        phase=4;leader.IssueBuddyCommand(BuddyCommand.Stay);leader.IssueBuddyCommand(BuddyCommand.Aggressive);yield return Frames(20);
        Check("real Stay Aggressive keeps anchors",sampled.All(b=>!b.GetComponent<BuddyBrain>().Following&&b.GetComponent<BuddyBrain>().Aggressive));if(finished)yield break;
        leader.IssueBuddyCommand(BuddyCommand.Passive);leader.IssueBuddyCommand(BuddyCommand.Follow);yield return Frames(20);
        Check("real Follow Passive restores following",sampled.All(b=>b.GetComponent<BuddyBrain>().Following&&!b.GetComponent<BuddyBrain>().Aggressive));
        Stage61RuntimeMetrics.Category("lifecycle");Check("real retreat accepted",RunReturnService.ReturnToCamp(GameSceneCatalog.Camp,true,true,RunReturnReason.Retreat,"Stage64D movement evidence"));
        yield return Wait(()=>Find<CampSceneController>()!=null,"Camp return coordinator ready");if(finished)yield break;
        end=Time.realtimeSinceStartup+60;
        while(!finished&&!Camp()&&Time.realtimeSinceStartup<end)
        {var c=Find<CampSceneController>();if(c.continueToSurvivorsButton!=null&&c.continueToSurvivorsButton.gameObject.activeInHierarchy)c.continueToSurvivorsButton.onClick.Invoke();else if(c.continueToCampButton!=null&&c.continueToCampButton.gameObject.activeInHierarchy)c.continueToCampButton.onClick.Invoke();yield return null;}
        Check("real Camp return and routines ready",Camp());if(finished)yield break;sampled=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);phase=5;Stage61RuntimeMetrics.Mark("movement returned Camp routines");Stage61RuntimeMetrics.Category("ordinary");yield return new WaitForSeconds(10);yield return Shot("movement-camp-return");
        Stage61RuntimeMetrics.Category("lifecycle");sampled=Array.Empty<BuddyUnit>();Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"real Menu teardown ready");yield return Frames(5);
        Check("movement terrain and actor resources released",DiggableTerrainService.Active==null&&Stage59ActorView.OwnedMeshes==0&&UnityEngine.Object.FindObjectsByType<RegionRoutePump>(FindObjectsSortMode.None).Length==0);
        Check("movement samples are bounded and present",bodyCount>100&&pairCount>100&&dropped==0);Finish();
    }
    void Finish()
    {
        if(finished)return;finished=true;if(Time.realtimeSinceStartup>deadline)errors.Add("Movement watchdog expired");
        Stage64DRouteObservation.Export(directory);Stage64DReciprocalObservation.Export(directory);
        if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState());Find<Stage61RuntimeMetrics>()?.Export();
        using(var file=new StreamWriter(Path.Combine(directory,"movement-bodies.csv"))){file.WriteLine("time,phase,actor,x,y,speed,waiting,type,age,halfX,halfY,desiredX,desiredY,goalX,goalY,waypointX,waypointY,nodes,id,task,status");for(int i=0;i<bodyCount;i++){var r=bodies[i];file.WriteLine(FormattableString.Invariant($"{r.time:R},{r.phase},{r.actor},{r.x:R},{r.y:R},{r.speed:R},{r.waiting},{r.type},{r.age},{r.halfX:R},{r.halfY:R},{r.desiredX:R},{r.desiredY:R},{r.goalX:R},{r.goalY:R},{r.waypointX:R},{r.waypointY:R},{r.nodes},{r.id},{r.task},{r.status}"));}}
        using(var file=new StreamWriter(Path.Combine(directory,"movement-pairs.csv"))){file.WriteLine("time,phase,a,b,gap,ignored");for(int i=0;i<pairCount;i++){var r=pairs[i];file.WriteLine(FormattableString.Invariant($"{r.time:R},{r.phase},{r.a},{r.b},{r.gap:R},{r.ignored}"));}}
        using(var file=new StreamWriter(Path.Combine(directory,"movement-navigation.csv"))){file.WriteLine("time,phase,queue,pending,requests,searches,coalesced,work,visited,completed,unreachable,latencySamples,latencyTotalMs,maximumLatencyMs");for(int i=0;i<navigationCount;i++){var r=navigation[i];file.WriteLine(FormattableString.Invariant($"{r.time:R},{r.phase},{r.queue},{r.pending},{r.requests},{r.searches},{r.coalesced},{r.work},{r.visited},{r.completed},{r.unreachable},{r.latencySamples},{r.latencyTotalMs:R},{r.maximumLatencyMs:R}"));}}
        ExportDecisions();nativeEvidence?.Export(directory);
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=errors.Count>0?"movement evidence error":"",checks=checks.ToArray(),runtimeErrors=errors.ToArray(),method="Real existing Camp and expedition, canonical five-Buddy squad, AI/physics/navigation and keyboard leader movement. Established synthetic fixture, direct production command API and preplanned automation route are explicit adapters.10Hz body/pair samples are not continuous collision bounds. No larger-world or natural recruitment claim."},true));
        Application.Quit(errors.Count==0&&checks.Count>0&&checks.All(c=>c.StartsWith("PASS "))?0:1);
    }
    [Serializable]sealed class Result{public bool finished;public string error,method;public string[] checks,runtimeErrors;}
    void ExportDecisions()
    {
        using(var file=new StreamWriter(Path.Combine(directory,"movement-decisions.csv")))
        {
            file.WriteLine("sequence,time,phase,tick,region,stage,actor,key,peer,peerKey,detail,ticket,peerTicket,group,role,state,reason,task,routeStatus,x,y,halfX,halfY,preferredX,preferredY,resolvedX,resolvedY,actualX,actualY,yieldX,yieldY,candidateX,candidateY,goalX,goalY,accessX,accessY,leaseX,leaseY,accessKey,leaseKey,camp,human,ready,selected,yielding,yieldRequested,access,granted,occupied,hasLease,leaseOccupied,yieldOwner,yieldOwnerTicket,ownerOriginX,ownerOriginY,yieldHeadingX,yieldHeadingY,rejectedYield,rejectedX,rejectedY");
            for(int i=0;i<decisionCount;i++)
            {
                var row=decisions[i];var r=row.trace;
                file.WriteLine(FormattableString.Invariant($"{i},{row.time:R},{row.phase},{r.Tick:R},{r.Region},{r.Stage},{r.Actor},{r.Key},{r.Peer},{r.PeerKey},{r.Detail},{r.Ticket},{r.PeerTicket},{r.Group},{r.Role},{r.State},{r.Reason},{r.Task},{r.RouteStatus},{r.Position.x:R},{r.Position.y:R},{r.Extents.x:R},{r.Extents.y:R},{r.Preferred.x:R},{r.Preferred.y:R},{r.Resolved.x:R},{r.Resolved.y:R},{r.Actual.x:R},{r.Actual.y:R},{r.YieldGoal.x:R},{r.YieldGoal.y:R},{r.Candidate.x:R},{r.Candidate.y:R},{r.Goal.x:R},{r.Goal.y:R},{r.AccessGoal.x:R},{r.AccessGoal.y:R},{r.LeaseGoal.x:R},{r.LeaseGoal.y:R},{r.AccessKey},{r.LeaseKey},{r.Camp},{r.Human},{r.Ready},{r.Selected},{r.Yielding},{r.YieldRequested},{r.Access},{r.Granted},{r.Occupied},{r.HasLease},{r.LeaseOccupied},{r.YieldOwner},{r.YieldOwnerTicket},{r.YieldOwnerOrigin.x:R},{r.YieldOwnerOrigin.y:R},{r.YieldHeading.x:R},{r.YieldHeading.y:R},{r.HasRejectedYield},{r.RejectedYieldGoal.x:R},{r.RejectedYieldGoal.y:R}"));
            }
        }
        File.WriteAllText(Path.Combine(directory,"movement-decisions-status.json"),JsonUtility.ToJson(new DecisionStatus{count=decisionCount,dropped=decisionDropped,capacity=decisions.Length,method="Observation only at original coordinator branches. Fixed buffer; no per-record I/O during replay. Unchanged movement scenario, deadlines and assertions. Diagnostic overhead is not a performance measurement."},true));
    }
    [Serializable]sealed class DecisionStatus{public int count,dropped,capacity;public string method;}
    void OnDestroy(){RegionMovementCoordinator.DiagnosticObserver-=CaptureDecision;Application.logMessageReceived-=Observe;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);}
}
// Separate bounded native replay: original five-Buddy Start workload above remains unchanged.
public sealed class Stage64DCalmWaitEvidence:MonoBehaviour
{
    readonly List<string> checks=new List<string>(),errors=new List<string>(),rows=new List<string>();
    string directory;Keyboard keyboard;bool finished;float deadline;BuddyUnit buddy;ActorTerrainBinding binding;GobboController observedPlayer;Stage64DNativeMotionEvidence observer;
    void Awake(){if(!Persistence.Isolated||!V1RuntimeProfile.DevelopmentStorage||!V1RuntimeProfile.DevelopmentToolsEnabled)throw new InvalidOperationException("Owned calm-wait evidence only");directory=Persistence.ExperimentPath("Evidence");deadline=Time.realtimeSinceStartup+180;Application.logMessageReceived+=Observe;observer=gameObject.AddComponent<Stage64DNativeMotionEvidence>();RegionMovementCoordinator.DiagnosticObserver+=Native;rows.Add("time,x,y,vx,vy,state,reason,goalX,goalY,playerX,playerY,playerVx,playerVy,inputContext");}
    void Native(RegionMovementCoordinator.DecisionTrace trace)=>observer?.Applied(trace);
    void Observe(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack);}
    void Update(){if(finished)return;if(errors.Count>0||Time.realtimeSinceStartup>deadline){Finish();return;}if(buddy==null)return;var body=buddy.GetComponent<Rigidbody2D>();rows.Add(FormattableString.Invariant($"{Time.realtimeSinceStartup:R},{body.position.x:R},{body.position.y:R},{body.linearVelocity.x:R},{body.linearVelocity.y:R},{binding.Movement.State(binding)},{binding.Movement.Reason(binding)},{binding.CurrentMovementGoal.x:R},{binding.CurrentMovementGoal.y:R},{observedPlayer?.GetComponent<Rigidbody2D>().position.x:R},{observedPlayer?.GetComponent<Rigidbody2D>().position.y:R},{observedPlayer?.GetComponent<Rigidbody2D>().linearVelocity.x:R},{observedPlayer?.GetComponent<Rigidbody2D>().linearVelocity.y:R},{SporeInputReader.Instance?.Context}"));}
    bool Check(string label,bool pass){checks.Add((pass?"PASS ":"FAIL ")+label);if(!pass)Finish();return pass;}
    IEnumerator Start()
    {
        yield return null;yield return null;
        keyboard=InputSystem.AddDevice<Keyboard>("64D owned calm-wait keyboard");keyboard.MakeCurrent();
        DevelopmentCampaignFixtures.Import(2);UnityEngine.Object.FindAnyObjectByType<MainMenuController>().LoadGame(2);
        float end=Time.realtimeSinceStartup+60;
        while(Time.realtimeSinceStartup<end&&UnityEngine.Object.FindAnyObjectByType<CampSceneController>()?.WorldInputReady!=true)yield return null;
        var region=UnityEngine.Object.FindAnyObjectByType<SettlementCampRegion>();
        if(!Check("real Camp ready for calm wait",region!=null&&region.Ready))yield break;
        end=Time.realtimeSinceStartup+60;
        while(Time.realtimeSinceStartup<end&&CampStartRoutineManager.Instance?.IsCampVisitRoutineRunning==true)yield return null;
        if(!Check("real Camp arrival routines finish before isolated duty",CampStartRoutineManager.Instance!=null&&!CampStartRoutineManager.Instance.IsCampVisitRoutineRunning))yield break;
        var player=UnityEngine.Object.FindAnyObjectByType<CampPlayableSpawner>().SpawnedPlayer;observedPlayer=player;
        buddy=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).First(b=>b.unitData.uniqueId=="development-b-buddy-1");binding=buddy.GetComponent<ActorTerrainBinding>();
        // Explicit fixture positioning, then the real task's BeginWalk API establishes its complete lifecycle.
        // Existing unused Camp floor isolates this two-actor contract from the separate crowded-Camp workload.
        var origin=region.World(new SporeGobbo.Geometry.Point2(145.5,82.5));
        var pb=player.GetComponent<Rigidbody2D>();var body=buddy.GetComponent<Rigidbody2D>();
        var playerExtent=TileMover.GetMapClearanceExtents(pb,.25f);var buddyExtent=TileMover.GetMapClearanceExtents(body,.25f);
        bool isolated=TileMover.CanTraverseBox(region,origin+new Vector2(.08f,.9f),origin+Vector2.down*3,buddyExtent);
        foreach(var other in UnityEngine.Object.FindObjectsByType<ActorTerrainBinding>(FindObjectsSortMode.None))
            if(other.gameObject!=buddy.gameObject&&other.gameObject!=player.gameObject&&Vector2.Distance(other.transform.position,origin)<5)isolated=false;
        if(!Check("isolated ordinary Camp floor has terrain clearance and no unrelated actors",isolated))yield break;
        pb.position=origin;player.transform.position=pb.position;pb.linearVelocity=Vector2.zero;
        body.position=origin+new Vector2(.08f,.9f);buddy.transform.position=body.position;body.linearVelocity=Vector2.zero;
        var marker=new GameObject("Owned calm-wait destination");marker.transform.position=origin+Vector2.down*3;
        var walk=buddy.GetComponent<CampDirectedWalk>()??buddy.gameObject.AddComponent<CampDirectedWalk>();
        walk.destroyWhenDone=false;walk.enableWanderWhenDone=false;walk.useArrivalSlot=true;
        walk.BeginWalk(marker.transform,1.6f,.18f,30);
        var goal=(Vector2)marker.transform.position;
        Physics2D.SyncTransforms();end=Time.realtimeSinceStartup+8;
        while(!finished&&Time.realtimeSinceStartup<end&&binding.Movement.State(binding)!=MovementResolutionState.WaitingForPeer)yield return null;
        if(!Check("real Buddy enters owned peer wait",binding.Movement.State(binding)==MovementResolutionState.WaitingForPeer))yield break;
        var start=body.position;bool calm=true;end=Time.realtimeSinceStartup+3;
        while(!finished&&Time.realtimeSinceStartup<end){calm&=Vector2.Distance(start,body.position)<.025f&&body.linearVelocity.magnitude<.01f;yield return null;}
        if(!Check("real stationary-player waiting is calm",calm&&binding.Movement.State(binding)==MovementResolutionState.WaitingForPeer))yield break;
        var playerStart=pb.position;var extent=TileMover.GetMapClearanceExtents(pb,.25f);Vector2 direction=Vector2.zero;
        foreach(var axis in new[]{Vector2.left,Vector2.right,Vector2.down})
        {
            if(!TileMover.CanTraverseBox(region,pb.position,pb.position+axis*2.2f,extent))continue;
            bool clear=true;
            foreach(var hit in Physics2D.BoxCastAll(pb.position,extent*2,0,axis,2.2f,Physics2D.AllLayers))
                if(hit.collider!=null&&!hit.collider.isTrigger&&hit.collider.attachedRigidbody!=pb)clear=false;
            if(clear){direction=axis;break;}
        }
        if(!Check("player has real terrain-clear release route",direction!=Vector2.zero))yield break;
        Key key=direction.x<0?Key.A:direction.x>0?Key.D:Key.S;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));end=Time.realtimeSinceStartup+8;
        while(!finished&&Time.realtimeSinceStartup<end&&Vector2.Distance(pb.position,playerStart)<2)yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        if(!Check("real player movement releases wait",Vector2.Distance(pb.position,playerStart)>=2&&binding.Movement.State(binding)!=MovementResolutionState.WaitingForPeer))yield break;
        float before=Vector2.Distance(start,goal);end=Time.realtimeSinceStartup+8;
        while(!finished&&Time.realtimeSinceStartup<end&&Vector2.Distance(body.position,goal)>before-.5f)yield return null;
        if(!Check("real duty resumes after player clears",Vector2.Distance(body.position,goal)<before-.5f))yield break;
        buddy=null;UnityEngine.Object.FindAnyObjectByType<PauseMenuController>().OpenPause();UnityEngine.Object.FindAnyObjectByType<PauseMenuController>().QuitToMainMenu();
        end=Time.realtimeSinceStartup+30;while(Time.realtimeSinceStartup<end&&SceneManager.GetActiveScene().name!="MainMenu")yield return null;
        for(int i=0;i<5;i++)yield return null;
        Check("calm wait cleanup",DiggableTerrainService.Active==null&&UnityEngine.Object.FindObjectsByType<RegionRoutePump>(FindObjectsSortMode.None).Length==0);Finish();
    }
    [Serializable]sealed class Result{public bool finished;public string error;public string[] checks,runtimeErrors;}
    void Finish(){if(finished)return;finished=true;if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState());if(Time.realtimeSinceStartup>deadline)errors.Add("calm wait watchdog");observer?.Export(directory);File.WriteAllLines(Path.Combine(directory,"calm-wait-motion.csv"),rows);File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=errors.Count>0?"calm wait error":"",checks=checks.ToArray(),runtimeErrors=errors.ToArray()},true));Application.Quit(errors.Count==0&&checks.All(c=>c.StartsWith("PASS "))?0:1);}
    void OnDestroy(){RegionMovementCoordinator.DiagnosticObserver-=Native;Application.logMessageReceived-=Observe;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);}
}
#endif
