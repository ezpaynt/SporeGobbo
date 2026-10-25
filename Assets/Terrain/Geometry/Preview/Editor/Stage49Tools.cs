using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
namespace SporeGobbo.Geometry.Preview.Editor
{
 public static class Stage49Tools
 {
  public const string ScenePath="Assets/Terrain/Geometry/Preview/Scenes/CaveGeometryStage49.unity";
  [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 4.9 Camera Composition")]
  public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}
  public static void Install(){if(!File.Exists(ScenePath))AssetDatabase.CopyAsset(Stage48Tools.ScenePath,ScenePath);var scene=EditorSceneManager.OpenScene(ScenePath);var l=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();if(l.GetComponent<Stage49Preview>()==null)l.gameObject.AddComponent<Stage49Preview>();if(l.GetComponent<Stage49Benchmark>()==null)l.gameObject.AddComponent<Stage49Benchmark>();l.stage48Optimized=true;l.settings=Stage49Preview.Settings(0);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();}
  public static void Build(){Install();var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-stage49BuildOutput");string p=args[i+1];Directory.CreateDirectory(Path.GetDirectoryName(p));var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=p,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Path.GetDirectoryName(p),"build-result.txt"),r.summary.result+"; "+r.summary.totalSize);if(r.summary.result!=BuildResult.Succeeded)throw new Exception("Stage49 build failed");}
 }
}
