using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview.Editor
{
 public static class Stage54Tools
 {
  public const string ScenePath="Assets/Terrain/Geometry/Preview/Scenes/CaveGeometryStage54.unity";
  [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 5.4 Integrated Cave Exploration")]
  public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}
  public static void Install(){if(!File.Exists(ScenePath))AssetDatabase.CopyAsset(Stage50Tools.ScenePath,ScenePath);var scene=EditorSceneManager.OpenScene(ScenePath);var live=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();if(live.GetComponent<Stage54Preview>()==null)live.gameObject.AddComponent<Stage54Preview>();if(live.GetComponent<Stage54Benchmark>()==null)live.gameObject.AddComponent<Stage54Benchmark>();var preview=live.GetComponent<Stage54Preview>();const string palettePath="Assets/Terrain/Geometry/Preview/Generated/Stage54Palette.asset";preview.palette=AssetDatabase.LoadAssetAtPath<Stage54Palette>(palettePath);if(preview.palette==null){preview.palette=ScriptableObject.CreateInstance<Stage54Palette>();AssetDatabase.CreateAsset(preview.palette,palettePath);}EditorUtility.SetDirty(preview);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();}
  public static void Build(){Install();var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,"-stage54BuildOutput");string output=args[n+1];Directory.CreateDirectory(Path.GetDirectoryName(output));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=output,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-result.txt"),report.summary.result+"; "+report.summary.totalSize);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage54 build failed");}
 }
}
