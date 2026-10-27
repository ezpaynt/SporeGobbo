#if UNITY_EDITOR
using System;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.Build.Reporting;using SporeGobbo.Safety;
/// <summary>Fixed protected V1 build; normal MainMenu-first scene policy, no preview entry.</summary>
public static class Stage63Build
{
 public static readonly string[] Scenes={"Assets/Scenes/MainMenu.unity","Assets/Intro/FirstGobboIntro.unity",V1RuntimeProfile.NormalCampSceneAsset,"Assets/Expedition/FirstExpedition.unity"};
 public static void Build()
 {
  string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
  if(project!=Environment.GetEnvironmentVariable("SPORE_STAGE60_PROJECT")||Environment.GetEnvironmentVariable("SPORE_STAGE60_MODE")!="build"||PlayerSettings.companyName!="SporeGobboSafety"||!PlayerSettings.productName.StartsWith("Safety_")||!string.IsNullOrEmpty(PlayerSettings.cloudProjectId))throw new InvalidOperationException("V1 build requires reviewed isolated identity");
  foreach(var scene in Scenes)if(!File.Exists(scene)||!File.Exists(scene+".meta"))throw new InvalidDataException("Missing V1 scene/GUID: "+scene);
  string folder=Persistence.ExperimentPath("Build");Directory.CreateDirectory(folder);
  var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=Scenes,target=BuildTarget.StandaloneWindows64,locationPathName=Path.Combine(folder,"SporeGobboV1.exe"),options=BuildOptions.Development});
  File.WriteAllText(Path.Combine(folder,"build-result.txt"),result.summary.result.ToString());if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Protected V1 build failed");
 }
}
#endif
