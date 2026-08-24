#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class TerrainValidationGallery
{
    public const string GalleryPath = "Assets/Editor/TerrainValidation/TerrainPaletteValidation.unity";

    [MenuItem("Terrain Tools/Create or Update Validation Gallery")]
    public static void CreateOrUpdateGallery()
    {
        TerrainVisualPalette palette = AssetDatabase.LoadAssetAtPath<TerrainVisualPalette>(TerrainPaletteSynchronizer.PalettePath);
        string summary = "Palette asset is missing.";
        if (palette == null || !palette.Validate(out summary)) { Debug.LogError("Cannot create terrain gallery: " + summary); return; }
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "TerrainPaletteValidation";

        GameObject root = new GameObject("Terrain Palette Validation - Editor Only");
        Grid grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(0.6f, 0.6f, 1f);
        Tilemap floor = CreateTilemap(root.transform, "FloorPresentation", 0);
        Tilemap blocked = CreateTilemap(root.transform, "BlockedTerrainPresentation", 1);

        for (int mask = 0; mask < 16; mask++)
        {
            DirtMaskVariantSet set = FindSet(palette, mask);
            GameObject label = new GameObject($"Mask {mask:00} - {(DirtExposureMask)mask}");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0, mask * 2.4f, 0);
            for (int variant = 0; variant < 5; variant++)
            {
                Vector3Int cell = new Vector3Int(variant * 2, mask * 4, 0);
                blocked.SetTile(cell, set.variants[variant]);
                floor.SetTile(cell + Vector3Int.down, palette.floorVariants[variant]);
            }
        }

        // Controlled adjacent transitions and tint samples.
        DirtMaskVariantSet none = FindSet(palette, 0);
        for (int x = 0; x < 16; x++)
        {
            Vector3Int cell = new Vector3Int(14 + x, x % 4, 0);
            blocked.SetTile(cell, FindSet(palette, x).variants[x % 5]);
            blocked.RemoveTileFlags(cell, TileFlags.LockColor);
            blocked.SetColor(cell, Color.Lerp(new Color(0.65f, 0.8f, 0.62f), new Color(0.72f, 0.45f, 0.34f), x / 15f));
        }
        for (int x = 0; x < 5; x++) floor.SetTile(new Vector3Int(14 + x, -3, 0), palette.floorVariants[x]);
        blocked.SetTile(new Vector3Int(14, -2, 0), none.variants[0]);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GalleryPath);
        Debug.Log("Terrain validation gallery created outside Build Settings: " + GalleryPath);
    }

    static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        Tilemap tilemap = gameObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = gameObject.AddComponent<TilemapRenderer>();
        renderer.mode = TilemapRenderer.Mode.Chunk;
        renderer.sortingOrder = sortingOrder;
        return tilemap;
    }

    static DirtMaskVariantSet FindSet(TerrainVisualPalette palette, int mask)
    {
        foreach (DirtMaskVariantSet set in palette.dirtMasks) if ((int)set.exposureMask == mask) return set;
        return null;
    }
}
#endif
