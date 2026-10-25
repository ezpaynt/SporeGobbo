using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
namespace SporeGobbo.Geometry.Preview.Editor
{
 public static class Stage50Tools
 {
  public const string ScenePath="Assets/Terrain/Geometry/Preview/Scenes/CaveGeometryStage50.unity";
  [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 5.0 Encounter Laboratory")]
  public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}
  public static void Install(){if(!File.Exists(ScenePath))AssetDatabase.CopyAsset(Stage49Tools.ScenePath,ScenePath);var scene=EditorSceneManager.OpenScene(ScenePath);var live=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();if(live.GetComponent<Stage50Preview>()==null)live.gameObject.AddComponent<Stage50Preview>();if(live.GetComponent<Stage50Benchmark>()==null)live.gameObject.AddComponent<Stage50Benchmark>();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();}
  public static void Build(){Install();var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,"-stage50BuildOutput");string output=args[n+1];Directory.CreateDirectory(Path.GetDirectoryName(output));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=output,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-result.txt"),report.summary.result+"; "+report.summary.totalSize);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage50 build failed");}
 }
}
