#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerrainProjectReferenceValidator
{
    [MenuItem("Terrain Tools/Validate Project Scenes And Prefabs")]
    public static void Validate()
    {
        var issues = new List<string>();
        int sceneCount = 0, prefabCount = 0;
        foreach (EditorBuildSettingsScene setting in EditorBuildSettings.scenes)
        {
            if (!setting.enabled) continue;
            Scene scene = EditorSceneManager.OpenScene(setting.path, OpenSceneMode.Single);
            sceneCount++;
            foreach (GameObject root in scene.GetRootGameObjects()) ScanHierarchy(root, setting.path, issues);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { prefabCount++; ScanHierarchy(root, path, issues); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        string report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "terrain-project-reference-validation.txt");
        var lines = new List<string> { $"scenes={sceneCount} prefabs={prefabCount} issues={issues.Count}" };
        lines.AddRange(issues);
        File.WriteAllLines(report, lines);
        foreach (string line in lines) Debug.Log("PROJECT REFERENCE VALIDATION | " + line);
    }

    static void ScanHierarchy(GameObject root, string assetPath, List<string> issues)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            Component[] components = transform.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null) { issues.Add($"MISSING_SCRIPT | {assetPath} | {GetPath(transform)}"); continue; }
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                if (!property.NextVisible(true)) continue;
                do
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        issues.Add($"BROKEN_REFERENCE | {assetPath} | {GetPath(transform)} | {component.GetType().Name}.{property.propertyPath}");
                }
                while (property.NextVisible(true));
            }
        }
    }

    static string GetPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }
}
#endif
