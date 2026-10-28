#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using SporeGobbo.Safety;using SporeGobbo.Input;
/// <summary>Real rendered squad/input proof. Fixture import and one leader move are setup, not natural recruitment evidence.</summary>
public sealed class Stage64BCommandEvidence:MonoBehaviour
{
 readonly List<string> checks=new List<string>(),errors=new List<string>();string directory;float deadline;bool finished;Keyboard keyboard;Mouse mouse;Gamepad pad;
 static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
 static bool Camp()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true;
 void Awake(){directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);deadline=Time.realtimeSinceStartup+240;Application.logMessageReceived+=Observe;}
 void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
 void Update(){if(!finished&&(errors.Count>0||Time.realtimeSinceStartup>deadline))Finish();}
 void Check(string name,bool value){checks.Add((value?"PASS ":"FAIL ")+name);File.WriteAllLines(Path.Combine(directory,"checks-in-progress.txt"),checks);if(!value)Finish();}
 IEnumerator Wait(Func<bool> condition,string name,float seconds=60){float end=Time.realtimeSinceStartup+seconds;while(!finished&&!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(name,!finished&&condition());}
 IEnumerator Frames(int count){for(int i=0;i<count&&!finished;i++)yield return null;}
 IEnumerator Release(){InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(pad,new GamepadState());yield return Frames(5);}
 IEnumerator Wheel(BuddyCommand command,bool controller=false)
 {
  yield return Release();Vector2 direction=command==BuddyCommand.Follow?Vector2.up:command==BuddyCommand.Stay?Vector2.left:command==BuddyCommand.Aggressive?Vector2.down:Vector2.right;
  if(controller)InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.LeftShoulder));else InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.C));yield return Frames(4);
  Check("actual wheel opens / "+command+" / "+controller,BuddyCommandWheelController.Active?.IsOpen==true&&SporeInputReader.Instance.Context==SporeInputContext.Wheel);if(finished)yield break;
  if(controller){var state=new GamepadState{rightStick=direction}.WithButton(GamepadButton.LeftShoulder);InputSystem.QueueStateEvent(pad,state);yield return Frames(3);InputSystem.QueueStateEvent(pad,state.WithButton(GamepadButton.South));}
  else{var point=new Vector2(Screen.width*.5f,Screen.height*.5f)+direction*250;InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Frames(3);InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});}
  yield return Frames(4);yield return Release();Check("actual wheel confirms / "+command,Find<ExpeditionRegion>().Player.IsBuddyCommandActive(command)&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay&&BuddyCommandWheelController.Active==null);
 }
 IEnumerator Start()
 {
  yield return Frames(2);Check("owned development command proof",Persistence.Isolated&&V1RuntimeProfile.DevelopmentToolsEnabled&&V1RuntimeProfile.EvidenceEnabled&&SceneManager.GetActiveScene().name==GameSceneCatalog.MainMenu);if(finished)yield break;
  keyboard=InputSystem.AddDevice<Keyboard>("64B command keyboard");mouse=InputSystem.AddDevice<Mouse>("64B command mouse");pad=InputSystem.AddDevice<Gamepad>("64B command gamepad");keyboard.MakeCurrent();mouse.MakeCurrent();
  DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);yield return Wait(Camp,"real Menu loads owned established Camp fixture");if(finished)yield break;
  var state=GameState.Instance;foreach(var data in state.ownedGobbos)if(state.activeSquadIds.Count<state.maxActiveSquad&&!state.activeSquadIds.Contains(data.uniqueId))BuddyRosterService.MoveBuddyToActiveSquad(state,data.uniqueId);
  Check("existing capacity remains five Buddies plus leader",state.maxActiveSquad==5&&state.activeSquadIds.Count==5);if(finished)yield break;
  var workers=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);var flags=workers.Select(u=>(u.GetComponent<BuddyFollow>()?.enabled??false,u.GetComponent<BuddyCombat>()?.enabled??false,u.GetComponent<BuddyBrain>()?.enabled??false)).ToArray();
  var campPlayer=Find<CampPlayableSpawner>().SpawnedPlayer;campPlayer.IssueBuddyCommand(BuddyCommand.Stay);campPlayer.IssueBuddyCommand(BuddyCommand.Passive);
  Check("Camp worker component ownership unchanged by expedition modes",workers.Select((u,i)=>(u.GetComponent<BuddyFollow>()?.enabled??false,u.GetComponent<BuddyCombat>()?.enabled??false,u.GetComponent<BuddyBrain>()?.enabled??false)==flags[i]).All(x=>x));
  var portal=Find<CampRunPortal>();portal.Interact(campPlayer);yield return null;portal.goButton.onClick.Invoke();
  yield return Wait(()=>Find<ExpeditionRegion>()?.Player!=null&&Find<ExpeditionRegion>().PresentationReady&&UnityEngine.Object.FindObjectsByType<BuddyBrain>(FindObjectsSortMode.None).Count(b=>b.SquadScoped)==5,"real portal spawns five canonical registered expedition Buddies");if(finished)yield break;
  var region=Find<ExpeditionRegion>();var leader=region.Player;var squad=UnityEngine.Object.FindObjectsByType<BuddyBrain>(FindObjectsSortMode.None).Where(b=>b.SquadScoped).ToArray();yield return Frames(30);
  Check("all actual spawned recipients have canonical campaign and terrain ownership",squad.All(b=>leader.CanControlBuddy(b)&&state.ownedGobbos.Contains(b.GetComponent<BuddyUnit>().unitData)&&object.ReferenceEquals(b.GetComponent<ActorTerrainBinding>().Terrain,region)));
  yield return Wheel(BuddyCommand.Passive);yield return Wheel(BuddyCommand.Stay);if(finished)yield break;var anchors=squad.Select(b=>b.StayAnchor).ToArray();
  Vector2 direction=Vector2.zero;var body=leader.GetComponent<Rigidbody2D>();var size=TileMover.GetMapClearanceExtents(body,.28f);foreach(var candidate in new[]{Vector2.right,Vector2.up,Vector2.left,Vector2.down})if(TileMover.CanTraverseBox(region,body.position,body.position+candidate*1.8f,size)){direction=candidate;break;}
  Check("authored arrival provides a clear ordinary movement direction",direction!=Vector2.zero);if(finished)yield break;Vector2 before=leader.transform.position;Key movement=direction==Vector2.right?Key.D:direction==Vector2.left?Key.A:direction==Vector2.up?Key.W:Key.S;
  InputSystem.QueueStateEvent(keyboard,new KeyboardState(movement));float end=Time.realtimeSinceStartup+2;while(!finished&&Time.realtimeSinceStartup<end&&Vector2.Distance(before,leader.transform.position)<1.4f)yield return null;yield return Release();yield return Frames(40);
  Check("real keyboard moves leader while Stay remains anchored",Vector2.Distance(before,leader.transform.position)>.5f&&squad.Select((b,i)=>b.StayAnchor==anchors[i]&&Vector2.Distance(b.transform.position,b.StayAnchor)<=b.StayRadius+.35f).All(x=>x));
  Check("Stay Passive has no combat target",squad.All(b=>!b.CombatPermitted&&!b.GetComponent<BuddyCombat>().WantsControl()));
  yield return Wheel(BuddyCommand.Aggressive,true);if(finished)yield break;Check("Stay Aggressive keeps previous anchors",squad.Select((b,i)=>b.CombatPermitted&&!b.Following&&b.StayAnchor==anchors[i]).All(x=>x));
  yield return Wheel(BuddyCommand.Follow);if(finished)yield break;Check("Follow Aggressive permits normal behavior",squad.All(b=>b.Following&&b.CombatPermitted));yield return Wheel(BuddyCommand.Passive,true);if(finished)yield break;
  float distanceBefore=squad.Average(b=>Vector2.Distance(b.transform.position,leader.transform.position));yield return new WaitForSeconds(3);Check("Follow Passive uses real following without attack permission",squad.All(b=>b.Following&&!b.CombatPermitted)&&squad.Average(b=>Vector2.Distance(b.transform.position,leader.transform.position))<=distanceBefore+.2f);
  yield return Release();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.C));yield return Frames(3);InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start));yield return Frames(4);Check("pause cancels actual wheel and blocks world input",BuddyCommandWheelController.Active==null&&Time.timeScale==0&&SporeInputReader.Instance.Context==SporeInputContext.Pause);if(finished)yield break;yield return Release();InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start));yield return Frames(4);yield return Release();Check("actual gamepad resumes Gameplay after wheel cancellation",Time.timeScale>0&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay);if(finished)yield break;
  yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,"five-buddies-command-proof.png"));yield return Frames(3);
  string[] identities=state.activeSquadIds.ToArray();Check("real retreat uses existing return service",RunReturnService.ReturnToCamp(GameSceneCatalog.Camp,true,true,RunReturnReason.Retreat,"64B command proof"));yield return Wait(()=>Find<CampSceneController>()!=null,"return coordinator available");if(finished)yield break;
  end=Time.realtimeSinceStartup+60;while(!finished&&!Camp()&&Time.realtimeSinceStartup<end){var c=Find<CampSceneController>();if(c.continueToSurvivorsButton!=null&&c.continueToSurvivorsButton.gameObject.activeInHierarchy)c.continueToSurvivorsButton.onClick.Invoke();else if(c.continueToCampButton!=null&&c.continueToCampButton.gameObject.activeInHierarchy)c.continueToCampButton.onClick.Invoke();yield return null;}
  Check("return releases real Camp with coherent squad",Camp()&&identities.All(id=>state.ownedGobbos.Any(u=>u.uniqueId==id))&&UnityEngine.Object.FindObjectsByType<BuddyBrain>(FindObjectsSortMode.None).All(b=>!b.SquadScoped));
  Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"real Menu teardown after command proof");yield return Frames(4);Check("command proof releases actor terrain resources",DiggableTerrainService.Active==null&&Stage59ActorView.OwnedMeshes==0);Finish();
 }
 void Finish(){if(finished)return;finished=true;if(Time.realtimeSinceStartup>deadline)errors.Add("Command proof watchdog exceeded240seconds");Find<Stage61RuntimeMetrics>()?.Export();File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=errors.Count>0?"Command proof failed":"",checks=checks.ToArray(),runtimeErrors=errors.ToArray(),method="Established synthetic campaign fixture through actual Menu/Camp/portal/run/return. Real keyboard mouse and gamepad wheel inputs, five actual Buddy actors, ordinary leader movement, Stay/Follow/Passive/Aggressive and pause. Fixture import and roster service setup are explicit adapters, not natural recruitment or new-combat balance proof."},true));Application.Quit(errors.Count==0&&checks.Count>0&&checks.All(c=>c.StartsWith("PASS "))?0:1);}
 [Serializable]sealed class Result{public bool finished;public string error,method;public string[] checks,runtimeErrors;}
 void OnDestroy(){Application.logMessageReceived-=Observe;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);if(pad!=null)InputSystem.RemoveDevice(pad);}
}
#endif
