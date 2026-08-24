using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class TerrainPresentationRenderer : MonoBehaviour
{
    const int DirtVariantSalt = 0x2D71;
    const int FloorVariantSalt = 0x4F19;
    static readonly Vector2Int[] CardinalOffsets = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    static readonly ProfilerMarker BuildMarker = new ProfilerMarker("SporeGobbo.TerrainPresentation.Build");
    static readonly ProfilerMarker MaskMarker = new ProfilerMarker("SporeGobbo.TerrainPresentation.CalculateMasks");
    static readonly ProfilerMarker TilemapMarker = new ProfilerMarker("SporeGobbo.TerrainPresentation.WriteTilemaps");
    static readonly ProfilerMarker DirtyMarker = new ProfilerMarker("SporeGobbo.TerrainPresentation.FlushDirty");

    [Header("Source")]
    public MonoBehaviour visualSourceBehaviour;
    public TerrainVisualPalette palette;
    [Header("Serialized Presentation Tilemaps")]
    public Tilemap blockedTerrainPresentation;
    public Tilemap floorPresentation;

    readonly DirtMaskVariantSet[] maskLookup = new DirtMaskVariantSet[16];
    readonly HashSet<Vector2Int> dirtyCells = new HashSet<Vector2Int>();
    ITerrainVisualSource source;
    bool lookupValid;

    public int LastBuiltBlockedCells { get; private set; }
    public int LastBuiltFloorCells { get; private set; }
    public int LastDirtyCellCount { get; private set; }

    void Awake() { ResolveSource(); BuildLookup(true); }
    void LateUpdate() { FlushDirty(); }

    public bool ResolveSource()
    {
        source = visualSourceBehaviour as ITerrainVisualSource;
        if (source == null && visualSourceBehaviour != null) Debug.LogError($"{name}: visual source must implement ITerrainVisualSource.", this);
        return source != null;
    }

    public void ClearAndDisable()
    {
        dirtyCells.Clear();
        SetPresentationEnabled(false);
        blockedTerrainPresentation?.ClearAllTiles();
        floorPresentation?.ClearAllTiles();
    }

    public bool RebuildAndEnable()
    {
        using (BuildMarker.Auto())
        {
            if (!ValidateRuntimeReferences()) return false;
            BoundsInt bounds = source.VisualBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0) return false;
            SetPresentationEnabled(false);
            dirtyCells.Clear();
            blockedTerrainPresentation.ClearAllTiles();
            floorPresentation.ClearAllTiles();

            int count = bounds.size.x * bounds.size.y * Mathf.Max(1, bounds.size.z);
            TileChangeData[] blockedChanges = new TileChangeData[count];
            TileBase[] floorTiles = new TileBase[count];
            int index = 0, blockedCount = 0, floorCount = 0;
            using (MaskMarker.Auto())
            {
                foreach (Vector3Int position in bounds.allPositionsWithin)
                {
                    Vector2Int cell = new Vector2Int(position.x, position.y);
                    TerrainVisualCellKind kind = source.GetVisualKind(cell);
                    if (source.ShouldRenderFloor(cell))
                    {
                        floorTiles[index] = SelectFloor(cell);
                        if (floorTiles[index] != null) floorCount++;
                    }
                    TileBase blockedTile = SelectBlocked(cell, kind);
                    Color blockedColor = blockedTile != null ? source.GetPresentationTint(cell) : Color.white;
                    blockedChanges[index] = new TileChangeData(position, blockedTile, blockedColor, Matrix4x4.identity);
                    if (blockedTile != null) blockedCount++;
                    index++;
                }
            }
            using (TilemapMarker.Auto())
            {
                blockedTerrainPresentation.SetTiles(blockedChanges, true);
                floorPresentation.SetTilesBlock(bounds, floorTiles);
            }
            LastBuiltBlockedCells = blockedCount;
            LastBuiltFloorCells = floorCount;
            SetPresentationEnabled(true);
            return true;
        }
    }

    public void MarkDirty(Vector2Int changedCell)
    {
        dirtyCells.Add(changedCell);
        for (int i = 0; i < CardinalOffsets.Length; i++) dirtyCells.Add(changedCell + CardinalOffsets[i]);
    }

    public void MarkDirty(IEnumerable<Vector2Int> changedCells)
    {
        if (changedCells == null) return;
        foreach (Vector2Int cell in changedCells) MarkDirty(cell);
    }

    public void FlushImmediate() { FlushDirty(); }

    public void FlushDirty()
    {
        if (dirtyCells.Count == 0) return;
        using (DirtyMarker.Auto())
        {
            if (!ValidateRuntimeReferences()) { dirtyCells.Clear(); return; }
            LastDirtyCellCount = dirtyCells.Count;
            foreach (Vector2Int cell in dirtyCells) RefreshCell(cell);
            dirtyCells.Clear();
        }
    }

    public DirtExposureMask CalculateExposureMask(Vector2Int cell)
    {
        if (source == null && !ResolveSource()) return DirtExposureMask.None;
        DirtExposureMask mask = DirtExposureMask.None;
        if (source.GetVisualKind(cell + Vector2Int.up) == TerrainVisualCellKind.Open) mask |= DirtExposureMask.Above;
        if (source.GetVisualKind(cell + Vector2Int.right) == TerrainVisualCellKind.Open) mask |= DirtExposureMask.Right;
        if (source.GetVisualKind(cell + Vector2Int.down) == TerrainVisualCellKind.Open) mask |= DirtExposureMask.Below;
        if (source.GetVisualKind(cell + Vector2Int.left) == TerrainVisualCellKind.Open) mask |= DirtExposureMask.Left;
        return mask;
    }

    public int GetStableVariantOrdinal(Vector2Int cell, int variantCount, bool floor)
    {
        if (variantCount <= 0) return -1;
        int seed = source != null ? source.VisualSeed : 0;
        int hash = StableHash(seed, cell.x, cell.y, floor ? FloorVariantSalt : DirtVariantSalt);
        return (int)((uint)hash % (uint)variantCount);
    }

    public bool ValidateRuntimeReferences()
    {
        bool valid = ResolveSource();
        if (palette == null) { Debug.LogError($"{name}: terrain palette is missing.", this); valid = false; }
        else valid &= BuildLookup(true);
        if (blockedTerrainPresentation == null) { Debug.LogError($"{name}: BlockedTerrainPresentation is missing.", this); valid = false; }
        if (floorPresentation == null) { Debug.LogError($"{name}: FloorPresentation is missing.", this); valid = false; }
        if (HasCollider(blockedTerrainPresentation) || HasCollider(floorPresentation)) { Debug.LogError($"{name}: presentation Tilemaps must not have Collider2D components.", this); valid = false; }
        return valid;
    }

    bool BuildLookup(bool logErrors)
    {
        for (int i = 0; i < maskLookup.Length; i++) maskLookup[i] = null;
        string summary = "Terrain palette is missing.";
        lookupValid = palette != null && palette.Validate(out summary);
        if (palette != null && palette.dirtMasks != null)
            foreach (DirtMaskVariantSet set in palette.dirtMasks)
            {
                if (set == null) continue;
                int mask = (int)set.exposureMask;
                if (mask >= 0 && mask < maskLookup.Length && maskLookup[mask] == null) maskLookup[mask] = set;
            }
        if (!lookupValid && logErrors) Debug.LogError($"{name}: {summary}", this);
        return lookupValid;
    }

    void RefreshCell(Vector2Int cell)
    {
        BoundsInt bounds = source.VisualBounds;
        if (!Contains(bounds, cell)) return;
        Vector3Int position = new Vector3Int(cell.x, cell.y, 0);
        TerrainVisualCellKind kind = source.GetVisualKind(cell);
        TileBase blocked = SelectBlocked(cell, kind);
        TileBase floor = source.ShouldRenderFloor(cell) ? SelectFloor(cell) : null;
        blockedTerrainPresentation.SetTile(position, blocked);
        floorPresentation.SetTile(position, floor);
        if (blocked != null)
        {
            blockedTerrainPresentation.RemoveTileFlags(position, TileFlags.LockColor);
            blockedTerrainPresentation.SetColor(position, source.GetPresentationTint(cell));
        }
        else blockedTerrainPresentation.SetColor(position, Color.white);
    }

    TileBase SelectBlocked(Vector2Int cell, TerrainVisualCellKind kind)
    {
        if (kind == TerrainVisualCellKind.Dirt)
        {
            DirtMaskVariantSet set = maskLookup[(int)CalculateExposureMask(cell)];
            if (set == null || set.variants == null || set.variants.Length == 0) return null;
            return set.variants[GetStableVariantOrdinal(cell, set.variants.Length, false)];
        }
        if (kind == TerrainVisualCellKind.RevealDirt || kind == TerrainVisualCellKind.Stone || kind == TerrainVisualCellKind.Root)
            return source.GetSpecialBlockedTile(cell);
        return null;
    }

    TileBase SelectFloor(Vector2Int cell)
    {
        if (palette == null || palette.floorVariants == null || palette.floorVariants.Length == 0) return null;
        return palette.floorVariants[GetStableVariantOrdinal(cell, palette.floorVariants.Length, true)];
    }

    void SetPresentationEnabled(bool enabled)
    {
        SetRendererEnabled(blockedTerrainPresentation, enabled);
        SetRendererEnabled(floorPresentation, enabled);
    }
    static void SetRendererEnabled(Tilemap tilemap, bool enabled) { TilemapRenderer renderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null; if (renderer != null) renderer.enabled = enabled; }
    static bool HasCollider(Tilemap tilemap) { return tilemap != null && tilemap.GetComponent<Collider2D>() != null; }
    static bool Contains(BoundsInt bounds, Vector2Int cell) { return cell.x >= bounds.xMin && cell.x < bounds.xMax && cell.y >= bounds.yMin && cell.y < bounds.yMax; }
    static int StableHash(params int[] values)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < values.Length; i++) { hash ^= (uint)values[i]; hash *= 16777619u; hash ^= hash >> 13; }
            return (int)hash;
        }
    }
}
