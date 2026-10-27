#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using SporeGobbo.Geometry;using SporeGobbo.Safety;
/// <summary>Tools-OFF actual MainMenu / Intro / Camp / expedition proof, explicit input and setup adapters.</summary>
public sealed class V1NormalEvidence:MonoBehaviour
{
 readonly List<string> checks=new List<string>(),errors=new List<string>();Keyboard keyboard;Mouse mouse;bool finished;string directory;float deadline;
 static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
 protected void Check(string n,bool ok){checks.Add((ok?"PASS ":"FAIL ")+n);File.WriteAllLines(Path.Combine(directory,"checks-in-progress.txt"),checks);if(!ok)Finish();}
 void Awake(){directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);deadline=Time.realtimeSinceStartup+900;Application.logMessageReceived+=Observe;}
 void Observe(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(m+"\n"+trace);}
 void Update(){if(!finished&&(errors.Count>0||Time.realtimeSinceStartup>deadline))Finish();}
 IEnumerator Wait(Func<bool> test,string n,float seconds=70){Stage61RuntimeMetrics.Category("setup");Stage61RuntimeMetrics.Mark(n);float end=Time.realtimeSinceStartup+seconds;while(!finished&&!test()&&Time.realtimeSinceStartup<end)yield return null;Check(n,!finished&&test());}
 static bool Camp()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true;
 public static bool Expedition()=>Find<ExpeditionRegion>()?.Player!=null&&Find<ExpeditionRegion>().PresentationReady;
 IEnumerator Start()
 {
  yield return null;yield return null;keyboard=InputSystem.AddDevice<Keyboard>("V1 normal proof keyboard");mouse=InputSystem.AddDevice<Mouse>("V1 normal proof mouse");keyboard.MakeCurrent();mouse.MakeCurrent();
  Check("normal MainMenu first, no historical entry bootstrap",SceneManager.GetActiveScene().name=="MainMenu"&&Find<DevelopmentCampEntry>()==null);
  Check("normal gameplay has development tools OFF",!V1RuntimeProfile.DevelopmentToolsEnabled&&!GameSceneCatalog.DevelopmentCandidate&&V1RuntimeProfile.StorageProfile=="normal");
  Find<MainMenuController>().StartNewGame(1);var naming=Find<NewGameButton>();if(naming!=null&&naming.namePromptPanel.activeInHierarchy){naming.playerNameInput.text="V1 Normal Gobbo";naming.confirmNameButton.onClick.Invoke();}
  yield return Wait(()=>Find<IntroRegion>()?.Player!=null&&Find<IntroRegion>().PresentationReady,"normal New Game reaches real authored Intro");if(finished)yield break;
  var intro=Find<IntroRegion>();string id=GameState.Instance.GetLeader().uniqueId;string initial=File.ReadAllText(SporeSaveManager.GetSlotPath(1));
  Check("normal fresh namespace excludes legacy Saves",SporeSaveManager.GetSlotPath(1).Replace('\\','/').Contains("V1/Campaigns/slot_1.json"));
  Stage61RuntimeMetrics.Category("ordinary");Stage61RuntimeMetrics.Mark("normal Intro birth and quiet");while(!finished&&!intro.ControlReady)yield return null;if(finished)yield break;yield return new WaitForSeconds(3.2f);
  for(int i=1;i<intro.Definition.Route.Length&&!finished;i++)
  {
   if(i==8){yield return Walk(intro,new Point2(97.5,47.5));float until=Time.realtimeSinceStartup+20;while(!intro.Sequence.PlugCleared&&Time.realtimeSinceStartup<until){var screen=intro.RegionCamera.WorldToScreenPoint(intro.Render(intro.World(new Point2(100.5,47.5))));InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift,Key.D));yield return null;}InputSystem.QueueStateEvent(keyboard,new KeyboardState());Check("normal real input digs required obstruction",intro.Sequence.PlugCleared);}
   yield return Walk(intro,intro.Definition.Route[i]);
  }
  yield return Wait(Camp,"normal Intro commits ready Camp arrival");if(finished)yield break;
  Check("normal arrival commits same identity once",GameState.Instance.GetLeader().uniqueId==id&&!SporeSaveManager.ShouldLoadIntro(SporeSaveManager.LoadSlot(1))&&!CampArrivalContext.HasPending);
  Check("normal entry needs no development UI",Find<DevelopmentGameControls>()==null&&!V1RuntimeProfile.DevelopmentToolsEnabled);
  var player=Find<CampPlayableSpawner>().SpawnedPlayer;player.Heal(player.maxHealth);int hp=player.health;player.TakeDamage(1);Check("normal damage authority works outside Intro",player.health<hp);
  var fire=Find<CampFireRecovery>();fire.Interact(player);fire.eatAndRestButton.onClick.Invoke();Check("normal actual Fire recovery repairs missing health",player.health==player.maxHealth);fire.CloseMenu();
  for(int trip=0;trip<2&&!finished;trip++)
  {
   var portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);portal.goButton.onClick.Invoke();yield return Wait(Expedition,"normal departure enters shared expedition / "+trip);if(finished)yield break;
   Check("normal expedition has one terrain authority and no legacy map",MapGenerator.Instance==null&&RegionQueries.Current==Find<ExpeditionRegion>());
   ExpeditionDevelopmentControls.SetGodMode(true);Check("normal tools OFF cannot enable God Mode",!ExpeditionDevelopmentControls.GodMode);var r=Find<ExpeditionRegion>();r.Player.Heal(r.Player.maxHealth);
   Stage61RuntimeMetrics.Category("ordinary");Stage61RuntimeMetrics.Mark("normal expedition movement");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return new WaitForSeconds(1);InputSystem.QueueStateEvent(keyboard,new KeyboardState());
   Stage61RuntimeMetrics.Category("transition");Check("normal real return transaction / "+trip,RunReturnService.ReturnToCamp("CampScene",true,true,trip==0?RunReturnReason.Retreat:RunReturnReason.NormalExit,"normal tools-OFF proof"));
   yield return OpenReturn();if(finished)yield break;Check("normal loop retains campaign and Camp snapshot",GameState.Instance.GetLeader().uniqueId==id&&SporeSaveManager.LoadSlot(1).campTerrainState.regionSnapshot!=null);
  }
  Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"normal pause to MainMenu");Find<MainMenuController>().ContinueLastGame();yield return Wait(Camp,"normal Continue restores persistent Camp");if(finished)yield break;
  Check("normal Continue preserves identity with tools disabled",GameState.Instance.GetLeader().uniqueId==id&&!V1RuntimeProfile.DevelopmentToolsEnabled);
  Check("normal save changed from incomplete Intro",File.ReadAllText(SporeSaveManager.GetSlotPath(1))!=initial);Finish();
 }
 IEnumerator Walk(IntroRegion r,Point2 target)
 {var goal=r.World(target);float end=Time.realtimeSinceStartup+40;while(!finished&&r!=null&&!r.Sequence.Arriving&&Vector2.Distance(r.Player.transform.position,goal)>.21f&&Time.realtimeSinceStartup<end){var d=goal-(Vector2)r.Player.transform.position;var keys=new List<Key>();if(d.x>.1f)keys.Add(Key.D);if(d.x<-.1f)keys.Add(Key.A);if(d.y>.1f)keys.Add(Key.W);if(d.y<-.1f)keys.Add(Key.S);InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));yield return null;}InputSystem.QueueStateEvent(keyboard,new KeyboardState());Check("normal authored waypoint "+target,Time.realtimeSinceStartup<end);}
 IEnumerator OpenReturn(){yield return Wait(()=>Find<CampSceneController>()!=null,"normal return coordinator");float end=Time.realtimeSinceStartup+90;while(!finished&&!Camp()&&Time.realtimeSinceStartup<end){var c=Find<CampSceneController>();if(c.continueToSurvivorsButton!=null&&c.continueToSurvivorsButton.gameObject.activeInHierarchy)c.continueToSurvivorsButton.onClick.Invoke();else if(c.continueToCampButton!=null&&c.continueToCampButton.gameObject.activeInHierarchy)c.continueToCampButton.onClick.Invoke();yield return null;}Check("normal return UI opens Camp",Camp());}
 void Finish(){if(finished)return;finished=true;if(Time.realtimeSinceStartup>deadline)errors.Add("Normal proof timeout");Find<Stage61RuntimeMetrics>()?.Export();File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=errors.Count>0?"Normal proof failed":"",checks=checks.ToArray(),runtimeErrors=errors.ToArray()},true));Application.Quit(errors.Count==0&&checks.All(x=>x.StartsWith("PASS "))?0:1);}
 [Serializable]sealed class Result{public bool finished;public string error;public string[] checks,runtimeErrors;}
 void OnDestroy(){Application.logMessageReceived-=Observe;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);}
}
#endif

