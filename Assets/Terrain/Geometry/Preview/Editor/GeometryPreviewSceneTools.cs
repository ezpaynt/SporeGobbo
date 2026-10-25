using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SporeGobbo.Geometry.Preview.Editor
{
    public static class GeometryPreviewSceneTools
    {
        public const string Root="Assets/Terrain/Geometry/Preview";
        public const string ScenePath=Root+"/Scenes/CaveGeometryStage2Reference.unity";
        public const string FixturePath=Root+"/Fixtures/SuppliedCave96x62.txt";

        [MenuItem("Tools/Spore Gobbo/Geometry/Open Stage 2 Preview")]
        public static void OpenPreview()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath))BuildScene();else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Spore Gobbo/Geometry/Rebuild Stage 2 Preview Assets")]
        public static void RebuildMenu()
        { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())BuildScene(); }

        public static void BuildScene()
        {
            var fixtureAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath);
            if(fixtureAsset==null)throw new FileNotFoundException("Supplied geometry fixture missing",FixturePath);
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Runtime/PreviewFlat.shader");
            if(shader==null)throw new InvalidOperationException("Preview shader missing");
            EnsureFolder(Root+"/Generated");EnsureFolder(Root+"/Scenes");
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Cave Geometry Preview — Stage 2 only");
            var controls=root.AddComponent<CaveGeometryPreview>();
            var cameraObject=new GameObject("Preview Camera");var camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.14f,.15f);
            camera.nearClipPlane=.1f;camera.farClipPlane=50;camera.allowHDR=false;camera.allowMSAA=false;
            controls.previewCamera=camera;controls.Overview();
            var layers=new Dictionary<PreviewLayer,List<MeshRenderer>>();
            foreach(PreviewLayer layer in Enum.GetValues(typeof(PreviewLayer)))layers[layer]=new List<MeshRenderer>();
            var materials=new Dictionary<PreviewLayer,Material>();
            foreach(PreviewLayer layer in Enum.GetValues(typeof(PreviewLayer)))
            {
                var material=new Material(shader){name="Preview_"+layer,color=ColorFor(layer)};
                materials[layer]=SaveAsset(material,Root+"/Generated/"+material.name+".mat");
            }
            var meshData=PreviewMeshBuilder.Build(new PreviewFixture(fixtureAsset.text));
            foreach(var data in meshData)
            {
                Mesh mesh=SaveAsset(data.CreateMesh(),Root+"/Generated/"+data.Name+".asset");
                var owner=new GameObject(data.Name);owner.transform.SetParent(root.transform,false);
                owner.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=owner.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[data.Layer];renderer.sortingOrder=OrderFor(data.Layer);
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                layers[data.Layer].Add(renderer);
            }
            controls.gridRenderers=layers[PreviewLayer.Grid].ToArray();controls.contourRenderers=layers[PreviewLayer.Contours].ToArray();
            controls.wallRenderers=layers[PreviewLayer.Faces].ToArray();controls.lipRenderers=layers[PreviewLayer.Lips].ToArray();
            controls.ApplyVisibility();EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("Stage 2 preview built: "+meshData.Count+" batched meshes, "+meshData.Sum(m=>m.Indices.Count/3)+" triangles. Scene: "+ScenePath);
        }

        public static void BuildSceneAndCapture()
        {
            BuildScene();
            var controls=UnityEngine.Object.FindAnyObjectByType<CaveGeometryPreview>();
            string output=Argument("-geometryPreviewOutput");
            if(string.IsNullOrWhiteSpace(output))throw new ArgumentException("Supply -geometryPreviewOutput with an output directory");
            Directory.CreateDirectory(output);
            controls.Overview();Capture(controls.previewCamera,Path.Combine(output,"overview.png"));
            controls.TunnelDetail();Capture(controls.previewCamera,Path.Combine(output,"tunnel.png"));
            controls.ChamberDetail();Capture(controls.previewCamera,Path.Combine(output,"gobbo-pillar.png"));
            controls.showContours=false;controls.ApplyVisibility();Capture(controls.previewCamera,Path.Combine(output,"joins-without-outline.png"));
            controls.showContours=true;controls.showGrid=true;controls.ApplyVisibility();Capture(controls.previewCamera,Path.Combine(output,"grid-detail.png"));
            controls.showGrid=false;controls.Overview();controls.ApplyVisibility();
            // Capture views are diagnostic only; the saved scene retains its original overview defaults.
            Debug.Log("Stage 2 captures: "+output);
        }
        static string Argument(string name)
        { var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];return null; }
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1920,1200,24,RenderTextureFormat.ARGB32);
            var image=new Texture2D(1920,1200,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
        static T SaveAsset<T>(T value,string path) where T:UnityEngine.Object
        {
            T existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(value);return existing;
        }
        static void EnsureFolder(string path)
        { if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }
        static int OrderFor(PreviewLayer layer)
        { switch(layer){case PreviewLayer.Floor:return 0;case PreviewLayer.Solid:return 1;case PreviewLayer.Faces:return 10;case PreviewLayer.Lips:return 11;case PreviewLayer.Contours:return 12;case PreviewLayer.Grid:return 13;default:return 14;} }
        static Color ColorFor(PreviewLayer layer)
        {
            switch(layer)
            {
                case PreviewLayer.Floor:return new Color(.51f,.60f,.58f);
                case PreviewLayer.Solid:return new Color(.45f,.32f,.24f);
                case PreviewLayer.Faces:return new Color(.25f,.17f,.12f);
                case PreviewLayer.Lips:return new Color(.64f,.46f,.30f);
                case PreviewLayer.Contours:return new Color(.12f,.10f,.08f);
                case PreviewLayer.Grid:return new Color(.85f,.91f,.87f,.24f);
                default:return new Color(.98f,.66f,.21f);
            }
        }
    }
}
