using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public sealed class DirtMaskVariantSet
{
    public DirtExposureMask exposureMask;
    public TileBase[] variants = Array.Empty<TileBase>();
}

[CreateAssetMenu(menuName = "SporeGobbo/Terrain/Visual Palette")]
public sealed class TerrainVisualPalette : ScriptableObject
{
    public TileBase[] floorVariants = Array.Empty<TileBase>();
    public DirtMaskVariantSet[] dirtMasks = Array.Empty<DirtMaskVariantSet>();

    public bool Validate(out string summary)
    {
        bool valid = true;
        bool[] masks = new bool[16];
        int errors = 0;
        if (floorVariants == null || floorVariants.Length < 5) { valid = false; errors++; }
        else for (int i = 0; i < 5; i++) if (floorVariants[i] == null) { valid = false; errors++; }

        if (dirtMasks == null) { valid = false; errors++; }
        else foreach (DirtMaskVariantSet set in dirtMasks)
        {
            if (set == null) { valid = false; errors++; continue; }
            int mask = (int)set.exposureMask;
            if (mask < 0 || mask > 15 || masks[mask]) { valid = false; errors++; continue; }
            masks[mask] = true;
            if (set.variants == null || set.variants.Length < 5) { valid = false; errors++; continue; }
            for (int i = 0; i < 5; i++) if (set.variants[i] == null) { valid = false; errors++; }
        }
        for (int mask = 0; mask < 16; mask++) if (!masks[mask]) { valid = false; errors++; }
        summary = valid ? "16 dirt masks x 5 variants and 5 floor variants are assigned." : $"Terrain palette has {errors} assignment error(s).";
        return valid;
    }
}
