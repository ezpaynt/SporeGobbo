#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.IO;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.InputSystem;using SporeGobbo.Safety;
/// <summary>Optional protected tooling/evidence only. Normal routing and campaign behavior need no bootstrap.</summary>
public sealed class V1EvidenceSession:MonoBehaviour
{
 FileStream lease;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Begin()
 {
  Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
  try
  {
   if(Persistence.ProtectedPlayerIdentity(Application.companyName,Application.productName))_ = Persistence.ExperimentRoot;
   if(!Persistence.Isolated||!V1RuntimeProfile.DevelopmentToolsEnabled&&!V1RuntimeProfile.EvidenceEnabled)return;
   var go=new GameObject("V1 optional diagnostics and test session");DontDestroyOnLoad(go);go.AddComponent<V1EvidenceSession>();
  }
  catch(Exception e){Debug.LogWarning("Protected V1 launch refused: "+e.Message);Application.Quit(2);}
 }
 void Awake()
 {
  try{lease=new FileStream(Persistence.ExperimentPath(".spore-player-storage-lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(Exception e){Debug.LogWarning("Protected V1 launch refused: "+e.Message);Application.Quit(2);enabled=false;return;}
  if(V1RuntimeProfile.EvidenceEnabled){Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;foreach(var d in InputSystem.devices)if(d.native)InputSystem.DisableDevice(d);}
  if(V1RuntimeProfile.DevelopmentToolsEnabled)gameObject.AddComponent<DevelopmentGameControls>();
  if(V1RuntimeProfile.EvidenceEnabled)gameObject.AddComponent<Stage61RuntimeMetrics>();
  SceneManager.sceneLoaded+=Loaded;
 }
 void Loaded(Scene scene,LoadSceneMode mode)
 {
  if(scene.name!="MainMenu"||!V1RuntimeProfile.EvidenceEnabled)return;SceneManager.sceneLoaded-=Loaded;
  string scenario=Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE");
  if(scenario=="lifecycle")gameObject.AddComponent<Stage62CIntroEvidence>();
  else if(scenario.StartsWith("continue-",StringComparison.Ordinal))gameObject.AddComponent<Stage61ContinueEvidence>();
  else if(scenario=="normal")gameObject.AddComponent<V1NormalEvidence>();
  else if(scenario=="performance"||scenario=="startup")gameObject.AddComponent<V1PerformanceEvidence>();
  else if(scenario=="occlusion")gameObject.AddComponent<V1VisualEvidence>();
  else if(scenario=="commands"){if(!Persistence.Isolated||!V1RuntimeProfile.DevelopmentToolsEnabled||!V1RuntimeProfile.EvidenceEnabled)throw new InvalidOperationException("Command proof requires owned development evidence");gameObject.AddComponent<Stage64BCommandEvidence>();}
  else if(scenario=="layout"){if(!Persistence.Isolated||!V1RuntimeProfile.DevelopmentToolsEnabled||!V1RuntimeProfile.EvidenceEnabled)throw new InvalidOperationException("Layout proof requires owned development evidence");gameObject.AddComponent<Stage64BLayoutEvidence>();}
  else if(scenario=="pit"){if(!Persistence.Isolated||!V1RuntimeProfile.DevelopmentStorage||!V1RuntimeProfile.DevelopmentToolsEnabled||!V1RuntimeProfile.EvidenceEnabled)throw new InvalidOperationException("Pit proof requires owned development evidence");gameObject.AddComponent<Stage64BPitEvidence>();}
  else if(scenario=="tools")gameObject.AddComponent<V1ToolsEvidence>();
  else throw new InvalidOperationException("Unreviewed V1 evidence scenario");
 }
 void OnDestroy(){SceneManager.sceneLoaded-=Loaded;lease?.Dispose();}
}
#endif
