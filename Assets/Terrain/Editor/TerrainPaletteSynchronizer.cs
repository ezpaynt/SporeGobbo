#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using UnityEditor.U2D;

public static class TerrainPaletteSynchronizer
{
    public const string Root = "Assets/Terrain/Palettes/DefaultCave";
    public const string SpritesRoot = Root + "/Sprites";
    public const string TilesRoot = Root + "/Tiles";
    public const string PalettePath = Root + "/DefaultCaveTerrainPalette.asset";
    public const string AtlasPath = Root + "/DefaultCave.spriteatlas";
    const int CanvasSize = 256;
    const int LogicalMin = 32;
    const int LogicalMax = 224;
    const float PixelsPerUnit = 320f;
    static readonly string[] MaskNames =
    {
        "None", "Above", "Right", "AboveRight", "Below", "AboveBelow", "BelowRight", "AboveBelowRight",
        "Left", "AboveLeft", "LeftRight", "AboveLeftRight", "BelowLeft", "AboveBelowLeft", "BelowLeftRight", "All"
    };

    [MenuItem("Terrain Tools/Synchronize Default Cave Palette")]
    public static void SynchronizeDefaultCavePalette()
    {
        EnsureFolders();
        CreatePermanentPlaceholderPngs();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureSpriteImports();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        TerrainVisualPalette palette = AssetDatabase.LoadAssetAtPath<TerrainVisualPalette>(PalettePath);
        if (palette == null)
        {
            palette = ScriptableObject.CreateInstance<TerrainVisualPalette>();
            AssetDatabase.CreateAsset(palette, PalettePath);
        }

        palette.floorVariants = new TileBase[5];
        for (int variant = 1; variant <= 5; variant++)
        {
            string baseName = $"Floor_Open_V{variant:00}";
            palette.floorVariants[variant - 1] = EnsureTile(baseName);
        }

        palette.dirtMasks = new DirtMaskVariantSet[16];
        for (int mask = 0; mask < 16; mask++)
        {
            DirtMaskVariantSet set = new DirtMaskVariantSet
            {
                exposureMask = (DirtExposureMask)mask,
                variants = new TileBase[5]
            };
            for (int variant = 1; variant <= 5; variant++)
            {
                string baseName = DirtBaseName(mask, variant);
                set.variants[variant - 1] = EnsureTile(baseName);
            }
            palette.dirtMasks[mask] = set;
        }

        EditorUtility.SetDirty(palette);
        EnsureAtlas();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Default Cave palette synchronized: 16 masks x 5 dirt variants, 5 floor variants, white Tiles, no Tile colliders.");
    }

    [MenuItem("Terrain Tools/Retire Legacy Cave Surface Prototype")]
    public static void RetireLegacyCaveSurfacePrototype()
    {
        string[] retiredAssets =
        {
            "Assets/CaveSurfaceRenderer.cs",
            "Assets/Terrain/CaveSurface"
        };

        foreach (string path in retiredAssets)
        {
            if ((AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null || AssetDatabase.IsValidFolder(path)) &&
                !AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException($"Could not delete retired terrain prototype asset: {path}");
        }

        ConfigureSingleSpriteImport("Assets/Terrain/floor.png");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("Retired CaveSurfaceRenderer and CaveSurface prototype assets; normalized the retained floor source import metadata.");
    }

    public static string DirtBaseName(int mask, int variant) => $"Dirt_M{mask:00}_{MaskNames[mask]}_V{variant:00}";

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Terrain", "Palettes");
        EnsureFolder("Assets/Terrain/Palettes", "DefaultCave");
        EnsureFolder(Root, "Sprites");
        EnsureFolder(Root, "Tiles");
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }

    static void CreatePermanentPlaceholderPngs()
    {
        for (int mask = 0; mask < 16; mask++)
            for (int variant = 1; variant <= 5; variant++)
                WritePlaceholderIfMissing(DirtBaseName(mask, variant), mask, variant, false);
        for (int variant = 1; variant <= 5; variant++)
            WritePlaceholderIfMissing($"Floor_Open_V{variant:00}", 0, variant, true);
    }

    static void WritePlaceholderIfMissing(string baseName, int mask, int variant, bool floor)
    {
        string assetPath = SpritesRoot + "/" + baseName + ".png";
        string absolutePath = Path.GetFullPath(assetPath);
        if (File.Exists(absolutePath)) return;

        Texture2D texture = new Texture2D(CanvasSize, CanvasSize, TextureFormat.RGBA32, false);
        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32[] pixels = new Color32[CanvasSize * CanvasSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = transparent;
        texture.SetPixels32(pixels);

        Color category = floor ? new Color(0.34f + variant * 0.025f, 0.28f + variant * 0.018f, 0.20f, 1f)
            : Color.HSVToRGB(mask / 16f, 0.48f, 0.58f + variant * 0.035f);
        FillRect(texture, LogicalMin, LogicalMin, LogicalMax, LogicalMax, category);

        if (!floor)
        {
            Color opening = new Color(0.055f, 0.045f, 0.035f, 1f);
            int depth = 25 + variant;
            if ((mask & (int)DirtExposureMask.Above) != 0) FillRect(texture, LogicalMin, LogicalMax - depth, LogicalMax, LogicalMax, opening);
            if ((mask & (int)DirtExposureMask.Right) != 0) FillRect(texture, LogicalMax - depth, LogicalMin, LogicalMax, LogicalMax, opening);
            if ((mask & (int)DirtExposureMask.Below) != 0) FillRect(texture, LogicalMin, LogicalMin, LogicalMax, LogicalMin + depth, opening);
            if ((mask & (int)DirtExposureMask.Left) != 0) FillRect(texture, LogicalMin, LogicalMin, LogicalMin + depth, LogicalMax, opening);
            DrawDirectionMarkers(texture, mask);
            DrawText(texture, 89, 116, $"M{mask:00}V{variant:00}", 4, Color.white);
        }
        else
        {
            DrawText(texture, 94, 116, $"V{variant:00}", 5, Color.white);
            DrawFloorPattern(texture, variant);
        }

        texture.Apply(false, false);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    static void ConfigureSpriteImports()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                           Mathf.Abs(importer.spritePixelsPerUnit - PixelsPerUnit) > 0.01f || importer.mipmapEnabled ||
                           importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear || !importer.alphaIsTransparency;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            if (changed) importer.SaveAndReimport();
        }
    }

    static void ConfigureSingleSpriteImport(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static Tile EnsureTile(string baseName)
    {
        string tilePath = TilesRoot + "/" + baseName + ".asset";
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = baseName;
            AssetDatabase.CreateAsset(tile, tilePath);
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesRoot + "/" + baseName + ".png");
        tile.sprite = sprite;
        tile.color = Color.white;
        tile.transform = Matrix4x4.identity;
        tile.gameObject = null;
        tile.flags = TileFlags.LockTransform;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static void EnsureAtlas()
    {
        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, AtlasPath);
        }
        SpriteAtlasPackingSettings packing = atlas.GetPackingSettings();
        packing.enableRotation = false;
        packing.enableTightPacking = false;
        packing.padding = 4;
        atlas.SetPackingSettings(packing);
        SpriteAtlasTextureSettings texture = atlas.GetTextureSettings();
        texture.generateMipMaps = false;
        texture.filterMode = FilterMode.Bilinear;
        texture.sRGB = true;
        atlas.SetTextureSettings(texture);
        UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SpritesRoot);
        UnityEngine.Object[] current = atlas.GetPackables();
        if (current.Length > 0) atlas.Remove(current);
        atlas.Add(new[] { folder });
        EditorUtility.SetDirty(atlas);
    }

    static void FillRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
    {
        for (int y = Mathf.Clamp(minY, 0, CanvasSize); y < Mathf.Clamp(maxY, 0, CanvasSize); y++)
            for (int x = Mathf.Clamp(minX, 0, CanvasSize); x < Mathf.Clamp(maxX, 0, CanvasSize); x++) texture.SetPixel(x, y, color);
    }

    static void DrawDirectionMarkers(Texture2D texture, int mask)
    {
        Color c = new Color(1f, 0.85f, 0.15f, 1f);
        if ((mask & 1) != 0) FillRect(texture, 120, 210, 136, 224, c);
        if ((mask & 2) != 0) FillRect(texture, 210, 120, 224, 136, c);
        if ((mask & 4) != 0) FillRect(texture, 120, 32, 136, 46, c);
        if ((mask & 8) != 0) FillRect(texture, 32, 120, 46, 136, c);
    }

    static void DrawFloorPattern(Texture2D texture, int variant)
    {
        Color c = new Color(0.18f, 0.14f, 0.10f, 0.7f);
        for (int i = 0; i < variant + 2; i++)
        {
            int x = 48 + ((i * 37 + variant * 19) % 150);
            int y = 48 + ((i * 53 + variant * 23) % 150);
            FillRect(texture, x, y, x + 6, y + 6, c);
        }
    }

    static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
        ['V'] = new[] { "10001", "10001", "10001", "10001", "01010", "01010", "00100" },
        ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
        ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
        ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
        ['3'] = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
        ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
        ['5'] = new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" },
        ['6'] = new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" },
        ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
        ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
        ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" }
    };

    static void DrawText(Texture2D texture, int x, int y, string text, int scale, Color color)
    {
        int cursor = x;
        foreach (char ch in text)
        {
            if (!Glyphs.TryGetValue(ch, out string[] glyph)) { cursor += 6 * scale; continue; }
            for (int row = 0; row < glyph.Length; row++)
                for (int column = 0; column < glyph[row].Length; column++)
                    if (glyph[row][column] == '1') FillRect(texture, cursor + column * scale, y + (6 - row) * scale, cursor + (column + 1) * scale, y + (7 - row) * scale, color);
            cursor += 6 * scale;
        }
    }
}
#endif
