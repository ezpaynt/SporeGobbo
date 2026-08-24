#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class TerrainPresentationInstaller
{
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    const string CampScenePath = "Assets/Scenes/CampScene.unity";

    [MenuItem("Terrain Tools/Install Shared Terrain Presentation")]
    public static void InstallSharedTerrainPresentation()
    {
        TerrainPaletteSynchronizer.SynchronizeDefaultCavePalette();
        TerrainVisualPalette palette = AssetDatabase.LoadAssetAtPath<TerrainVisualPalette>(TerrainPaletteSynchronizer.PalettePath);
        InstallSampleScene(palette);
        InstallCampScene(palette);
        TerrainValidationGallery.CreateOrUpdateGallery();
        TerrainPaletteValidator.ValidateDefaultPalette();
        TerrainSceneValidator.ValidateTerrainScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("Shared terrain presentation installation complete.");
    }

    static void InstallSampleScene(TerrainVisualPalette palette)
    {
        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        MapGenerator map = Object.FindAnyObjectByType<MapGenerator>(FindObjectsInactive.Include);
        if (map == null) throw new System.InvalidOperationException("SampleScene MapGenerator was not found.");
        Grid grid = map.grid != null ? map.grid : Object.FindAnyObjectByType<Grid>(FindObjectsInactive.Include);
        if (grid == null) throw new System.InvalidOperationException("SampleScene Grid was not found.");

        Tilemap blocked = EnsurePresentationTilemap(grid, "BlockedTerrainPresentation", 1, map.dirtTilemap != null ? map.dirtTilemap.gameObject.layer : 0);
        Tilemap floor = EnsurePresentationTilemap(grid, "FloorPresentation", -8, map.dirtTilemap != null ? map.dirtTilemap.gameObject.layer : 0);
        MapGeneratorVisualSource source = GetOrAdd<MapGeneratorVisualSource>(map.gameObject);
        source.mapGenerator = map;
        TerrainPresentationRenderer renderer = GetOrAdd<TerrainPresentationRenderer>(map.gameObject);
        renderer.visualSourceBehaviour = source;
        renderer.palette = palette;
        renderer.blockedTerrainPresentation = blocked;
        renderer.floorPresentation = floor;
        map.terrainPresentationRenderer = renderer;

        RemoveLegacyComponentByName(map.gameObject, "CaveSurfaceRenderer");
        TilemapRenderer oldRenderer = map.dirtTilemap != null ? map.dirtTilemap.GetComponent<TilemapRenderer>() : null;
        if (oldRenderer != null) oldRenderer.enabled = false;

        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(map);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void InstallCampScene(TerrainVisualPalette palette)
    {
        Scene scene = EditorSceneManager.OpenScene(CampScenePath, OpenSceneMode.Single);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        if (terrain == null) throw new System.InvalidOperationException("CampScene HandcraftedCampTerrain was not found.");
        Grid grid = terrain.grid;
        if (grid == null) throw new System.InvalidOperationException("CampTerrainGrid was not found.");

        Tilemap blocked = EnsurePresentationTilemap(grid, "BlockedTerrainPresentation", 4, terrain.diggableDirtTilemap != null ? terrain.diggableDirtTilemap.gameObject.layer : 0);
        Tilemap floor = EnsurePresentationTilemap(grid, "FloorPresentation", 1, terrain.diggableDirtTilemap != null ? terrain.diggableDirtTilemap.gameObject.layer : 0);
        CampTerrainVisualSource source = GetOrAdd<CampTerrainVisualSource>(terrain.gameObject);
        source.campTerrain = terrain;
        TerrainPresentationRenderer renderer = GetOrAdd<TerrainPresentationRenderer>(terrain.gameObject);
        renderer.visualSourceBehaviour = source;
        renderer.palette = palette;
        renderer.blockedTerrainPresentation = blocked;
        renderer.floorPresentation = floor;
        terrain.terrainPresentationRenderer = renderer;
        TilemapRenderer oldRenderer = terrain.diggableDirtTilemap != null ? terrain.diggableDirtTilemap.GetComponent<TilemapRenderer>() : null;
        if (oldRenderer != null) oldRenderer.enabled = false;

        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(terrain);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static Tilemap EnsurePresentationTilemap(Grid grid, string objectName, int sortingOrder, int layer)
    {
        foreach (Tilemap existing in grid.GetComponentsInChildren<Tilemap>(true))
            if (existing.name == objectName)
            {
                Configure(existing, sortingOrder, layer);
                return existing;
            }
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(grid.transform, false);
        Tilemap tilemap = child.AddComponent<Tilemap>();
        child.AddComponent<TilemapRenderer>();
        Configure(tilemap, sortingOrder, layer);
        return tilemap;
    }

    static void Configure(Tilemap tilemap, int sortingOrder, int layer)
    {
        tilemap.gameObject.layer = layer;
        foreach (Collider2D collider in tilemap.GetComponents<Collider2D>()) Object.DestroyImmediate(collider, true);
        TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
        renderer.mode = TilemapRenderer.Mode.Chunk;
        renderer.sortingOrder = sortingOrder;
        EditorUtility.SetDirty(tilemap.gameObject);
        EditorUtility.SetDirty(tilemap);
        EditorUtility.SetDirty(renderer);
    }

    static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    static void RemoveLegacyComponentByName(GameObject gameObject, string typeName)
    {
        foreach (MonoBehaviour behaviour in gameObject.GetComponents<MonoBehaviour>())
        {
            if (behaviour != null && behaviour.GetType().Name == typeName)
                Object.DestroyImmediate(behaviour, true);
        }
    }
}
#endif
