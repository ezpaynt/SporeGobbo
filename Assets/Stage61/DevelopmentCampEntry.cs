#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SporeGobbo.Safety;
/// <summary>Development entry only. Seeds via existing campaign services; never owns campaign or Camp lifecycle.</summary>
public sealed class DevelopmentCampEntry:MonoBehaviour
{
 System.IO.FileStream storageLease;
 public static System.IO.FileStream AcquireOwnedStorageLease()=>new System.IO.FileStream(Persistence.ExperimentPath(".spore-player-storage-lock"),System.IO.FileMode.OpenOrCreate,System.IO.FileAccess.ReadWrite,System.IO.FileShare.None);
 void Awake()
 {
  try{storageLease=AcquireOwnedStorageLease();}
  catch(Exception error){Debug.LogError("Protected development storage is unavailable or already open: "+error.Message);Application.Quit(2);}
 }
 void OnDestroy(){storageLease?.Dispose();storageLease=null;}
 IEnumerator Start()
 {
  if(storageLease==null)yield break;
  Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
  DontDestroyOnLoad(gameObject);
  GameSceneCatalog.EnableDevelopmentCandidate();GameSceneCatalog.EnableExpedition();GameSceneCatalog.EnableAuthoredIntro();
  if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE")))
  {
      UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
      foreach(var device in UnityEngine.InputSystem.InputSystem.devices.ToArray())
          if(device.native)UnityEngine.InputSystem.InputSystem.DisableDevice(device);
  }
  gameObject.AddComponent<DevelopmentGameControls>();
  gameObject.AddComponent<Stage61RuntimeMetrics>();
  // The actual Main Menu supplies the single persistent input reader and UI bootstrap.
  yield return SceneManager.LoadSceneAsync("MainMenu");
  yield return null;
  try
  {
   string storage=Persistence.ExperimentPath("Stage61");GameSceneCatalog.EnableDevelopmentCandidate();
   Debug.Log("STAGE61 DEVELOPMENT CAMPAIGNS ONLY / owned storage: "+Persistence.ExperimentRoot);
   string mode=Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE");
   if(mode!=null&&mode.StartsWith("continue-",StringComparison.Ordinal))
   {var continuation=new GameObject("Stage61 cross-process real campaign proof");DontDestroyOnLoad(continuation);continuation.AddComponent<Stage61ContinueEvidence>();yield break;}
   if(mode=="lifecycle")
   {var lifecycle=new GameObject("Stage61 actual lifecycle evidence");DontDestroyOnLoad(lifecycle);lifecycle.AddComponent<Stage62CIntroEvidence>();yield break;}
   if(mode!="camp")yield break; // Protected manual entry stays in the actual Main Menu.
   // Logical campaign B. This provisional C fixture uses the real current serializer; Gate D versions it.
   var data=SporeSaveManager.CreateNewGame(2,"Development Camp B","SampleScene",false);if(data==null)throw new InvalidOperationException("Synthetic development slot B was unexpectedly occupied");
   var state=GameState.Instance;state.leader.health=Math.Max(1,state.leader.maxHealth-2);
   for(int i=1;i<=5;i++)state.AddGobbo(new GobboUnitSaveData{uniqueId="development-b-buddy-"+i,displayName="Burrow Buddy "+i,gobboType=i==2?BuddyType.Tank:i==4?BuddyType.Fast:BuddyType.Baby,ageStage=i==2||i==4?GobboAgeStage.Young:GobboAgeStage.Baby,health=8,maxHealth=10,moveSpeed=3.5f,visualSetId=""},true);
   CampResourceService.Add(state,CampResourceType.Mushrooms,12);CampResourceService.Add(state,CampResourceType.Shinies,8);StoryEventService.Complete(state,StoryEventIds.ASporeHatchesCampDiscovered);SporeSaveManager.SaveCurrentSlotFromGameState();
   var proof=new GameObject("Stage61 real Camp service evidence");DontDestroyOnLoad(proof);proof.AddComponent<Stage61ServiceEvidence>();CampArrivalContext.SetPending(CampArrivalMode.LoadedSave);SceneManager.LoadScene(GameSceneCatalog.Camp);
  }
  catch(Exception e){Debug.LogError("Protected development entry failed: "+e);Application.Quit(2);}
 }
}

#endif
