using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using SporeGobbo.CampCandidate.Preview;
namespace SporeGobbo.CampCandidate.Editor
{
 public static class Stage58Tools
 {
  public const string ScenePath="Assets/Terrain/Camp/Scenes/FutureCampStage58Profile.unity";
  [MenuItem("Tools/Spore Gobbo/Camp/Open Stage 5.8 Performance Preview")] public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene(ScenePath);}
  public static void Build(){if(!File.Exists(ScenePath))AssetDatabase.CopyAsset(Stage57Tools.ScenePath,ScenePath);var scene=EditorSceneManager.OpenScene(ScenePath);var host=UnityEngine.Object.FindAnyObjectByType<Stage57Preview>();var old=host.GetComponent<Stage57Benchmark>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);if(host.GetComponent<Stage58Benchmark>()==null)host.gameObject.AddComponent<Stage58Benchmark>();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();const string cave="Assets/Terrain/Camp/Scenes/CaveProfileStage58.unity";if(!File.Exists(cave))AssetDatabase.CopyAsset("Assets/Terrain/Geometry/Preview/Scenes/CaveGeometryStage55.unity",cave);var caveScene=EditorSceneManager.OpenScene(cave);var caveHost=UnityEngine.Object.FindAnyObjectByType<SporeGobbo.Geometry.Preview.Stage55Preview>();var prior=caveHost.GetComponent<SporeGobbo.Geometry.Preview.Stage55Benchmark>();if(prior!=null)UnityEngine.Object.DestroyImmediate(prior);if(caveHost.GetComponent<Stage58CaveBenchmark>()==null)caveHost.gameObject.AddComponent<Stage58CaveBenchmark>();EditorSceneManager.SaveScene(caveScene);AssetDatabase.SaveAssets();var args=Environment.GetCommandLineArgs();string path=Path.GetFullPath(args[Array.IndexOf(args,"-stage58BuildOutput")+1]);Directory.CreateDirectory(Path.GetDirectoryName(path));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath,cave},target=BuildTarget.StandaloneWindows64,locationPathName=path,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"build-result.txt"),report.summary.result.ToString());if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage58 build failed");
   var api=typeof(UnityEditorInternal.ProfilerDriver).GetMethods().Where(m=>m.Name.Contains("Frame")||m.Name.Contains("Profile")).Select(m=>m.ToString());File.WriteAllLines(Path.Combine(Path.GetDirectoryName(path),"profiler-api.txt"),api);
  }
 }
}
