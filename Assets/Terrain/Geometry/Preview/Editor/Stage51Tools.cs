using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview.Editor
{
 public static class Stage51Tools
 {
  public const string ScenePath="Assets/Terrain/Geometry/Preview/Scenes/CaveGeometryStage51.unity";
  [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 5.1 Environment Proof")]
  public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}
  public static void Install(){if(!File.Exists(ScenePath))AssetDatabase.CopyAsset(Stage50Tools.ScenePath,ScenePath);var scene=EditorSceneManager.OpenScene(ScenePath);var live=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();if(live.GetComponent<Stage51Preview>()==null)live.gameObject.AddComponent<Stage51Preview>();if(live.GetComponent<Stage51Benchmark>()==null)live.gameObject.AddComponent<Stage51Benchmark>();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();}
  public static void Build(){Install();var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,"-stage51BuildOutput");string output=args[n+1];Directory.CreateDirectory(Path.GetDirectoryName(output));var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=output,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-result.txt"),r.summary.result+"; "+r.summary.totalSize);if(r.summary.result!=BuildResult.Succeeded)throw new Exception("Stage51 build failed");}
 }
}
