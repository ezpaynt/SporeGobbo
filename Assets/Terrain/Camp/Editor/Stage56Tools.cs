using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.Geometry.Preview.Editor;
using SporeGobbo.CampCandidate.Preview;
namespace SporeGobbo.CampCandidate.Editor
{
 public static class Stage56Tools
 {
  public const string ScenePath="Assets/Terrain/Camp/Scenes/FutureCampStage56.unity";
  [MenuItem("Tools/Spore Gobbo/Camp/Open Stage 5.6 Future Camp Candidate")]
  public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}
  public static void Install(){Directory.CreateDirectory("Assets/Terrain/Camp/Scenes");if(!File.Exists(ScenePath))AssetDatabase.CopyAsset(Stage55Tools.ScenePath,ScenePath);var scene=EditorSceneManager.OpenScene(ScenePath);var live=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();var prior=live.GetComponent<Stage55Preview>();if(prior!=null)UnityEngine.Object.DestroyImmediate(prior);foreach(var b in live.GetComponents<MonoBehaviour>())if((b.GetType().Name.Contains("Benchmark")&&!(b is Stage56Benchmark))||b is Stage50Preview)UnityEngine.Object.DestroyImmediate(b);var camera=live.GetComponent<Stage49Preview>();camera.enabled=false;live.worldSize=192;live.showUI=false;live.stage48Optimized=true;var p=live.GetComponent<Stage56Preview>();if(p==null)p=live.gameObject.AddComponent<Stage56Preview>();p.palette=AssetDatabase.LoadAssetAtPath<Stage55Palette>("Assets/Terrain/Geometry/Preview/Generated/Stage55Palette.asset");p.terrainShader=Shader.Find("SporeGobbo/CampCandidate/Terrain");if(live.GetComponent<Stage56Benchmark>()==null)live.gameObject.AddComponent<Stage56Benchmark>();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();}
  public static void Build(){Install();var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,"-stage56BuildOutput");if(n<0)throw new ArgumentException("-stage56BuildOutput required");string path=Path.GetFullPath(args[n+1]);Stage56DefinitionEvidence.Export(Path.Combine(Path.GetDirectoryName(path),"..","definition"));Directory.CreateDirectory(Path.GetDirectoryName(path));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=path,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"build-result.txt"),report.summary.result+";"+report.summary.totalSize);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Camp build failed");}
 }
}
