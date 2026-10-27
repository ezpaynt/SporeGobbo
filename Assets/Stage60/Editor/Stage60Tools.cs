#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using SporeGobbo.Safety;
public static class Stage60Tools
{
 public const string ScenePath="Assets/Stage60/FutureCampStage60.unity";
 [MenuItem("Tools/Spore Gobbo/Stage 6.0/Open First Buddy Camp")]
 public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
 public static void Build()
 {
  string expected=Environment.GetEnvironmentVariable("SPORE_STAGE60_PROJECT");
  string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
  if(string.IsNullOrEmpty(expected)||!string.Equals(Path.GetFullPath(expected),project,StringComparison.OrdinalIgnoreCase)||Environment.GetEnvironmentVariable("SPORE_STAGE60_MODE")!="build"||PlayerSettings.companyName!="SporeGobboSafety"||!PlayerSettings.productName.StartsWith("Safety_")||!string.IsNullOrEmpty(PlayerSettings.cloudProjectId))throw new InvalidOperationException("Stage60 builds require the reviewed disposable-project runner");
  Persistence.NoLinks(project);string folder=Persistence.ExperimentPath("Build");Directory.CreateDirectory(folder);
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=Path.Combine(folder,"SporeGobboStage60.exe"),options=BuildOptions.Development});
  File.WriteAllText(Path.Combine(folder,"build-result.txt"),report.summary.result.ToString());
  if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage60 isolated build failed");
 }
}
#endif
