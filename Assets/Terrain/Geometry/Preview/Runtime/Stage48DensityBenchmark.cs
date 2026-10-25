using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
namespace SporeGobbo.Geometry.Preview
{
 public sealed class Stage48DensityBenchmark:MonoBehaviour
 {
  sealed class Pattern:IOccupancySource{public int Mode;public int Width=>33;public int Height=>33;public bool IsSolid(int x,int y){switch(Mode){case 0:return true;case 1:return false;case 2:return Math.Abs(y-16-5*Math.Sin(x*.3))>2;case 3:return Math.Sin(x*.6)+Math.Cos(y*.5)>0;case 4:return x%4==0&&y%4==0;default:return(x+y)%2==0;}}}
  [Serializable] public struct Frame{public double intervalMs,mainMs,gpuMs;public long allocated,draws;public bool focused,presented;}
  [Serializable] public class Case{public string pattern;public double buildMs;public int vertices,triangles;public long meshBytes;public Frame[] frames;}
  [Serializable] public class Report{public string environment,method;public List<Case> cases=new List<Case>();}
  IEnumerator Start()
  {
   var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-stage48DensityOutput");if(arg<0)yield break;string output=args[arg+1];Directory.CreateDirectory(output);
   var live=GetComponent<Stage47Preview>();live.enabled=false;var gpu=GetComponent<Stage45GpuProjection>();gpu.cacheMaterialBindings=true;var camera=live.previewCamera;camera.orthographicSize=20;camera.aspect=16f/9;camera.transform.position=new Vector3(0,0,-10);
   QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;Profiler.enabled=true;Profiler.SetAreaEnabled(ProfilerArea.Rendering,true);
   var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);var alloc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Standard Draw Calls Count",16);var timings=new FrameTiming[1];ulong prior=0;
   var filters=new MeshFilter[5];for(int i=0;i<5;i++){var go=new GameObject("Density layer "+i);go.transform.SetParent(transform,false);var f=go.AddComponent<MeshFilter>();f.sharedMesh=new Mesh();var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=live.materials[(int)(i==4?PreviewLayer.Floor:new[]{PreviewLayer.Solid,PreviewLayer.Faces,PreviewLayer.Lips,PreviewLayer.Contours}[i])];renderer.sortingOrder=i==4?0:i==0?1:10+i;filters[i]=f;}
   var settings=new Stage4Settings{centerWidth=16,centerHeight=12,side=.25f,top=.2f,foreground=1.9f};var projection=new FocusProjection(new Point2(16,16),16,12,12,.25,.2,1.9,new Point2(0,0));var bounds=new ProjectionBounds(new Point2(0,0),new Point2(33,33));var report=new Report{environment=SystemInfo.processorType+"; "+SystemInfo.graphicsDeviceName+"; "+Screen.width+"x"+Screen.height,method="Visible standalone D3D11, isolated 16x16 dual-tile density region with real floor and accepted Strong mapping; 60 warm +240 captured frames; not a full-world synthetic throughput claim."};
   string[] names={"solid","open","winding","branching","pillars","checkerboard"};var end=new WaitForEndOfFrame();
   for(int mode=0;mode<6;mode++)
   {
    var source=new Pattern{Mode=mode};long start=System.Diagnostics.Stopwatch.GetTimestamp();var cache=new ContourTerrainCache(new ChunkReadWindow(source,new DualTile(0,0)));var layers=PreviewMeshBuilder.BuildOptimizedRows(source,cache,0,0,16,0,16);var c=new Case{pattern=names[mode],buildMs=StreamingGeometry.Elapsed(start),frames=new Frame[240]};var floor=new PreviewMeshData("density floor",PreviewLayer.Floor);floor.Rectangle(0,0,16.5,16.5);var data=new[]{layers[0],layers[1],layers[2],layers[3],floor};
    for(int i=0;i<5;i++){c.vertices+=data[i].Vertices.Count;c.triangles+=data[i].Indices.Count/3;}bool first=true;double last=Time.realtimeSinceStartupAsDouble;
    for(int i=-60;i<240;i++)
    {gpu.BeginFrame(projection,settings);for(int j=0;j<5;j++)gpu.Draw(data[j],filters[j],bounds,first);first=false;FrameTimingManager.CaptureFrameTimings();yield return end;double now=Time.realtimeSinceStartupAsDouble;var f=new Frame{intervalMs=(now-last)*1000,mainMs=main.Valid?main.LastValue/1e6:0,allocated=alloc.Valid?alloc.LastValue:-1,draws=draws.Valid?draws.LastValue:-1,focused=Application.isFocused};if(FrameTimingManager.GetLatestTimings(1,timings)>0){var t=timings[0];if(t.frameStartTimestamp!=prior)f.gpuMs=t.gpuFrameTime;prior=t.frameStartTimestamp;f.presented=t.cpuTimePresentCalled>0;}if(i>=0)c.frames[i]=f;last=now;yield return null;}
    foreach(var f in filters)c.meshBytes+=Profiler.GetRuntimeMemorySizeLong(f.sharedMesh);report.cases.Add(c);File.WriteAllText(Path.Combine(output,"density.json"),JsonUtility.ToJson(report,true));cache.Clear();
   }
   foreach(var f in filters){gpu.Forget(f);Destroy(f.sharedMesh);Destroy(f.gameObject);}main.Dispose();alloc.Dispose();draws.Dispose();Application.Quit();
  }
 }
}
