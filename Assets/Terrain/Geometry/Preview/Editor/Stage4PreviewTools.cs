using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview.Editor
{
    public static class Stage4PreviewTools
    {
        public const string ScenePath=GeometryPreviewSceneTools.Root+"/Scenes/CaveGeometryStage4.unity";
        [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 4 Compressed Camera")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){if(!System.IO.File.Exists(ScenePath))Install();EditorSceneManager.OpenScene(ScenePath);}}
        public static void Install()
        {
            if(!System.IO.File.Exists(ScenePath))AssetDatabase.CopyAsset(PlayablePreviewTools.ScenePath,ScenePath);
            var scene=EditorSceneManager.OpenScene(ScenePath);var live=Object.FindAnyObjectByType<PlayableCavePreview>();
            if(live.GetComponent<Stage4CameraPreview>()==null)live.gameObject.AddComponent<Stage4CameraPreview>();
            live.footprint=3;live.patchSize=16;live.followPlayer=true;
            live.GetComponent<CaveGeometryPreview>().previewCamera.aspect=16f/9;
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("Stage 4 isolated preview installed: "+ScenePath);
        }
    }
}
