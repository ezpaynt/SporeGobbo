using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
namespace SporeGobbo.Geometry.Preview.Editor
{
 public static class Stage48Tools
 {
  const string Root="Assets/Terrain/Geometry/Preview";
  public const string ScenePath=Root+"/Scenes/CaveGeometryStage48.unity",ArtPath=Root+"/Scenes/CaveGeometryStage48Art.unity";
  [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 4.8 Optimized World")]
  public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}
  [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 4.8 Texture Proof")]
  public static void OpenArt(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ArtPath))Install();EditorSceneManager.OpenScene(ArtPath);}
  public static void Install()
  {
   foreach(string name in new[]{"floor","earth","wall","rim","lip"}){var importer=AssetImporter.GetAtPath(Root+"/ArtProof/"+name+".png") as TextureImporter;if(importer!=null){importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaIsTransparency=name=="rim"||name=="lip";importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=name=="wall"||name=="lip"?TextureWrapMode.Clamp:TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}}
   foreach(bool art in new[]{false,true})
   {
    string path=art?ArtPath:ScenePath;if(!File.Exists(path))AssetDatabase.CopyAsset(Stage47PreviewTools.ScenePath,path);
    var scene=EditorSceneManager.OpenScene(path);var p=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();p.stage48Optimized=true;p.worldSize=1024;if(p.GetComponent<Stage48DensityBenchmark>()==null)p.gameObject.AddComponent<Stage48DensityBenchmark>();
    if(art){p.terrainArtShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Runtime/Stage48TerrainArt.shader");p.terrainTextures=new Texture2D[5];string[] names={"floor","earth","wall","rim","lip"};for(int i=0;i<5;i++)p.terrainTextures[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ArtProof/"+names[i]+".png");}
    EditorSceneManager.SaveScene(scene);
   }
   AssetDatabase.SaveAssets();
  }
  public static void Build()
  {
   Install();var args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"-stage48ExportOutput")>=0)Stage48Study.Run();int i=Array.IndexOf(args,"-stage48BuildOutput");string path=args[i+1];Directory.CreateDirectory(Path.GetDirectoryName(path));bool art=Array.IndexOf(args,"-stage48ArtBuild")>=0;
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{art?ArtPath:ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
   File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"build-result.txt"),report.summary.result+"; "+report.summary.totalSize+"; "+report.summary.totalTime);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage48 build failed");
  }
  [Serializable] public sealed class P{public double x,y;public P(Point2 p){x=p.X;y=p.Y;}}
  [Serializable] public sealed class Curve{public string template;public P start,c1,c2,end,tangentStart,tangentEnd;public P[] samples;}
  [Serializable] public sealed class Case{public int code;public string policy,label;public Curve[] curves;}
  [Serializable] public sealed class Kit{public int subdivisions=ContourGeometry.Subdivisions;public double wallHeight=2.15,lipWidth=.22;public string coordinates="unit square, x right, y down; bits TL1 TR2 BR4 BL8; actual dual tile = unit + (tileX+.5,tileY+.5)";public Case[] cases;public string source="SporeGobbo.Geometry.ContourGeometry and WallGeometry";}
  public static Kit ExportData()
  {
   var cases=new List<Case>();for(int i=0;i<18;i++){int code=i<16?i:i==16?5:10;var policy=i<16?DiagonalPolicy.ConnectSolid:DiagonalPolicy.ConnectOpen;var curves=new List<Curve>();foreach(var t in ContourGeometry.Templates(code,policy)){var c=ContourGeometry.Curve(t);var samples=ContourGeometry.Samples(t);var points=new P[samples.Count];for(int j=0;j<points.Length;j++)points[j]=new P(samples[j]);curves.Add(new Curve{template=t.ToString(),start=new P(c.Start),c1=new P(c.Control1),c2=new P(c.Control2),end=new P(c.End),tangentStart=new P((c.Control1-c.Start)*3),tangentEnd=new P((c.End-c.Control2)*3),samples=points});}cases.Add(new Case{code=code,policy=policy.ToString(),label=$"case_{code:00}_{policy}",curves=curves.ToArray()});}return new Kit{cases=cases.ToArray()};
  }
  public static void Export()
  {
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-stage48ExportOutput");string path=args[i+1];Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"canonical-geometry.json"),JsonUtility.ToJson(ExportData(),true));Debug.Log("Exported exact 18-case C# geometry: "+path);
  }
 }
}


