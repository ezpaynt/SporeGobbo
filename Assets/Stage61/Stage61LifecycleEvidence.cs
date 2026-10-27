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
public sealed class Stage61LifecycleEvidence : MonoBehaviour
{
    readonly List<string> checks=new List<string>(), errors=new List<string>();
    string directory; bool finished; float watchdog; Keyboard keyboard;Gamepad ownedGamepad;
    void Awake(){directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);watchdog=Time.realtimeSinceStartup+720;Application.logMessageReceived+=Observe;}
    void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    void Update(){if(!finished&&(errors.Count>0||Time.realtimeSinceStartup>watchdog)){if(errors.Count==0)errors.Add("Lifecycle capture timed out");Finish();}}
    static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    static bool CampReady()=>SceneManager.GetActiveScene().name==GameSceneCatalog.Camp&&Find<CampSceneController>()!=null&&Find<CampSceneController>().WorldInputReady&&Find<SettlementCampRegion>()!=null&&Find<SettlementCampRegion>().Ready;
    IEnumerator Wait(Func<bool> ready,string name,float seconds=90){Stage61RuntimeMetrics.Mark(name);float end=Time.realtimeSinceStartup+seconds;while(!finished&&!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(name,!finished&&ready());}
    IEnumerator Start()
    {
        yield return null;yield return null;
        keyboard=InputSystem.AddDevice<Keyboard>("Stage61 owned lifecycle keyboard");keyboard.MakeCurrent();
        Check("entry uses actual Main Menu",SceneManager.GetActiveScene().name=="MainMenu"&&Find<MainMenuController>()!=null);
        var menu=Find<MainMenuController>();menu.StartNewGame(1);
        var naming=Find<NewGameButton>();
        if(naming!=null&&naming.namePromptPanel!=null&&naming.namePromptPanel.activeInHierarchy)
        {naming.playerNameInput.text="Development Fresh A";naming.confirmNameButton.onClick.Invoke();}
        yield return Wait(()=>SceneManager.GetActiveScene().name=="SampleScene"&&SampleSceneModeController.IsIntroMode&&MapGenerator.Instance!=null&&MapGenerator.Instance.Data!=null&&Find<GobboController>()!=null,"real New Game enters actual Intro");
        if(finished)yield break;
        Check("Intro has no run snapshot",!GameState.Instance.HasActiveRunSnapshot);
        yield return Wait(()=>Find<IntroCampTransition>()!=null&&Find<IntroCampTransition>().gameObject.activeInHierarchy,"actual Intro terminal becomes active");
        var player=Find<GobboController>();var intro=Find<IntroCampTransition>();
        // Deliberate test shortcut, using the actual terminal's Physics2D trigger.
        var body=player.GetComponent<Rigidbody2D>();body.position=intro.transform.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
        yield return Wait(CampReady,"actual Intro trigger reaches ready replacement Camp");if(finished)yield break;
        Check("Intro discovery committed by real Camp arrival",!SporeSaveManager.ShouldLoadIntro(SporeSaveManager.LoadSlot(1)));
        CheckAuthorities("first arrival");yield return Capture("intro-arrival-real-camp");
        yield return GamepadProof();
        var region=Find<SettlementCampRegion>();string initialPhysical=JsonUtility.ToJson(region.CaptureRegionSnapshot(GameState.Instance));
        var portal=Find<CampRunPortal>();Check("portal preserves Normal Run Baseline selection",portal.developmentRunProfileOverride==null);
        portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);yield return null;portal.goButton.onClick.Invoke();
        yield return Wait(()=>SceneManager.GetActiveScene().name=="SampleScene"&&!SampleSceneModeController.IsIntroMode&&MapGenerator.Instance!=null&&MapGenerator.Instance.Data!=null&&Find<GobboController>()!=null,"actual Camp portal enters existing NormalRun");
        for(int i=0;i<30;i++)yield return null;if(finished)yield break;
        Check("existing run snapshot active",GameState.Instance.HasActiveRunSnapshot);
        Check("existing normal profile selected",MapGenerator.Instance.selectedProfile==Find<SampleSceneModeController>().normalRunProfile);
        Check("Camp terrain provider unregisters for run",!(DiggableTerrainService.Active is SettlementCampRegion));
        player=Find<GobboController>();
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
        yield return Wait(()=>SceneManager.GetActiveScene().name=="SampleScene"&&!SampleSceneModeController.IsIntroMode&&Find<GobboController>()!=null&&MapGenerator.Instance!=null&&MapGenerator.Instance.Data!=null,"second existing run starts");
        for(int i=0;i<30;i++)yield return null;
        Check("real active squad transfers to existing run",UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Any(unit=>unit.unitData.uniqueId==buddyId));
        Check("normal return uses existing service",RunReturnService.ReturnToCamp("CampScene",true,true,RunReturnReason.NormalExit,"owned normal return"));
        yield return OpenReturnPanels();if(finished)yield break;
        Check("repeated returns keep one region",Find<SettlementCampRegion>().RegionId=="camp");
        var pause=Find<PauseMenuController>();pause.OpenPause();pause.QuitToMainMenu();
        yield return Wait(()=>SceneManager.GetActiveScene().name=="MainMenu"&&Find<MainMenuController>()!=null,"actual pause quits to Main Menu");yield return null;
        Find<MainMenuController>().ContinueLastGame();yield return Wait(CampReady,"actual Continue restores replacement Camp");if(finished)yield break;
        Check("Continue preserves recruited Buddy",GameState.Instance.ownedGobbos.Any(unit=>unit.uniqueId==buddyId));CheckAuthorities("Continue");
        // Actual player death with a living successor; save failure must remain retryable.
        portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);portal.goButton.onClick.Invoke();
        yield return Wait(()=>SceneManager.GetActiveScene().name=="SampleScene"&&Find<GobboController>()!=null&&MapGenerator.Instance!=null&&MapGenerator.Instance.Data!=null,"death scenario enters existing run");
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
        yield return Wait(()=>SceneManager.GetActiveScene().name=="SampleScene"&&Find<GobboController>()!=null&&MapGenerator.Instance!=null&&MapGenerator.Instance.Data!=null,"terminal scenario enters actual NormalRun");
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
    IEnumerator GamepadProof()
    {
        Stage61RuntimeMetrics.Mark("real gamepad movement and pause");var pad=ownedGamepad=InputSystem.AddDevice<Gamepad>("Stage61 owned virtual gamepad");pad.MakeCurrent();
        var region=Find<SettlementCampRegion>();var controls=region.GetComponent<DevelopmentCampControls>();Check("gamepad fixture reaches clear observation point",controls.Warp("fire.watch"));for(int i=0;i<10;i++)yield return null;
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
    void Finish(){if(finished)return;finished=true;bool ok=errors.Count==0&&checks.All(item=>item.StartsWith("PASS "));File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=ok?"":"Actual lifecycle validation failed",checks=checks.ToArray(),runtimeErrors=errors.ToArray()},true));Application.Quit(ok?0:1);}
    void OnDestroy(){Application.logMessageReceived-=Observe;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(ownedGamepad!=null)InputSystem.RemoveDevice(ownedGamepad);}
}
#endif
