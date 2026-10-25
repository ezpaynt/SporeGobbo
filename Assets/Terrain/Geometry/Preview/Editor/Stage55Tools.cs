using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview.Editor
{
 public static class Stage55Tools
 {
  public const string ScenePath="Assets/Terrain/Geometry/Preview/Scenes/CaveGeometryStage55.unity";
  [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 5.5 Structural Perspective and Cave Depth")]
  public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}
  public static void Install(){if(!File.Exists(ScenePath))AssetDatabase.CopyAsset(Stage50Tools.ScenePath,ScenePath);var scene=EditorSceneManager.OpenScene(ScenePath);var live=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();if(live.GetComponent<Stage55Preview>()==null)live.gameObject.AddComponent<Stage55Preview>();if(live.GetComponent<Stage55Benchmark>()==null)live.gameObject.AddComponent<Stage55Benchmark>();var preview=live.GetComponent<Stage55Preview>();preview.structureShader=Shader.Find("SporeGobbo/GeometryPreview/Stage55Structure");preview.terrainShader=Shader.Find("SporeGobbo/GeometryPreview/Stage55Terrain");const string palettePath="Assets/Terrain/Geometry/Preview/Generated/Stage55Palette.asset";preview.palette=AssetDatabase.LoadAssetAtPath<Stage55Palette>(palettePath);if(preview.palette==null){preview.palette=ScriptableObject.CreateInstance<Stage55Palette>();AssetDatabase.CreateAsset(preview.palette,palettePath);}EditorUtility.SetDirty(preview);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();}
  public static void Build(){Install();var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,"-stage55BuildOutput");if(n<0||n+1>=args.Length)throw new ArgumentException("Required -stage55BuildOutput path missing");string output=Path.GetFullPath(args[n+1]);if(!output.EndsWith("SporeGobboStage55.exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Expected isolated SporeGobboStage55.exe output");Directory.CreateDirectory(Path.GetDirectoryName(output));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=output,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-result.txt"),report.summary.result+"; "+report.summary.totalSize);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage55 build failed");}
 }
}
