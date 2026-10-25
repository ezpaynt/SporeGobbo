using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using SporeGobbo.Geometry.Preview.Editor;
namespace SporeGobbo.Geometry.Preview.Tests
{
 public sealed class Stage48Tests
 {
  sealed class Pattern:IOccupancySource{public int Mode;public int Width=>33;public int Height=>33;public bool IsSolid(int x,int y)=>Mode==0||Mode==1?Mode==0:Mode==2?(x+y)%2==0:Mode==3?x%4==0&&y%4==0:Math.Sin(x*.6)+Math.Cos(y*.5)>0;}
  static double Area(PreviewMeshData m){double a=0;for(int i=0;i<m.LogicalVertices.Count;i+=3)a+=Math.Abs(PreviewMeshData.Cross(m.LogicalVertices[i].Logical,m.LogicalVertices[i+1].Logical,m.LogicalVertices[i+2].Logical))*.5;return a;}
  static string Signature(PreviewMeshData m)=>string.Join(";",m.LogicalVertices.Select(v=>$"{v.Logical.X:R},{v.Logical.Y:R},{v.Role},{v.WallSampleIndex}"));
  [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
  public void OptimizedGeometryRetainsFillAreaAndExactWallLipContourStreams(int mode)
  {
   var source=new Pattern{Mode=mode};var old=PreviewMeshBuilder.BuildPatch(source,new ContourTerrainCache(source),0,0,16);var fast=PreviewMeshBuilder.BuildOptimizedRows(source,new ContourTerrainCache(source),0,0,16,0,16);
   Assert.That(Area(fast[0]),Is.EqualTo(Area(old[0])).Within(1e-8));for(int i=1;i<4;i++)Assert.That(Signature(fast[i]),Is.EqualTo(Signature(old[i])));
   if(mode==0)Assert.That(fast[0].Vertices.Count,Is.EqualTo(96));
  }
  [Test] public void IncrementalRowsAreEquivalentToOneShotAndDeterministic()
  {
   var source=new Pattern{Mode=4};var a=PreviewMeshBuilder.BuildOptimizedRows(source,new ContourTerrainCache(source),0,0,16,0,16);var cache=new ContourTerrainCache(source);PreviewMeshData[] b=null;
   for(int row=0;row<16;row+=2)b=PreviewMeshBuilder.BuildOptimizedRows(source,cache,0,0,16,row,2,b,null,row==0);
   for(int i=0;i<4;i++)Assert.That(Signature(b[i]),Is.EqualTo(Signature(a[i])));
  }
  [Test] public void OptimizedStreamPoolsRemainBoundedAndReleaseOnDispose()
  {
   var world=new StreamingWorld(256,256);var stream=new StreamingGeometry(world,64,true);for(int n=0;n<12;n++){var p=new FocusProjection(new Point2(n%2==0?32:220,128),24,16,12,.25,.2,1.9,new Point2(0,0));for(int i=0;i<250;i++){stream.Select(p,24,16,2,0);stream.Pump(2,1000);if(stream.Ready&&stream.Pending==0)break;}Assert.That(stream.Ready,Is.True);Assert.That(stream.Resident.Count+stream.Preparing,Is.LessThanOrEqualTo(65));Assert.That(stream.PooledData,Is.LessThanOrEqualTo(8));}stream.Dispose();Assert.That(stream.PooledData,Is.Zero);Assert.That(stream.Preparing,Is.Zero);Assert.That(stream.CacheSamples,Is.Zero);
  }
  [Test] public void ExportIsExactEighteenCasesWithCanonicalAnchorsSamplesAndTangents()
  {
   var kit=Stage48Tools.ExportData();Assert.That(kit.cases.Length,Is.EqualTo(18));foreach(var c in kit.cases)foreach(var curve in c.curves){var t=(ContourTemplate)Enum.Parse(typeof(ContourTemplate),curve.template);var expected=ContourGeometry.Curve(t);Assert.That(curve.start.x,Is.EqualTo(expected.Start.X));Assert.That(curve.tangentStart.x,Is.EqualTo(3*(expected.Control1.X-expected.Start.X)));for(int i=0;i<curve.samples.Length;i++){var p=ContourGeometry.Samples(t)[i];Assert.That(curve.samples[i].x,Is.EqualTo(p.X));Assert.That(curve.samples[i].y,Is.EqualTo(p.Y));}}
  }
  [TestCase(256)] [TestCase(512)] [TestCase(1024)] [TestCase(2048)]
  public void WorldSizeDoesNotChangeInteriorSelectionOrPermanentEditSemantics(int size)
  {
   var w=new StreamingWorld(size,size);Assert.That(w.OccupancyBytes,Is.EqualTo(size*size/8));var p=new FocusProjection(new Point2(size*.5,size*.5),48,28,24,.25,.2,1.9,new Point2(0,3));var set=new HashSet<DualTile>();StreamingVisibility.Select(StreamingVisibility.LogicalRegion(p,87,49,2.15),size,size,set);Assert.That(set.Count,Is.LessThan(200));Assert.That(w.IsSolid(size/2,size/2),Is.False);
  }
  [Test] public void AlternateDiagonalOptimizedFillAndReadWindowMatchReferenceAfterEdits()
  {
   var p=new Pattern{Mode=2};var a=PreviewMeshBuilder.BuildPatch(p,new ContourTerrainCache(p,DiagonalPolicy.ConnectOpen),0,0,16);var b=PreviewMeshBuilder.BuildOptimizedRows(p,new ContourTerrainCache(p,DiagonalPolicy.ConnectOpen),0,0,16,0,16);Assert.That(Area(a[0]),Is.EqualTo(Area(b[0])).Within(1e-8));
   var world=new StreamingWorld(128,128);var window=new ChunkReadWindow(world,new DualTile(3,3));var local=new ContourTerrainCache(window);var cell=new DualTile(49,49);world.Excavate(cell.X,cell.Y);window.Refresh();local.InvalidateCell(cell.X,cell.Y);
   var reference=new WallOrientationCache(world);for(int y=46;y<60;y++)for(int x=46;x<60;x++){var q=new Point2(x+.5,y+.5);var aNormal=local.Orientation.Get(q).SolidNormal;var bNormal=reference.Get(q).SolidNormal;Assert.That(aNormal.X,Is.EqualTo(bNormal.X).Within(1e-9));Assert.That(aNormal.Y,Is.EqualTo(bNormal.Y).Within(1e-9));}
  }
  [Test] public void LargerOriginRelativeGpuCoordinatesRemainAccurate()
  {
   if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Requires D3D11");var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Terrain/Geometry/Preview/Runtime/Stage45Parity.compute");var settings=new Stage4Settings();
   var map=new FocusProjection(new Point2(1000.25,-1000.5),48,28,24,.25,.2,1.9,new Point2(0,3));var samples=new Stage45GpuValidation.Sample[512];for(int i=0;i<512;i++){var p=new Point2((i*31)%2048-1024+.125,(i*47)%2048-1024+.375);samples[i]=new Stage45GpuValidation.Sample(p,i%5,new WallSample(p,new Point2(Math.Cos(i*.7),Math.Sin(i*.7))));}var values=Stage45GpuValidation.Evaluate(shader,map,settings,samples);double error=0;for(int i=0;i<samples.Length;i++){var p=Stage45GpuValidation.Cpu(map,settings,samples[i]);error=Math.Max(error,Math.Max(Math.Abs(p.X-values[i].x),Math.Abs(p.Y-values[i].y)));}TestContext.WriteLine("2048 coordinate GPU error "+error);Assert.That(error,Is.LessThan(.001));
  }
  static Stage47Preview Open48(bool art)
  {
   Stage48Tools.Install();UnityEditor.SceneManagement.EditorSceneManager.OpenScene(art?Stage48Tools.ArtPath:Stage48Tools.ScenePath);var live=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();live.Initialize();live.settings.smoothing=0;live.showUI=false;for(int n=0;n<1500;n++){live.Refresh(0);if(live.Ready&&live.Stream.Pending==0)break;}Assert.That(live.Ready,Is.True);return live;
  }
  [Test] public void OptimizedStrongRenderedSilhouetteMatchesReference()
  {
   if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Requires graphics");
   try
   {
    Stage48Tools.Install();var images=new List<Color32[]>();string output="C:/Users/ezpay/Documents/SporeGobbo/stage48_work/visual-tests";System.IO.Directory.CreateDirectory(output);
    foreach(string scene in new[]{Stage47PreviewTools.ScenePath,Stage48Tools.ScenePath})
    {UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene);var live=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();live.worldSize=128;live.Initialize();live.settings.smoothing=0;for(int i=0;i<1500;i++){live.Refresh(0);if(live.Ready&&live.Stream.Pending==0)break;}Assert.That(live.Ready,Is.True);string path=System.IO.Path.Combine(output,images.Count==0?"reference-color.png":"optimized-color.png");Capture(live.previewCamera,path);var texture=new Texture2D(2,2);texture.LoadImage(System.IO.File.ReadAllBytes(path));images.Add(texture.GetPixels32());UnityEngine.Object.DestroyImmediate(texture);}
    long error=0;int changed=0;for(int i=0;i<images[0].Length;i++){var a=images[0][i];var b=images[1][i];int delta=Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);error+=delta;if(delta>24)changed++;}TestContext.WriteLine("Mean image channel difference "+error/(double)(images[0].Length*3));Assert.That(error/(double)(images[0].Length*3),Is.LessThan(.25));Assert.That(changed/(double)images[0].Length,Is.LessThan(.001));
   }
   finally{UnityEditor.SceneManagement.EditorSceneManager.OpenScene(PlayablePreviewTools.ScenePath);}
  }
  [Test] public void TextureUvSurvivesCameraCompressionDigAndPooledReload()
  {
   if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Requires graphics");
   try
   {
    var p=Open48(true);Assert.That(p.terrainTextures.All(t=>t!=null),Is.True);int checkedVertices=0;
    Action inspect=()=>{foreach(var f in p.GetComponentsInChildren<MeshFilter>()){if(!f.GetComponent<MeshRenderer>().enabled||f.sharedMesh.vertexCount==0)continue;var uv=new List<Vector2>();f.sharedMesh.GetUVs(2,uv);if(f.name=="Resident floor"){var positions=f.sharedMesh.vertices;for(int i=0;i<positions.Length;i+=3){float xmin=Math.Min(positions[i].x,Math.Min(positions[i+1].x,positions[i+2].x)),xmax=Math.Max(positions[i].x,Math.Max(positions[i+1].x,positions[i+2].x));Assert.That(xmax-xmin,Is.LessThanOrEqualTo(1.001f));for(int j=0;j<3;j++){Assert.That(uv[i+j].x,Is.EqualTo((positions[i+j].x+512)/4).Within(.00003));Assert.That(uv[i+j].y,Is.EqualTo((positions[i+j].y+512)/4).Within(.00003));}}}if(f.name.Contains("Chunk")||f.name=="Resident floor"){Assert.That(uv.Count,Is.EqualTo(f.sharedMesh.vertexCount));foreach(var v in uv){Assert.That(float.IsNaN(v.x)||float.IsNaN(v.y),Is.False);checkedVertices++;}}}};
    inspect();p.settings.uniform=true;for(int n=0;n<200;n++)p.Refresh(0);inspect();p.settings.uniform=false;for(int n=0;n<400;n++)p.Refresh(0);inspect();
    long queries=p.World.Queries;int builds=p.Stream.TotalBuilt;for(int n=0;n<24;n++){p.settings.anchor+=.000001f;p.Refresh(0);}Assert.That(p.Stream.TotalBuilt,Is.EqualTo(builds));Assert.That(p.World.Queries,Is.EqualTo(queries));
    p.DigAt(new Vector2(525.5f,525.5f));p.Refresh(0);inspect();p.Teleport(new Vector2(24.5f,512.5f));for(int n=0;n<1000;n++)p.Refresh(0);p.Teleport(new Vector2(512.5f,512.5f));for(int n=0;n<1000;n++)p.Refresh(0);inspect();Assert.That(p.PooledPresentations,Is.LessThanOrEqualTo(8));Assert.That(p.Gpu.PooledBufferCount,Is.LessThanOrEqualTo(48));Assert.That(checkedVertices,Is.GreaterThan(1000));
    string directory="C:/Users/ezpay/Documents/SporeGobbo/stage48_work/visual-tests";System.IO.Directory.CreateDirectory(directory);Capture(p.previewCamera,System.IO.Path.Combine(directory,"textured-strong.png"));p.settings.uniform=true;for(int n=0;n<200;n++)p.Refresh(0);Capture(p.previewCamera,System.IO.Path.Combine(directory,"textured-uniform.png"));
   }
   finally{UnityEditor.SceneManagement.EditorSceneManager.OpenScene(PlayablePreviewTools.ScenePath);}
  }
  static void Capture(Camera camera,string path){var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=camera.targetTexture;var active=RenderTexture.active;try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}}
 }
}

