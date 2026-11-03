#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SporeGobbo.Input;
using SporeGobbo.Safety;

/// <summary>Owned test choreography through actual UI, physics, actors and lifecycle services.</summary>
public sealed class Stage62LifecycleEvidence : MonoBehaviour
{
    public bool ContinueFromReadyCamp;
    readonly List<string> checks=new List<string>(), errors=new List<string>();
    string directory; bool finished; float watchdog; Keyboard keyboard;Gamepad ownedGamepad;Mouse ownedMouse;
    void Awake(){directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);watchdog=Time.realtimeSinceStartup+1200;Application.logMessageReceived+=Observe;}
    void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    void Update(){if(!finished&&(errors.Count>0||Time.realtimeSinceStartup>watchdog)){if(errors.Count==0)errors.Add("Lifecycle capture timed out");Finish();}}
    static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    static bool CampReady()=>SceneManager.GetActiveScene().name==GameSceneCatalog.Camp&&Find<CampSceneController>()!=null&&Find<CampSceneController>().WorldInputReady&&Find<SettlementCampRegion>()!=null&&Find<SettlementCampRegion>().Ready;
    IEnumerator Wait(Func<bool> ready,string name,float seconds=90){Stage61RuntimeMetrics.Mark(name);float end=Time.realtimeSinceStartup+seconds;while(!finished&&!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(name,!finished&&ready());}
    IEnumerator Start()
    {
        yield return null;yield return null;
        keyboard=InputSystem.AddDevice<Keyboard>("Stage61 owned lifecycle keyboard");keyboard.MakeCurrent();
        GobboController player=null;
        if(!ContinueFromReadyCamp){
        Check("entry uses actual Main Menu",SceneManager.GetActiveScene().name=="MainMenu"&&Find<MainMenuController>()!=null);
        var menu=Find<MainMenuController>();menu.StartNewGame(1);
        var naming=Find<NewGameButton>();
        if(naming!=null&&naming.namePromptPanel!=null&&naming.namePromptPanel.activeInHierarchy)
        {naming.playerNameInput.text="Development Fresh A";naming.confirmNameButton.onClick.Invoke();}
        yield return Wait(()=>SceneManager.GetActiveScene().name=="SampleScene"&&SampleSceneModeController.IsIntroMode&&MapGenerator.Instance!=null&&MapGenerator.Instance.Data!=null&&Find<GobboController>()!=null,"real New Game enters actual Intro");
        if(finished)yield break;
        Check("Intro has no run snapshot",!GameState.Instance.HasActiveRunSnapshot);
        yield return Wait(()=>Find<IntroCampTransition>()!=null&&Find<IntroCampTransition>().gameObject.activeInHierarchy,"actual Intro terminal becomes active");
        player=Find<GobboController>();var intro=Find<IntroCampTransition>();
        // Deliberate test shortcut, using the actual terminal's Physics2D trigger.
        var body=player.GetComponent<Rigidbody2D>();body.position=intro.transform.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
        yield return Wait(CampReady,"actual Intro trigger reaches ready replacement Camp");if(finished)yield break;
        }
        Check("Intro discovery committed by real Camp arrival",!SporeSaveManager.ShouldLoadIntro(SporeSaveManager.LoadSlot(1)));
        CheckAuthorities("first arrival");yield return Capture("intro-arrival-real-camp");
        yield return GamepadProof();
        var region=Find<SettlementCampRegion>();string initialPhysical=JsonUtility.ToJson(region.CaptureRegionSnapshot(GameState.Instance));
        var portal=Find<CampRunPortal>();Check("portal preserves Normal Run Baseline selection",portal.developmentRunProfileOverride==null);
        portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);yield return null;portal.goButton.onClick.Invoke();
        yield return Wait(()=>ExpeditionReady(),"actual Camp portal enters new terrain expedition");
        for(int i=0;i<30;i++)yield return null;if(finished)yield break;
        Check("existing run snapshot active",GameState.Instance.HasActiveRunSnapshot);
        Check("new expedition has no dummy generator or Tilemap collision",MapGenerator.Instance==null&&Find<MapGenerator>()==null&&Find<UnityEngine.Tilemaps.TilemapCollider2D>()==null);
        Check("Camp terrain provider unregisters for run",!(DiggableTerrainService.Active is SettlementCampRegion));
        player=Find<GobboController>();
        yield return ExpeditionProof();if(finished)yield break;
        // Resource acquisition shortcut calls the existing real inventory service; planting/hatching stays real.
        player.GetComponent<SporeInventory>().AddSpore(1);
        string preRunSave=File.ReadAllText(SporeSaveManager.GetSlotPath(1));SporeSaveManager.SaveCurrentSlotFromGameState();
        Check("mid-run autosave retains last committed Camp",File.ReadAllText(SporeSaveManager.GetSlotPath(1))==preRunSave);
        yield return Press(Key.Q,2);
        yield return Wait(()=>Find<SporeGrow>()!=null,"real plant action creates real spore",15);
        yield return Wait(()=>Find<BuddyChoiceScreen>()!=null&&Find<BuddyChoiceScreen>().panel.activeInHierarchy,"real growth opens actual naming UI",20);
        var choice=Find<BuddyChoiceScreen>();choice.nameInput.text="Lifecycle Buddy";choice.continueButton.onClick.Invoke();yield return null;
        yield return Wait(()=>GameState.Instance.ownedGobbos.Count==1&&UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Length==1,"real hatch recruits and spawns authoritative Buddy",15);
        string buddyId=GameState.Instance.ownedGobbos[0].uniqueId;
        yield return NewlyOpenedBuddyProof();if(finished)yield break;
        var state=GameState.Instance;int runBefore=state.currentRunNumber,cycleBefore=state.campCycleNumber;int mushroomsBefore=CampResourceService.GetAmount(state,CampResourceType.Mushrooms);
        Check("invalid return destination rejected without effects",!RunReturnService.ReturnToCamp("MissingDevelopmentDestination",true,true,RunReturnReason.NormalExit,"owned invalid destination test")&&state.currentRunNumber==runBefore&&state.campCycleNumber==cycleBefore&&state.HasActiveRunSnapshot);
        CampResourceService.Add(state,CampResourceType.Mushrooms,8);
        SporeSaveManager.FailNextDevelopmentWrite("validated");
        Check("interrupted real return reports failure",!RunReturnService.ReturnToCamp("CampScene",true,true,RunReturnReason.Retreat,"owned interrupted return"));
        int onceRun=state.currentRunNumber,onceCycle=state.campCycleNumber,onceMushrooms=CampResourceService.GetAmount(state,CampResourceType.Mushrooms);
        Check("failed return preserves committed Camp",File.ReadAllText(SporeSaveManager.GetSlotPath(1))==preRunSave);
        Check("retreat applies existing new-currency penalty",onceMushrooms==mushroomsBefore+2);
        Check("return retry uses existing service",RunReturnService.ReturnToCamp("CampScene",true,true,RunReturnReason.Retreat,"owned return retry"));
        yield return OpenReturnPanels();if(finished)yield break;
        Check("retry does not repeat run cycle or resource effects",state.currentRunNumber==onceRun&&state.campCycleNumber==onceCycle&&CampResourceService.GetAmount(state,CampResourceType.Mushrooms)==onceMushrooms);
        Check("recruited identity returns to the same campaign",state.ownedGobbos.Any(unit=>unit.uniqueId==buddyId)&&SporeSaveManager.LoadSlot(1).ownedGobbos.Any(unit=>unit.uniqueId==buddyId));
        Check("return preserves physical Camp region",JsonUtility.ToJson(Find<SettlementCampRegion>().CaptureRegionSnapshot(state))==initialPhysical);
        Check("first-run recruit is not a departing or Camp Buddy",state.lastRun.buddiesStart==0&&state.lastRun.activeBuddyReports.Count==0&&state.lastRun.reserveBuddyReports.Any(report=>report.buddyId==buddyId&&report.role=="Recruited this run"));
        var campControls=Find<DevelopmentCampControls>();Check("first recruit observation approach is reachable",campControls.Warp("fire.watch"));
        yield return Wait(()=>BuddyHomeThought.RenderedCount>0,"real recruited Buddy visibly expresses first-home intention",60);Check("real recruited Buddy settles near Fire before cue",UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Any(unit=>unit.unitData.uniqueId==buddyId&&Vector2.Distance(unit.transform.position,Find<SettlementCampRegion>().World(Find<SettlementCampRegion>().Definition.Marker("fire").Interaction))<3));yield return Capture("recruited-buddy-sleepy-cue");
        yield return Capture("first-home-fire-view");
        yield return Wait(()=>state.campTerrainState.residentialSlotsEstablished==1,"real recruited Buddy automatically establishes first bed without root",180);
        Check("first home preserves untouched optional root",!Find<SettlementCampRegion>().ResidentialEntranceOpen&&state.FindOwnedGobbo(buddyId).campResidentialSlotId==1);
        yield return Capture("first-recruit-established-home");
        CheckAuthorities("retreat return");yield return Capture("real-retreat-return");
        // Repeat with a normal return. This is service-level exit choreography, not a boss-combat claim.
        portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);portal.goButton.onClick.Invoke();
        yield return Wait(()=>ExpeditionReady(),"second existing run starts");
        for(int i=0;i<30;i++)yield return null;
        Check("real active squad transfers to existing run",UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Any(unit=>unit.unitData.uniqueId==buddyId));
        yield return MushProof();if(finished)yield break;
        Check("normal return uses existing service",RunReturnService.ReturnToCamp("CampScene",true,true,RunReturnReason.NormalExit,"owned normal return"));
        yield return OpenReturnPanels();if(finished)yield break;
        Check("repeated returns keep one region",Find<SettlementCampRegion>().RegionId=="camp");
        var pause=Find<PauseMenuController>();pause.OpenPause();pause.QuitToMainMenu();
        yield return Wait(()=>SceneManager.GetActiveScene().name=="MainMenu"&&Find<MainMenuController>()!=null,"actual pause quits to Main Menu");yield return null;
        Find<MainMenuController>().ContinueLastGame();yield return Wait(CampReady,"actual Continue restores replacement Camp");if(finished)yield break;
        Check("Continue preserves recruited Buddy",GameState.Instance.ownedGobbos.Any(unit=>unit.uniqueId==buddyId));CheckAuthorities("Continue");
        // Actual player death with a living successor; save failure must remain retryable.
        portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);portal.goButton.onClick.Invoke();
        yield return Wait(()=>ExpeditionReady(),"death scenario enters existing run");
        for(int i=0;i<30;i++)yield return null;
        string beforeDeath=File.ReadAllText(SporeSaveManager.GetSlotPath(1));string oldLeader=GameState.Instance.GetLeader().uniqueId;
        Find<GobboController>().TakeDamage(1000000);
        yield return Wait(()=>SceneManager.GetActiveScene().name==GameSceneCatalog.Camp&&Find<CampSuccessionUI>()!=null&&Find<CampSuccessionUI>().successionPanel.activeInHierarchy,"actual death reaches real succession UI");if(finished)yield break;
        SporeSaveManager.SaveCurrentSlotFromGameState();
        Check("unresolved succession leaves committed Camp unchanged",File.ReadAllText(SporeSaveManager.GetSlotPath(1))==beforeDeath);
        Check("unresolved leader remains dead",GameState.Instance.GetLeader().isDead);
        yield return Capture("real-succession-selection");
        SporeSaveManager.FailNextDevelopmentWrite("validated");Find<CampSuccessionUI>().letCampChooseButton.onClick.Invoke();yield return null;
        Check("failed successor save retains pending guard and retry UI",PlayerDeathRunStore.Instance.playerDiedThisRun&&Find<CampSuccessionUI>().successionPanel.activeInHierarchy&&!Find<CampSceneController>().WorldInputReady&&File.ReadAllText(SporeSaveManager.GetSlotPath(1))==beforeDeath);
        yield return Capture("real-successor-save-retry");
        Find<CampSuccessionUI>().letCampChooseButton.onClick.Invoke();yield return Wait(CampReady,"successor save retry opens real Camp");if(finished)yield break;
        var successorSave=SporeSaveManager.LoadSlot(1);
        Check("real succession commits identity and history together",successorSave.leader.uniqueId==buddyId&&successorSave.leader.campResidentialSlotId==0&&successorSave.deathHistory.Any(record=>record.wasLeader&&record.gobboId==oldLeader)&&!successorSave.ownedGobbos.Any(unit=>unit.uniqueId==buddyId));
        CheckAuthorities("succession");yield return Capture("real-successor-camp");
        var memory=new List<string>{"cycle,phase,monoBytes,allocatedBytes,reservedBytes,meshObjects,materialObjects,ownedActorMeshes,ownedActorMaterials"};
        for(int cycle=0;cycle<3;cycle++)
        {
            Stage61RuntimeMetrics.Mark("teardown forced collection");
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
            Check("terrain and projected actor resources released / "+cycle,DiggableTerrainService.Active==null&&Stage59ActorView.OwnedMeshes==0&&Stage59ActorView.OwnedMaterials==0&&UnityEngine.Object.FindObjectsByType<SporeGobbo.Geometry.Preview.Stage45GpuProjection>(FindObjectsSortMode.None).Length==0);
            yield return Resources.UnloadUnusedAssets();GC.Collect();GC.WaitForPendingFinalizers();yield return null;
            memory.Add(MemoryRow(cycle,"menu"));Find<MainMenuController>().ContinueLastGame();yield return Wait(CampReady,"repeated real Camp reload / "+cycle);
            for(int i=0;i<90;i++)yield return null;memory.Add(MemoryRow(cycle,"camp"));CheckAuthorities("reload "+cycle);
        }
        File.WriteAllLines(Path.Combine(directory,"teardown-memory.csv"),memory);
        Check("terminal scenario has no remaining successors",GameState.Instance.ownedGobbos.Count==0);
        string terminalLeader=GameState.Instance.GetLeader().uniqueId;
        portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);portal.goButton.onClick.Invoke();
        yield return Wait(()=>ExpeditionReady(),"terminal scenario enters actual authored expedition");
        for(int i=0;i<30;i++)yield return null;Find<GobboController>().TakeDamage(1000000);
        yield return Wait(()=>GameState.Instance.lineageEnded&&Find<LineageGameOverScreen>()!=null,"real no-successor death opens terminal UI");if(finished)yield break;
        var ended=SporeSaveManager.LoadSlot(1);
        Check("resolved terminal death commits dead leader and history",ended.lineageEnded&&ended.leader.isDead&&ended.leader.health==0&&ended.deathHistory.Any(record=>record.wasLeader&&record.gobboId==terminalLeader));
        yield return Capture("real-terminal-game-over");Find<LineageGameOverScreen>().GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();
        yield return Wait(()=>SceneManager.GetActiveScene().name=="MainMenu"&&Find<MainMenuController>()!=null,"real terminal button returns to Main Menu");
        Find<MainMenuController>().ContinueLastGame();yield return null;
        Check("ended lineage cannot Continue into playable Camp",SceneManager.GetActiveScene().name=="MainMenu"&&Find<CampSceneController>()==null&&SporeSaveManager.LoadSlot(1).lineageEnded);
        Finish();
    }
    static bool ExpeditionReady()=>SceneManager.GetActiveScene().name==GameSceneCatalog.CandidateExpedition&&Find<ExpeditionRegion>()!=null&&Find<ExpeditionRegion>().PresentationReady&&Find<ExpeditionRegion>().Player!=null;
    void Warp(ExpeditionRegion r,SporeGobbo.Geometry.Point2 p){var body=r.Player.GetComponent<Rigidbody2D>();body.position=r.World(p);body.linearVelocity=Vector2.zero;r.Player.transform.position=body.position;Physics2D.SyncTransforms();}
    void PickupDiagnostic(ExpeditionRegion r,MonoBehaviour item,string phase){if(item==null)return;var point=(Vector2)item.transform.position;File.AppendAllText(Path.Combine(directory,"pickup-diagnostics.txt"),phase+" / "+item.name+" distance="+Vector2.Distance(r.Player.transform.position,point)+" actor="+r.Player.transform.position+" item="+point+" ready="+r.PresentationReady+" context="+SporeInputReader.Instance.Context+" pointSight="+r.Player.GetComponent<ActorTerrainBinding>().InteractionSight(r.Player.transform.position,point)+" bodyExtent="+TileMover.GetMapClearanceExtents(r.Player.GetComponent<Rigidbody2D>(),.22f)+Environment.NewLine);}
    // The traversal assertion needs a 2.4m clear lane. Select an existing suitable pickup;
    // never mutate terrain, move a pickup, or weaken the actual traversal/interaction checks.
    T TraversalPickup<T>(ExpeditionRegion region,Func<T,bool> filter=null) where T:MonoBehaviour
    {
        var extents=TileMover.GetMapClearanceExtents(region.Player.GetComponent<Rigidbody2D>(),.22f);
        foreach(var pickup in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None)
            .Where(p=>filter==null||filter(p)).OrderBy(p=>Vector2.Distance(p.transform.position,region.Player.transform.position)).ThenBy(p=>p.name,StringComparer.Ordinal))
        {
            var direction=PickupTraversalDirection(region,pickup.transform.position,extents);
            File.AppendAllText(Path.Combine(directory,"pickup-fixture-selection.txt"),typeof(T).Name+" / "+pickup.name+" extent="+extents+" lane="+direction+Environment.NewLine);
            if(direction!=Vector2.zero)return pickup;
        }
        throw new InvalidOperationException("No existing "+typeof(T).Name+" satisfies original pickup traversal precondition");
    }
    public static Vector2 PickupTraversalDirection(IDiggableTerrain terrain,Vector2 target,Vector2 extent)
    {
        foreach(var axis in new[]{Vector2.right,Vector2.up,Vector2.left,Vector2.down})
        {
            bool clear=true;
            for(int step=-12;step<=12;step++)clear&=TileMover.CanOccupyBox(terrain,target+axis*(step*.1f),extent);
            if(clear)return axis;
        }
        return Vector2.zero;
    }
    IEnumerator WalkAcrossLoosePickup(ExpeditionRegion r,MonoBehaviour pickup)
    {
        // Setup chooses an unobstructed straight lane; traversal uses actual keyboard movement and native physics.
        var target=(Vector2)pickup.transform.position;
        var body=r.Player.GetComponent<Rigidbody2D>();
        var extent=TileMover.GetMapClearanceExtents(body,.22f);
        Vector2 direction=PickupTraversalDirection(r,target,extent);
        if(direction==Vector2.zero)throw new InvalidOperationException("No terrain-clear pickup traversal: "+pickup.name);
        string label=pickup.name;
        foreach(var collider in pickup.GetComponentsInChildren<Collider2D>())Check("loose resource trigger / "+label,collider.isTrigger);
        int spores=r.Player.GetComponent<SporeInventory>().spores;
        int shinies=CampResourceService.GetAmount(GameState.Instance,CampResourceType.Shinies);
        int food=CampResourceService.GetAmount(GameState.Instance,CampResourceType.Mushrooms);
        Warp(r,r.LogicalPosition(target-direction*1.2f));
        yield return Wait(()=>r.PresentationReady,"pickup traversal view ready",10);
        Key key=direction.x>0?Key.D:direction.x<0?Key.A:direction.y>0?Key.W:Key.S;
        float deadline=Time.realtimeSinceStartup+8;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
        while(Time.realtimeSinceStartup<deadline&&Vector2.Dot(body.position-target,direction)<1)yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        for(int i=0;i<4;i++)yield return null;
        Check("actual movement crosses loose resource / "+label,Vector2.Dot(body.position-target,direction)>=1);
        Check("walking does not collect loose resource / "+label,pickup!=null&&r.Player.GetComponent<SporeInventory>().spores==spores&&CampResourceService.GetAmount(GameState.Instance,CampResourceType.Shinies)==shinies&&CampResourceService.GetAmount(GameState.Instance,CampResourceType.Mushrooms)==food);
    }
    void ApproachPickup(ExpeditionRegion r,MonoBehaviour pickup){var target=(Vector2)pickup.transform.position;var extent=TileMover.GetMapClearanceExtents(r.Player.GetComponent<Rigidbody2D>(),.22f);for(float radius=.75f;radius<=.95f;radius+=.1f)for(int direction=0;direction<16;direction++){float angle=direction*Mathf.PI/8;var candidate=target+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;if(!TileMover.CanOccupyBox(r,candidate,extent)||!r.RayClear(candidate,target))continue;Warp(r,r.LogicalPosition(candidate));return;}throw new InvalidOperationException("No fitting pickup approach: "+pickup.name);}
    IEnumerator ExpeditionProof()
    {
        var r=Find<ExpeditionRegion>();var player=r.Player;
        Check("single real campaign/input/UI/terrain in expedition",Find<MapGenerator>()==null&&ReferenceEquals(DiggableTerrainService.Active,r)&&UnityEngine.Object.FindObjectsByType<GameState>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SporeInputReader>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SporeUiCoordinator>(FindObjectsSortMode.None).Length==1);
        Check("actual naming and pause UI transferred",Find<BuddyChoiceScreen>()!=null&&Find<PauseMenuController>()!=null);
        var before=(Vector2)player.transform.position;yield return Press(Key.D,12);Check("real input moves logical expedition actor",Vector2.Distance(before,player.transform.position)>.05f);yield return Capture("expedition-arrival");
        var grid=new System.Collections.Generic.List<string>{"x,y,solid,permanent,discoveryUnit,resistance"};for(int y=0;y<128;y++)for(int x=0;x<144;x++){r.Discovery.Index.ByCell.TryGetValue(y*144+x,out var unit);grid.Add(x+","+y+","+(r.State.World.IsSolid(x,y)?1:0)+","+(r.Definition.Protected(x,y)?1:0)+","+(unit?.Id??"")+","+r.Definition.Resistance(x,y));}File.WriteAllLines(Path.Combine(directory,"expedition-grid.csv"),grid);
        Check("one actual world interaction detector",UnityEngine.Object.FindObjectsByType<CampInteractionDetector>(FindObjectsSortMode.None).Length==1);Warp(r,new SporeGobbo.Geometry.Point2(20.5,94.5));for(int i=0;i<8;i++)yield return null;ownedMouse=InputSystem.AddDevice<Mouse>("Stage62 owned digging mouse");ownedMouse.MakeCurrent();int physicalBefore=r.Excavations,attemptsBefore=player.GetComponent<ActorTerrainBinding>().DigAttempts;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));for(int i=0;i<30;i++){var targetScreen=r.RegionCamera.WorldToScreenPoint(r.Render(r.World(new SporeGobbo.Geometry.Point2(20.5,90.5))));InputSystem.QueueStateEvent(ownedMouse,new MouseState{position=new Vector2(targetScreen.x,targetScreen.y)});yield return null;}InputSystem.QueueStateEvent(keyboard,new KeyboardState());for(int i=0;i<4;i++)yield return null;Check("real routed Shift digging publishes terrain edits",r.Excavations>physicalBefore&&player.GetComponent<ActorTerrainBinding>().DigAttempts>attemptsBefore&&r.LogicalReady);yield return Capture("real-input-digging");InputSystem.RemoveDevice(ownedMouse);ownedMouse=null;keyboard.MakeCurrent();
        var discovery=r.Discovery;int physical=r.State.World.Revision;var small=r.Definition.Concealed.Find(g=>g.Id=="tiny");Check("tiny pocket physically pre-open and visually hidden",small.Cells.All(i=>!r.State.World.IsSolid(i%144,i/144)&&!discovery.Shown(i%144,i/144)));Check("discovery reads do not edit occupancy",physical==r.State.World.Revision);
        Warp(r,new SporeGobbo.Geometry.Point2(33.5,71.5));for(int i=0;i<3;i++)yield return null;
        int edits=r.Excavations;
        // Explicit excavation setup through the same region transaction used by actual digging; not a natural-input claim.
        for(int x=28;x>=20;x--)r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(x+.5,71.5)),.8f);
        for(int i=0;i<90;i++)yield return null;Check("ordinary edits reveal pre-existing tiny pocket",r.Excavations>edits&&small.Cells.Any(i=>discovery.Shown(i%144,i/144)));yield return Capture("tiny-pocket-breakthrough");
        Warp(r,new SporeGobbo.Geometry.Point2(88.5,94.5));for(int i=0;i<4;i++)yield return null;var snake=r.Definition.Concealed.Find(g=>g.Id=="snake");Check("long snaking tunnel starts physically open but concealed",snake.Cells.All(i=>!r.State.World.IsSolid(i%144,i/144)&&!discovery.Shown(i%144,i/144)));for(int y=93;y>=88;y--)r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(88.5,y+.5)),.9f);yield return null;yield return Capture("snaking-network-reveal-start");for(int i=0;i<90;i++)yield return null;Check("long snaking network releases existing floor",snake.Cells.Any(i=>discovery.Shown(i%144,i/144)));yield return Capture("snaking-network-reveal-end");
        Warp(r,new SporeGobbo.Geometry.Point2(105.5,105.5));for(int i=0;i<4;i++)yield return null;
        var hidden=r.Definition.Concealed.Find(g=>g.Id=="combat");int hiddenBefore=hidden.Cells.Count(i=>discovery.Shown(i%144,i/144));
        for(int y=105;y<=112;y++)r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(108.5,y+.5)),1.2f);
        float deadline=Time.realtimeSinceStartup+.2f;while(Time.realtimeSinceStartup<deadline)yield return null;
        Check("combat annex rapidly exposes tactical floor",hidden.Cells.Count(i=>discovery.Shown(i%144,i/144))>hiddenBefore);yield return Capture("combat-annex-fast-reveal");
        yield return NewRouteEnemyProof();if(finished)yield break;
        ExpeditionDevelopmentControls.SetGodMode(true);Warp(r,new SporeGobbo.Geometry.Point2(66.5,92.5));int attempts=ExpeditionDevelopmentControls.DamageAttempts;
        Stage64DReplayEvidence.BeginCombat(gameObject,r,player);
        yield return Wait(()=>Find<TunnelWeevilEnemy>()!=null,"real Weevil instantiated on discovered floor",10);
        yield return Wait(()=>ExpeditionDevelopmentControls.DamageAttempts>attempts,"actual Weevil AI winds up and damages real leader",20);
        Check("God Mode retains actual damage attempts and survival",ExpeditionDevelopmentControls.GodMode&&player.health>0&&ExpeditionDevelopmentControls.DamageAttempts>attempts);Stage64DReplayEvidence.EndCombat(directory);yield return Capture("real-weevil-god-mode");
        ownedMouse=InputSystem.AddDevice<Mouse>("Stage62 owned combat mouse");ownedMouse.MakeCurrent();var target=UnityEngine.Object.FindObjectsByType<TunnelWeevilEnemy>(FindObjectsSortMode.None).OrderBy(e=>Vector2.Distance(e.transform.position,player.transform.position)).First();int enemyHP=target.health;for(int i=0;i<150&&target!=null;i++){var screen=r.RegionCamera.WorldToScreenPoint(r.Render(target.transform.position));InputSystem.QueueStateEvent(ownedMouse,new MouseState{position=new Vector2(screen.x,screen.y),buttons=1});yield return null;}InputSystem.QueueStateEvent(ownedMouse,new MouseState());Check("real inverse-pointer melee input damages real Weevil",target==null||target.health<enemyHP);yield return Capture("real-player-melee");InputSystem.RemoveDevice(ownedMouse);ownedMouse=null;
        Warp(r,new SporeGobbo.Geometry.Point2(113.5,64.5));yield return Wait(()=>Find<BlobSpitterEnemy>()!=null,"real Blob instantiated",10);yield return Wait(()=>Find<EnemyProjectile>()!=null,"actual Blob fires actual projectile",15);yield return Capture("real-blob-projectile");
        // Real projectile prefab, deterministic launch at permanent pillar: setup adapter, actual swept flight.
        var blob=Find<BlobSpitterEnemy>();int terrainHits=EnemyProjectile.TerrainImpacts;var shot=Instantiate(blob.projectilePrefab,r.World(new SporeGobbo.Geometry.Point2(104.5,58.5)),Quaternion.identity);shot.speed=100;shot.Launch(Vector2.right,blob.transform);for(int i=0;i<12;i++)yield return null;Check("fast real projectile stops at permanent terrain",shot==null&&EnemyProjectile.TerrainImpacts>terrainHits);
        Warp(r,r.Definition.Anchors["nursery"]);yield return Capture("nursery-root-stone-overview");
        var spore=TraversalPickup<CollectibleSpore>(r);int spores=player.GetComponent<SporeInventory>().spores;yield return WalkAcrossLoosePickup(r,spore);ApproachPickup(r,spore);yield return Wait(()=>r.PresentationReady,"pickup view ready / spore",10);PickupDiagnostic(r,spore,"before F");yield return Press(Key.F,2);PickupDiagnostic(r,spore,"after F if still present");Check("routed F spore pickup uses actual inventory",player.GetComponent<SporeInventory>().spores>spores);
        var shiny=TraversalPickup<CollectibleShiny>(r);int shinies=CampResourceService.GetAmount(GameState.Instance,CampResourceType.Shinies);yield return WalkAcrossLoosePickup(r,shiny);ApproachPickup(r,shiny);yield return Wait(()=>r.PresentationReady,"pickup view ready / shiny",10);PickupDiagnostic(r,shiny,"before F");yield return Press(Key.F,2);PickupDiagnostic(r,shiny,"after F if still present");Check("routed F shiny pickup updates canonical resources",CampResourceService.GetAmount(GameState.Instance,CampResourceType.Shinies)>shinies);
        var food=TraversalPickup<FoodItem>(r,item=>item.name.StartsWith("food.",StringComparison.Ordinal));int mushrooms=CampResourceService.GetAmount(GameState.Instance,CampResourceType.Mushrooms);yield return WalkAcrossLoosePickup(r,food);ApproachPickup(r,food);yield return Wait(()=>r.PresentationReady,"pickup view ready / food",10);PickupDiagnostic(r,food,"before F");yield return Press(Key.F,2);PickupDiagnostic(r,food,"after F if still present");Check("routed F food pickup preserves resource/XP service",CampResourceService.GetAmount(GameState.Instance,CampResourceType.Mushrooms)>mushrooms);
        Warp(r,new SporeGobbo.Geometry.Point2(86.5,103.5));for(int i=0;i<4;i++)yield return null;int resisted=r.RejectedResistance;r.TestDigPower=1;r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(86.5,106.5)),.35f);Check("L2 development fixture rejects power one",r.IsBlocked(new Vector2Int(86,106))&&r.RejectedResistance>resisted);r.TestDigPower=2;r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(86.5,106.5)),.35f);Check("L2 development fixture accepts power two",!r.IsBlocked(new Vector2Int(86,106)));r.TestDigPower=3;r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(91.5,106.5)),.35f);Check("L3 development fixture accepts power three",!r.IsBlocked(new Vector2Int(91,106)));r.TestDigPower=1;int protectedRevision=r.Revision;r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(108.5,58.5)),.35f);Check("actual protected pillar rejects excavation",r.Revision==protectedRevision);yield return Capture("development-resistance-fixtures");
        // Stable no-screenshot performance interval, actual enemies remain active. Explicit God protection and warp setup.
        Warp(r,new SporeGobbo.Geometry.Point2(66.5,92.5));Stage61RuntimeMetrics.Mark("expedition combat warmup");yield return new WaitForSeconds(2);for(int window=0;window<3;window++){Stage61RuntimeMetrics.Mark("expedition steady real combat 60s / "+window);float until=Time.realtimeSinceStartup+60;while(Time.realtimeSinceStartup<until)yield return null;}
        Warp(r,r.Definition.Anchors["nursery"]);player.EatFood(0,player.maxHealth,0);ExpeditionDevelopmentControls.SetGodMode(false);Check("God toggle OFF restores ordinary damage",!ExpeditionDevelopmentControls.GodMode);int hp=player.health;player.TakeDamage(1);Check("ordinary damage path active when OFF",player.health<hp);if(player.health<=0){Check("proof leader remains alive before lifecycle section",false);Finish();yield break;}
        yield return Press(Key.Escape,2);Check("actual expedition pause blocks world",Time.timeScale==0&&SporeInputReader.Instance.Context!=SporeInputContext.Gameplay);yield return Press(Key.Escape,2);
    }
    IEnumerator NewRouteEnemyProof()
    {
        var r=Find<ExpeditionRegion>();ExpeditionDevelopmentControls.SetGodMode(true);Warp(r,new SporeGobbo.Geometry.Point2(56.5,84.5));
        var go=Instantiate(r.weevilPrefab,r.World(new SporeGobbo.Geometry.Point2(64.5,85.5)),Quaternion.identity);go.name="Owned real-prefab navigation proof";r.BindActor(go);for(int i=0;i<3;i++)yield return null;
        var binding=go.GetComponent<ActorTerrainBinding>();Check("real enemy initially faces an obstructed direct corridor",!binding.Sight(go.transform.position,r.Player.transform.position));int prior=r.Revision;
        for(int x=57;x<=63;x++)r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(x+.5,84.5)),1.2f);
        bool crossed=false,clear=true;float end=Time.realtimeSinceStartup+15;
        while(go!=null&&Time.realtimeSinceStartup<end&&Vector2.Distance(go.transform.position,r.Player.transform.position)>1){var cell=r.WorldToCell(go.transform.position);if(cell.x>=59&&cell.x<=61&&cell.y>=83&&cell.y<=86)crossed=true;clear&=TileMover.CanOccupyBox(r,go.transform.position,TileMover.GetMapClearanceExtents(go.GetComponent<Rigidbody2D>(),.22f));yield return null;}
        Check("actual Weevil pursues through newly excavated corridor",r.Revision>prior&&crossed&&clear&&go!=null&&Vector2.Distance(go.transform.position,r.Player.transform.position)<=1);yield return Capture("weevil-newly-opened-route");if(go!=null)Destroy(go);yield return null;
        // Controlled launch of the original projectile after excavation; original actor damage handler.
        Stage61RuntimeMetrics.Category("validation");
        var sourceObject=Instantiate(r.blobPrefab,r.World(new SporeGobbo.Geometry.Point2(64.5,84.5)),Quaternion.identity);sourceObject.name="Owned actual Blob projectile route source";var blob=sourceObject.GetComponent<BlobSpitterEnemy>();blob.targetLayers=0; // Owned manual-shot fixture: suppress acquisition before Start without disabling source eligibility.
        r.BindActor(sourceObject);yield return null;yield return null;
        Check("projectile route proof uses actual Blob projectile prefab",blob!=null&&blob.projectilePrefab!=null&&EnemyDamage.Alive(blob)&&EnemyDamage.Ready(sourceObject)&&object.ReferenceEquals(ActorTerrainBinding.Resolve(sourceObject),r));
        var projectileTrace=new List<string>{"phase,target,accepted,damage,failure,leaderHealth,sourceReady"};string projectilePhase="thin";int leaderReceipts=0;Action<EnemyDamageRequest,EnemyDamageResult> observeProjectile=(request,result)=>{if(request?.source!=sourceObject)return;projectileTrace.Add(projectilePhase+","+(result.Target!=null?result.Target.name:"none")+","+result.Accepted+","+result.AppliedDamage+","+result.Failure+","+r.Player.health+","+EnemyDamage.Ready(sourceObject));if(projectilePhase=="wide"&&result.Target==r.Player&&result.Accepted)leaderReceipts++;};EnemyDamage.EvidenceReceipt+=observeProjectile;var injectedShots=new List<EnemyProjectile>();
        try
        {
        Warp(r,new SporeGobbo.Geometry.Point2(56.5,84.5));r.Player.Heal(r.Player.maxHealth);ExpeditionDevelopmentControls.SetGodMode(false);int hp=r.Player.health;
        var start=r.World(new SporeGobbo.Geometry.Point2(64.5,84.5));Check("excavated corridor exposes thin projectile sightline",r.RayClear(start,r.Player.transform.position,.1f));
        int hits=EnemyProjectile.TerrainImpacts;var shot=Instantiate(blob.projectilePrefab,start,Quaternion.identity);injectedShots.Add(shot);shot.speed=30;shot.Launch(((Vector2)r.Player.transform.position-start).normalized,blob.transform);float until=Time.realtimeSinceStartup+1;while(shot!=null&&Time.realtimeSinceStartup<until)yield return null;
        Check("original wide projectile stops where thin sight fits but body does not",shot==null&&r.Player.health==hp&&EnemyProjectile.TerrainImpacts>hits);
        // The original prefab radius is over a world unit. Widen the player-dug passage, not the projectile.
        for(int x=54;x<=66;x++)r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(x+.5,84.5)),1.6f);
        float radius=blob.projectilePrefab.GetComponent<CircleCollider2D>().radius*blob.projectilePrefab.transform.lossyScale.x;Check("wider excavation admits actual projectile clearance",r.RayClear(start,r.Player.transform.position,radius));
        projectilePhase="wide";int wideLaunchCount=EnemyProjectile.LaunchCount;shot=Instantiate(blob.projectilePrefab,start,Quaternion.identity);injectedShots.Add(shot);shot.speed=30;shot.Launch(((Vector2)r.Player.transform.position-start).normalized,blob.transform);until=Time.realtimeSinceStartup+1;while(shot!=null&&Time.realtimeSinceStartup<until)yield return null;
        Check("real projectile traverses widened excavation and damages actual leader",shot==null&&leaderReceipts==1&&EnemyProjectile.LaunchCount==wideLaunchCount+1&&r.Player.health<hp&&r.Player.health>0);yield return Capture("projectile-through-excavated-route");
        }
        finally{EnemyDamage.EvidenceReceipt-=observeProjectile;foreach(var injected in injectedShots)if(injected!=null){injected.gameObject.SetActive(false);Destroy(injected.gameObject);}if(sourceObject!=null){sourceObject.SetActive(false);Destroy(sourceObject);}File.WriteAllLines(Path.Combine(directory,"owned-projectile-route-receipts.csv"),projectileTrace);}
        yield return null;yield return null;
        r.Player.Heal(r.Player.maxHealth);ExpeditionDevelopmentControls.SetGodMode(true);int attempts=ExpeditionDevelopmentControls.DamageAttempts;r.Player.TakePoison(1,.15f,.05f);Check("existing poison routine remains active under God Mode",r.Player.isPoisoned);yield return new WaitForSeconds(.3f);Check("existing poison ticks remain observable and finish",!r.Player.isPoisoned&&r.Player.health>0&&ExpeditionDevelopmentControls.DamageAttempts>attempts);
    }
    IEnumerator NewlyOpenedBuddyProof()
    {
        var r=Find<ExpeditionRegion>();var buddy=Find<BuddyUnit>();if(buddy==null){Check("recruited Buddy exists before route proof",false);Finish();yield break;}
        // Quiet-area route isolates following from combat survival; enemies remain alive and unmodified.
        int before=r.Revision;for(int x=51;x<=65;x++)r.DigCircle(r.World(new SporeGobbo.Geometry.Point2(x+.5,44.5)),1.2f);
        Warp(r,new SporeGobbo.Geometry.Point2(52.5,44.5));var body=buddy.GetComponent<Rigidbody2D>();body.position=r.World(new SporeGobbo.Geometry.Point2(64.5,44.5));buddy.transform.position=body.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
        bool crossed=false;float until=Time.realtimeSinceStartup+20;while(buddy!=null&&Time.realtimeSinceStartup<until&&Vector2.Distance(buddy.transform.position,r.Player.transform.position)>1.7f){var c=r.WorldToCell(buddy.transform.position);if(c.x>=57&&c.x<=60&&c.y>=43&&c.y<=46)crossed=true;yield return null;}
        Check("real recruited Buddy follows through excavated passage",r.Revision>before&&crossed&&buddy!=null&&Vector2.Distance(buddy.transform.position,r.Player.transform.position)<=1.7f&&buddy.GetComponent<ActorTerrainBinding>()!=null);yield return Capture("recruit-follows-new-passage");
        yield return LeaderCutPassageProof(r,buddy);
    }
    IEnumerator LeaderCutPassageProof(ExpeditionRegion r,BuddyUnit buddy)
    {
        Stage61RuntimeMetrics.Category("validation");
        var paths=new List<SporeGobbo.Geometry.Point2[]>();
        var straight=new List<SporeGobbo.Geometry.Point2>();var diagonal=new List<SporeGobbo.Geometry.Point2>();var turning=new List<SporeGobbo.Geometry.Point2>();
        for(int i=0;i<=32;i++){straight.Add(new SporeGobbo.Geometry.Point2(60.5+i*.5,30.5));diagonal.Add(new SporeGobbo.Geometry.Point2(60.5+i*.5,30.5-i*.5));turning.Add(new SporeGobbo.Geometry.Point2(60.5-i*.5,30.5));}
        for(int i=1;i<=32;i++)turning.Add(new SporeGobbo.Geometry.Point2(44.5,30.5-i*.5));
        paths.Add(straight.ToArray());paths.Add(diagonal.ToArray());paths.Add(turning.ToArray());
        var names=new[]{"straight","diagonal","turning"};var samples=new List<string>{"case,time,leaderX,leaderY,buddyX,buddyY,ready,revision"};float nextSample=0;
        ownedMouse=InputSystem.AddDevice<Mouse>("Stage64D actual leader-cut passages");ownedMouse.MakeCurrent();
        var diagonalObservation=Stage64DDiagonalEvidence.Begin(gameObject,r,r.Player,buddy,keyboard,ownedMouse);
        Stage64DReplayEvidence.BeginBuddy(gameObject,r,r.Player,buddy);
        try
        {
            for(int test=0;test<paths.Count;test++)
            {
                Stage64DReplayEvidence.SetCase(test);
                var points=paths[test];var player=r.Player;var playerBody=player.GetComponent<Rigidbody2D>();var buddyBody=buddy.GetComponent<Rigidbody2D>();
                // Owned positioning only. All excavation and subsequent traversal use actual game input/AI/physics.
                Warp(r,points[0]);var forward=(r.World(points[1])-r.World(points[0])).normalized;
                buddyBody.position=playerBody.position-forward*1.8f;buddy.transform.position=buddyBody.position;buddyBody.linearVelocity=Vector2.zero;
                buddy.GetComponent<BuddyFollow>()?.ResetCommandMotion();
                Physics2D.SyncTransforms();player.IssueBuddyCommand(BuddyCommand.Passive);player.IssueBuddyCommand(BuddyCommand.Follow);
                int edits=r.Excavations,attempts=player.GetComponent<ActorTerrainBinding>().DigAttempts;int cursor=1;
                bool bodyClear=true,crossedMiddle=false;var middle=r.World(points[points.Length/2]);float end=Time.realtimeSinceStartup+20;
                var playerExtent=TileMover.GetMapClearanceExtents(playerBody,.25f);var buddyExtent=TileMover.GetMapClearanceExtents(buddyBody,.25f);
                while(!finished&&cursor<points.Length&&Time.realtimeSinceStartup<end)
                {
                    Vector2 delta=r.World(points[cursor])-playerBody.position;
                    if(delta.magnitude<.08f){cursor++;continue;}
                    var keys=new List<Key>{Key.LeftShift};if(delta.x>.05f)keys.Add(Key.D);else if(delta.x<-.05f)keys.Add(Key.A);if(delta.y>.05f)keys.Add(Key.W);else if(delta.y<-.05f)keys.Add(Key.S);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));
                    var screen=r.RegionCamera.WorldToScreenPoint(r.Render(r.World(points[cursor])));InputSystem.QueueStateEvent(ownedMouse,new MouseState{position=new Vector2(screen.x,screen.y)});
                    diagonalObservation.Queued(test,cursor,r.World(points[cursor]),keys,new Vector2(screen.x,screen.y));
                    if(r.LogicalReady)bodyClear&=TileMover.CanOccupyBox(r,playerBody.position,playerExtent)&&TileMover.CanOccupyBox(r,buddyBody.position,buddyExtent);
                    crossedMiddle|=Vector2.Distance(buddyBody.position,middle)<1.2f;
                    if(Time.realtimeSinceStartup>=nextSample){nextSample=Time.realtimeSinceStartup+.1f;samples.Add(FormattableString.Invariant($"{names[test]},{Time.realtimeSinceStartup:R},{playerBody.position.x:R},{playerBody.position.y:R},{buddyBody.position.x:R},{buddyBody.position.y:R},{r.LogicalReady},{r.Revision}"));}
                    if(samples.Count>=4000){Check("leader-cut samples remain bounded",false);Finish();yield break;}
                    yield return null;
                }
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(ownedMouse,new MouseState());
                Stage64DReplayEvidence.Passage(test,"Leader",cursor==points.Length,bodyClear,crossedMiddle,false);
                Check("actual leader input excavates and traverses "+names[test]+" passage",cursor==points.Length&&r.Excavations>edits&&player.GetComponent<ActorTerrainBinding>().DigAttempts>attempts);
                if(cursor!=points.Length){Finish();yield break;}
                yield return Wait(()=>r.PresentationReady,"leader-cut "+names[test]+" collision and view publication",10);
                bool usable=true;for(int i=1;i<points.Length;i++)usable&=TileMover.CanTraverseBox(r,r.World(points[i-1]),r.World(points[i]),buddyExtent);
                Stage64DReplayEvidence.Passage(test,"Clearance",cursor==points.Length,bodyClear,crossedMiddle,usable);
                Check("leader-cut "+names[test]+" passage fits actual Buddy body",usable);
                if(!usable){Finish();yield break;} // Clearance rejection is not labelled a navigation failure.
                end=Time.realtimeSinceStartup+20;
                while(!finished&&Time.realtimeSinceStartup<end&&Vector2.Distance(buddyBody.position,playerBody.position)>1.7f)
                {if(r.LogicalReady)bodyClear&=TileMover.CanOccupyBox(r,buddyBody.position,buddyExtent);crossedMiddle|=Vector2.Distance(buddyBody.position,middle)<1.2f;yield return null;}
                Stage64DReplayEvidence.Passage(test,"Buddy",cursor==points.Length,bodyClear,crossedMiddle,usable);
                Check("real Buddy follows leader-cut "+names[test]+" passage with physical clearance",bodyClear&&crossedMiddle&&Vector2.Distance(buddyBody.position,playerBody.position)<=1.7f);
                if(!bodyClear||!crossedMiddle||Vector2.Distance(buddyBody.position,playerBody.position)>1.7f){Finish();yield break;}
                yield return Capture("leader-cut-"+names[test]+"-passage");
            }
        }
        finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());if(ownedMouse!=null){InputSystem.RemoveDevice(ownedMouse);ownedMouse=null;}File.WriteAllLines(Path.Combine(directory,"leader-cut-passage-samples.csv"),samples);diagonalObservation.Export(directory);Stage64DReplayEvidence.EndBuddy(directory);}
    }
    IEnumerator MushProof()
    {
        var r=Find<ExpeditionRegion>();Check("new attempt resets excavation and discovery",r.Excavations==0&&r.Definition.Concealed.All(g=>g.Cells.All(i=>!r.Discovery.Shown(i%144,i/144))));
        yield return Wait(()=>Find<MushWormIntroRuntime>()!=null,"original Mush opportunity available on eligible second run",10);if(finished)yield break;
        string reference=JsonUtility.ToJson(r.Presentation.settings);float normalSize=(float)(r.Presentation.settings.centerWidth/r.Presentation.settings.coverage/(16.0/9)*.5);
        Warp(r,new SporeGobbo.Geometry.Point2(60.5,33.5));yield return Wait(()=>StoryEventCoordinator.HasActiveEvent&&Find<StoryEventPresentation>()!=null,"actual proximity trigger opens original Mush event",15);
        for(int i=0;i<45;i++)yield return null;var presentation=Find<StoryEventPresentation>();
        Check("story uses real modal and projected uniform zoom",SporeInputReader.Instance.Context==SporeInputContext.Story&&r.Presentation.StoryZoom<.9f&&r.RegionCamera.orthographicSize<normalSize*.9f);
        var target=(Vector2)r.Player.transform.position+new Vector2(.7f,.4f);var screen=r.RegionCamera.WorldToScreenPoint(r.Render(target));var inverse=r.Player.GetComponent<ActorTerrainBinding>().PointerToWorld(new Vector2(screen.x,screen.y));Check("inverse pointer remains accurate during story view",Vector2.Distance(inverse,target)<.001f);yield return Capture("mush-original-placeholder-conversation");
        presentation.ContinueButton.onClick.Invoke();yield return Wait(()=>!StoryEventCoordinator.HasActiveEvent&&Mathf.Abs(r.Presentation.StoryZoom-1)<1e-6,"existing event restores camera and releases modal",15);for(int i=0;i<45;i++)yield return null;
        Check("Mush history is canonical and baseline settings unchanged",StoryEventService.IsCompleted(GameState.Instance,StoryEventIds.MushWormIntro)&&JsonUtility.ToJson(r.Presentation.settings)==reference&&Mathf.Abs(r.RegionCamera.orthographicSize-normalSize)<.0001f&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay);
        Check("completed Mush opportunity cannot be offered again",!MushWormIntroEligibility.IsEligible(GameState.Instance.currentRunNumber,GameState.Instance.storyProgress,true));yield return Capture("mush-view-restored");
    }
    IEnumerator GamepadProof()
    {
        Stage61RuntimeMetrics.Mark("real gamepad movement and pause");var pad=ownedGamepad=InputSystem.AddDevice<Gamepad>("Stage61 owned virtual gamepad");pad.MakeCurrent();
        var region=Find<SettlementCampRegion>();var controls=region.GetComponent<DevelopmentCampControls>();yield return Wait(()=>controls.Warp("fire.watch"),"gamepad fixture reaches clear observation point",15);for(int i=0;i<10;i++)yield return null;
        var body=Find<CampPlayableSpawner>().SpawnedPlayer.GetComponent<Rigidbody2D>();Vector2 before=body.position;
        InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right});for(int i=0;i<30;i++)yield return null;
        InputSystem.QueueStateEvent(pad,new GamepadState());for(int i=0;i<4;i++)yield return null;
        Check("real gamepad action moves logical player",Vector2.Distance(before,body.position)>.2f&&SporeInputReader.Instance.ActiveControlScheme==SporeControlScheme.Gamepad);
        InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start));yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());for(int i=0;i<4;i++)yield return null;
        var pause=Find<PauseMenuController>();Check("real gamepad pause owns input",pause.IsPauseOpen&&Time.timeScale==0);
        var selected=UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;Check("pause has a valid gamepad focus target",selected!=null&&selected.transform.IsChildOf(pause.pausePanel.transform));
        InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());for(int i=0;i<4;i++)yield return null;
        Check("real gamepad cancel resumes gameplay",!pause.IsPauseOpen&&Time.timeScale>0&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay);
        InputSystem.RemoveDevice(pad);ownedGamepad=null;keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState());for(int i=0;i<4;i++)yield return null;
    }
    string MemoryRow(int cycle,string phase)=>cycle+","+phase+","+UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()+","+UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()+","+UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()+","+Resources.FindObjectsOfTypeAll<Mesh>().Length+","+Resources.FindObjectsOfTypeAll<Material>().Length+","+Stage59ActorView.OwnedMeshes+","+Stage59ActorView.OwnedMaterials;
    IEnumerator OpenReturnPanels()
    {
        yield return Wait(()=>SceneManager.GetActiveScene().name==GameSceneCatalog.Camp&&Find<CampSceneController>()!=null&&Find<SettlementCampRegion>()!=null&&Find<SettlementCampRegion>().Ready,"return restores region before report");
        if(finished)yield break;var controller=Find<CampSceneController>();for(int i=0;i<4;i++)yield return null;
        Check("real run summary is presented",controller.runStatsPanel.activeInHierarchy);
        yield return Capture("actual-run-summary-"+GameState.Instance.lastRun.runNumber);controller.continueToSurvivorsButton.onClick.Invoke();yield return null;yield return Capture("actual-buddy-origins-"+GameState.Instance.lastRun.runNumber);controller.continueToCampButton.onClick.Invoke();
        yield return Wait(CampReady,"real return UI opens ready Camp");
    }
    void CheckAuthorities(string phase)
    {
        Check("single authorities / "+phase,UnityEngine.Object.FindObjectsByType<GameState>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<CampSceneController>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SporeInputReader>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SporeUiCoordinator>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SettlementCampRegion>(FindObjectsSortMode.None).Length==1&&ReferenceEquals(DiggableTerrainService.Active,Find<SettlementCampRegion>()));
        Check("one Camp routine and spawner without legacy collision / "+phase,UnityEngine.Object.FindObjectsByType<CampStartRoutineManager>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<CampPlayableSpawner>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<UnityEngine.Tilemaps.TilemapCollider2D>(FindObjectsSortMode.None).All(c=>!c.enabled||!c.gameObject.activeInHierarchy));
    }
    IEnumerator Press(Key key,int frames){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));for(int i=0;i<frames;i++)yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());for(int i=0;i<4;i++)yield return null;}
    IEnumerator Capture(string name){Stage61RuntimeMetrics.Mark(name);yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());}finally{Destroy(texture);}}
    void Check(string name,bool passed){checks.Add((passed?"PASS ":"FAIL ")+name);File.WriteAllLines(Path.Combine(directory,"checks-in-progress.txt"),checks);}
    [Serializable]sealed class Result{public bool finished;public string error;public string[] checks,runtimeErrors;}
    void Finish(){if(finished)return;finished=true;bool ok=errors.Count==0&&checks.All(item=>item.StartsWith("PASS "));File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=ok?"":"Actual lifecycle validation failed",checks=Stage62CIntroEvidence.FinalChecks.Concat(checks).ToArray(),runtimeErrors=errors.ToArray()},true));Application.Quit(ok?0:1);}
    void OnDestroy(){Application.logMessageReceived-=Observe;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(ownedGamepad!=null)InputSystem.RemoveDevice(ownedGamepad);if(ownedMouse!=null)InputSystem.RemoveDevice(ownedMouse);}
}
#endif
