#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;
using NUnit.Framework;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.TestTools;
using SporeGobbo.Safety;using SporeGobbo.Geometry;
// Real Camp residents, existing directed duty API, unchanged bodies/terrain/coordinator.
public sealed class Stage64DCooperativeCampArrivalTests
{
    Stage64DContractHost host;Stage64DNativeMotionEvidence native;string directory;
    readonly Dictionary<Behaviour,bool> enabled=new Dictionary<Behaviour,bool>();readonly List<GameObject> markers=new List<GameObject>();
    readonly List<string> trace=new List<string>();readonly HashSet<string> selected=new HashSet<string>();int dropped;
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    IEnumerator Wait(Func<bool> f,string label,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!f()&&Time.realtimeSinceStartup<end)yield return null;Assert.That(f(),Is.True,label);}
    void Capture(RegionMovementCoordinator.DecisionTrace d)
    {native?.Applied(d);if(!selected.Contains(d.Key))return;if(trace.Count>=30000){dropped++;return;}trace.Add(FormattableString.Invariant($"{d.Tick:R},{d.Stage},{d.Key},{d.PeerKey},{d.State},{d.Reason},{d.Position.x:R},{d.Position.y:R},{d.Preferred.x:R},{d.Preferred.y:R},{d.Resolved.x:R},{d.Resolved.y:R},{d.YieldGoal.x:R},{d.YieldGoal.y:R}"));}
    [UnityTest,Timeout(240000)]public IEnumerator MixedSizeArrivalOrdersAndOversizedGoalRejection()
    {
        Assert.That(Persistence.Isolated&&V1RuntimeProfile.DevelopmentStorage&&V1RuntimeProfile.DevelopmentToolsEnabled,Is.True);
        yield return new EnterPlayMode();var go=new GameObject("Owned Camp arrival-order host");UnityEngine.Object.DontDestroyOnLoad(go);host=go.AddComponent<Stage64DContractHost>();host.Begin(Exercise());float end=Time.realtimeSinceStartup+180;while(!host.Complete&&Time.realtimeSinceStartup<end)yield return null;
        Assert.That(host.Complete,Is.True,"Finite scoped host");if(host.Error!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(host.Error).Throw();
    }
    IEnumerator Exercise()
    {
        directory=Persistence.ExperimentPath("Evidence/camp-arrival");Directory.CreateDirectory(directory);trace.Add("tick,stage,key,peer,state,reason,x,y,preferredX,preferredY,resolvedX,resolvedY,yieldX,yieldY");
        SceneManager.LoadScene(GameSceneCatalog.MainMenu);yield return Wait(()=>Find<MainMenuController>()!=null,"Menu",30);yield return null;DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);
        yield return Wait(()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true,"Camp",60);
        yield return Wait(()=>CampStartRoutineManager.Instance!=null&&!CampStartRoutineManager.Instance.IsCampVisitRoutineRunning,"Existing arrival routines complete",60);
        var r=Find<SettlementCampRegion>();var residents=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        var names=new[]{"development-b-buddy-1","development-b-buddy-4","development-b-buddy-2"};var buddies=names.Select(n=>residents.Single(b=>b.unitData.uniqueId==n)).ToArray();foreach(var n in names)selected.Add(n);
        foreach(var b in buddies)foreach(var task in b.GetComponents<Behaviour>().Where(c=>c is BuddyBrain||c is BuddyFollow||c is BuddyCombat||c is BuddyScavenger||c is CampWander||c is CampDirectedWalk))
        {enabled[task]=task.enabled;task.enabled=false;} // Explicit duty-isolation adapter; real body and motion services remain.
        var bodies=buddies.Select(b=>b.GetComponent<Rigidbody2D>()).ToArray();var extents=bodies.Select(b=>TileMover.GetMapClearanceExtents(b,.25f)).ToArray();var bindings=buddies.Select(b=>b.GetComponent<ActorTerrainBinding>()).ToArray();
        foreach(var b in bindings){b.CancelNavigation();ActorMotionIntent.Cancel(b.gameObject);}foreach(var b in bodies)b.linearVelocity=Vector2.zero;
        // A real authored subcell destination fits the small resident, but not the large resident.
        Vector2 narrow=default;bool found=false;
        for(int y=20;y<110&&!found;y+=2)for(int x=20;x<170&&!found;x+=2)
        {var p=r.World(new Point2(x+.25,y+.25));if(TileMover.CanOccupyBox(r,p,extents[0])&&!TileMover.CanOccupyBox(r,p,extents[2])){narrow=p;found=true;}}
        Assert.That(found,Is.True,"Authored geometry must demonstrate actual mixed-size clearance, not invented collider shrinking");
        bool succeeded=false;float until=Time.realtimeSinceStartup+20;
        do{succeeded=bindings[2].TryPath(bodies[2].position,narrow,out var ignored);if(bindings[2].RouteStatus!=GridRouteStatus.Pending)break;yield return null;}while(Time.realtimeSinceStartup<until);
        Debug.Log($"CAMP_OVERSIZED smallHalf={extents[0].ToString("R")} bigHalf={extents[2].ToString("R")} goal={narrow.ToString("R")} smallClear={TileMover.CanOccupyBox(r,narrow,extents[0])} bigClear={TileMover.CanOccupyBox(r,narrow,extents[2])} route={bindings[2].RouteStatus}");
        Assert.That(succeeded,Is.False,"Physically invalid large-body goal is not a traversable route");Assert.That(bindings[2].RouteStatus,Is.Not.EqualTo(GridRouteStatus.Pending),"Honest bounded completion");bindings[2].CancelNavigation();
        var origin=r.World(new Point2(145.5,82.5));Vector2 center=default;bool clear=false;
        for(int oy=-2;oy<=2&&!clear;oy++)for(int ox=-3;ox<=3&&!clear;ox++)
        {
            var candidate=origin+new Vector2(ox,oy);bool ok=true;
            for(int i=0;i<3;i++){var a=candidate+new Vector2((i-1)*2,-1.2f);var b=candidate+new Vector2((1-i)*2,1.2f);ok&=TileMover.CanTraverseBox(r,a,b,extents[i]);}
            foreach(var other in UnityEngine.Object.FindObjectsByType<ActorTerrainBinding>(FindObjectsSortMode.None))if(!bindings.Contains(other)&&Vector2.Distance(other.transform.position,candidate)<5)ok=false;
            if(ok){center=candidate;clear=true;}
        }
        Assert.That(clear,Is.True,"Existing unused Camp floor must fit all real full-body connectors and exclude unrelated blockers");
        native=host.gameObject.AddComponent<Stage64DNativeMotionEvidence>();RegionMovementCoordinator.DiagnosticObserver+=Capture;
        var samples=new List<string>{"round,tick,aX,aY,bX,bY,cX,cY,maxPenetration"};
        try
        {
            for(int round=0;round<2;round++)
            {
                var walks=new CampDirectedWalk[3];
                for(int i=0;i<3;i++)
                {
                    bindings[i].CancelNavigation();ActorMotionIntent.Cancel(buddies[i].gameObject);bodies[i].position=center+new Vector2((i-1)*2,-1.2f);buddies[i].transform.position=bodies[i].position;bodies[i].linearVelocity=Vector2.zero;
                    var marker=new GameObject("Owned separate arrival "+round+"/"+i);markers.Add(marker);marker.transform.position=center+new Vector2((1-i)*2,1.2f);
                    walks[i]=buddies[i].GetComponent<CampDirectedWalk>()??buddies[i].gameObject.AddComponent<CampDirectedWalk>();walks[i].destroyWhenDone=false;walks[i].enableWanderWhenDone=false;walks[i].useArrivalSlot=true;walks[i].enabled=true;
                }
                Physics2D.SyncTransforms();foreach(int i in round==0?new[]{0,1,2}:new[]{2,1,0})walks[i].BeginWalk(markers[round*3+i].transform,1.6f,.18f,30);
                until=Time.realtimeSinceStartup+30;
                while(walks.Any(w=>w.IsWalking)&&Time.realtimeSinceStartup<until)
                {
                    yield return new WaitForFixedUpdate();float max=0;
                    for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)
                    {float penetration=MovementContactPolicy.Penetration(bodies[i].position-bodies[j].position,extents[i]+extents[j]);max=Mathf.Max(max,penetration);Assert.That(penetration,Is.LessThanOrEqualTo(MovementContactPolicy.Allowance(extents[i],extents[j],true)+.0001f),"Unchanged bounded Camp overlap");}
                    samples.Add(FormattableString.Invariant($"{round},{Time.fixedTimeAsDouble:R},{bodies[0].position.x:R},{bodies[0].position.y:R},{bodies[1].position.x:R},{bodies[1].position.y:R},{bodies[2].position.x:R},{bodies[2].position.y:R},{max:R}"));
                    for(int i=0;i<3;i++)Assert.That(TileMover.CanOccupyBox(r,bodies[i].position,extents[i]),Is.True,"No terrain phasing");
                }
                for(int i=0;i<3;i++)Debug.Log($"CAMP_ARRIVAL round={round} key={names[i]} result={walks[i].Result} position={bodies[i].position.ToString("R")} blocker={walks[i].BlockingColliderDescription}");
                Assert.That(walks.All(w=>w.Result==CampDirectedWalkResult.Arrived),Is.True,"Every mixed-size duty must arrive under its original finite30-second budget");
                for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)Assert.That(MovementContactPolicy.Penetration(bodies[i].position-bodies[j].position,extents[i]+extents[j]),Is.Zero,"Distinct settled bodies");
            }
        }
        finally{RegionMovementCoordinator.DiagnosticObserver-=Capture;native.Export(directory);File.WriteAllLines(Path.Combine(directory,"arrival-samples.csv"),samples);File.WriteAllLines(Path.Combine(directory,"arrival-decisions.csv"),trace);File.WriteAllText(Path.Combine(directory,"observer-dropped.txt"),dropped.ToString());}
        Assert.That(dropped,Is.Zero);var coordinator=bindings[0].Movement;
        foreach(var pair in enabled)if(pair.Key!=null)pair.Key.enabled=pair.Value;enabled.Clear();
        Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"Menu",30);for(int i=0;i<5;i++)yield return null;
        Assert.That(coordinator.ActorCount,Is.Zero);Assert.That(coordinator.DestinationCount,Is.Zero);Assert.That(coordinator.OwnedContactOverrides,Is.Zero);Assert.That(coordinator.OwnedNativeFilterLeases,Is.Zero);
    }
    [UnityTearDown]public IEnumerator Cleanup()
    {RegionMovementCoordinator.DiagnosticObserver-=Capture;if(host!=null)host.StopAllCoroutines();foreach(var pair in enabled)if(pair.Key!=null)pair.Key.enabled=pair.Value;enabled.Clear();foreach(var m in markers)if(m!=null)UnityEngine.Object.Destroy(m);if(host!=null)UnityEngine.Object.Destroy(host.gameObject);if(Application.isPlaying)yield return new ExitPlayMode();}
}
#endif
