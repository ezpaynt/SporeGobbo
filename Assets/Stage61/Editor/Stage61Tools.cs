#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using SporeGobbo.Safety;
public static partial class Stage61Tools
{
 public static void BuildCandidate()
 {
  string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));string expected=Environment.GetEnvironmentVariable("SPORE_STAGE60_PROJECT");
  if(string.IsNullOrEmpty(expected)||project!=Path.GetFullPath(expected)||Environment.GetEnvironmentVariable("SPORE_STAGE60_MODE")!="build"||PlayerSettings.companyName!="SporeGobboSafety"||!PlayerSettings.productName.StartsWith("Safety_")||!string.IsNullOrEmpty(PlayerSettings.cloudProjectId))throw new InvalidOperationException("Candidate build requires the reviewed isolated runner");
  Persistence.NoLinks(project);string folder=Persistence.ExperimentPath("Build");Directory.CreateDirectory(folder);string settings=Sha("ProjectSettings/EditorBuildSettings.asset");
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Stage61/DevelopmentEntry.unity","Assets/Scenes/MainMenu.unity","Assets/Scenes/SampleScene.unity",CandidatePath,"Assets/Expedition/FirstExpedition.unity","Assets/Intro/FirstGobboIntro.unity"},target=BuildTarget.StandaloneWindows64,locationPathName=Path.Combine(folder,"SporeGobboStage61Candidate.exe"),options=BuildOptions.Development});
  if(Sha("ProjectSettings/EditorBuildSettings.asset")!=settings)throw new InvalidOperationException("Build changed official settings");
  File.WriteAllText(Path.Combine(folder,"build-result.txt"),report.summary.result.ToString());if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Candidate development build failed");
 }
 public static void BuildGeometry()
 {
  string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));string expected=Environment.GetEnvironmentVariable("SPORE_STAGE60_PROJECT");
  if(string.IsNullOrEmpty(expected)||project!=Path.GetFullPath(expected)||Environment.GetEnvironmentVariable("SPORE_STAGE60_MODE")!="build"||PlayerSettings.companyName!="SporeGobboSafety"||!PlayerSettings.productName.StartsWith("Safety_")||!string.IsNullOrEmpty(PlayerSettings.cloudProjectId))throw new InvalidOperationException("Stage61 tools require the reviewed isolated runner");
  Persistence.NoLinks(project);string folder=Persistence.ExperimentPath("Build");Directory.CreateDirectory(folder);
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Stage61/GeometryValidation.unity"},target=BuildTarget.StandaloneWindows64,locationPathName=Path.Combine(folder,"SporeGobboStage61Geometry.exe"),options=BuildOptions.Development});
  File.WriteAllText(Path.Combine(folder,"build-result.txt"),report.summary.result.ToString());if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Geometry validation build failed");
 }
}
#endif
