#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SporeGobbo.Safety;
/// <summary>Cross-process owned Continue and approved unresolved-succession rollback proof.</summary>
public sealed class Stage61ContinueEvidence:MonoBehaviour
{
    [Serializable]sealed class Expectation{public int process,revision,beds,mushrooms;public string leader,physical,primarySha256;public string[] roster;}
    [Serializable]sealed class Result{public bool finished;public string error;public string[] checks,runtimeErrors;}
    readonly List<string> checks=new List<string>(),errors=new List<string>();string directory,phase;bool finished;float deadline;
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    static bool Ready()=>Find<CampSceneController>()!=null&&Find<CampSceneController>().WorldInputReady&&Find<SettlementCampRegion>()!=null&&Find<SettlementCampRegion>().Ready;
    void Awake(){phase=Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE");directory=Persistence.ExperimentPath("Evidence",Environment.GetEnvironmentVariable("SPORE_STAGE61_EVIDENCE_ID"));Directory.CreateDirectory(directory);deadline=Time.realtimeSinceStartup+180;Application.logMessageReceived+=Observe;}
    void Observe(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    void Update(){if(!finished&&(errors.Count>0||Time.realtimeSinceStartup>deadline)){if(errors.Count==0)errors.Add("Cross-process proof timed out");Finish();}}
    IEnumerator Wait(Func<bool> condition,string name){while(!finished&&!condition()&&Time.realtimeSinceStartup<deadline)yield return null;Check(name,!finished&&condition());}
    IEnumerator Start()
    {
        yield return null;yield return null;
        if(phase=="continue-intro-seed"||phase=="continue-intro-check"){yield return IntroProof();yield break;}
        if(phase=="continue-seed")
        {
            DevelopmentCampaignFixtures.Import(1);SporeSaveManager.SaveCurrentSlotFromGameState();
            string aPath=SporeSaveManager.GetSlotPath(1),aBytes=File.ReadAllText(aPath);
            SporeSaveManager.DeleteSlot(1);
            Check("owned reset archives primary and backup and frees only A",SporeSaveManager.InspectSlot(1)==SporeSaveManager.SlotStatus.Missing&&Directory.GetFiles(Persistence.ExperimentPath("RetiredCampaigns")).Length==2&&Directory.GetFiles(Persistence.ExperimentPath("RetiredCampaigns")).Any(path=>File.ReadAllText(path)==aBytes));
            DevelopmentCampaignFixtures.Import(1);DevelopmentCampaignFixtures.Import(3);
            DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);
            yield return Wait(Ready,"seed uses real load and ready Camp");if(finished)yield break;
            CampResourceService.Add(GameState.Instance,CampResourceType.Mushrooms,1);SporeSaveManager.SaveCurrentSlotFromGameState();
            Store("expected",CaptureExpectation());
            Check("seed contains ten real owned beds",GameState.Instance.campTerrainState.residentialSlotsEstablished==10&&GameState.Instance.ownedGobbos.Count==10);
            Stage61RuntimeMetrics.Mark("cross-process seed quit");Finish(true);yield break;
        }
        Find<MainMenuController>().ContinueLastGame();yield return Wait(Ready,"fresh process actual Continue restores Camp");if(finished)yield break;
        if(phase=="continue-check")
        {
            var expected=Read("expected");Check("new operating-system process",expected.process!=System.Diagnostics.Process.GetCurrentProcess().Id);
            Compare(expected,"cross-process Continue");Finish();yield break;
        }
        if(phase=="continue-rollback")
        {
            var expected=Read("rollback");Check("rollback uses a new process",expected.process!=System.Diagnostics.Process.GetCurrentProcess().Id);
            Compare(expected,"unresolved succession rollback");
            Check("rollback restores living previous leader",!GameState.Instance.GetLeader().isDead&&GameState.Instance.GetLeader().health>0);
            Check("pending successor UI does not leak into Continue",PlayerDeathRunStore.Instance==null||!PlayerDeathRunStore.Instance.playerDiedThisRun);
            Finish();yield break;
        }
        if(phase!="continue-pending-death"){errors.Add("Unreviewed proof phase");Finish();yield break;}
        var prior=CaptureExpectation();Store("rollback",prior);
        var portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);portal.goButton.onClick.Invoke();
        yield return Wait(()=>SceneManager.GetActiveScene().name==GameSceneCatalog.CandidateExpedition&&Find<ExpeditionRegion>()!=null&&Find<ExpeditionRegion>().PresentationReady&&Find<GobboController>()!=null,"rollback proof enters actual NormalRun");
        for(int i=0;i<30;i++)yield return null;
        // Portal departure commits once; this is the exact last Camp generation to protect.
        prior.primarySha256=CampaignSnapshotStorage.Hash(File.ReadAllText(SporeSaveManager.GetSlotPath(2)));Store("rollback",prior);
        Find<GobboController>().TakeDamage(1000000);
        yield return Wait(()=>SceneManager.GetActiveScene().name==GameSceneCatalog.Camp&&Find<CampSuccessionUI>()!=null&&Find<CampSuccessionUI>().successionPanel.activeInHierarchy,"real death waits for successor selection");if(finished)yield break;
        SporeSaveManager.SaveCurrentSlotFromGameState();
        Check("pending death preserves exact committed bytes before quit",CampaignSnapshotStorage.Hash(File.ReadAllText(SporeSaveManager.GetSlotPath(2)))==prior.primarySha256);
        Check("pending selection stays unresolved on quit",PlayerDeathRunStore.Instance.playerDiedThisRun&&GameState.Instance.GetLeader().isDead&&!Find<CampSceneController>().WorldInputReady);
        Finish();
    }
    [Serializable]sealed class IntroExpectation{public int process;public string identity,primary,otherA,otherB;}
    string SlotHash(int slot){string path=SporeSaveManager.GetSlotPath(slot);return File.Exists(path)?CampaignSnapshotStorage.Hash(File.ReadAllText(path)):"missing";}
    IEnumerator IntroProof()
    {
        string path=Persistence.ExperimentPath("ContinueProof","intro.json");Directory.CreateDirectory(Path.GetDirectoryName(path));
        if(phase=="continue-intro-seed")
        {
            if(SporeSaveManager.InspectSlot(3)!=SporeSaveManager.SlotStatus.Missing)SporeSaveManager.DeleteSlot(3);
            Find<MainMenuController>().StartNewGame(3);var naming=Find<NewGameButton>();if(naming!=null&&naming.namePromptPanel.activeInHierarchy){naming.playerNameInput.text="Intro OS restart proof";naming.confirmNameButton.onClick.Invoke();}
            yield return Wait(()=>Find<IntroRegion>()!=null&&Find<IntroRegion>().Player!=null&&Find<IntroRegion>().Sequence.BirthTime>=1.2f,"cross-process seed reaches actual falling spore");if(finished)yield break;
            var expected=new IntroExpectation{process=System.Diagnostics.Process.GetCurrentProcess().Id,identity=GameState.Instance.GetLeader().uniqueId,primary=SlotHash(3),otherA=SlotHash(1),otherB=SlotHash(2)};File.WriteAllText(path,JsonUtility.ToJson(expected,true));SporeSaveManager.SaveCurrentSlotFromGameState();
            Check("Intro save defers and completion remains unset",SlotHash(3)==expected.primary&&SporeSaveManager.ShouldLoadIntro(SporeSaveManager.LoadSlot(3))&&!GameState.Instance.HasActiveRunSnapshot);Finish(true);yield break;
        }
        var prior=JsonUtility.FromJson<IntroExpectation>(File.ReadAllText(path));Find<MainMenuController>().ContinueLastGame();
        yield return Wait(()=>Find<IntroRegion>()!=null&&Find<IntroRegion>().Player!=null&&Find<IntroRegion>().PresentationReady,"fresh process actual Continue restarts authored Intro");if(finished)yield break;
        var region=Find<IntroRegion>();Check("Intro restart uses fresh OS process and same leader",prior.process!=System.Diagnostics.Process.GetCurrentProcess().Id&&GameState.Instance.GetLeader().uniqueId==prior.identity);
        Check("restart is at birth with one real actor and no completed fact",region.Sequence.BirthTime<1.5f&&UnityEngine.Object.FindObjectsByType<GobboController>(FindObjectsSortMode.None).Length==1&&SporeSaveManager.ShouldLoadIntro(SporeSaveManager.LoadSlot(3)));
        Check("other owned campaign commits remain byte-exact",SlotHash(1)==prior.otherA&&SlotHash(2)==prior.otherB);SporeSaveManager.SaveCurrentSlotFromGameState();Check("interrupted birth snapshot stays exact",SlotHash(3)==prior.primary);Finish();
    }
    Expectation CaptureExpectation(){var state=GameState.Instance;var region=Find<SettlementCampRegion>();return new Expectation{process=System.Diagnostics.Process.GetCurrentProcess().Id,leader=state.GetLeader().uniqueId,revision=region.Revision,beds=state.campTerrainState.residentialSlotsEstablished,mushrooms=CampResourceService.GetAmount(state,CampResourceType.Mushrooms),physical=JsonUtility.ToJson(region.CaptureRegionSnapshot(state)),roster=state.ownedGobbos.Select(unit=>unit.uniqueId+":"+unit.campResidentialSlotId).OrderBy(id=>id).ToArray(),primarySha256=CampaignSnapshotStorage.Hash(File.ReadAllText(SporeSaveManager.GetSlotPath(2)))};}
    void Compare(Expectation expected,string name){var actual=CaptureExpectation();Check(name+" preserves leader/roster/ownership",actual.leader==expected.leader&&actual.roster.SequenceEqual(expected.roster));Check(name+" preserves physical root edits and beds",actual.physical==expected.physical&&actual.beds==expected.beds&&actual.revision==expected.revision);Check(name+" preserves real resources",actual.mushrooms==expected.mushrooms);Check(name+" has single campaign and terrain",UnityEngine.Object.FindObjectsByType<GameState>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SettlementCampRegion>(FindObjectsSortMode.None).Length==1&&ReferenceEquals(DiggableTerrainService.Active,Find<SettlementCampRegion>()));}
    string FilePath(string name){string folder=Persistence.ExperimentPath("ContinueProof");Directory.CreateDirectory(folder);return Path.Combine(folder,name+".json");}
    void Store(string name,Expectation value){File.WriteAllText(FilePath(name),JsonUtility.ToJson(value,true));}
    Expectation Read(string name)=>JsonUtility.FromJson<Expectation>(File.ReadAllText(FilePath(name)));
    void Check(string name,bool pass){checks.Add((pass?"PASS ":"FAIL ")+name);File.WriteAllLines(Path.Combine(directory,"checks-in-progress.txt"),checks);}
    void Finish(bool quitThroughPause=false){if(finished)return;finished=true;bool ok=errors.Count==0&&checks.All(check=>check.StartsWith("PASS "));File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=ok?"":"Cross-process proof failed",checks=checks.ToArray(),runtimeErrors=errors.ToArray()},true));if(ok&&quitThroughPause){var pause=Find<PauseMenuController>();pause.OpenPause();pause.QuitToDesktop();}else Application.Quit(ok?0:1);}
    void OnDestroy(){Application.logMessageReceived-=Observe;}
}
#endif
