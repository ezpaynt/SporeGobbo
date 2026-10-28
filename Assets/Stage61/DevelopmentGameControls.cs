#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using SporeGobbo.Safety;
/// <summary>One discoverable hub; delegates commands to existing region/game services.</summary>
[DefaultExecutionOrder(-9000)]
public sealed class DevelopmentGameControls : MonoBehaviour
{
 public static DevelopmentGameControls Instance { get; private set; }
 public static bool HasHub => Instance != null && V1RuntimeProfile.DevelopmentToolsEnabled;
 bool visible, modal; int page; string message=""; Vector2 scroll; SporeUiCoordinator ui;
 Rect Panel => new Rect(8,8,420,Mathf.Min(740,Screen.height-70));
 void Awake(){if(!V1RuntimeProfile.DevelopmentToolsEnabled){enabled=false;return;}if(Instance!=null&&Instance!=this){Destroy(this);return;}Instance=this;}
 void OnEnable(){SceneManager.sceneLoaded+=SceneLoaded;}
 void OnDisable(){SceneManager.sceneLoaded-=SceneLoaded;ReleaseModal();}
 void SceneLoaded(Scene scene,LoadSceneMode mode){if(mode==LoadSceneMode.Single){modal=false;page=0;scroll=Vector2.zero;}}
 public static bool OpenContext(){if(!HasHub)return false;Instance.visible=true;Instance.page=0;return true;}
 void Update()
 {
  if(!V1RuntimeProfile.DevelopmentToolsEnabled){visible=false;ReleaseModal();return;}
  if(Keyboard.current!=null&&Keyboard.current.f9Key.wasPressedThisFrame)visible=!visible;
  ui=SporeUiCoordinator.Instance;
  bool hover=visible&&Mouse.current!=null&&Panel.Contains(new Vector2(Mouse.current.position.ReadValue().x,Screen.height-Mouse.current.position.ReadValue().y));
  if(hover!=modal&&ui!=null){modal=hover;if(hover)ui.PushModal(this,null,false);else ui.PopModal(this);}
 }
 void ReleaseModal(){if(modal&&ui!=null)ui.PopModal(this);modal=false;}
 void Command(Action action){try{action();message="Done through existing game services.";}catch(Exception error){message=error.Message;Debug.LogWarning("[Development hub] "+message);}}
 void OnGUI()
 {
  if(!V1RuntimeProfile.DevelopmentToolsEnabled)return;
  var intro=UnityEngine.Object.FindAnyObjectByType<IntroRegion>();if(intro!=null&&!intro.ControlReady)return;
  GUI.Label(new Rect(12,Screen.height-55,Screen.width-24,25),"V1 DEVELOPMENT — F9 tools / Pause: Data and Journal");if(!visible)return;
  GUILayout.BeginArea(Panel,GUI.skin.box);GUILayout.Label("Spore Gobbo development tools");
  GUILayout.Label(V1RuntimeProfile.DevelopmentStorage?"Owned synthetic development campaigns":"Fresh V1 namespace / development tools enabled");
  GUILayout.BeginHorizontal();
  if(GUILayout.Button("Current region")){page=0;scroll=Vector2.zero;}
  if(GUILayout.Button("Campaign / replay")){page=1;scroll=Vector2.zero;}
  if(GUILayout.Button("Data / Journal"))Command(()=>{UnityEngine.Object.FindAnyObjectByType<PauseMenuController>()?.OpenPause();visible=false;ReleaseModal();});
  GUILayout.EndHorizontal();scroll=GUILayout.BeginScrollView(scroll);
  if(page==0)
  {
   var camp=UnityEngine.Object.FindAnyObjectByType<DevelopmentCampControls>();var expedition=UnityEngine.Object.FindAnyObjectByType<ExpeditionDevelopmentControls>();
   if(intro!=null)intro.Sequence.DrawDevelopmentControls();else if(camp!=null)camp.DrawDevelopmentControls();else if(expedition!=null)expedition.DrawDevelopmentControls();else GUILayout.Label("No region loaded. Start or Continue from the real Main Menu.");
  }
  else DrawCampaignControls();
  GUILayout.Label(message);GUILayout.EndScrollView();GUILayout.EndArea();
 }
 void DrawCampaignControls()
 {
  GUILayout.Label(Persistence.ExperimentRoot);bool menu=SceneManager.GetActiveScene().name=="MainMenu";
  if(menu)
  {
   for(int slot=1;slot<=3;slot++){int owned=slot;var status=SporeSaveManager.InspectSlot(slot);GUILayout.Label("Campaign "+(char)('A'+slot-1)+": "+status);
    if(status==SporeSaveManager.SlotStatus.Missing&&GUILayout.Button("Import "+(slot==1?"fresh A":slot==2?"established ten-bed B":"recovery C")))Command(()=>DevelopmentCampaignFixtures.Import(owned));
    if(status==SporeSaveManager.SlotStatus.RecoveryAvailable&&GUILayout.Button("Recover owned campaign "+slot))Command(()=>SporeSaveManager.RecoverDevelopmentSlot(owned));
   }
   if(GUILayout.Button("Replay Intro: archive/reset owned Fresh A"))Command(()=>{SporeSaveManager.DeleteSlot(1);DevelopmentCampaignFixtures.Import(1);});
   GUILayout.Label("Use real Continue after reset; previous owned A is archived.");
  }
  var controller=UnityEngine.Object.FindAnyObjectByType<CampSceneController>();if(controller!=null&&!controller.WorldInputReady&&GUILayout.Button("Retry ready Camp arrival"))Command(controller.RetryDevelopmentArrival);
  if(!menu&&GUILayout.Button("Recover last committed campaign to Main Menu"))Command(()=>{var lab=UnityEngine.Object.FindAnyObjectByType<CampFightPitLab>();int slot=SporeSaveManager.GetCurrentSlot();if(!(lab!=null?lab.RecoverCommittedCampaign(slot):SporeSaveManager.LoadSlotIntoGameState(slot)))throw new InvalidOperationException("No validated campaign to recover");SporePauseService.ResetAll();SceneManager.LoadScene("MainMenu");});
 }
 void OnDestroy(){ReleaseModal();if(Instance==this)Instance=null;}
}
#endif
