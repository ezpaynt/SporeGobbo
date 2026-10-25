using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
namespace SporeGobbo.Geometry.Preview.Editor
{
    public static class Stage47PreviewTools
    {
        public const string ScenePath=GeometryPreviewSceneTools.Root+"/Scenes/CaveGeometryStage47.unity";
        [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 4.7 Large World")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){if(!File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}}
        public static void Install()
        {
            if(File.Exists(ScenePath)){EditorSceneManager.OpenScene(ScenePath);return;}
            EditorSceneManager.OpenScene(Stage45PreviewTools.ScenePath);var reference=UnityEngine.Object.FindAnyObjectByType<PlayableCavePreview>();var materials=reference.layerMaterials;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=new GameObject("Stage 4.7 isolated streaming host");
            // Legacy backend's required host is disabled; none of its finite-fixture initialization runs.
            var gpu=go.AddComponent<Stage45GpuProjection>();go.GetComponent<Stage4CameraPreview>().enabled=false;go.GetComponent<PlayableCavePreview>().enabled=false;go.GetComponent<CaveGeometryPreview>().enabled=false;
            gpu.projectionShader=AssetDatabase.LoadAssetAtPath<Shader>(GeometryPreviewSceneTools.Root+"/Runtime/Stage45Projection.shader");gpu.parityShader=AssetDatabase.LoadAssetAtPath<ComputeShader>(GeometryPreviewSceneTools.Root+"/Runtime/Stage45Parity.compute");
            var camera=new GameObject("Large-world preview camera").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.03f,.035f);camera.transform.position=new Vector3(0,0,-10);
            var preview=go.AddComponent<Stage47Preview>();preview.materials=materials;preview.previewCamera=camera;
            go.AddComponent<Stage47Benchmark>();EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("Stage 4.7 installed: "+ScenePath);
        }
        public static void BuildDevelopmentPlayer()
        {
            Install();var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-stage47BuildOutput");if(index<0)throw new ArgumentException("-stage47BuildOutput required");string path=args[index+1];Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"build-result.txt"),report.summary.result+"; "+report.summary.totalSize+" bytes; "+report.summary.totalTime);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage 4.7 build failed");
        }
    }
}
