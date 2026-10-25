using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using SporeGobbo.Geometry.Preview.Editor;

namespace SporeGobbo.Geometry.Preview.Tests
{
    /// <summary>Explicit benchmark, excluded from ordinary regression runs. Drives actual Update/input frames.</summary>
    public sealed class Stage35ProfilingTests
    {
        [Serializable] public sealed class Scenario
        {
            public int patchSize,body,meshes,objects,vertices,triangles,enabledMeshes;
            public long managedUsedBytes,nativeUsedBytes;
            public bool allocationAvailable,mainThreadAvailable,gcMarkerAvailable,drawCounterAvailable,presentAvailable;
            public GeometryFrameSample[] frames;
        }
        [Serializable] public sealed class Report
        {
            public string label,environment,method;
            public List<Scenario> scenarios=new List<Scenario>();
        }
        [Serializable] public sealed class Golden {public List<string> names=new List<string>();public List<string> hashes=new List<string>();}

        [UnityTest,Explicit("Use the documented Stage 3.5 native profiling command.")]
        public IEnumerator ActualPlayFrames()
        {
            if(!Environment.GetCommandLineArgs().Contains("-stage35Output"))Assert.Ignore("Native profiling requires the documented command-line options.");
            string output=Argument("-stage35Output"),label=Argument("-stage35Label");Directory.CreateDirectory(output);
            if(label=="before")
            {
                const string reference=GeometryPreviewSceneTools.Root+"/Scenes/CaveGeometryStage3Reference.unity";
                if(!File.Exists(reference))Assert.That(AssetDatabase.CopyAsset(PlayablePreviewTools.ScenePath,reference),Is.True);
                File.WriteAllText(Path.Combine(output,"stage3-geometry-goldens.json"),JsonUtility.ToJson(BuildGoldens(),true));
            }
            EditorSceneManager.OpenScene(PlayablePreviewTools.ScenePath);
            // A real Game View is requested; render/present counters expose whether the host supplies it.
            var gameView=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if(gameView!=null){var window=EditorWindow.GetWindow(gameView);window.position=new Rect(100,100,1400,900);window.Show();window.Focus();}
            yield return new EnterPlayMode();yield return null;
            output=Argument("-stage35Output");label=Argument("-stage35Label");
            var report=new Report{label=label,environment=SystemInfo.processorType+"; "+SystemInfo.graphicsDeviceName+"; Unity "+Application.unityVersion,
                method="Actual Play Mode Update/TickInput with virtual W+Space, 480 engine frames/scenario, captureDeltaTime 1/60. Phase timers inclusive; native Unity markers available in Profiler. Presented-frame availability is reported by native counters, never assumed."};
            bool oldProfiler=Profiler.enabled;Profiler.enabled=true;
            bool oldBackground=Application.runInBackground;int oldTarget=Application.targetFrameRate,oldVsync=QualitySettings.vSyncCount;
            float oldCapture=Time.captureDeltaTime;
            var oldBehavior=InputSystem.settings.backgroundBehavior;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.captureDeltaTime=1f/60;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                int[] sizes=label=="before"?new[]{8,16}:new[]{8,16,32};
                foreach(int size in sizes)foreach(int body in new[]{1,3,9})
                {
                    var live=UnityEngine.Object.FindAnyObjectByType<PlayableCavePreview>();
                    live.SetPatchSize(size);live.ResetFixture();live.SetFootprint(body);
                    live.brush=body==1?DigBrush.Single:body==3?DigBrush.ThreeWide:DigBrush.Radius;
                    live.digRadius=body*.707107f+.8f;live.followPlayer=true;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                    var capture=live.gameObject.AddComponent<GeometryFrameCapture>();
                    for(int warm=0;warm<20;warm++)yield return null;
                    // Validate the native allocation counter using a known allocation outside recorded frames.
                    GC.KeepAlive(new byte[65536]);yield return null;yield return null;
                    capture.Begin(live,480);
                    while(capture.Count<480)
                    {
                        var camera=live.GetComponent<CaveGeometryPreview>().previewCamera;
                        Vector3 p=camera.WorldToScreenPoint(new Vector3(live.Position.x,-live.Position.y+4,0));
                        InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(p.x,p.y)});
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Space));
                        InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();
                        yield return null;
                    }
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                    Assert.That(live.Session.DigCount,Is.GreaterThan(5),"Profiling must exercise excavation, not just idle frames.");
                    report.scenarios.Add(new Scenario{patchSize=size,body=body,meshes=live.RuntimeMeshCount,objects=live.RuntimeObjectCount,
                        enabledMeshes=live.ActiveSceneMeshCount,vertices=live.VertexCount,triangles=live.TriangleCount,
                        managedUsedBytes=Profiler.GetMonoUsedSizeLong(),nativeUsedBytes=Profiler.GetTotalAllocatedMemoryLong(),
                        allocationAvailable=capture.AllocationCounterValidated,mainThreadAvailable=capture.MainThreadAvailable,
                        gcMarkerAvailable=capture.GCMarkerAvailable,drawCounterAvailable=capture.DrawCounterAvailable,presentAvailable=capture.PresentMarkerAvailable,frames=capture.Samples});
                    File.WriteAllText(Path.Combine(output,label+"-native-frames.json"),JsonUtility.ToJson(report,true));
                    UnityEngine.Object.Destroy(capture);yield return null;
                    live.ResetFixture();Debug.Log($"Stage 3.5 {label}: {size}-cell patches, {body}-cell body, recorded 480 Play frames.");
                }
                File.WriteAllText(Path.Combine(output,label+"-native-frames.json"),JsonUtility.ToJson(report,true));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=oldBehavior;
                Application.runInBackground=oldBackground;Application.targetFrameRate=oldTarget;QualitySettings.vSyncCount=oldVsync;Time.captureDeltaTime=oldCapture;
                NativeGeometryProfiling.CollectTimings=false;Profiler.enabled=oldProfiler;
            }
            yield return new ExitPlayMode();
        }
        public static Golden BuildGoldens()
        {
            var result=new Golden();string fixture=AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryPreviewSceneTools.FixturePath).text;
            foreach(int size in new[]{8,16,32})
            {
                var terrain=new PlayableTerrain(fixture);var cache=new ContourTerrainCache(terrain);
                AddGolden(result,"fixture-"+size,terrain,cache,size);
                var random=new System.Random(35035);var cells=new List<DualTile>();
                for(int step=0;step<12;step++)
                {
                    PreviewDigBrush.Cells(new Vector2(random.Next(96),random.Next(62)),(DigBrush)(step%3),4.5f,terrain,cells);
                    for(int i=0;i<cells.Count;i++)terrain.Excavate(cells[i]);cache.InvalidateCells(cells);
                    AddGolden(result,$"dig-{size}-{step}",terrain,cache,size);
                }
            }
            for(int c=0;c<16;c++)
            {
                string text=((c&1)>0?"1":"0")+((c&2)>0?"1":"0")+"\n"+((c&8)>0?"1":"0")+((c&4)>0?"1":"0");
                var t=new PlayableTerrain(text);AddGolden(result,"case-"+c,t,new ContourTerrainCache(t),16);
            }
            return result;
        }
        static void AddGolden(Golden result,string name,IOccupancySource source,ContourTerrainCache cache,int size)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                for(int y=0;y<source.Height-1;y+=size)for(int x=0;x<source.Width-1;x+=size)
                foreach(var mesh in PreviewMeshBuilder.BuildPatch(source,cache,x,y,size))
                {
                    writer.Write(mesh.Name);writer.Write(mesh.Vertices.Count);writer.Write(mesh.Indices.Count);
                    foreach(var v in mesh.Vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
                    foreach(int index in mesh.Indices)writer.Write(index);
                }
                writer.Flush();using(var hash=SHA256.Create())
                {result.names.Add(name);result.hashes.Add(BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-",""));}
            }
        }
        static string Argument(string key)
        {var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]==key)return args[i+1];throw new ArgumentException(key+" required");}
    }
}
