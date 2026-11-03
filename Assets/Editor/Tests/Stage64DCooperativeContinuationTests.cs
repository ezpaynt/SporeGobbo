#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SporeGobbo.Safety;
using SporeGobbo.Input;
using SporeGobbo.Geometry;

// Fixture-only extraction of the existing leader-cut passage workload. No gameplay implementation.
// Synthetic B, canonical roster/portal, real input, actual excavation and automatic native physics.
public sealed class Stage64DCooperativeContinuationTests
{
    Stage64DContractHost host;Keyboard keyboard;Mouse mouse;string directory;
    Stage64DNativeMotionEvidence observer;readonly List<string> samples=new List<string>();
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    IEnumerator Wait(Func<bool> done,string label,float seconds)
    {float end=Time.realtimeSinceStartup+seconds;while(!done()&&Time.realtimeSinceStartup<end)yield return null;Assert.That(done(),Is.True,label);}
    [UnityTest,Timeout(300000)]public IEnumerator StraightDiagonalTurningRealInputExcavationAndFollowing()
    {
        Assert.That(Persistence.Isolated&&V1RuntimeProfile.DevelopmentStorage&&V1RuntimeProfile.DevelopmentToolsEnabled,Is.True);
        yield return new EnterPlayMode();var go=new GameObject("Owned scoped excavation host");UnityEngine.Object.DontDestroyOnLoad(go);host=go.AddComponent<Stage64DContractHost>();host.Begin(Exercise());
        float end=Time.realtimeSinceStartup+240;while(!host.Complete&&Time.realtimeSinceStartup<end)yield return null;
        Assert.That(host.Complete,Is.True,"Bounded host; each existing traversal/follow deadline remains20 seconds");if(host.Error!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(host.Error).Throw();
    }
    IEnumerator Exercise()
    {
        directory=Persistence.ExperimentPath("Evidence/leader-cut-scoped");Directory.CreateDirectory(directory);
        samples.Add("case,time,leaderX,leaderY,buddyX,buddyY,ready,revision");
        SceneManager.LoadScene(GameSceneCatalog.MainMenu);yield return Wait(()=>Find<MainMenuController>()!=null,"Main Menu",30);yield return null;
        DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);
        yield return Wait(()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true,"Real Camp",60);
        var state=GameState.Instance;
        foreach(var id in state.activeSquadIds.ToArray())Assert.That(BuddyRosterService.MoveBuddyToReserve(state,id),Is.True);
        var selected=state.ownedGobbos.Single(b=>b.uniqueId=="development-b-buddy-1");
        Assert.That(BuddyRosterService.MoveBuddyToActiveSquad(state,selected.uniqueId),Is.True,"Real roster selects one existing small Buddy; no body-size edits");
        var portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);yield return null;portal.goButton.onClick.Invoke();
        yield return Wait(()=>Find<ExpeditionRegion>()?.PresentationReady==true&&Find<ExpeditionRegion>()?.Player!=null&&UnityEngine.Object.FindObjectsByType<BuddyBrain>(FindObjectsSortMode.None).Count(b=>b.SquadScoped)==1,"Actual expedition",60);
        var r=Find<ExpeditionRegion>();var buddy=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Single(b=>b.unitData.uniqueId==selected.uniqueId);
        keyboard=InputSystem.AddDevice<Keyboard>("Owned scoped passage keyboard");mouse=InputSystem.AddDevice<Mouse>("Owned scoped passage mouse");keyboard.MakeCurrent();mouse.MakeCurrent();
        observer=host.gameObject.AddComponent<Stage64DNativeMotionEvidence>();RegionMovementCoordinator.DiagnosticObserver+=Capture;
        var paths=new List<Point2[]>();var straight=new List<Point2>();var diagonal=new List<Point2>();var turning=new List<Point2>();
        for(int i=0;i<=32;i++){straight.Add(new Point2(60.5+i*.5,30.5));diagonal.Add(new Point2(60.5+i*.5,30.5-i*.5));turning.Add(new Point2(60.5-i*.5,30.5));}
        for(int i=1;i<=32;i++)turning.Add(new Point2(44.5,30.5-i*.5));
        paths.Add(straight.ToArray());paths.Add(diagonal.ToArray());paths.Add(turning.ToArray());var names=new[]{"straight","diagonal","turning"};float nextSample=0;
        try
        {
            for(int test=0;test<paths.Count;test++)
            {
                var points=paths[test];var player=r.Player;var pb=player.GetComponent<Rigidbody2D>();var bb=buddy.GetComponent<Rigidbody2D>();
                // Identical original owned start positioning, then real keyboard/aim/AI only.
                pb.position=r.World(points[0]);pb.linearVelocity=Vector2.zero;player.transform.position=pb.position;
                var forward=(r.World(points[1])-r.World(points[0])).normalized;bb.position=pb.position-forward*1.8f;buddy.transform.position=bb.position;bb.linearVelocity=Vector2.zero;
                buddy.GetComponent<BuddyFollow>()?.ResetCommandMotion();Physics2D.SyncTransforms();player.IssueBuddyCommand(BuddyCommand.Passive);player.IssueBuddyCommand(BuddyCommand.Follow);
                int edits=r.Excavations,attempts=player.GetComponent<ActorTerrainBinding>().DigAttempts,cursor=1;bool bodyClear=true,crossedMiddle=false;var middle=r.World(points[points.Length/2]);float end=Time.realtimeSinceStartup+20;
                var pe=TileMover.GetMapClearanceExtents(pb,.25f);var be=TileMover.GetMapClearanceExtents(bb,.25f);
                while(cursor<points.Length&&Time.realtimeSinceStartup<end)
                {
                    Vector2 delta=r.World(points[cursor])-pb.position;if(delta.magnitude<.08f){cursor++;continue;}
                    var keys=new List<Key>{Key.LeftShift};if(delta.x>.05f)keys.Add(Key.D);else if(delta.x<-.05f)keys.Add(Key.A);if(delta.y>.05f)keys.Add(Key.W);else if(delta.y<-.05f)keys.Add(Key.S);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));var screen=r.RegionCamera.WorldToScreenPoint(r.Render(r.World(points[cursor])));InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});
                    // Original queued inputs above are consumed by the normal Dynamic input update.
                    if(r.LogicalReady)bodyClear&=TileMover.CanOccupyBox(r,pb.position,pe)&&TileMover.CanOccupyBox(r,bb.position,be);
                    crossedMiddle|=Vector2.Distance(bb.position,middle)<1.2f;
                    if(Time.realtimeSinceStartup>=nextSample){nextSample=Time.realtimeSinceStartup+.1f;samples.Add(FormattableString.Invariant($"{names[test]},{Time.realtimeSinceStartup:R},{pb.position.x:R},{pb.position.y:R},{bb.position.x:R},{bb.position.y:R},{r.LogicalReady},{r.Revision}"));}
                    Assert.That(samples.Count,Is.LessThan(4000),"Original bounded sample capacity");yield return null;
                }
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());
                Debug.Log($"SCOPED_PASSAGE case={names[test]} stage=leader cursor={cursor}/{points.Length} edits={r.Excavations-edits} attempts={player.GetComponent<ActorTerrainBinding>().DigAttempts-attempts} position={pb.position.ToString("R")} buddy={bb.position.ToString("R")} bodyClear={bodyClear}");
                Assert.That(cursor==points.Length&&r.Excavations>edits&&player.GetComponent<ActorTerrainBinding>().DigAttempts>attempts,Is.True,"Existing20-second actual leader excavation/traversal: "+names[test]);
                yield return Wait(()=>r.PresentationReady,"Collision/view publication: "+names[test],10);
                bool usable=true;for(int i=1;i<points.Length;i++)usable&=TileMover.CanTraverseBox(r,r.World(points[i-1]),r.World(points[i]),be);
                Assert.That(usable,Is.True,"Actual Buddy body passage clearance: "+names[test]);
                end=Time.realtimeSinceStartup+20;
                while(Time.realtimeSinceStartup<end&&Vector2.Distance(bb.position,pb.position)>1.7f)
                {if(r.LogicalReady)bodyClear&=TileMover.CanOccupyBox(r,bb.position,be);crossedMiddle|=Vector2.Distance(bb.position,middle)<1.2f;yield return null;}
                Debug.Log($"SCOPED_PASSAGE case={names[test]} stage=buddy distance={Vector2.Distance(bb.position,pb.position):R} crossed={crossedMiddle} bodyClear={bodyClear} half={be.ToString("R")} followGoal={buddy.GetComponent<ActorTerrainBinding>().CurrentMovementGoal.ToString("R")} route={buddy.GetComponent<ActorTerrainBinding>().RouteStatus}");
                Assert.That(bodyClear&&crossedMiddle&&Vector2.Distance(bb.position,pb.position)<=1.7f,Is.True,"Original full-body/midpoint/1.7m following contract: "+names[test]);
            }
        }
        finally{RegionMovementCoordinator.DiagnosticObserver-=Capture;Release();File.WriteAllLines(Path.Combine(directory,"leader-cut-passage-samples.csv"),samples);observer.Export(directory);}
        var coordinator=r.Player.GetComponent<ActorTerrainBinding>().Movement;
        Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"Menu teardown",30);for(int i=0;i<5;i++)yield return null;
        Assert.That(coordinator.ActorCount,Is.Zero);Assert.That(coordinator.DestinationCount,Is.Zero);Assert.That(coordinator.OwnedContactOverrides,Is.Zero);Assert.That(coordinator.OwnedNativeFilterLeases,Is.Zero);
    }
    void Capture(RegionMovementCoordinator.DecisionTrace trace)=>observer?.Applied(trace);
    void Release(){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState());if(mouse!=null)InputSystem.QueueStateEvent(mouse,new MouseState());}
    [UnityTearDown]public IEnumerator Cleanup()
    {
        RegionMovementCoordinator.DiagnosticObserver-=Capture;Release();if(host!=null)host.StopAllCoroutines();
        if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}
        if(host!=null){UnityEngine.Object.Destroy(host.gameObject);host=null;}
        if(Application.isPlaying)yield return new ExitPlayMode();
    }
}
#endif

