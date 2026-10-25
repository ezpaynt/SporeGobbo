using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Profiling;
namespace SporeGobbo.Geometry.Preview.Editor
{
 public static class Stage48Study
 {
  [Serializable] public class Sample{public string pattern,backend;public double ms;public long allocated,meshBytes;public int vertices,triangles;}
  [Serializable] public class Size{public int dimension,occupancyBytes,edits,saveBytes,visibleChunks;public double generationMs;public long allocated;}
  [Serializable] public class Report{public bool allocationCounterValidated;public List<Sample> builds=new List<Sample>();public List<Size> sizes=new List<Size>();}
  public sealed class Pattern:IOccupancySource
  {
   public int Mode;public int Width=>33;public int Height=>33;
   public bool IsSolid(int x,int y){switch(Mode){case 0:return true;case 1:return false;case 2:return Math.Abs(y-16-5*Math.Sin(x*.3))>2;case 3:return Math.Sin(x*.6)+Math.Cos(y*.5)>0;case 4:return x%4==0&&y%4==0;default:return (x+y)%2==0;}}
  }
  [Serializable] public class Layer{public string name;public Vector2[] vertices;public int[] indices;}
  [Serializable] public class Sheet{public int width,height;public string name;public Layer[] layers;}
  [Serializable] public class Sheets{public Sheet[] sheets;}
  public static void Run()
  {
   var args=Environment.GetCommandLineArgs();int a=Array.IndexOf(args,"-stage48ExportOutput");string output=args[a+1];Directory.CreateDirectory(output);Stage48Tools.Export();var report=new Report();long probe=GC.GetAllocatedBytesForCurrentThread();GC.KeepAlive(new byte[1048576]);report.allocationCounterValidated=GC.GetAllocatedBytesForCurrentThread()-probe>=1048576;PreviewMeshBuilder.WarmOptimizedTemplates();
   string[] names={"solid","open","winding","branching","pillars","checkerboard"};
   for(int mode=0;mode<6;mode++)foreach(bool optimized in new[]{false,true})for(int repeat=0;repeat<5;repeat++)
   {
    var source=new Pattern{Mode=mode};long before=GC.GetAllocatedBytesForCurrentThread(),start=System.Diagnostics.Stopwatch.GetTimestamp();var cache=new ContourTerrainCache(optimized?(IOccupancySource)new ChunkReadWindow(source,new DualTile(0,0)):source);
    var data=optimized?PreviewMeshBuilder.BuildOptimizedRows(source,cache,0,0,16,0,16):PreviewMeshBuilder.BuildPatch(source,cache,0,0,16);
    var sample=new Sample{pattern=names[mode],backend=optimized?"optimized":"reference",ms=StreamingGeometry.Elapsed(start),allocated=report.allocationCounterValidated?GC.GetAllocatedBytesForCurrentThread()-before:-1};
    foreach(var layer in data){sample.vertices+=layer.Vertices.Count;sample.triangles+=layer.Indices.Count/3;var mesh=layer.CreateMesh();sample.meshBytes+=Profiler.GetRuntimeMemorySizeLong(mesh);UnityEngine.Object.DestroyImmediate(mesh);}report.builds.Add(sample);cache.Clear();
   }
   foreach(int size in new[]{256,512,1024,2048})
   {
    long before=GC.GetAllocatedBytesForCurrentThread(),start=System.Diagnostics.Stopwatch.GetTimestamp();var w=new StreamingWorld(size,size);var row=new Size{dimension=size,generationMs=StreamingGeometry.Elapsed(start),allocated=report.allocationCounterValidated?GC.GetAllocatedBytesForCurrentThread()-before:-1,occupancyBytes=w.OccupancyBytes};
    for(int y=8;y<size-8&&w.EditCount<1000;y++)for(int x=8;x<size-8&&w.EditCount<1000;x++)w.Excavate(x,y);row.edits=w.EditCount;using(var mem=new MemoryStream()){w.Save(mem);row.saveBytes=(int)mem.Length;}
    var visible=new HashSet<DualTile>();StreamingVisibility.Select(StreamingVisibility.LogicalRegion(new FocusProjection(new Point2(size/2,size/2),48,28,24,.25,.2,1.9,new Point2(0,3)),87,49,2.15),size,size,visible);row.visibleChunks=visible.Count;report.sizes.Add(row);
   }
   File.WriteAllText(Path.Combine(output,"engineering-study.json"),JsonUtility.ToJson(report,true));
   var fixture=AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryPreviewSceneTools.FixturePath);var terrain=new PlayableTerrain(fixture.text);var sheets=new List<Sheet>();
   for(int state=0;state<3;state++)
   {
    if(state>0)for(int y=25;y<25+state*3;y++)for(int x=45;x<45+state*5;x++)terrain.Excavate(new DualTile(x,y));
    sheets.Add(new Sheet{width=terrain.Width,height=terrain.Height,name="cave_excavation_"+state,layers=PreviewMeshBuilder.Build(terrain).Where(d=>d.Layer!=PreviewLayer.Grid&&d.Layer!=PreviewLayer.Gobbo).Select(d=>new Layer{name=d.Layer.ToString(),vertices=d.Vertices.Select(v=>new Vector2(v.x,-v.y)).ToArray(),indices=d.Indices.ToArray()}).ToArray()});
   }
   File.WriteAllText(Path.Combine(output,"cave-sheets.json"),JsonUtility.ToJson(new Sheets{sheets=sheets.ToArray()},false));
  }
 }
}
