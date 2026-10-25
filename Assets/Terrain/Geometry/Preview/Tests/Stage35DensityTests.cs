using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;

namespace SporeGobbo.Geometry.Preview.Tests
{
    public sealed class Stage35DensityTests
    {
        [Serializable] public sealed class Row
        {
            public int size,vertices,triangles,maxLayerVertices,normalSamples;
            public string pattern;
            public double coldBuildMs,warmBuildMeanMs,uploadMeanMs;
            public long meshNativeBytes,logicalPayloadBytes,geometryPayloadBytes;
        }
        [Serializable] public sealed class Report {public string method="Synchronous Unity Editor density probe; one patch, cold build then five warm rebuild/upload iterations. No gameplay frames or presentation. Payload bytes exclude list capacity and object overhead; native mesh bytes from Unity Profiler.";public List<Row> rows=new List<Row>();}
        [Test] public void DensityAndMemory()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-stage35DensityOutput");
            if(at<0)Assert.Ignore("Density benchmark requires -stage35DensityOutput.");
            string output=args[at+1];Directory.CreateDirectory(output);var report=new Report();
            foreach(int size in new[]{8,16,32})foreach(string pattern in new[]{"empty","solid","checker"})
            {
                string fixture=string.Join("\n",Enumerable.Range(0,size+1).Select(y=>new string(Enumerable.Range(0,size+1).Select(x=>pattern=="solid"||pattern=="checker"&&(x+y)%2==0?'1':'0').ToArray())));
                var source=new PlayableTerrain(fixture);var cache=new ContourTerrainCache(source);var scratch=new PreviewMeshWorkspace();
                var timer=System.Diagnostics.Stopwatch.StartNew();var data=PreviewMeshBuilder.BuildPatch(source,cache,0,0,size,null,scratch);timer.Stop();
                var row=new Row{size=size,pattern=pattern,coldBuildMs=timer.Elapsed.TotalMilliseconds};
                var meshes=data.Select(d=>new Mesh()).ToArray();
                try
                {
                    for(int i=0;i<5;i++)
                    {
                        timer.Restart();PreviewMeshBuilder.BuildPatch(source,cache,0,0,size,data,scratch);timer.Stop();row.warmBuildMeanMs+=timer.Elapsed.TotalMilliseconds/5;
                        timer.Restart();for(int k=0;k<data.Length;k++)data[k].ApplyTo(meshes[k]);timer.Stop();row.uploadMeanMs+=timer.Elapsed.TotalMilliseconds/5;
                    }
                    foreach(var layer in data)
                    {
                        row.vertices+=layer.Vertices.Count;row.triangles+=layer.Indices.Count/3;row.maxLayerVertices=Math.Max(row.maxLayerVertices,layer.Vertices.Count);
                        row.logicalPayloadBytes+=24L*layer.LogicalVertices.Count+48L*layer.WallSamples.Count;
                        row.geometryPayloadBytes+=12L*layer.Vertices.Count+4L*layer.Indices.Count;
                    }
                    row.meshNativeBytes=meshes.Sum(m=>Profiler.GetRuntimeMemorySizeLong(m));row.normalSamples=cache.Orientation.CachedCount;
                    report.rows.Add(row);
                }
                finally{foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(Path.Combine(output,"density.json"),JsonUtility.ToJson(report,true));
        }
    }
}
