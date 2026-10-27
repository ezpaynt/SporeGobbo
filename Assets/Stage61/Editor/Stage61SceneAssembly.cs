#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using SporeGobbo.Geometry;
using SporeGobbo.CampCandidate;
using SporeGobbo.Safety;
public static partial class Stage61Tools
{
 public const string CandidatePath="Assets/Stage61/FutureCampCandidate.unity";
 static string Sha(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
 static void RequireAssemblyProcess()
 {
  string actual=Path.GetFullPath(Path.Combine(Application.dataPath,"..")),expected=Environment.GetEnvironmentVariable("SPORE_STAGE60_PROJECT");
  if(string.IsNullOrEmpty(expected)||actual!=Path.GetFullPath(expected)||Environment.GetEnvironmentVariable("SPORE_STAGE60_MODE")!="assemble"||PlayerSettings.companyName!="SporeGobboSafety"||!PlayerSettings.productName.StartsWith("Safety_")||!string.IsNullOrEmpty(PlayerSettings.cloudProjectId))throw new InvalidOperationException("Scene assembly requires the reviewed disposable project");
  Persistence.NoLinks(actual);Persistence.ExperimentPath("SceneExport");
 }
 public static void AssembleCandidate()
 {
  RequireAssemblyProcess();
  if(File.Exists(CandidatePath)||File.Exists(CandidatePath+".meta"))
  {
   string reviewed=Environment.GetEnvironmentVariable("SPORE_STAGE61_ASSEMBLY_INPUT")??"";
   string backup=Persistence.ExperimentPath("BeforeSceneAssembly");Directory.CreateDirectory(backup);
   foreach(string path in new[]{CandidatePath,CandidatePath+".meta"})
   {if(!File.Exists(path)||!reviewed.Contains("\""+path+"\": \""+Sha(path)+"\""))throw new InvalidOperationException("Existing candidate lacks exact reassembly review");File.Copy(path,Path.Combine(backup,Path.GetFileName(path)));}
  }
  const string legacy="Assets/Scenes/CampScene.unity";string before=Sha(legacy),meta=Sha(legacy+".meta"),settings=Sha("ProjectSettings/EditorBuildSettings.asset");
  var original=EditorSceneManager.OpenScene(legacy,OpenSceneMode.Single);var objects=original.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
  var chosen=new HashSet<Transform>();
  string[] names={"CampManager","CampFire","CampDeathSystems","CampPortal","SquadSelectSpot","BonesSpot","CampResourcePile","CampInteractionSystem","CampStartRoutineManager","PauseMenu","GobboVisualDatabase","ItemDatabase","EventSystem","OldBonesWall","BonesWallUnlocked","CampResidentialPresentation","CampFireMenuPanel","PortalPanel","CampMessageUI"};
  foreach(var t in objects)if(names.Contains(t.name)||t.GetComponent<Canvas>()!=null)chosen.Add(t);
  var roots=chosen.Where(t=>!Ancestors(t).Any(chosen.Contains)).ToArray();
  var group=new GameObject("Selected real Camp services and UI");foreach(var t in roots)t.SetParent(group.transform,true);
  var next=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(next);var transferred=UnityEngine.Object.Instantiate(group);transferred.name="Real Camp services and UI";SceneManager.MoveGameObjectToScene(transferred,next);
  var forbidden=new HashSet<string>{"GameState","GameStateSaveBridge","PlayerDeathRunStore","SporeInputReader","SporeUiCoordinator","HandcraftedCampTerrain","MapGenerator","CampCameraLockedLayer","CampLightOverlay","CameraFollow","Stage59Preview","Stage60Preview","Stage57Preview","Stage47Preview"};
  foreach(var component in transferred.GetComponentsInChildren<MonoBehaviour>(true))if(component!=null&&forbidden.Contains(component.GetType().Name))UnityEngine.Object.DestroyImmediate(component);
  var definition=SettlementCampLayout.Create();var grid=new LogicalGridTransform(new Point2(0,0),.6,192);var points=new Dictionary<string,Transform>();
  foreach(var marker in definition.Markers){var go=new GameObject("Marker / "+marker.Id);SceneManager.MoveGameObjectToScene(go,next);var p=grid.ToWorld(marker.Interaction);go.transform.position=new Vector3((float)p.X,(float)p.Y);points[marker.Id]=go.transform;}
  var regionObject=new GameObject("Camp logical region and derived presentation");SceneManager.MoveGameObjectToScene(regionObject,next);var region=regionObject.AddComponent<SettlementCampRegion>();region.terrainShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Terrain/Camp/Preview/Stage56Terrain.shader");region.spriteShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Stage59/Stage59Sprite.shader");
  T Find<T>()where T:Component=>transferred.GetComponentInChildren<T>(true)??throw new InvalidOperationException("Selected legacy hierarchy lacks "+typeof(T).Name);
  void Station(Component station,string marker){station.transform.position=points[marker].position;station.gameObject.AddComponent<CampStationMarker>().markerId=marker;foreach(var collider in station.GetComponentsInChildren<Collider2D>(true))collider.isTrigger=true;}
  var fire=Find<CampFireRecovery>();Station(fire,"fire");ArrangeFireMenu(fire);fire.gameObject.AddComponent<CampfireSnackMenuView>();regionObject.AddComponent<DevelopmentCampControls>();var squad=Find<CampSquadSelect>();squad.campTerrain=null;Station(squad,"squad");var bones=Find<CampOldBonesWall>();bones.campTerrain=null;
  if(bones.wallVisualRoot!=null){bones.wallVisualRoot.transform.SetParent(bones.transform,false);bones.wallVisualRoot.transform.localPosition=Vector3.zero;}
  Station(bones,"bones");ArrangeBonesMenu(bones);
  var portal=Find<CampRunPortal>();portal.developmentRunProfileOverride=null;Station(portal,"run.departure");foreach(var pile in transferred.GetComponentsInChildren<CampResourcePile>(true))Station(pile,"storage");
  var controller=Find<CampSceneController>();var spawner=Find<CampPlayableSpawner>();controller.campPlayableSpawner=spawner;controller.newGameIntroSpawn=points["arrival.first"];controller.mainCampArrivalSpawn=points["run.departure"];spawner.playerSpawnPoint=points["run.departure"];spawner.cameraFollow=null;spawner.assignMainCameraIfMissing=false;spawner.activeBuddySpots=new[]{points["fire"]};spawner.reserveBuddySpots=new[]{points["fire"]};
  var routine=Find<CampStartRoutineManager>();routine.fireGatherPoint=points["fire"];routine.defaultWanderAnchors=new[]{points["fire"],points["storage"],points["squad"]};controller.campStartRoutineManager=routine;
  var root=points[SettlementCampLayout.RootId].gameObject;root.layer=fire.gameObject.layer;root.AddComponent<CircleCollider2D>().isTrigger=true;root.AddComponent<CampRootEntrance>();
  foreach(var canvas in transferred.GetComponentsInChildren<Canvas>(true))canvas.worldCamera=null;
  // Reject references to excluded scene objects instead of silently nulling gameplay/UI wiring.
  var external=new List<string>();foreach(var component in transferred.GetComponentsInChildren<MonoBehaviour>(true))
  {
   if(component==null)throw new InvalidOperationException("Missing script in selected legacy gameplay hierarchy");
   var serialized=new SerializedObject(component);var property=serialized.GetIterator();while(property.Next(true))
   {
    if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;var value=property.objectReferenceValue;
    if(value is Component other&&!EditorUtility.IsPersistent(other)&&other.gameObject.scene!=next)external.Add(component.GetType().Name+"."+property.propertyPath+" -> "+other.name);
    if(value is GameObject otherObject&&!EditorUtility.IsPersistent(otherObject)&&otherObject.scene!=next)external.Add(component.GetType().Name+"."+property.propertyPath+" -> "+otherObject.name);
   }
  }
  if(external.Count>0)throw new InvalidOperationException("Unbound excluded-scene references:\n"+string.Join("\n",external));
  if(!EditorSceneManager.SaveScene(next,CandidatePath))throw new InvalidOperationException("Candidate scene save failed");
  if(Sha(legacy)!=before||Sha(legacy+".meta")!=meta||Sha("ProjectSettings/EditorBuildSettings.asset")!=settings)throw new InvalidOperationException("Protected legacy scene/build settings changed");
  string output=Persistence.ExperimentPath("SceneExport");Directory.CreateDirectory(output);File.Copy(CandidatePath,Path.Combine(output,"FutureCampCandidate.unity"));File.Copy(CandidatePath+".meta",Path.Combine(output,"FutureCampCandidate.unity.meta"));
  File.WriteAllText(Path.Combine(output,"assembly-result.json"),JsonUtility.ToJson(new AssemblyResult{scene=CandidatePath,sha256=Sha(CandidatePath),metaSha256=Sha(CandidatePath+".meta"),legacyUnchanged=true,productionBuildSettingsUnchanged=true,excludedSceneReferences=external.ToArray()},true));
  EditorSceneManager.CloseScene(original,true);
 }
 static IEnumerable<Transform> Ancestors(Transform t){for(var p=t.parent;p!=null;p=p.parent)yield return p;}
 static void Place(RectTransform rect,Transform parent,Vector2 position,Vector2 size)
 {rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;}
 static void ArrangeFireMenu(CampFireRecovery fire)
 {
  var root=fire.fireMenuPanel??throw new InvalidOperationException("Real Fire menu is missing");
  // Legacy leaf canvases retain editor positions after transfer. Keep the existing handlers/references,
  // but give this candidate one coherent screen-space panel and nonoverlapping controls.
  foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))if(canvas.gameObject!=root)
  {var ray=canvas.GetComponent<GraphicRaycaster>();if(ray!=null)UnityEngine.Object.DestroyImmediate(ray);var scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null)UnityEngine.Object.DestroyImmediate(scaler);UnityEngine.Object.DestroyImmediate(canvas);}
  var main=root.GetComponent<Canvas>();if(main==null)main=root.AddComponent<Canvas>();main.renderMode=RenderMode.ScreenSpaceOverlay;main.sortingOrder=500;
  var scale=root.GetComponent<CanvasScaler>();if(scale==null)scale=root.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);
  if(root.GetComponent<GraphicRaycaster>()==null)root.AddComponent<GraphicRaycaster>();
  var window=new GameObject("Fire menu layout",typeof(RectTransform),typeof(Image));Place(window.GetComponent<RectTransform>(),root.transform,Vector2.zero,new Vector2(760,610));window.GetComponent<Image>().color=new Color(.045f,.08f,.06f,.98f);
  void Label(TMP_Text text,Vector2 position,Vector2 size,int font){if(text==null)return;Place(text.rectTransform,window.transform,position,size);text.enableAutoSizing=false;text.fontSize=font;text.alignment=TextAlignmentOptions.TopLeft;text.raycastTarget=false;}
  void Action(Button button,Vector2 position){if(button==null)return;Place(button.GetComponent<RectTransform>(),window.transform,position,new Vector2(290,52));foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)){text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;text.fontSize=24;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;}}
  Label(fire.titleText,new Vector2(0,240),new Vector2(660,55),32);Label(fire.bodyText,new Vector2(0,30),new Vector2(660,340),24);
  Action(fire.eatAndRestButton,new Vector2(-160,-210));Action(fire.closeButton,new Vector2(160,-210));Action(fire.upgradesButton,new Vector2(0,-275));
 }
 static void ArrangeBonesMenu(CampOldBonesWall bones)
 {
  var root=bones.panel;if(root==null)throw new InvalidOperationException("Real Bones panel is missing");
  foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))if(canvas.gameObject!=root)
  {var ray=canvas.GetComponent<GraphicRaycaster>();if(ray!=null)UnityEngine.Object.DestroyImmediate(ray);var scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null)UnityEngine.Object.DestroyImmediate(scaler);UnityEngine.Object.DestroyImmediate(canvas);}
  var main=root.GetComponent<Canvas>();if(main==null)main=root.AddComponent<Canvas>();main.renderMode=RenderMode.ScreenSpaceOverlay;main.sortingOrder=500;
  var scale=root.GetComponent<CanvasScaler>();if(scale==null)scale=root.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);
  if(root.GetComponent<GraphicRaycaster>()==null)root.AddComponent<GraphicRaycaster>();var shade=root.GetComponent<Image>();if(shade!=null)shade.color=new Color(0,0,0,.75f);
  var window=new GameObject("Memorial reading layout",typeof(RectTransform),typeof(Image));Place(window.GetComponent<RectTransform>(),root.transform,Vector2.zero,new Vector2(1180,810));window.GetComponent<Image>().color=new Color(.045f,.08f,.06f,.98f);
  Place(bones.titleText.rectTransform,window.transform,new Vector2(0,335),new Vector2(1060,55));bones.titleText.enableAutoSizing=false;bones.titleText.fontSize=34;bones.titleText.alignment=TextAlignmentOptions.Center;
  void Column(TMP_Text text,float x)
  {
   var viewport=new GameObject("Scrollable memorial records",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));Place(viewport.GetComponent<RectTransform>(),window.transform,new Vector2(x,10),new Vector2(520,565));viewport.GetComponent<Image>().color=new Color(0,0,0,.18f);
   var rect=text.rectTransform;rect.SetParent(viewport.transform,false);rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(-10,0);rect.sizeDelta=new Vector2(-55,0);rect.localScale=Vector3.one;
   text.enableAutoSizing=false;text.fontSize=24;text.alignment=TextAlignmentOptions.TopLeft;text.raycastTarget=false;var fitter=text.GetComponent<ContentSizeFitter>();if(fitter==null)fitter=text.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
   var bar=new GameObject("Memorial scroll",typeof(RectTransform),typeof(Image),typeof(Scrollbar));Place(bar.GetComponent<RectTransform>(),viewport.transform,new Vector2(245,0),new Vector2(16,550));bar.GetComponent<Image>().color=new Color(.1f,.17f,.13f);
   var handle=new GameObject("Handle",typeof(RectTransform),typeof(Image));var handleRect=handle.GetComponent<RectTransform>();handleRect.SetParent(bar.transform,false);handleRect.anchorMin=Vector2.zero;handleRect.anchorMax=Vector2.one;handleRect.offsetMin=handleRect.offsetMax=Vector2.zero;handle.GetComponent<Image>().color=new Color(.55f,.65f,.52f);
   var scrollbar=bar.GetComponent<Scrollbar>();scrollbar.handleRect=handleRect;scrollbar.targetGraphic=handle.GetComponent<Image>();scrollbar.direction=Scrollbar.Direction.BottomToTop;
   var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=rect;scroll.horizontal=false;scroll.vertical=true;scroll.inertia=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.verticalScrollbar=scrollbar;
  }
  Column(bones.deadLeadersText,-280);Column(bones.deadBuddiesText,280);Place(bones.continueButton.GetComponent<RectTransform>(),window.transform,new Vector2(0,-335),new Vector2(300,55));
  foreach(var text in bones.continueButton.GetComponentsInChildren<TMP_Text>(true)){text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;text.fontSize=24;text.alignment=TextAlignmentOptions.Center;}
 }
 [Serializable]class AssemblyResult{public string scene,sha256,metaSha256;public bool legacyUnchanged,productionBuildSettingsUnchanged;public string[] excludedSceneReferences;}
}
#endif
