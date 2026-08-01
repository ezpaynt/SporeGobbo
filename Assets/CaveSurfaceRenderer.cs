using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class CaveSurfaceRenderer : MonoBehaviour
{
    public const string FloorTilemapName = "CaveFloorTilemap";
    public const string DetailTilemapName = "FloorDetailTilemap";
    public const string SlopeTilemapName = "RearSlopeOverlayTilemap";

    [Header("References")]
    public Grid grid;
    public Tilemap dirtTilemap;
    public TileBase floorTile;
    public TileBase[] additionalFloorTiles = new TileBase[0];
    public TileBase floorDetailTile;
    public TileBase rearSlopeTile;

    [Header("Prototype")]
    [Tooltip("Temporary visual-only proof that floor details can be generated independently.")]
    public bool showTestFloorDetails = true;
    [Range(0f, 1f)] public float testFloorDetailChance = 0.035f;
    [Range(0f, 0.12f)] public float maximumFloorOffset = 0.045f;

    [Header("Sorting")]
    public int caveFloorSortingOrder = -8;
    public int floorDetailSortingOrder = -7;
    public int rearSlopeSortingOrder = -6;

    private Tilemap caveFloorTilemap;
    private Tilemap floorDetailTilemap;
    private Tilemap rearSlopeOverlayTilemap;
    private MapData currentData;
    private int currentSeed;

    public Tilemap CaveFloorTilemap => caveFloorTilemap;
    public Tilemap FloorDetailTilemap => floorDetailTilemap;
    public Tilemap RearSlopeOverlayTilemap => rearSlopeOverlayTilemap;

    public void Rebuild(MapData data, int deterministicSeed)
    {
        currentData = data;
        currentSeed = deterministicSeed;
        EnsureTilemaps();
        Clear();
        SetSurfaceRenderersEnabled(isActiveAndEnabled);
        if (!isActiveAndEnabled || data == null) return;

        for (int x = 0; x < data.width; x++)
        {
            for (int y = 0; y < data.height; y++)
                RefreshVisualCell(new Vector2Int(x, y));
        }
    }

    public void Clear()
    {
        EnsureTilemaps();
        caveFloorTilemap?.ClearAllTiles();
        floorDetailTilemap?.ClearAllTiles();
        rearSlopeOverlayTilemap?.ClearAllTiles();
    }

    public void RefreshCell(MapData data, Vector2Int changedCell, int deterministicSeed)
    {
        currentData = data;
        currentSeed = deterministicSeed;
        if (!isActiveAndEnabled || data == null) return;
        EnsureTilemaps();

        RefreshVisualCell(changedCell);
        RefreshSlopeCell(changedCell + Vector2Int.down);
    }

    public void RefreshCells(MapData data, IEnumerable<Vector2Int> changedCells, int deterministicSeed)
    {
        currentData = data;
        currentSeed = deterministicSeed;
        if (!isActiveAndEnabled || data == null || changedCells == null) return;
        EnsureTilemaps();

        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in changedCells)
        {
            cells.Add(cell);
            cells.Add(cell + Vector2Int.down);
        }

        foreach (Vector2Int cell in cells)
            RefreshVisualCell(cell);
    }

    public bool ValidateSurface(out string summary)
    {
        int open = 0;
        int floor = 0;
        int blockedFloor = 0;
        int expectedSlopes = 0;
        int slopes = 0;
        int invalidSlopes = 0;
        int details = 0;
        int invalidDetails = 0;

        if (currentData != null)
        {
            EnsureTilemaps();
            for (int x = 0; x < currentData.width; x++)
            {
                for (int y = 0; y < currentData.height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    Vector3Int position = ToTilePosition(cell);
                    bool isOpen = !currentData.IsBlocked(cell);
                    bool hasFloor = caveFloorTilemap != null && caveFloorTilemap.HasTile(position);
                    bool hasSlope = rearSlopeOverlayTilemap != null && rearSlopeOverlayTilemap.HasTile(position);
                    bool hasDetail = floorDetailTilemap != null && floorDetailTilemap.HasTile(position);
                    bool shouldSlope = ShouldPlaceRearSlope(cell);

                    if (isOpen) open++;
                    if (hasFloor) floor++;
                    if (!isOpen && hasFloor) blockedFloor++;
                    if (shouldSlope) expectedSlopes++;
                    if (hasSlope) slopes++;
                    if (hasSlope && !shouldSlope) invalidSlopes++;
                    if (hasDetail) details++;
                    if (hasDetail && !isOpen) invalidDetails++;
                }
            }
        }

        summary = $"open={open} floor={floor} blockedFloor={blockedFloor} expectedSlopes={expectedSlopes} " +
                  $"slopes={slopes} invalidSlopes={invalidSlopes} details={details} invalidDetails={invalidDetails}";
        return open == floor && blockedFloor == 0 && expectedSlopes == slopes &&
               invalidSlopes == 0 && invalidDetails == 0 && !HasSurfaceColliders();
    }

    public bool HasSurfaceColliders()
    {
        EnsureTilemaps();
        return HasCollider(caveFloorTilemap) || HasCollider(floorDetailTilemap) || HasCollider(rearSlopeOverlayTilemap);
    }

    private void RefreshVisualCell(Vector2Int cell)
    {
        if (currentData == null || !currentData.InBounds(cell)) return;
        Vector3Int position = ToTilePosition(cell);
        bool open = !currentData.IsBlocked(cell);

        TileBase selectedFloor = open ? SelectFloorTile(cell) : null;
        caveFloorTilemap.SetTile(position, selectedFloor);
        if (open && selectedFloor != null)
        {
            caveFloorTilemap.RemoveTileFlags(position, TileFlags.LockColor | TileFlags.LockTransform);
            caveFloorTilemap.SetColor(position, Color.white);
            caveFloorTilemap.SetTransformMatrix(position, GetFloorTransform(cell));
        }

        bool showDetail = open && showTestFloorDetails && floorDetailTile != null &&
                          Stable01(currentSeed, cell.x, cell.y, 7103) < testFloorDetailChance;
        floorDetailTilemap.SetTile(position, showDetail ? floorDetailTile : null);
        RefreshSlopeCell(cell);
    }

    private TileBase SelectFloorTile(Vector2Int cell)
    {
        if (floorTile == null) return null;
        if (additionalFloorTiles == null || additionalFloorTiles.Length == 0) return floorTile;

        float roll = Stable01(currentSeed, cell.x, cell.y, 7121);
        if (additionalFloorTiles.Length >= 2)
        {
            if (roll < 0.60f) return floorTile;
            if (roll < 0.85f && additionalFloorTiles[0] != null) return additionalFloorTiles[0];
            return additionalFloorTiles[1] != null ? additionalFloorTiles[1] : floorTile;
        }

        return roll < 0.72f || additionalFloorTiles[0] == null ? floorTile : additionalFloorTiles[0];
    }

    private Matrix4x4 GetFloorTransform(Vector2Int cell)
    {
        float offset = Mathf.Clamp(maximumFloorOffset, 0f, 0.12f);
        float x = (Stable01(currentSeed, cell.x, cell.y, 7127) * 2f - 1f) * offset;
        float y = (Stable01(currentSeed, cell.x, cell.y, 7129) * 2f - 1f) * offset;
        return Matrix4x4.Translate(new Vector3(x, y, 0f));
    }

    private void RefreshSlopeCell(Vector2Int cell)
    {
        if (currentData == null || !currentData.InBounds(cell)) return;
        rearSlopeOverlayTilemap.SetTile(ToTilePosition(cell), ShouldPlaceRearSlope(cell) ? rearSlopeTile : null);
    }

    private bool ShouldPlaceRearSlope(Vector2Int cell)
    {
        if (currentData == null || !currentData.InBounds(cell) || currentData.IsBlocked(cell)) return false;
        return currentData.IsBlocked(cell + Vector2Int.up);
    }

    private void EnsureTilemaps()
    {
        if (grid == null) grid = GetComponentInChildren<Grid>();
        if (grid == null) return;

        caveFloorTilemap = EnsureTilemap(caveFloorTilemap, FloorTilemapName, caveFloorSortingOrder);
        floorDetailTilemap = EnsureTilemap(floorDetailTilemap, DetailTilemapName, floorDetailSortingOrder);
        rearSlopeOverlayTilemap = EnsureTilemap(rearSlopeOverlayTilemap, SlopeTilemapName, rearSlopeSortingOrder);
    }

    private Tilemap EnsureTilemap(Tilemap cached, string objectName, int sortingOrder)
    {
        if (cached == null)
        {
            foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>(true))
            {
                if (tilemap.name == objectName)
                {
                    cached = tilemap;
                    break;
                }
            }
        }

        if (cached == null)
        {
            GameObject child = new GameObject(objectName);
#if UNITY_EDITOR
            if (!Application.isPlaying) child.hideFlags = HideFlags.DontSaveInEditor;
#endif
            child.layer = dirtTilemap != null ? dirtTilemap.gameObject.layer : gameObject.layer;
            child.transform.SetParent(grid.transform, false);
            cached = child.AddComponent<Tilemap>();
            child.AddComponent<TilemapRenderer>();
        }

        TilemapRenderer renderer = cached.GetComponent<TilemapRenderer>();
        TilemapRenderer dirtRenderer = dirtTilemap != null ? dirtTilemap.GetComponent<TilemapRenderer>() : null;
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
            if (dirtRenderer != null)
            {
                renderer.sortingLayerID = dirtRenderer.sortingLayerID;
                if (dirtRenderer.sharedMaterial != null)
                    renderer.sharedMaterial = dirtRenderer.sharedMaterial;
            }
        }

        return cached;
    }

    private static bool HasCollider(Tilemap tilemap)
    {
        return tilemap != null && tilemap.GetComponent<Collider2D>() != null;
    }

    private static Vector3Int ToTilePosition(Vector2Int cell)
    {
        return new Vector3Int(cell.x, cell.y, 0);
    }

    private static float Stable01(int seed, int x, int y, int salt)
    {
        unchecked
        {
            uint hash = (uint)seed;
            hash ^= (uint)x * 0x9E3779B9u;
            hash = (hash << 13) | (hash >> 19);
            hash ^= (uint)y * 0x85EBCA6Bu;
            hash ^= (uint)salt * 0xC2B2AE35u;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            return (hash & 0x00FFFFFFu) / 16777216f;
        }
    }

    private void OnDisable()
    {
        SetSurfaceRenderersEnabled(false);
    }

    private void OnEnable()
    {
        SetSurfaceRenderersEnabled(true);
        if (currentData != null) Rebuild(currentData, currentSeed);
    }

    private void SetSurfaceRenderersEnabled(bool enabled)
    {
        EnsureTilemaps();
        SetRendererEnabled(caveFloorTilemap, enabled);
        SetRendererEnabled(floorDetailTilemap, enabled);
        SetRendererEnabled(rearSlopeOverlayTilemap, enabled);
    }

    private static void SetRendererEnabled(Tilemap tilemap, bool enabled)
    {
        TilemapRenderer renderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;
        if (renderer != null) renderer.enabled = enabled;
    }
}
