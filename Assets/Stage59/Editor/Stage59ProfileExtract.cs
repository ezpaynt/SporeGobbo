using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Profiling;
using UnityEditorInternal;
namespace SporeGobbo.CampCandidate.Editor
{
 public static class Stage59ProfileExtract
 {
  [Serializable]public class Marker{public string thread,name;public long calls,gcBytes;public double totalMs,worstMs;public int worstFrame;}
  [Serializable]public class Profile{public string file;public bool loaded;public int first,last;public List<Marker> markers=new List<Marker>();public List<string> threads=new List<string>();}
  public static void Export(){var args=Environment.GetCommandLineArgs();string dir=args[Array.IndexOf(args,"-stage59ProfileInput")+1];foreach(string file in Directory.GetFiles(dir,"*.raw")){ProfilerDriver.ClearAllFrames();var p=new Profile{file=file,loaded=ProfilerDriver.LoadProfile(file,false)};if(!p.loaded){File.WriteAllText(file+".analysis.json",JsonUtility.ToJson(p,true));continue;}p.first=ProfilerDriver.firstFrameIndex;p.last=ProfilerDriver.lastFrameIndex;var totals=new Dictionary<string,Marker>();for(int f=p.first;f<=p.last;f++)for(int thread=0;thread<128;thread++)using(var view=ProfilerDriver.GetRawFrameDataView(f,thread)){if(!view.valid)break;string name=view.threadName??"";if(!p.threads.Contains(name))p.threads.Add(name);if(!name.Contains("Main Thread")&&!name.Contains("Render Thread"))continue;for(int i=0;i<view.sampleCount;i++){string marker=view.GetSampleName(i)??"<unnamed>";string key=name+"/"+marker;if(!totals.TryGetValue(key,out var m))totals[key]=m=new Marker{thread=name,name=marker};double ms=view.GetSampleTimeMs(i);m.calls++;m.totalMs+=ms;if(ms>m.worstMs){m.worstMs=ms;m.worstFrame=f;}if((marker.StartsWith("SG58.")||marker.StartsWith("SG59."))){int end=i+view.GetSampleChildrenCountRecursive(i);for(int j=i+1;j<=end&&j<view.sampleCount;j++)if(view.GetSampleName(j)=="GC.Alloc"&&view.GetSampleMetadataCount(j)>0)m.gcBytes+=view.GetSampleMetadataAsLong(j,0);}}}p.markers=totals.Values.Where(x=>(x.name.StartsWith("SG58.")||x.name.StartsWith("SG59."))||x.name.Contains("GobboVisualController")||x.name.Contains("Physics2D")||x.totalMs>10).OrderByDescending(x=>x.totalMs).ToList();File.WriteAllText(file+".analysis.json",JsonUtility.ToJson(p,true));}
   var snapshotTypes=typeof(UnityEditor.Editor).Assembly.GetTypes().Where(t=>t.Name.Contains("MemorySnapshot")||t.Name.Contains("PackedMemory"));File.WriteAllLines(Path.Combine(dir,"snapshot-analysis-api.txt"),snapshotTypes.SelectMany(t=>new[]{t.FullName}.Concat(t.GetMethods().Where(m=>m.Name.Contains("Load")||m.Name.Contains("Read")).Select(m=>m.ToString()))));
  }
 }
}
