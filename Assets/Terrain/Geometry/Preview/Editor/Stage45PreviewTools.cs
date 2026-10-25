using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview.Editor
{
    public static class Stage45PreviewTools
    {
        public const string ScenePath=GeometryPreviewSceneTools.Root+"/Scenes/CaveGeometryStage45.unity";
        [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 4.5 GPU and Actors")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}}
        public static void Install()
        {
            if(!File.Exists(ScenePath))AssetDatabase.CopyAsset(Stage4PreviewTools.ScenePath,ScenePath);
            var scene=EditorSceneManager.OpenScene(ScenePath);var view=UnityEngine.Object.FindAnyObjectByType<Stage4CameraPreview>();
            var gpu=view.GetComponent<Stage45GpuProjection>()??view.gameObject.AddComponent<Stage45GpuProjection>();
            gpu.projectionShader=AssetDatabase.LoadAssetAtPath<Shader>(GeometryPreviewSceneTools.Root+"/Runtime/Stage45Projection.shader");
            gpu.parityShader=AssetDatabase.LoadAssetAtPath<ComputeShader>(GeometryPreviewSceneTools.Root+"/Runtime/Stage45Parity.compute");
            var demo=view.GetComponent<Stage45MovingDemo>()??view.gameObject.AddComponent<Stage45MovingDemo>();
            demo.referenceSprites=new[]{Sprite("Assets/GobboBuddyMovement/GobboBuddyFront.PNG"),Sprite("Assets/Resources/weevilFront.png"),Sprite("Assets/GobboBuddyMovement/GobboBuddyFrontLeft.PNG"),Sprite("Assets/Resources/weevilBack.png")};
            if(view.GetComponent<Stage45Benchmark>()==null)view.gameObject.AddComponent<Stage45Benchmark>();
            gpu.useGpu=true;view.Preset(2);view.settings.coverage=.55f;EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("Stage 4.5 isolated scene: "+ScenePath);
        }
        static Sprite Sprite(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        public static void BuildDevelopmentPlayer()
        {
            Install();var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-stage45BuildOutput");if(i<0)throw new ArgumentException("-stage45BuildOutput required");
            string path=args[i+1];Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"build-result.txt"),report.summary.result+"; "+report.summary.totalSize+" bytes; "+report.summary.totalTime);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage 4.5 Development build failed: "+report.summary.result);
        }
    }
}
