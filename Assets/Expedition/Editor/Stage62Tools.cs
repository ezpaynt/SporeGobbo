#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;using UnityEngine;using UnityEngine.SceneManagement;using UnityEditor;using UnityEditor.SceneManagement;using SporeGobbo.Safety;
public static class Stage62Tools
{
 const string Target="Assets/Expedition/FirstExpedition.unity";
 static string Hash(string p){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 public static void AssembleExpedition()
 {
  string actual=Path.GetFullPath(Path.Combine(Application.dataPath,"..")),expected=Environment.GetEnvironmentVariable("SPORE_STAGE60_PROJECT");if(string.IsNullOrEmpty(expected)||actual!=Path.GetFullPath(expected)||Environment.GetEnvironmentVariable("SPORE_STAGE60_MODE")!="assemble"||PlayerSettings.companyName!="SporeGobboSafety"||!PlayerSettings.productName.StartsWith("Safety_")||!string.IsNullOrEmpty(PlayerSettings.cloudProjectId))throw new InvalidOperationException("Use controlled disposable assembly");
  Persistence.NoLinks(actual);string backup=Persistence.ExperimentPath("BeforeSceneAssembly");Directory.CreateDirectory(backup);string reviewed=Environment.GetEnvironmentVariable("SPORE_STAGE61_ASSEMBLY_INPUT")??"";foreach(string p in new[]{Target,Target+".meta"}){if(!reviewed.Contains("\""+p+"\": \""+Hash(p)+"\""))throw new InvalidOperationException("Unreviewed scene input");File.Copy(p,Path.Combine(backup,Path.GetFileName(p)));}
  string source="Assets/Scenes/SampleScene.unity",before=Hash(source),camp=Hash("Assets/Stage61/FutureCampCandidate.unity"),settings=Hash("ProjectSettings/EditorBuildSettings.asset");
  var sample=EditorSceneManager.OpenScene(source,OpenSceneMode.Single);var all=sample.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();var selected=new HashSet<Transform>();
  foreach(var t in all)if(t.GetComponent<Canvas>()!=null||t.GetComponent<PauseMenuController>()!=null||t.GetComponent<BuddyChoiceScreen>()!=null||t.GetComponent<UnityEngine.EventSystems.EventSystem>()!=null||t.name=="GobboVisualDatabase"||t.name=="ItemDatabase")selected.Add(t);
  bool Under(Transform t){while(t.parent!=null){t=t.parent;if(selected.Contains(t))return true;}return false;}
  var roots=selected.Where(t=>!Under(t)).ToArray();var group=new GameObject("Existing run UI and presentation services");foreach(var t in roots)t.SetParent(group.transform,true);
  var target=EditorSceneManager.OpenScene(Target,OpenSceneMode.Additive);SceneManager.SetActiveScene(target);var clone=UnityEngine.Object.Instantiate(group);SceneManager.MoveGameObjectToScene(clone,target);
  foreach(var canvas in clone.GetComponentsInChildren<Canvas>(true))canvas.worldCamera=null;
  var excluded=new List<string>();foreach(var c in clone.GetComponentsInChildren<MonoBehaviour>(true)){
   if(c==null)throw new InvalidOperationException("Missing UI script");var so=new SerializedObject(c);var property=so.GetIterator();while(property.Next(true)){if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;var v=property.objectReferenceValue;GameObject go=v is Component comp?comp.gameObject:v as GameObject;if(go!=null&&!EditorUtility.IsPersistent(go)&&go.scene!=target)excluded.Add(c.GetType().Name+"."+property.propertyPath+" -> "+go.name);}}
  if(excluded.Count>0)throw new InvalidOperationException("Unbound expedition UI references: "+string.Join(";",excluded));
  if(clone.GetComponentInChildren<BuddyChoiceScreen>(true)==null||clone.GetComponentInChildren<PauseMenuController>(true)==null)throw new InvalidOperationException("Actual run UI selection incomplete");
  EditorSceneManager.SaveScene(target,Target);string output=Persistence.ExperimentPath("SceneExport");Directory.CreateDirectory(output);File.Copy(Target,Path.Combine(output,Path.GetFileName(Target)));File.Copy(Target+".meta",Path.Combine(output,Path.GetFileName(Target)+".meta"));
  File.WriteAllText(Path.Combine(output,"assembly-result.json"),"{\"scene\":\""+Target+"\",\"sha256\":\""+Hash(Target)+"\",\"metaSha256\":\""+Hash(Target+".meta")+"\",\"legacyUnchanged\":"+(Hash(source)==before&&Hash("Assets/Stage61/FutureCampCandidate.unity")==camp?"true":"false")+",\"productionBuildSettingsUnchanged\":"+(Hash("ProjectSettings/EditorBuildSettings.asset")==settings?"true":"false")+",\"excludedSceneReferences\":[]}");
  EditorSceneManager.CloseScene(sample,true);
 }
}
#endif
