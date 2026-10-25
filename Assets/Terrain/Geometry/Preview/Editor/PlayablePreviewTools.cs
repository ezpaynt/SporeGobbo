using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SporeGobbo.Geometry.Preview.Editor
{
    public static class PlayablePreviewTools
    {
        public const string ScenePath=GeometryPreviewSceneTools.Root+"/Scenes/CaveGeometryPreview.unity";
        [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 3 Playable Preview")]
        public static void Open()
        {if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
        public static void Install()
        {
            if(!File.Exists(GeometryPreviewSceneTools.ScenePath))
            {
                if(!AssetDatabase.CopyAsset(ScenePath,GeometryPreviewSceneTools.ScenePath))throw new Exception("Could not preserve Stage 2 scene");
            }
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var display=UnityEngine.Object.FindAnyObjectByType<CaveGeometryPreview>();
            var live=display.GetComponent<PlayableCavePreview>()??display.gameObject.AddComponent<PlayableCavePreview>();
            live.fixture=AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryPreviewSceneTools.FixturePath);
            live.layerMaterials=Enum.GetValues(typeof(PreviewLayer)).Cast<PreviewLayer>().Select(layer=>AssetDatabase.LoadAssetAtPath<Material>(GeometryPreviewSceneTools.Root+"/Generated/Preview_"+layer+".mat")).ToArray();
            display.gameObject.name="Cave Geometry Preview — Stage 3 isolated experiment";
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("Stage 3 installed. Stage 2 preserved at "+GeometryPreviewSceneTools.ScenePath);
        }
        [Serializable] public sealed class Measurement
        {
            public bool fullRebuild;public string brush;
            public int patchSize,footprint,successfulDigs,liveMeshes,liveObjects,activeRenderers,totalSceneObjects,vertices,triangles,maxPatches;
            public double averageMs,worstMs,averageAllocatedKiB,totalStepMs,averagePatches,averageStepMs,worstStepMs;
            public float distanceMoved;
        }
        [Serializable] public sealed class Report {public string environment;public Measurement[] measurements;}
        public static void CaptureAndBenchmark()
        {
            Install();string output=Argument("-stage3Output");Directory.CreateDirectory(output);
            var results=new System.Collections.Generic.List<Measurement>();
            foreach(bool full in new[]{false,true})foreach(int patch in new[]{8,16})foreach(int body in new[]{1,3,9})
            {
                if(full&&body!=3)continue;
                EditorSceneManager.OpenScene(ScenePath);var live=UnityEngine.Object.FindAnyObjectByType<PlayableCavePreview>();
                live.patchSize=patch;live.fullRebuildComparison=full;live.Initialize();live.SetFootprint(body);live.brush=body==1?DigBrush.Single:body==3?DigBrush.ThreeWide:DigBrush.Radius;live.digRadius=body*.707107f+.8f;live.followPlayer=true;
                Vector2 start=live.Position;long allocation=0;int maxPatches=0,patchSum=0;bool allocationAvailable=true;double stepTotal=0,worstStep=0;
                var elapsed=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<480;i++)
                {
                    var stepWatch=System.Diagnostics.Stopwatch.StartNew();int old=live.Session.DigCount;live.Step(new Vector2(0,-1),new Vector2(0,-1),true,1f/60);live.DrawOverlay();stepWatch.Stop();stepTotal+=stepWatch.Elapsed.TotalMilliseconds;worstStep=Math.Max(worstStep,stepWatch.Elapsed.TotalMilliseconds);
                    if(i==179){elapsed.Stop();live.DrawOverlay();Capture(live.GetComponent<CaveGeometryPreview>().previewCamera,Path.Combine(output,$"digging-{patch}-{body}-{(full?"full":"incremental")}.png"));elapsed.Start();}
                    if(live.Session.DigCount!=old){allocationAvailable&=live.LastUpdateAllocatedBytes>=0;allocation+=Math.Max(0,live.LastUpdateAllocatedBytes);patchSum+=live.Session.LastPatchCount;maxPatches=Math.Max(maxPatches,live.Session.LastPatchCount);}
                }
                elapsed.Stop();live.DrawOverlay();
                results.Add(new Measurement{brush=live.brush.ToString(),averageStepMs=stepTotal/480,worstStepMs=worstStep,fullRebuild=full,activeRenderers=live.ActiveSceneMeshCount,totalSceneObjects=live.TotalSceneObjectCount,averagePatches=live.Session.DigCount==0?0:patchSum/(double)live.Session.DigCount,patchSize=patch,footprint=body,successfulDigs=live.Session.DigCount,liveMeshes=live.RuntimeMeshCount,liveObjects=live.RuntimeObjectCount,vertices=live.VertexCount,triangles=live.TriangleCount,maxPatches=maxPatches,averageMs=live.AverageUpdateMilliseconds,worstMs=live.WorstUpdateMilliseconds,averageAllocatedKiB=!allocationAvailable?-1:live.Session.DigCount==0?0:allocation/1024.0/live.Session.DigCount,totalStepMs=elapsed.Elapsed.TotalMilliseconds,distanceMoved=Vector2.Distance(start,live.Position)});
                if(!full&&patch==16&&body==3)
                {
                    live.GetComponent<CaveGeometryPreview>().Overview();live.showPatchBoundaries=true;live.DrawOverlay();Capture(live.GetComponent<CaveGeometryPreview>().previewCamera,Path.Combine(output,"excavated-overview.png"));
                }
            }
            File.WriteAllText(Path.Combine(output,"benchmark.json"),JsonUtility.ToJson(new Report{environment=SystemInfo.operatingSystem+"; "+SystemInfo.processorType+"; Unity "+Application.unityVersion+" Editor synchronous CPU build + Mesh upload; 480 simulated 60 Hz movement/dig steps with diagnostic mesh upload per scenario; excludes IMGUI, presentation and GPU frame time; allocation KiB -1 means Mono allocation counter unavailable; totalStepMs excludes diagnostic capture",measurements=results.ToArray()},true));
            EditorSceneManager.OpenScene(ScenePath);Debug.Log("Stage 3 benchmark and captures: "+output);
        }
        static string Argument(string name){var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];throw new ArgumentException(name+" required");}
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1600,1000,24);var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
