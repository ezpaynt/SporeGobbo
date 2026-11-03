#if UNITY_EDITOR
using System;
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

// Real-scene/input contact gate. Stationary peers are explicit fixture task-disable adapters;
// body types, colliders, physics settings and player motor are not replaced.
public sealed class Stage64DExpeditionContactPolicyTests
{
    Stage64DContractHost host;Keyboard keyboard;readonly Dictionary<Behaviour,bool> enabled=new Dictionary<Behaviour,bool>();
    RegionMovementCoordinator coordinator;Collider2D playerCollider;readonly List<(Collider2D collider,bool ignored)> pairs=new List<(Collider2D,bool)>();
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    IEnumerator Wait(Func<bool> done,string label,float seconds)
    {float end=Time.realtimeSinceStartup+seconds;while(!done()&&Time.realtimeSinceStartup<end)yield return null;Assert.That(done(),Is.True,label);}
    [UnityTest,Timeout(240000)]public IEnumerator RealInputPlayerPassesStationarySquadAndLeavesOverlap()
    {
        Assert.That(Persistence.Isolated&&V1RuntimeProfile.DevelopmentStorage&&V1RuntimeProfile.DevelopmentToolsEnabled,Is.True);
        yield return new EnterPlayMode();var go=new GameObject("Owned expedition contact input host");UnityEngine.Object.DontDestroyOnLoad(go);host=go.AddComponent<Stage64DContractHost>();host.Begin(Exercise());
        float end=Time.realtimeSinceStartup+120;while(!host.Complete&&Time.realtimeSinceStartup<end)yield return null;
        Assert.That(host.Complete,Is.True,"Existing real-service host finite120-second deadline");if(host.Error!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(host.Error).Throw();
    }
    IEnumerator Exercise()
    {
        SceneManager.LoadScene(GameSceneCatalog.MainMenu);yield return Wait(()=>Find<MainMenuController>()!=null,"Main Menu ready",30);yield return null;
        DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);
        yield return Wait(()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true,"Real established Camp ready",60);
        var state=GameState.Instance;foreach(var data in state.ownedGobbos)if(state.activeSquadIds.Count<state.maxActiveSquad&&!state.activeSquadIds.Contains(data.uniqueId))BuddyRosterService.MoveBuddyToActiveSquad(state,data.uniqueId);
        var portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);yield return null;portal.goButton.onClick.Invoke();
        yield return Wait(()=>Find<ExpeditionRegion>()?.PresentationReady==true&&Find<ExpeditionRegion>()?.Player!=null&&UnityEngine.Object.FindObjectsByType<BuddyBrain>(FindObjectsSortMode.None).Count(b=>b.SquadScoped)==5,"Real five-Buddy expedition ready",60);
        var region=Find<ExpeditionRegion>();var player=region.Player;var pb=player.GetComponent<Rigidbody2D>();playerCollider=player.GetComponent<Collider2D>();
        var buddies=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Where(b=>b.GetComponent<BuddyBrain>()?.SquadScoped==true).OrderBy(b=>b.unitData.uniqueId,StringComparer.Ordinal).ToArray();
        coordinator=player.GetComponent<ActorTerrainBinding>().Movement;Assert.That(coordinator,Is.Not.Null);
        foreach(var b in buddies)
        {
            foreach(var task in b.GetComponents<Behaviour>().Where(c=>c is BuddyBrain||c is BuddyFollow||c is BuddyCombat||c is BuddyScavenger||c is CampWander||c is CampDirectedWalk))
            {enabled[task]=task.enabled;task.enabled=false;}
            b.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
            var c=b.GetComponent<Collider2D>();pairs.Add((c,Physics2D.GetIgnoreCollision(playerCollider,c)));
        }
        coordinator.RefreshContacts();foreach(var b in buddies)
        {
            Assert.That(RegionMovementCoordinator.EffectiveNativeContact(playerCollider,b.GetComponent<Collider2D>()),Is.False,"Declared friendly group has explicit native passage");
            Assert.That(Physics2D.GetIgnoreCollision(playerCollider,b.GetComponent<Collider2D>()),Is.True,"Region explicitly owns the friendly pair, not just the global matrix default");
        }
        keyboard=InputSystem.AddDevice<Keyboard>("Owned expedition contact keyboard");keyboard.MakeCurrent();
        var origin=new Vector2(12.3f,16.5f);
        foreach(var mode in new[]{"stationary","several","diagonal","already-overlapped"})
        {
            Place(region,player.gameObject,origin);
            Vector2 direction=mode=="diagonal"?new Vector2(1,1).normalized:Vector2.right;
            int count=mode=="several"?3:1;
            for(int i=0;i<count;i++)Place(region,buddies[i].gameObject,origin+direction*(mode=="already-overlapped"?0:1.25f+i*1.1f));
            var last=buddies[count-1].GetComponent<Rigidbody2D>();float goal=mode=="several"?4.6f:2.5f;
            Assert.That(TileMover.CanTraverseBox(region,origin,origin+direction*(goal+.2f),TileMover.GetMapClearanceExtents(pb,.25f)),Is.True,"Fixture input connector must be physically traversable");
            var stationary=buddies.Take(count).Select(b=>b.GetComponent<Rigidbody2D>().position).ToArray();
            InputSystem.QueueStateEvent(keyboard,mode=="diagonal"?new KeyboardState(Key.D,Key.W):new KeyboardState(Key.D));
            int samples=0,inside=0;float end=Time.realtimeSinceStartup+2;float maxOverlap=0;
            while(Time.realtimeSinceStartup<end&&Vector2.Dot(pb.position-origin,direction)<goal)
            {
                yield return new WaitForFixedUpdate();samples++;
                float depth=MovementContactPolicy.Penetration(pb.position-last.position,TileMover.GetMapClearanceExtents(pb,.25f)+TileMover.GetMapClearanceExtents(last,.1f));maxOverlap=Mathf.Max(maxOverlap,depth);if(depth>.1f)inside++;
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            TestContext.WriteLine($"REAL_PLAYER_PASS mode={mode} samples={samples} inside={inside} maxOverlap={maxOverlap:R} progress={Vector2.Dot(pb.position-origin,direction):R} velocity={pb.linearVelocity}");
            Assert.That(Vector2.Dot(pb.position-origin,direction),Is.GreaterThanOrEqualTo(goal),"Own Buddies cannot stop or redirect real keyboard movement");
            Assert.That(samples,Is.GreaterThan(0));Assert.That(inside,Is.GreaterThan(0),"Actual passage must overlap, not merely bypass a body");
            for(int i=0;i<count;i++)Assert.That(Vector2.Distance(buddies[i].GetComponent<Rigidbody2D>().position,stationary[i]),Is.LessThan(.01f),"No native pushing or invented avoidance");
        }
        var removed=buddies[0].GetComponent<ActorTerrainBinding>();coordinator.Unregister(removed);
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider,buddies[0].GetComponent<Collider2D>()),Is.False,"Unregistration releases owned pair filtering");
        coordinator.Configure(removed,buddies[0].unitData.uniqueId,1,false);coordinator.RefreshContacts();
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider,buddies[0].GetComponent<Collider2D>()),Is.True,"Re-registration owns the declared friendly pair again");
        foreach(var pair in enabled)if(pair.Key!=null)pair.Key.enabled=pair.Value;enabled.Clear();
        Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"Menu teardown",30);for(int i=0;i<5;i++)yield return null;
        Assert.That(coordinator.ActorCount,Is.Zero);Assert.That(coordinator.OwnedContactOverrides,Is.Zero);Assert.That(coordinator.OwnedNativeFilterLeases,Is.Zero);
    }
    static void Place(ExpeditionRegion region,GameObject actor,Vector2 p)
    {var body=actor.GetComponent<Rigidbody2D>();Assert.That(TileMover.CanOccupyBox(region,p,TileMover.GetMapClearanceExtents(body,.1f)),Is.True,"Owned fixture placement uses full native terrain clearance");body.position=p;body.linearVelocity=Vector2.zero;actor.transform.position=p;Physics2D.SyncTransforms();}
    [UnityTearDown]public IEnumerator Cleanup()
    {
        if(keyboard!=null){InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);keyboard=null;}
        foreach(var pair in enabled)if(pair.Key!=null)pair.Key.enabled=pair.Value;enabled.Clear();
        if(host!=null){host.StopAllCoroutines();UnityEngine.Object.Destroy(host.gameObject);host=null;}
        if(Application.isPlaying)yield return new ExitPlayMode();
    }
}
#endif
