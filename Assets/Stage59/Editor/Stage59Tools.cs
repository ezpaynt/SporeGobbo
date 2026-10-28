using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using SporeGobbo.CampCandidate.Preview;
using SporeGobbo.Input;

public static class Stage59Tools
{
    public const string ScenePath="Assets/Stage59/RealActorStage59.unity";
    [MenuItem("Tools/Spore Gobbo/Stage 5.9/Open Real Actor Proof")]
    public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene(ScenePath);}
    public static void Install()
    {
        if(!File.Exists(ScenePath))AssetDatabase.CopyAsset("Assets/Terrain/Camp/Scenes/FutureCampStage58Profile.unity",ScenePath);
        var scene=EditorSceneManager.OpenScene(ScenePath);var camp=UnityEngine.Object.FindAnyObjectByType<Stage57Preview>();
        foreach(var b in camp.GetComponents<MonoBehaviour>())if(b is Stage58Benchmark||b is Stage57Benchmark)UnityEngine.Object.DestroyImmediate(b);
        var proof=camp.GetComponent<Stage59Preview>();if(proof==null)proof=camp.gameObject.AddComponent<Stage59Preview>();
        proof.PlayerPrefab=MakeActorPrefab("Assets/Gobbo.prefab",true);proof.BuddyPrefab=MakeActorPrefab("Assets/GobboBuddy.prefab",false);proof.SpriteShader=Shader.Find("SporeGobbo/Stage59/ProjectedSprite");
        // Read only: clone serialized visual identity data, never save the production scene.
        var production=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
        var database=production.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<GobboVisualDatabase>(true)).FirstOrDefault();
        if(database==null)throw new Exception("Production visual database not found");
        proof.VisualSets=database.visualSets;
        var reader=production.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<SporeInputReader>(true)).FirstOrDefault();if(reader==null)throw new Exception("Production input bootstrap not found");
        SceneManager.SetActiveScene(scene);var existing=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<SporeInputReader>(true)).FirstOrDefault();if(existing==null){var inputGo=new GameObject("Stage59 production semantic input bootstrap");existing=inputGo.AddComponent<SporeInputReader>();}EditorUtility.CopySerialized(reader,existing);
        EditorSceneManager.CloseScene(production,true);SceneManager.SetActiveScene(scene);
        if(camp.GetComponent<Stage59Benchmark>()==null)camp.gameObject.AddComponent<Stage59Benchmark>();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static GameObject MakeActorPrefab(string path,bool player)
    {var root=PrefabUtility.LoadPrefabContents(path);try{foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true)){bool keep=c is GobboVisualController||c is BuddyDirectionalSprite||(player?c is GobboController:c is BuddyFollow||c is BuddyDigAbility);if(!keep)UnityEngine.Object.DestroyImmediate(c);}return PrefabUtility.SaveAsPrefabAsset(root,player?"Assets/Stage59/IsolatedGobbo.prefab":"Assets/Stage59/IsolatedBuddy.prefab");}finally{PrefabUtility.UnloadPrefabContents(root);}}
    public static void Build()
    {
        Install();var args=Environment.GetCommandLineArgs();string output=Path.GetFullPath(args[Array.IndexOf(args,"-stage59BuildOutput")+1]);Directory.CreateDirectory(Path.GetDirectoryName(output));
        var lines=new System.Collections.Generic.List<string>();foreach(var path in new[]{"Assets/Gobbo.prefab","Assets/GobboBuddy.prefab","Assets/Stage59/IsolatedGobbo.prefab","Assets/Stage59/IsolatedBuddy.prefab"}){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);lines.Add(path);foreach(var c in prefab.GetComponentsInChildren<Component>(true))lines.Add("  "+(c!=null?c.GetType().FullName:"Missing script"));}File.WriteAllLines(Path.Combine(Path.GetDirectoryName(output),"actor-components.txt"),lines);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName=output,options=BuildOptions.Development});
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-result.txt"),report.summary.result.ToString());if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage59 build failed");
    }
}
