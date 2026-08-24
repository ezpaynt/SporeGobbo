#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class TerrainSceneValidator
{
    [MenuItem("Terrain Tools/Validate Terrain Scene Wiring")]
    public static void ValidateTerrainScenes()
    {
        int errors = 0;
        ValidateScene("Assets/Scenes/SampleScene.unity", true, ref errors);
        ValidateScene("Assets/Scenes/CampScene.unity", false, ref errors);
        Debug.Log($"Terrain scene wiring validation complete: errors={errors}.");
    }

    static void ValidateScene(string path, bool runScene, ref int errors)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        TerrainPresentationRenderer renderer = Object.FindAnyObjectByType<TerrainPresentationRenderer>(FindObjectsInactive.Include);
        if (renderer == null) { Debug.LogError($"{path}: TerrainPresentationRenderer missing."); errors++; return; }
        if (renderer.palette == null) { Debug.LogError($"{path}: palette missing.", renderer); errors++; }
        if (!(renderer.visualSourceBehaviour is ITerrainVisualSource)) { Debug.LogError($"{path}: visual source is invalid.", renderer); errors++; }
        ValidateMap(renderer.blockedTerrainPresentation, "BlockedTerrainPresentation", path, ref errors);
        ValidateMap(renderer.floorPresentation, "FloorPresentation", path, ref errors);
        if (Object.FindObjectsByType<TerrainPresentationRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1) { Debug.LogError($"{path}: expected exactly one shared terrain renderer."); errors++; }
        foreach (Tilemap tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (tilemap.name.Contains("RearSlope")) { Debug.LogError($"{path}: legacy rear-slope Tilemap remains: {tilemap.name}", tilemap); errors++; }
        if (runScene)
        {
            MapGenerator map = Object.FindAnyObjectByType<MapGenerator>(FindObjectsInactive.Include);
            if (map == null || map.terrainPresentationRenderer != renderer) { Debug.LogError($"{path}: MapGenerator renderer reference is invalid."); errors++; }
            if (map != null && HasComponentNamed(map.gameObject, "CaveSurfaceRenderer")) { Debug.LogError($"{path}: legacy CaveSurfaceRenderer is still present."); errors++; }
            TilemapRenderer oldDirt = map != null && map.dirtTilemap != null ? map.dirtTilemap.GetComponent<TilemapRenderer>() : null;
            if (oldDirt == null || oldDirt.enabled) { Debug.LogError($"{path}: authoritative Run Dirt renderer must exist but be visually disabled."); errors++; }
        }
        else
        {
            HandcraftedCampTerrain camp = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
            if (camp == null || camp.terrainPresentationRenderer != renderer) { Debug.LogError($"{path}: Camp renderer reference is invalid."); errors++; }
            TilemapRenderer oldDirt = camp != null && camp.diggableDirtTilemap != null ? camp.diggableDirtTilemap.GetComponent<TilemapRenderer>() : null;
            if (oldDirt == null || oldDirt.enabled) { Debug.LogError($"{path}: authoritative Camp diggable renderer must exist but be visually disabled."); errors++; }
            GameObject frame = GameObject.Find("ForegroundFrame");
            SpriteRenderer frameRenderer = frame != null ? frame.GetComponent<SpriteRenderer>() : null;
            if (frameRenderer == null || frameRenderer.sortingOrder != 300) { Debug.LogError($"{path}: Camp ForegroundFrame sorting changed."); errors++; }
        }
    }

    static void ValidateMap(Tilemap tilemap, string expectedName, string path, ref int errors)
    {
        if (tilemap == null || tilemap.name != expectedName) { Debug.LogError($"{path}: {expectedName} missing."); errors++; return; }
        if (tilemap.GetComponent<Collider2D>() != null) { Debug.LogError($"{path}: {expectedName} must not have a collider.", tilemap); errors++; }
        TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
        if (renderer == null || renderer.mode != TilemapRenderer.Mode.Chunk) { Debug.LogError($"{path}: {expectedName} must use Chunk mode.", tilemap); errors++; }
    }

    static bool HasComponentNamed(GameObject gameObject, string typeName)
    {
        foreach (MonoBehaviour behaviour in gameObject.GetComponents<MonoBehaviour>())
        {
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return true;
        }

        return false;
    }
}
#endif
