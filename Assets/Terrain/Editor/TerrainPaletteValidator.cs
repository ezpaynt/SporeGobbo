#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TerrainPaletteValidator
{
    [MenuItem("Terrain Tools/Validate Default Cave Palette")]
    public static void ValidateDefaultPalette()
    {
        TerrainVisualPalette palette = AssetDatabase.LoadAssetAtPath<TerrainVisualPalette>(TerrainPaletteSynchronizer.PalettePath);
        int errors = 0, warnings = 0;
        if (palette == null) { Debug.LogError("Default Cave terrain palette is missing."); return; }
        if (!palette.Validate(out string summary)) { Debug.LogError(summary, palette); errors++; }

        HashSet<int> masks = new HashSet<int>();
        if (palette.dirtMasks != null)
            foreach (DirtMaskVariantSet set in palette.dirtMasks)
            {
                if (set == null) { errors++; continue; }
                int mask = (int)set.exposureMask;
                if (!masks.Add(mask)) { Debug.LogError($"Duplicate dirt mask {mask}.", palette); errors++; }
                if (set.variants == null || set.variants.Length < 5) { errors++; continue; }
                for (int i = 0; i < set.variants.Length; i++) ValidateTile(set.variants[i], $"mask {mask} variant {i + 1}", ref errors, ref warnings);
            }
        if (palette.floorVariants != null)
            for (int i = 0; i < palette.floorVariants.Length; i++) ValidateTile(palette.floorVariants[i], $"floor variant {i + 1}", ref errors, ref warnings);
        Debug.Log($"Terrain palette validation: errors={errors} warnings={warnings}. {summary}", palette);
    }

    static void ValidateTile(TileBase tileBase, string context, ref int errors, ref int warnings)
    {
        Tile tile = tileBase as Tile;
        if (tile == null) { Debug.LogError($"{context}: Tile is missing or is not a standard Tile."); errors++; return; }
        if (tile.sprite == null) { Debug.LogError($"{context}: Sprite is missing.", tile); errors++; return; }
        if (tile.color != Color.white) { Debug.LogError($"{context}: Tile color must be white.", tile); errors++; }
        if (tile.colliderType != Tile.ColliderType.None) { Debug.LogError($"{context}: presentation Tile collider must be None.", tile); errors++; }
        string spritePath = AssetDatabase.GetAssetPath(tile.sprite);
        TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (importer == null) { errors++; return; }
        if (Mathf.Abs(importer.spritePixelsPerUnit - 320f) > 0.01f) { Debug.LogError($"{context}: PPU is {importer.spritePixelsPerUnit}, expected 320.", tile); errors++; }
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
        if (texture == null || texture.width != 256 || texture.height != 256) { Debug.LogError($"{context}: canvas must be 256x256.", tile); errors++; }
        if ((tile.sprite.pivot - new Vector2(128f, 128f)).sqrMagnitude > 0.01f) { Debug.LogError($"{context}: pivot is not centered.", tile); errors++; }
        string tileName = tile.name;
        if (!spritePath.Contains(tileName)) { Debug.LogWarning($"{context}: Tile and Sprite names do not match.", tile); warnings++; }
    }
}
#endif
