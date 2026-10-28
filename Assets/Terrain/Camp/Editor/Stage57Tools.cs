using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.CampCandidate.Preview;
namespace SporeGobbo.CampCandidate.Editor
{
 public static class Stage57Tools
 {
  public const string ScenePath="Assets/Terrain/Camp/Scenes/FutureCampStage57.unity";
  [MenuItem("Tools/Spore Gobbo/Camp/Open Stage 5.7 Settlement Design")]
  public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}
  public static void Install(){if(!File.Exists(ScenePath))AssetDatabase.CopyAsset(Stage56Tools.ScenePath,ScenePath);var scene=EditorSceneManager.OpenScene(ScenePath);var live=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();foreach(var b in live.GetComponents<MonoBehaviour>())if(b is Stage56Preview||b is Stage56Benchmark)UnityEngine.Object.DestroyImmediate(b);var p=live.GetComponent<Stage57Preview>();if(p==null)p=live.gameObject.AddComponent<Stage57Preview>();p.palette=AssetDatabase.LoadAssetAtPath<Stage55Palette>("Assets/Terrain/Geometry/Preview/Generated/Stage55Palette.asset");p.terrainShader=Shader.Find("SporeGobbo/CampCandidate/Terrain");if(live.GetComponent<Stage57Benchmark>()==null)live.gameObject.AddComponent<Stage57Benchmark>();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();}
  public static void Build(){Install();var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,"-stage57BuildOutput");string path=Path.GetFullPath(args[n+1]);Stage57Evidence.Export(Path.Combine(Path.GetDirectoryName(path),"..","definition"));Stage57Evidence.Export(Path.Combine(Path.GetDirectoryName(path),"..","definition"),1);Directory.CreateDirectory(Path.GetDirectoryName(path));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=path,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"build-result.txt"),report.summary.result+";"+report.summary.totalSize);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage57 build failed");}
 }
}
