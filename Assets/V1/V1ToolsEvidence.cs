#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using SporeGobbo.Safety;using SporeGobbo.Input;
/// <summary>Explicit synthetic outcome through the same builder and return transaction as the F9 Camp tool. Not natural expedition evidence.</summary>
public sealed class V1ToolsEvidence:MonoBehaviour
{
 readonly List<string> checks=new List<string>(),errors=new List<string>();string directory;float deadline;bool finished;Keyboard keyboard;
 static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
 static bool Camp()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true;
 void Awake(){directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);deadline=Time.realtimeSinceStartup+180;Application.logMessageReceived+=Observe;}
 void Observe(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(m+"\n"+trace);}
 void Update(){if(!finished&&(errors.Count>0||Time.realtimeSinceStartup>deadline))Finish();}
 void Check(string name,bool pass){checks.Add((pass?"PASS ":"FAIL ")+name);if(!pass)Finish();}
 IEnumerator Wait(Func<bool> ready,string name,float seconds=70){Stage61RuntimeMetrics.Category("tools setup");Stage61RuntimeMetrics.Mark(name);float end=Time.realtimeSinceStartup+seconds;while(!finished&&!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(name,!finished&&ready());}
 IEnumerator PressF9(){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F9));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
 IEnumerator Start()
 {
  yield return null;yield return null;
  Check("tools evidence uses owned development MainMenu",Persistence.Isolated&&V1RuntimeProfile.DevelopmentToolsEnabled&&V1RuntimeProfile.EvidenceEnabled&&SceneManager.GetActiveScene().name=="MainMenu");if(finished)yield break;
  keyboard=InputSystem.AddDevice<Keyboard>("V1 tools proof keyboard");keyboard.MakeCurrent();
  DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);yield return Wait(Camp,"real Menu loads established synthetic campaign B");if(finished)yield break;
  Check("one discoverable F9 hub",DevelopmentGameControls.HasHub&&UnityEngine.Object.FindObjectsByType<DevelopmentGameControls>(FindObjectsSortMode.None).Length==1);
  yield return PressF9();var visible=typeof(DevelopmentGameControls).GetField("visible",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);Check("actual F9 input opens existing hub",visible!=null&&(bool)visible.GetValue(DevelopmentGameControls.Instance));yield return PressF9();Check("actual F9 input closes existing hub",visible!=null&&!(bool)visible.GetValue(DevelopmentGameControls.Instance));if(finished)yield break;
  var state=GameState.Instance;Check("fixture B real owned roster has ten Buddies",state.ownedGobbos.Count==10);if(finished)yield break;
  string path=SporeSaveManager.GetSlotPath(2),primary=File.ReadAllText(path);int deathBefore=state.deathHistory.Count,runBefore=state.currentRunNumber,callbacks=0;FakeCompletedRunResult outcome=null;
  var input=new FakeCompletedRunInput(2,1,2);Check("same Camp tool builder validates requested synthetic outcome",FakeCompletedRunBuilder.Validate(state,input,out _));
  Stage61RuntimeMetrics.Category("tools fault transaction");Stage61RuntimeMetrics.Exclude("injected failed write",2);SporeSaveManager.FailNextDevelopmentWrite("validated");
  bool accepted=RunReturnService.ReturnSimulatedCampOutcome(()=>{callbacks++;outcome=FakeCompletedRunBuilder.Apply(state,input);});
  Check("injected commit failure holds prepared return without primary replacement",!accepted&&callbacks==1&&RunReturnService.HasPreparedCampSimulation&&File.ReadAllText(path)==primary);
  Check("synthetic callback applied exactly one death and two stable recruits",outcome!=null&&outcome.DeathIds.Count==1&&outcome.NewBuddyIds.Count==2&&state.deathHistory.Count==deathBefore+1&&state.ownedGobbos.Count==11&&state.currentRunNumber==runBefore+1);if(finished)yield break;
  string ids=Ids(state.ownedGobbos);string deathId=outcome.DeathIds[0];string[] recruited=outcome.NewBuddyIds.ToArray();
  Check("retry uses prepared return without invoking callback again",RunReturnService.ReturnSimulatedCampOutcome(()=>{callbacks++;throw new InvalidOperationException("Prepared retry must never reapply builder");}));
  yield return OpenReturn();if(finished)yield break;state=GameState.Instance;
  Check("return arrival preserves outcome exactly once",callbacks==1&&Ids(state.ownedGobbos)==ids&&state.deathHistory.Count==deathBefore+1&&state.currentRunNumber==runBefore+1&&!RunReturnService.HasPreparedCampSimulation);
  Check("dead identity removed and both new identities canonical",!state.ownedGobbos.Any(g=>g.uniqueId==deathId)&&recruited.All(id=>state.ownedGobbos.Count(g=>g.uniqueId==id)==1));
  var saved=SporeSaveManager.LoadSlot(2);Check("durable snapshot retains coherent campaign and region",saved!=null&&saved.campTerrainState?.regionSnapshot!=null&&Ids(saved.ownedGobbos)==ids&&saved.deathHistory.Count==deathBefore+1&&saved.ownedGobbos.Where(g=>g.campResidentialSlotId>0).GroupBy(g=>g.campResidentialSlotId).All(g=>g.Count()==1));
  string canonical=JsonUtility.ToJson(state),bytes=File.ReadAllText(path);var library=Resources.FindObjectsOfTypeAll<JournalContentLibrary>().FirstOrDefault();Check("actual Journal content library available",library!=null);if(finished)yield break;
  var dataView=DevStateSnapshot.Build(state);var journalView=JournalSnapshotBuilder.Build(library,state);InventoryService.GetStacks(state);
  Check("Data Journal inventory reads preserve canonical runtime and primary bytes",dataView!=null&&journalView!=null&&JsonUtility.ToJson(state)==canonical&&File.ReadAllText(path)==bytes);
  CheckAuthorities();if(finished)yield break;
  Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"real pause quits to Menu");if(finished)yield break;Find<MainMenuController>().ContinueLastGame();yield return Wait(Camp,"real Continue restores committed synthetic outcome");if(finished)yield break;
  state=GameState.Instance;Check("Continue persists IDs death and outcome once",Ids(state.ownedGobbos)==ids&&state.deathHistory.Count==deathBefore+1&&state.currentRunNumber==runBefore+1&&recruited.All(id=>state.ownedGobbos.Any(g=>g.uniqueId==id)));CheckAuthorities();if(finished)yield break;
  Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"binding proof returns to Menu");if(finished)yield break;
  Check("Fresh A slot remains unused before binding proof",SporeSaveManager.InspectSlot(1)==SporeSaveManager.SlotStatus.Missing);if(finished)yield break;DevelopmentCampaignFixtures.Import(1);Find<MainMenuController>().LoadGame(1);
  yield return Wait(()=>Find<IntroRegion>()?.Player!=null&&Find<IntroRegion>().PresentationReady,"real Intro prompt presentation initializes");if(finished)yield break;
  var sequence=Find<IntroSequence>();var bindings=SporeInputReader.Instance.Bindings;string original=bindings.SaveJson(),oldDisplay=sequence.Binding("Dig");var action=bindings.FindAction("Gameplay","Dig");int index=-1;for(int i=0;i<action.bindings.Count;i++)if(!action.bindings[i].isComposite&&!action.bindings[i].isPartOfComposite&&(action.bindings[i].groups??"").Split(';').Contains("KeyboardMouse")){index=i;break;}
  Check("actual Dig keyboard binding found",index>=0);if(finished)yield break;
  try{action.ApplyBindingOverride(index,"<Keyboard>/f6");bindings.LoadJson(bindings.SaveJson());string expected=bindings.GetDisplay("Gameplay","Dig",BindingScheme.KeyboardMouse);Check("Intro cache invalidates on existing BindingsChanged event",sequence.Binding("Dig")==expected&&expected!=oldDisplay);}finally{bindings.LoadJson(string.IsNullOrWhiteSpace(original)?"{\"bindings\":[]}":original);}
  string restoredDisplay=sequence.Binding("Dig"),restoredJson=bindings.SaveJson();File.WriteAllText(Path.Combine(directory,"binding-cache-diagnostics.json"),JsonUtility.ToJson(new BindingDiagnostics{originalJson=original,restoredJson=restoredJson,originalDisplay=oldDisplay,restoredDisplay=restoredDisplay},true));
  Check("Intro cached prompt restores original binding without preference Save",restoredDisplay==oldDisplay&&restoredJson==original);Finish();
 }
 static string Ids(System.Collections.Generic.IEnumerable<GobboUnitSaveData> roster)=>string.Join("|",roster.Where(x=>x!=null).Select(x=>x.uniqueId).OrderBy(x=>x,StringComparer.Ordinal));
 void CheckAuthorities(){Check("one real campaign terrain input UI and hub",UnityEngine.Object.FindObjectsByType<GameState>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SettlementCampRegion>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SporeInputReader>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SporeUiCoordinator>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<DevelopmentGameControls>(FindObjectsSortMode.None).Length==1&&ReferenceEquals(DiggableTerrainService.Active,Find<SettlementCampRegion>()));}
 IEnumerator OpenReturn(){yield return Wait(()=>Find<CampSceneController>()!=null,"actual return coordinator");float end=Time.realtimeSinceStartup+70;while(!finished&&!Camp()&&Time.realtimeSinceStartup<end){var c=Find<CampSceneController>();if(c.continueToSurvivorsButton!=null&&c.continueToSurvivorsButton.gameObject.activeInHierarchy)c.continueToSurvivorsButton.onClick.Invoke();else if(c.continueToCampButton!=null&&c.continueToCampButton.gameObject.activeInHierarchy)c.continueToCampButton.onClick.Invoke();yield return null;}Check("actual summary buttons release Camp",Camp());}
 void Finish(){if(finished)return;finished=true;Stage61RuntimeMetrics.Category("tools evidence export");Stage61RuntimeMetrics.Exclude("export",2);if(Time.realtimeSinceStartup>deadline)errors.Add("Tools proof exceeded180second watchdog");Find<Stage61RuntimeMetrics>()?.Export();File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=errors.Count>0?"Tools proof failed":"",checks=checks.ToArray(),runtimeErrors=errors.ToArray(),method="Explicit synthetic Camp outcome(2 participants,1 death,2 recruits) through the exact F9 builder/return service; actual F9 input and existing summary buttons; fault injection and fixture import are isolated adapters, not natural expedition gameplay."},true));Application.Quit(errors.Count==0&&checks.Count>0&&checks.All(x=>x.StartsWith("PASS "))?0:1);}
 [Serializable]sealed class Result{public bool finished;public string error,method;public string[] checks,runtimeErrors;}
 [Serializable]sealed class BindingDiagnostics{public string originalJson,restoredJson,originalDisplay,restoredDisplay;}
 void OnDestroy(){Application.logMessageReceived-=Observe;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);}
}
#endif
