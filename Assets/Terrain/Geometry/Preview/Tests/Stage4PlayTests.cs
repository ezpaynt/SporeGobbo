using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using SporeGobbo.Geometry.Preview.Editor;
namespace SporeGobbo.Geometry.Preview.Tests
{
    public sealed class Stage4PlayTests
    {
        [Serializable] public sealed class Scenario {public string name;public Stage4Frame[] frames;public int poolMeshes,poolObjects,entries;public bool allocationValidated;}
        [Serializable] public sealed class Report {public string environment,method;public List<Scenario> scenarios=new List<Scenario>();}
        [UnityTest] public IEnumerator ActualCameraFrames()
        {
            if(!Environment.GetCommandLineArgs().Contains("-stage4Output"))Assert.Ignore("Use -stage4Output for actual Play Mode capture.");
            EditorSceneManager.OpenScene(Stage4PreviewTools.ScenePath);yield return new EnterPlayMode();yield return null;
            string output=Arg();Directory.CreateDirectory(output);
            var report=new Report{environment=SystemInfo.processorType+"; "+SystemInfo.graphicsDeviceName+"; Unity "+Application.unityVersion,method="240 actual Play Mode frames per scenario; native Main Thread/GC counters, phase clocks and Stage4 CPU projection/upload markers. Fixed simulation delta 1/60, no presented-frame assumption."};
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();var oldBehavior=InputSystem.settings.backgroundBehavior;
            bool oldProfiler=Profiler.enabled,oldBackground=Application.runInBackground;float oldCapture=Time.captureDeltaTime;int oldVsync=QualitySettings.vSyncCount,oldTarget=Application.targetFrameRate;
            Profiler.enabled=true;Application.runInBackground=true;Time.captureDeltaTime=1f/60;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                var live=UnityEngine.Object.FindAnyObjectByType<PlayableCavePreview>();var view=live.GetComponent<Stage4CameraPreview>();var camera=live.GetComponent<CaveGeometryPreview>().previewCamera;
                foreach(string name in Environment.GetCommandLineArgs().Contains("-stage4CaptureOnly")?Array.Empty<string>():new[]{"camera-only","dig-stationary-camera","dig-follow","switch-presets","strong-dense-view","cross-transitions"})
                {
                    live.ResetFixture();view.settings=new Stage4Settings();view.settings.smoothing=0;view.ExperimentFocus=null;view.FreezeCamera=name=="dig-stationary-camera";live.followPlayer=true;
                    if(name=="strong-dense-view"){view.Preset(2);view.settings.coverage=.4f;view.settings.centerWidth=60;view.settings.centerHeight=32;}
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();for(int i=0;i<15;i++)yield return null;
                    var capture=live.gameObject.AddComponent<Stage4FrameCapture>();capture.view=view;
                    GC.KeepAlive(new byte[65536]);yield return null;yield return null;capture.Begin(240);
                    while(capture.Count<240)
                    {
                        int i=capture.Count;bool dig=name.StartsWith("dig-");
                        if(name=="camera-only"||name=="cross-transitions"||name=="strong-dense-view")view.ExperimentFocus=new Vector2(50.5f+Mathf.Sin(i*.04f)*(name=="cross-transitions"?34:12),39.5f+Mathf.Cos(i*.04f)*14);
                        if(name=="switch-presets")view.Preset((i/10)%3);
                        var logical=new Point2(live.Position.x,live.Position.y-4);var projected=view.Snapshot.Project(logical);var screen=camera.WorldToScreenPoint(PreviewMeshData.RenderPoint(projected));
                        InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});InputSystem.QueueStateEvent(keyboard,dig?new KeyboardState(Key.W,Key.Space):new KeyboardState());InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();
                        yield return null;
                    }
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                    if(!name.StartsWith("dig-"))
                    {Assert.That(capture.Frames.Sum(f=>f.occupancyQueries),Is.EqualTo(0),name);Assert.That(capture.Frames.Sum(f=>f.filterBuilds),Is.EqualTo(0),name);Assert.That(capture.Frames.Sum(f=>f.phases[(int)GeometryPhase.Contour]+f.phases[(int)GeometryPhase.Triangulation]+f.phases[(int)GeometryPhase.MeshBuild]),Is.EqualTo(0),name);}
                    else Assert.That(capture.Frames.Sum(f=>f.digs),Is.GreaterThan(0),name);
                    report.scenarios.Add(new Scenario{name=name,frames=capture.Frames,poolMeshes=live.RuntimeMeshCount,poolObjects=live.RuntimeObjectCount,entries=view.EntryCount,allocationValidated=capture.AllocationValidated});
                    File.WriteAllText(Path.Combine(output,"stage4-native.json"),JsonUtility.ToJson(report,true));UnityEngine.Object.Destroy(capture);yield return null;Debug.Log("Stage4 capture passed: "+name);
                }
                view.ExperimentFocus=null;view.FreezeCamera=false;live.ResetFixture();
                for(int preset=0;preset<3;preset++){view.settings=new Stage4Settings();view.Preset(preset);view.Refresh(1);yield return null;Capture(camera,Path.Combine(output,new[]{"gentle","balanced","strong"}[preset]+".png"));}
                view.settings=new Stage4Settings();view.settings.heightPolicy=WallHeightPolicy.ReferenceLocalScale;view.Refresh(1);Capture(camera,Path.Combine(output,"balanced-reference-height.png"));
                view.settings.heightPolicy=WallHeightPolicy.ProjectLogicalExtrusion;view.settings.actorMode=1;view.Refresh(1);Capture(camera,Path.Combine(output,"balanced-grid-actors.png"));
                live.GetComponent<CaveGeometryPreview>().showGrid=true;view.Refresh(1);Capture(camera,Path.Combine(output,"balanced-grid.png"));live.GetComponent<CaveGeometryPreview>().showGrid=false;
                view.settings.stage3Comparison=true;view.Refresh(1);Capture(camera,Path.Combine(output,"uniform-stage3.png"));
            }
            finally{InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=oldBehavior;Profiler.enabled=oldProfiler;Application.runInBackground=oldBackground;Time.captureDeltaTime=oldCapture;QualitySettings.vSyncCount=oldVsync;Application.targetFrameRate=oldTarget;}
            yield return new ExitPlayMode();
        }
        static string Arg(){var a=Environment.GetCommandLineArgs();return a[Array.IndexOf(a,"-stage4Output")+1];}
        static void Capture(Camera camera,string path)
        {var target=new RenderTexture(1920,1080,24);var old=camera.targetTexture;var oldRect=camera.rect;var active=RenderTexture.active;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);try{camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=16f/9;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}finally{camera.targetTexture=old;camera.rect=oldRect;RenderTexture.active=active;UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);}}
    }
}
