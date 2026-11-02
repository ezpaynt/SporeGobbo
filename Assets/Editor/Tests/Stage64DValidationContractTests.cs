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
using SporeGobbo.CampCandidate;

// Reviewed real-service gate: actual Menu -> synthetic B -> actual Camp/Pit, real actors/AI/damage.
// Automated input, clear initial placement and the bounded intent probe are explicit fixture adapters.
public sealed class Stage64DValidationContractTests
{
    readonly List<string> failures=new List<string>();
    readonly List<Stage64DContactProbe> probes=new List<Stage64DContactProbe>();
    CampFightPitLab lab; SettlementCampRegion region; GobboController player;
    Keyboard keyboard; Mouse mouse; Stage64DContractHost host;RegionMovementCoordinator exitingCoordinator;
    static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    void Check(string name,bool ok){Debug.Log("CONTRACT_CHECK "+(ok?"PASS ":"FAIL ")+name);if(!ok)failures.Add(name);}
    static int Health(GameObject a)=>a.GetComponent<GobboController>() is GobboController p?p.health:a.GetComponent<BuddyUnit>() is BuddyUnit b?b.unitData.health:a.GetComponent<TunnelWeevilEnemy>().health;
    IEnumerator Wait(Func<bool> done,string description,float seconds)
    {float end=Time.realtimeSinceStartup+seconds;while(!done()&&Time.realtimeSinceStartup<end)yield return null;Assert.That(done(),Is.True,description+" (unchanged bounded wait)");}
    IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
    void ReleaseInput(){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState());if(mouse!=null)InputSystem.QueueStateEvent(mouse,new MouseState());}
    void DrivePlayer(GameObject target)
    {
        var delta=(Vector2)(target.transform.position-player.transform.position);
        var screen=region.RegionCamera.WorldToScreenPoint(region.Render(target.transform.position));
        Stage64DPlayerAttackObservation.Queue(player,screen,false);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,buttons=1});
        var key=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?(delta.x>0?Key.D:Key.A):(delta.y>0?Key.W:Key.S);
        InputSystem.QueueStateEvent(keyboard,delta.magnitude>Mathf.Max(.65f,player.attackRange)?new KeyboardState(key):new KeyboardState());
        // Queue only: the normal Dynamic update delivers these events through actual InputActions.
        // The observer stage below records queue return, before automatic input dispatch.
        Stage64DPlayerAttackObservation.Queue(player,screen,true);
    }
    void Place(GameObject actor,Vector2 p)
    {
        var body=actor.GetComponent<Rigidbody2D>();
        Assert.That(TileMover.CanOccupyBox(region,p,TileMover.GetMapClearanceExtents(body,.25f)),Is.True,"Fixture placement requires actual executable terrain clearance");
        body.position=p;body.linearVelocity=Vector2.zero;actor.transform.position=p;Physics2D.SyncTransforms();
    }
    [UnityTest, Timeout(240000)]
    public IEnumerator RealPlayerBuddyEnemyAndOpposingPitContacts()
    {
        Assert.That(Persistence.Isolated&&V1RuntimeProfile.DevelopmentStorage&&V1RuntimeProfile.DevelopmentToolsEnabled,Is.True,"Reviewed development profile only");
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying,Is.True);Assert.That(Physics2D.simulationMode,Is.EqualTo(SimulationMode2D.FixedUpdate),"Real-service gate requires unchanged production automatic physics");
        Assert.That(Persistence.Isolated&&V1RuntimeProfile.DevelopmentStorage&&V1RuntimeProfile.DevelopmentToolsEnabled,Is.True);
        var owner=new GameObject("Owned collision-contract coroutine host");UnityEngine.Object.DontDestroyOnLoad(owner);host=owner.AddComponent<Stage64DContractHost>();host.Begin(RealContacts());
        float deadline=Time.realtimeSinceStartup+120;while(!host.Complete&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.That(host.Complete,Is.True,"Real-service contract host 120-second bounded deadline");if(host.Error!=null){Debug.Log("CONTRACT_HOST_EXCEPTION "+host.Error);System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(host.Error).Throw();}
    }
    IEnumerator RealContacts()
    {
        SceneManager.LoadScene(GameSceneCatalog.MainMenu);
        yield return Wait(()=>Find<MainMenuController>()!=null,"Actual Main Menu",30);
        yield return Frames(2);
        DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);
        yield return Wait(()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true,"Actual synthetic established Camp",60);
        region=Find<SettlementCampRegion>();lab=Find<CampFightPitLab>();player=Find<CampPlayableSpawner>().SpawnedPlayer;
        Assert.That(region.OpenPitDevelopmentEntrance(),Is.True);yield return Frames(3);
        keyboard=InputSystem.AddDevice<Keyboard>("Owned contract keyboard");mouse=InputSystem.AddDevice<Mouse>("Owned contract mouse");keyboard.MakeCurrent();mouse.MakeCurrent();
        // Real input reads remain active; a virtual device is an adapter, not direct attack dispatch.
        foreach(var mode in new[]{"PlayerWeevil","BuddyWeevil","BuddyBuddy","BuddyBuddyTeams"})
        {
            var residents=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Where(b=>b.CombatAlive&&GameState.Instance.ownedGobbos.Contains(b.unitData)).OrderBy(b=>b.unitData.uniqueId,StringComparer.Ordinal).ToArray();
            Assert.That(residents.Length,Is.GreaterThanOrEqualTo(3));
            var savedFilters=CaptureFilters(residents.Select(r=>r.gameObject));
            var savedHealth=residents.ToDictionary(b=>b,b=>b.unitData.health);
            var savedSim=residents.ToDictionary(b=>b,b=>b.GetComponent<Rigidbody2D>().simulated);
            var savedLayers=residents.ToDictionary(b=>b,b=>b.gameObject.layer);
            var savedEnabled=residents.ToDictionary(b=>b,b=>b.GetComponents<Behaviour>().Where(x=>x is BuddyBrain||x is BuddyCombat||x is BuddyFollow||x is CampWander||x is CampDirectedWalk||x is BuddyScavenger).ToDictionary(x=>x,x=>x.enabled));
            lab.Watch=mode!="PlayerWeevil";lab.Opponent=mode.StartsWith("BuddyBuddy",StringComparison.Ordinal)?"Buddy":"Weevil";lab.OpponentCount=1;lab.Preset="Open";lab.SelectedBuddies.Clear();if(lab.Watch)lab.SelectedBuddies.Add(residents[0].unitData.uniqueId);if(mode=="BuddyBuddyTeams")lab.SelectedBuddies.Add(residents[1].unitData.uniqueId);
            yield return Wait(()=>region.Ready&&region.ResidentialEntranceOpen&&!lab.Active,"Real Pit terrain readiness before natural session",10);
            Debug.Log($"CONTRACT_READY mode={mode} ready={region.Ready} entrance={region.ResidentialEntranceOpen} active={lab.Active} development={V1RuntimeProfile.DevelopmentStorage}/{V1RuntimeProfile.DevelopmentToolsEnabled}");
            lab.StartSession();Assert.That(lab.Fighting,Is.True);
            var a=lab.Fighters.First(x=>x.GetComponent<CombatSessionBinding>().Team==0);
            var b=lab.Fighters.Single(x=>x.GetComponent<CombatSessionBinding>().Team==1);
            var ca=a.GetComponent<Collider2D>();var cb=b.GetComponent<Collider2D>();
            var ba=a.GetComponent<ActorTerrainBinding>();var bb=b.GetComponent<ActorTerrainBinding>();
            Check(mode+" same live regional coordinator",ba.Movement!=null&&ReferenceEquals(ba.Movement,bb.Movement));
            Check(mode+" real opposing session",ReferenceEquals(a.GetComponent<CombatSessionBinding>().Rules,lab)&&ReferenceEquals(b.GetComponent<CombatSessionBinding>().Rules,lab)&&lab.Allows(a,b)&&lab.Allows(b,a));
            var spectators=lab.Spectators.ToArray();
            Check(mode+" spectators ineligible and nonparticipating",spectators.Length>0&&spectators.All(s=>!lab.IsMovementParticipant(s)&&!lab.Allows(s,a)&&!s.GetComponent<Rigidbody2D>().simulated));
            bool layerIgnored=Physics2D.GetIgnoreLayerCollision(ca.gameObject.layer,cb.gameObject.layer),pairIgnored=Physics2D.GetIgnoreCollision(ca,cb);
            Debug.Log($"CONTRACT_FILTER mode={mode} layers={ca.gameObject.layer}/{cb.gameObject.layer} layerIgnored={layerIgnored} pairIgnored={pairIgnored} include={ca.includeLayers.value}/{cb.includeLayers.value} exclude={ca.excludeLayers.value}/{cb.excludeLayers.value} strictReported={ba.Movement.VerifyStrict(a)}/{bb.Movement.VerifyStrict(b)} collider={ca.GetType().Name}/{cb.GetType().Name}");
            Check(mode+" effective native collision enabled",RegionMovementCoordinator.EffectiveNativeContact(ca,cb));
            Check(mode+" global Buddy matrix unchanged",Physics2D.GetIgnoreLayerCollision(6,6));
            if(mode=="BuddyBuddyTeams")
            {var mate=lab.Fighters.First(x=>x!=a&&x.GetComponent<CombatSessionBinding>().Team==0);Check(mode+" same-team native exclusion preserved",!RegionMovementCoordinator.EffectiveNativeContact(ca,mate.GetComponent<Collider2D>()));}
            var center=region.World(CampFightPitLayout.Center);float halfA=ca.bounds.extents.x,halfB=cb.bounds.extents.x;
            Place(a,center-Vector2.right*(halfA+.4f));Place(b,center+Vector2.right*(halfB+.4f));
            // Observe actual autonomous attacks before the service-driven contact probe.
            int hitsA=0,hitsB=0,stopSamples=0;float minOrdering=float.PositiveInfinity;int receipts=0;
            Action<EnemyDamageRequest,EnemyDamageResult> observe=(request,result)=>{if(!result.Accepted||request?.source==null)return;if(request.source==a&&result.Target!=null&&result.Target.gameObject==b)hitsA++;if(request.source==b&&result.Target!=null&&result.Target.gameObject==a)hitsB++;receipts++;};
            EnemyDamage.EvidenceReceipt+=observe;
            if(mode=="PlayerWeevil")Stage64DPlayerAttackObservation.Begin(player,b,mouse);
            try
            {
                float end=Time.realtimeSinceStartup+5;
                while(lab.Fighting&&Time.realtimeSinceStartup<end)
                {
                    if(mode=="PlayerWeevil")DrivePlayer(b);
                    if(a.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude<.0001f||b.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude<.0001f)stopSamples++;
                    minOrdering=Mathf.Min(minOrdering,b.transform.position.x-a.transform.position.x);
                    yield return new WaitForFixedUpdate();
                }
            }
            finally{EnemyDamage.EvidenceReceipt-=observe;if(mode=="PlayerWeevil")Check("player observer no evidence overflow",Stage64DPlayerAttackObservation.End()==0);ReleaseInput();}
            hitsA=lab.FriendlyHits;hitsB=lab.OpponentHits;
            Debug.Log($"CONTRACT_COMBAT mode={mode} hitsA={hitsA} hitsB={hitsB} optionalEvidenceReceipts={receipts} evidenceEnabled={V1RuntimeProfile.EvidenceEnabled} stopSamples={stopSamples} time={Time.time:R} fixed={Time.fixedTime:R} context={SporeGobbo.Input.SporeInputReader.Instance.Context} primary={SporeGobbo.Input.SporeInputReader.Instance.PrimaryAttack.IsHeld} minAxisOrder={minOrdering:R} health={Health(a)}/{Health(b)} phase={lab.Phase}");
            var reader=SporeGobbo.Input.SporeInputReader.Instance;
            Debug.Log($"CONTRACT_COMBAT_STATE mode={mode} alive={EnemyDamage.Alive(EnemyDamage.Resolve(a))}/{EnemyDamage.Alive(EnemyDamage.Resolve(b))} ready={EnemyDamage.Ready(a)}/{EnemyDamage.Ready(b)} eligible={EnemyDamage.Eligible(a,EnemyDamage.Resolve(b))}/{EnemyDamage.Eligible(b,EnemyDamage.Resolve(a))} sight={ActorTerrainBinding.HasSight(a,a.transform.position,b.transform.position)} suppressed={reader.GameplayButtonsSuppressed} rawMouse={mouse.leftButton.isPressed} inputMode={InputSystem.settings.updateMode} aiEnabled={b.GetComponent<TunnelWeevilEnemy>()?.enabled} sourceEnabled={a.GetComponent<GobboController>()?.enabled}");
            Check(mode+" natural opposing damage",hitsA>0&&hitsB>0);
            Check(mode+" attack stopping observed",stopSamples>0);
            Check(mode+" protected combat living identities",Health(a)>=1&&Health(b)>=1);
            if(lab.Fighting)lab.Resolve("contract natural resolution");
            Check(mode+" filters restored immediately at resolution",FiltersMatch(savedFilters));
            lab.StopSession("contract natural round cleanup");yield return Frames(2);
            Check(mode+" natural cleanup",!lab.Active&&!lab.CleanupBlocked);
            // New real session, same campaign identities/parameters, finite probe without attacks.
            yield return Wait(()=>region.Ready&&region.ResidentialEntranceOpen&&!lab.Active,"Real Pit terrain readiness before contact session",10);
            lab.StartSession();a=lab.Fighters.First(x=>x.GetComponent<CombatSessionBinding>().Team==0);b=lab.Fighters.Single(x=>x.GetComponent<CombatSessionBinding>().Team==1);
            ca=a.GetComponent<Collider2D>();cb=b.GetComponent<Collider2D>();
            // Real resolution freezes attacks without falsely disabling a living health owner during Fighting.
            if(mode.StartsWith("BuddyBuddy",StringComparison.Ordinal))
            {
                // Keep the actual match Fighting for native participant contact; BuddyUnit remains the living health owner.
                foreach(var fighter in lab.Fighters)foreach(var behavior in fighter.GetComponents<Behaviour>())
                    if(behavior is BuddyBrain||behavior is BuddyCombat||behavior is BuddyFollow||behavior is BuddyScavenger)behavior.enabled=false;
                Assert.That(lab.Fighting,Is.True);
            }
            else lab.Resolve("Owned resolved-session enemy contact characterization");
            player.SuppressTemporaryInput=true;
            Place(a,center-Vector2.right*(ca.bounds.extents.x+.15f));Place(b,center+Vector2.right*(cb.bounds.extents.x+.15f));
            var pa=a.AddComponent<Stage64DContactProbe>();var pb=b.AddComponent<Stage64DContactProbe>();probes.Add(pa);probes.Add(pb);
            float speedA=a.GetComponent<GobboController>() is GobboController gp?gp.moveSpeed:a.GetComponent<BuddyUnit>().unitData.moveSpeed;
            float speedB=b.GetComponent<TunnelWeevilEnemy>() is TunnelWeevilEnemy weevil?weevil.chaseSpeed:b.GetComponent<BuddyUnit>().unitData.moveSpeed;
            pa.Requested=Vector2.right*speedA;pb.Requested=Vector2.left*speedB;
            var ra=a.GetComponent<Rigidbody2D>();var rb=b.GetComponent<Rigidbody2D>();var startA=ra.position;var startB=rb.position;
            int contacts=0;float minOrder=float.PositiveInfinity,minDistance=float.PositiveInfinity;var contactBuffer=new ContactPoint2D[32];
            for(int tick=0;tick<40;tick++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(ra!=null&&rb!=null&&lab.Active,Is.True,"Real session unexpectedly ended during resolved contact probe: "+lab.Outcome);
                int count=ra.GetContacts(contactBuffer);Assert.That(count,Is.LessThan(contactBuffer.Length),"contact evidence overflow");
                for(int i=0;i<count;i++)if(contactBuffer[i].collider==cb||contactBuffer[i].otherCollider==cb)contacts++;
                minOrder=Mathf.Min(minOrder,rb.position.x-ra.position.x);var distance=ca.Distance(cb);if(distance.isValid)minDistance=Mathf.Min(minDistance,distance.distance);
                Debug.Log($"CONTRACT_CONTACT mode={mode} tick={tick} a={ra.position.ToString("R")} b={rb.position.ToString("R")} velocityA={ra.linearVelocity.ToString("R")} velocityB={rb.linearVelocity.ToString("R")} nativeDistance={distance.distance:R} contacts={count}");
            }
            pa.Requested=pb.Requested=Vector2.zero;for(int tick=0;tick<20;tick++)yield return new WaitForFixedUpdate();
            if(mode.StartsWith("BuddyBuddy",StringComparison.Ordinal))Check(mode+" opposing contact measured during Fighting",lab.Fighting&&RegionMovementCoordinator.EffectiveNativeContact(ca,cb));
            Check(mode+" actual native opposing contacts",contacts>0);
            Check(mode+" no physical pass-through",minOrder>0);
            Check(mode+" native blocking prevents requested relative travel",(startB.x-startA.x)-(rb.position.x-ra.position.x)<(speedA+speedB)*40*Time.fixedDeltaTime);
            Check(mode+" stopped after movement release",ra.linearVelocity.sqrMagnitude<.0001f&&rb.linearVelocity.sqrMagnitude<.0001f);
            Debug.Log($"CONTRACT_PROBE mode={mode} contacts={contacts} minOrder={minOrder:R} minNativeDistance={minDistance:R} intendedTravel={speedA*40*Time.fixedDeltaTime:R}/{speedB*40*Time.fixedDeltaTime:R} final={ra.position.ToString("R")}/{rb.position.ToString("R")}");
            foreach(var probe in probes)if(probe!=null){probe.enabled=false;UnityEngine.Object.Destroy(probe);}probes.Clear();
            if(lab.Fighting)lab.Resolve("contract contact resolution");
            Check(mode+" contact filters restored at resolution",FiltersMatch(savedFilters));
            lab.StopSession("contract probe cleanup");yield return Frames(3);
            Check(mode+" session teardown complete",!lab.Active&&!lab.CleanupBlocked&&lab.Fighters.Count==0&&lab.Spectators.Count==0);
            Check(mode+" health simulation layers routines restored",residents.All(r=>r!=null&&r.unitData.health==savedHealth[r]&&r.GetComponent<Rigidbody2D>().simulated==savedSim[r]&&r.gameObject.layer==savedLayers[r]&&savedEnabled[r].All(p=>p.Key!=null&&p.Key.enabled==p.Value)&&r.GetComponent<CombatSessionBinding>()?.Rules==null));
            Check(mode+" original collider filters restored",FiltersMatch(savedFilters));
            Check(mode+" no session projectiles",!UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Any(p=>ReferenceEquals(p.SessionRules,lab)));
        }
        yield return FailurePaths();
        Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();
        yield return Wait(()=>Find<MainMenuController>()!=null,"Real Menu teardown",30);yield return Frames(5);
        Check("regional contact ownership empty at Menu",exitingCoordinator!=null&&exitingCoordinator.ActorCount==0&&exitingCoordinator.OwnedNativeFilterLeases==0&&exitingCoordinator.OwnedContactOverrides==0);
        Check("region authority released at Menu",DiggableTerrainService.Active==null&&Find<SettlementCampRegion>()==null&&Find<CampFightPitLab>()==null);
        Assert.That(failures,Is.Empty,string.Join("; ",failures));
    }
    struct FilterState
    {
        public int Include,Exclude,Priority;
        public FilterState(Collider2D c){Include=c.includeLayers;Exclude=c.excludeLayers;Priority=c.layerOverridePriority;}
        public bool Matches(Collider2D c)=>c!=null&&c.includeLayers.value==Include&&c.excludeLayers.value==Exclude&&c.layerOverridePriority==Priority;
    }
    static Dictionary<Collider2D,FilterState> CaptureFilters(IEnumerable<GameObject> actors)
        =>actors.SelectMany(a=>a.GetComponentsInChildren<Collider2D>(true)).ToDictionary(c=>c,c=>new FilterState(c));
    static bool FiltersMatch(Dictionary<Collider2D,FilterState> expected)=>expected.All(p=>p.Value.Matches(p.Key));
    IEnumerator FailurePaths()
    {
        var residents=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Where(b=>b.CombatAlive&&GameState.Instance.ownedGobbos.Contains(b.unitData)).OrderBy(b=>b.unitData.uniqueId,StringComparer.Ordinal).ToArray();
        var filters=CaptureFilters(residents.Select(r=>r.gameObject));
        lab.SelectedBuddies.Clear();lab.SelectedBuddies.Add(residents[0].unitData.uniqueId);lab.Watch=true;lab.Opponent="Buddy";lab.OpponentCount=1;lab.Preset="Open";
        yield return Wait(()=>region.Ready,"Failure-path terrain readiness",10);
        lab.StartSession();Check("cancellation acquired opposing filter",RegionMovementCoordinator.EffectiveNativeContact(lab.Fighters[0].GetComponent<Collider2D>(),lab.Fighters[1].GetComponent<Collider2D>()));
        lab.StopSession("explicit cancellation");Check("cancellation immediately restores all owned filters",!lab.Active&&!lab.CleanupBlocked&&FiltersMatch(filters));yield return Frames(3);
        yield return Wait(()=>region.Ready,"Deactivation terrain readiness",10);
        lab.StartSession();var participant=lab.Fighters[0].GetComponent<ActorTerrainBinding>();
        participant.enabled=false;Check("participant deactivation releases opponent filter",FiltersMatch(filters));
        participant.enabled=true;participant.Movement.RefreshContacts();Check("participant reactivation re-establishes contact",RegionMovementCoordinator.EffectiveNativeContact(lab.Fighters[0].GetComponent<Collider2D>(),lab.Fighters[1].GetComponent<Collider2D>()));
        var coordinator=participant.Movement;coordinator.Unregister(participant);
        Check("regional participant unregistration releases all opposing filters",FiltersMatch(filters));
        coordinator.Configure(participant,"owned restored Camp registration",1,true,false,true);coordinator.RefreshContacts();
        Check("regional participant registration re-establishes contact",RegionMovementCoordinator.EffectiveNativeContact(lab.Fighters[0].GetComponent<Collider2D>(),lab.Fighters[1].GetComponent<Collider2D>()));
        lab.StopSession("deactivation test cleanup");Check("deactivation cleanup restores original filters",!lab.Active&&!lab.CleanupBlocked&&FiltersMatch(filters));yield return Frames(3);
        // Exercise exact nonzero collider-state restoration, not only the prefab's zero defaults.
        var custom=residents[0].GetComponent<Collider2D>();var originalCustom=new FilterState(custom);
        try
        {
            custom.includeLayers=1<<2;custom.excludeLayers=1<<6;custom.layerOverridePriority=11;
            var customSnapshot=CaptureFilters(residents.Select(r=>r.gameObject));
            yield return Wait(()=>region.Ready,"Nonzero filter terrain readiness",10);
            lab.StartSession();
            Check("nonzero original filters support scoped opposing inclusion",RegionMovementCoordinator.EffectiveNativeContact(lab.Fighters[0].GetComponent<Collider2D>(),lab.Fighters[1].GetComponent<Collider2D>())&&custom.layerOverridePriority==11);
            lab.Resolve("nonzero filter resolution");
            Check("resolution restores exact nonzero masks and priority",FiltersMatch(customSnapshot));
            lab.StopSession("nonzero filter cleanup");Check("cancellation preserves restored nonzero filters",!lab.Active&&!lab.CleanupBlocked&&FiltersMatch(customSnapshot));
        }
        finally
        {
            if(lab.Active)lab.StopSession("nonzero fixture failure cleanup");
            custom.includeLayers=originalCustom.Include;custom.excludeLayers=originalCustom.Exclude;custom.layerOverridePriority=originalCustom.Priority;
        }
        yield return Frames(3);
        // Deliberate unsupported attached-body exclusion: preparation must fail closed, not change body policy.
        var body=residents[0].GetComponent<Rigidbody2D>();int originalBodyExclude=body.excludeLayers;
        try
        {
            body.excludeLayers=originalBodyExclude|(1<<6);
            yield return Wait(()=>region.Ready,"Preparation-fault terrain readiness",10);
            Assert.Throws<InvalidOperationException>(()=>lab.StartSession(),"A conflicting attached-body filter must not be silently overridden");
            Check("failed preparation releases collider lease",!lab.Active&&!lab.CleanupBlocked&&FiltersMatch(filters)&&body.excludeLayers.value==(originalBodyExclude|(1<<6)));
        }
        finally{body.excludeLayers=originalBodyExclude;}
        yield return Frames(3);yield return Wait(()=>region.Ready,"Menu-exit terrain readiness",10);
        lab.StartSession();Check("active Menu-exit session owns contact",RegionMovementCoordinator.EffectiveNativeContact(lab.Fighters[0].GetComponent<Collider2D>(),lab.Fighters[1].GetComponent<Collider2D>()));
        exitingCoordinator=lab.Fighters[0].GetComponent<ActorTerrainBinding>().Movement;
        // Real QuitToMainMenu invokes campaign transient cleanup BEFORE unload, then normal region teardown.
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if(host!=null){host.StopAllCoroutines();UnityEngine.Object.Destroy(host.gameObject);host=null;}
        Stage64DPlayerAttackObservation.End();
        ReleaseInput();foreach(var probe in probes)if(probe!=null){probe.enabled=false;UnityEngine.Object.Destroy(probe);}probes.Clear();
        if(lab!=null&&lab.Active)lab.StopSession("exception-safe contract fixture teardown");
        if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}
        if(Application.isPlaying)yield return new ExitPlayMode();
    }
}
#endif
