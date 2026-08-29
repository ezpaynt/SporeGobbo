using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using SporeGobbo.CampLifecycle;

/// <summary>
/// Authored, persistent Camp terrain. The serialized cell lists are the immutable baseline;
/// CampTerrainState stores only player-made differences from that baseline.
/// </summary>
[System.Serializable]
public sealed class CampTerrainRegion
{
    public string regionId = "";
    public BoundsInt bounds = new BoundsInt();
}

[System.Serializable]
public sealed class CampTerrainPath
{
    public string pathId = "";
    [Min(1)] public int halfWidth = 3;
    public List<CampCellCoordinate> waypoints = new List<CampCellCoordinate>();
}

[System.Serializable]
public sealed class CampReservedFootprint
{
    public string footprintId = "";
    public BoundsInt bounds = new BoundsInt();
}
public sealed class HandcraftedCampTerrain : MonoBehaviour, IDiggableTerrain
{
    [Header("Stable Grid")]
    public Grid grid;
    public Tilemap diggableDirtTilemap;
    public Tilemap permanentRockTilemap;
    public TerrainPresentationRenderer terrainPresentationRenderer;
    public TileBase dirtTile;
    public TileBase permanentRockTile;
    [Min(1)] public int layoutRevision = 1;
    public BoundsInt authoredBounds = new BoundsInt(0, 0, 0, 6, 4, 1);
    public bool fillUnassignedCellsWithPermanentRock = true;

    [Header("Authored Baseline")]
    public List<CampCellCoordinate> authoredOpenCells = new List<CampCellCoordinate>();
    public List<CampTerrainRegion> authoredOpenRegions = new List<CampTerrainRegion>();
    public List<CampCellCoordinate> authoredDiggableCells = new List<CampCellCoordinate>();
    public List<CampTerrainRegion> authoredDiggableRegions = new List<CampTerrainRegion>();
    public List<CampTerrainPath> authoredDiggablePaths = new List<CampTerrainPath>();
    public List<CampCellCoordinate> authoredPermanentRockCells = new List<CampCellCoordinate>();
    public List<CampTerrainRegion> authoredPermanentRockRegions = new List<CampTerrainRegion>();
    public List<CampReservedFootprint> reservedStationFootprints = new List<CampReservedFootprint>();

    [Header("Canonical Spatial Contract")]
    public CampSpatialContract spatialContract;

    [Header("Authoritative Exit Protection")]
    public Transform authoritativeCampExit;
    public string authoritativeExitFootprintId = "run-exit";

    [Header("Main Chamber Reveal")]
    public bool forceMainChamberRevealedForCurrentCamp = false;
    public bool hasMainChamberRevealTrigger = true;
    public CampCellCoordinate mainChamberRevealTriggerCell = new CampCellCoordinate();
    public List<CampCellCoordinate> mainChamberRevealCells = new List<CampCellCoordinate>();
    public List<CampTerrainRegion> mainChamberRevealRegions = new List<CampTerrainRegion>();
    public List<CampTerrainRegion> mainChamberRevealExclusionRegions = new List<CampTerrainRegion>();

    public int LastIgnoredSavedCellCount { get; private set; }
    public float CellSize => grid != null ? Mathf.Max(0.0001f, Mathf.Abs(grid.cellSize.x)) : 1f;
    public BoundsInt AuthoredBounds => authoredBounds;
    public int LayoutRevision => layoutRevision;
    public CampSpatialContract SpatialContract
    {
        get
        {
            EnsureSpatialContract();
            return spatialContract;
        }
    }

    readonly HashSet<Vector2Int> baselineOpen = new HashSet<Vector2Int>();
    readonly HashSet<Vector2Int> baselineDiggable = new HashSet<Vector2Int>();
    readonly HashSet<Vector2Int> baselineRock = new HashSet<Vector2Int>();
    readonly HashSet<Vector2Int> revealCells = new HashSet<Vector2Int>();
    bool baselineCached;
    bool started;
    bool rebuilding;
    bool spatialContractValidated;
    CampResidentialCatalog residentialCatalog;
    HashSet<(int x, int y)> residentialAuthorizationCells;
    HashSet<(int x, int y)> optionalPlayerDigCells;

    void OnValidate()
    {
        baselineCached = false;
        residentialCatalog = null;
        residentialAuthorizationCells = null;
        optionalPlayerDigCells = null;
        layoutRevision = Mathf.Max(1, layoutRevision);
    }
    void Awake()
    {
        CacheAuthoredBaseline();
    }

    void OnEnable()
    {
        DiggableTerrainService.Register(this);
        if (started) RebuildFromBaseline();
    }

    void Start()
    {
        started = true;
        RebuildFromBaseline();
    }

    void OnDisable()
    {
        DiggableTerrainService.Unregister(this);
    }

    void OnDestroy()
    {
        DiggableTerrainService.Unregister(this);
    }

    public void InitializeFromGameState()
    {
        DiggableTerrainService.Register(this);
        CacheAuthoredBaseline();
        RebuildFromBaseline();
    }

    public void DigCircle(Vector2 worldPosition, float radius)
    {
        EnsureReady();
        float safeRadius = Mathf.Max(0f, radius);
        Vector2Int center = WorldToCell(worldPosition);
        int cellRadius = Mathf.CeilToInt(safeRadius / CellSize) + 1;
        bool changed = false;

        for (int x = center.x - cellRadius; x <= center.x + cellRadius; x++)
        for (int y = center.y - cellRadius; y <= center.y + cellRadius; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (!Contains(authoredBounds, cell)) continue;
            if (Vector2.Distance(CellToWorld(cell), worldPosition) > safeRadius) continue;
            changed |= DigCell(cell, false);
        }

        if (changed) RefreshTerrain();
    }

    public TerrainDigResult DigCircle(Vector2 worldPosition, float radius, TerrainDigAuthority authority,
        int residentialStage, IReadOnlyCollection<Vector2Int> authorizedCells)
    {
        EnsureReady();
        HashSet<Vector2Int> authorized = authorizedCells != null
            ? new HashSet<Vector2Int>(authorizedCells) : new HashSet<Vector2Int>();
        CampTerrainState currentState = GetState(true);
        int expectedSlot = currentState != null ? currentState.residentialSlotsEstablished + 1 : 0;
        CampResidentialCatalog catalog = GetResidentialCatalog();
        CampResidentialRoomDefinition expectedRoom = null;
        bool expectedSlotExists = catalog != null &&
            catalog.TryGetRoomForSlot(expectedSlot, out expectedRoom);
        HashSet<Vector2Int> expectedFootprint = new HashSet<Vector2Int>(GetResidentialSlotFootprint(expectedSlot));
        bool validResidentialRequest = authority != TerrainDigAuthority.ResidentialProgression ||
            CampSpatialPolicy.CanAuthorizeResidentialProgression(residentialStage,
                expectedRoom != null ? expectedRoom.ProgressionIndex : 0,
                expectedSlotExists &&
                authorized.SetEquals(expectedFootprint));
        Vector2Int center = WorldToCell(worldPosition);
        int cellRadius = Mathf.CeilToInt(Mathf.Max(0f, radius) / CellSize) + 1;
        int evaluated = 0, eligible = 0, removed = 0;
        for (int x = center.x - cellRadius; x <= center.x + cellRadius; x++)
        for (int y = center.y - cellRadius; y <= center.y + cellRadius; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (!Contains(authoredBounds, cell) || Vector2.Distance(CellToWorld(cell), worldPosition) > radius) continue;
            evaluated++;
            bool authorizedResidential = authority == TerrainDigAuthority.ResidentialProgression &&
                validResidentialRequest && IsAuthorizedResidentialProgressionCell(cell, residentialStage, authorized);
            if (!CampSpatialPolicy.CanDig(GetSpatialDigCategory(cell), authority, authorizedResidential)) continue;
            eligible++;
            bool changed = authorizedResidential ? RemoveResidentialCell(cell) : DigCell(cell, false);
            if (changed) removed++;
        }
        if (removed > 0) RefreshTerrain();
        TerrainDigFailureReason failure = removed > 0 ? TerrainDigFailureReason.None :
            evaluated == 0 ? TerrainDigFailureReason.NoEvaluatedCells :
            !validResidentialRequest || eligible == 0 ? TerrainDigFailureReason.AuthorizationRejected :
            TerrainDigFailureReason.NoDirtRemoved;
        return new TerrainDigResult(evaluated, eligible, removed, failure);
    }

    bool IsAuthorizedResidentialProgressionCell(Vector2Int cell, int requestedStage, HashSet<Vector2Int> requestedCells)
    {
        CampTerrainState state = GetState(true);
        CampResidentialCatalog catalog = GetResidentialCatalog();
        int nextSlot = state != null ? state.residentialSlotsEstablished + 1 : 0;
        if (state == null || catalog == null ||
            !catalog.TryGetRoomForSlot(nextSlot, out CampResidentialRoomDefinition room) ||
            requestedStage != room.ProgressionIndex)
            return false;
        return requestedCells.Contains(cell) && GetResidentialSlotFootprint(nextSlot).Contains(cell) && IsResidentialTerrainCell(cell);
    }

    bool IsResidentialTerrainCell(Vector2Int cell)
    {
        GetResidentialCatalog();
        return residentialAuthorizationCells != null &&
               residentialAuthorizationCells.Contains((cell.x, cell.y));
    }

    bool RemoveResidentialCell(Vector2Int cell)
    {
        Vector3Int tileCell = ToTileCell(cell);
        bool changed = diggableDirtTilemap != null && diggableDirtTilemap.HasTile(tileCell);
        if (diggableDirtTilemap != null) diggableDirtTilemap.SetTile(tileCell, null);
        if (permanentRockTilemap != null) permanentRockTilemap.SetTile(tileCell, null);
        if (changed) terrainPresentationRenderer?.MarkDirty(cell);
        return changed;
    }

    public bool IsBlocked(Vector2Int cell)
    {
        if (!Contains(authoredBounds, cell)) return true;
        return permanentRockTilemap != null && permanentRockTilemap.HasTile(ToTileCell(cell)) ||
               diggableDirtTilemap != null && diggableDirtTilemap.HasTile(ToTileCell(cell));
    }

    public bool IsDiggable(Vector2Int cell)
    {
        EnsureReady();
        CampDigCategory category = GetSpatialDigCategory(cell);
        if (category == CampDigCategory.NeverDiggable || category == CampDigCategory.ResidentialReserved)
            return false;
        return baselineDiggable.Contains(cell) && !baselineRock.Contains(cell) &&
               diggableDirtTilemap != null && diggableDirtTilemap.HasTile(ToTileCell(cell));
    }

    public CampDigCategory GetSpatialDigCategory(Vector2Int cell)
    {
        EnsureSpatialContract();
        GetResidentialCatalog();
        if (residentialAuthorizationCells != null)
        {
            var tuple = (cell.x, cell.y);
            if (residentialAuthorizationCells.Contains(tuple))
                return CampDigCategory.ResidentialReserved;
            if (optionalPlayerDigCells != null && optionalPlayerDigCells.Contains(tuple))
                return CampDigCategory.NormalCampDiggable;
        }
        return spatialContract != null ? spatialContract.Classify(cell) : CampDigCategory.NormalCampDiggable;
    }

    public bool IsInSpatialZone(Vector2Int cell, CampZoneKind kind)
    {
        EnsureSpatialContract();
        if (spatialContract == null) return false;
        foreach (CampSpatialZone zone in spatialContract.ZonesAt(cell))
            if (zone.kind == kind) return true;
        return false;
    }

    public Vector2Int WorldToCell(Vector2 worldPosition)
    {
        if (grid == null) return Vector2Int.RoundToInt(worldPosition);
        Vector3Int cell = grid.WorldToCell(worldPosition);
        return new Vector2Int(cell.x, cell.y);
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        if (grid == null) return cell;
        return grid.GetCellCenterWorld(ToTileCell(cell));
    }

    [ContextMenu("Reveal Main Chamber")]
    public void RevealMainChamber()
    {
        EnsureReady();
        RevealMainChamberInternal(true);
    }

    [ContextMenu("Rebuild Authored Camp Terrain")]
    public void RebuildFromBaseline()
    {
        RebuildFromBaseline(true);
    }

    public void RebuildFromBaseline(bool rebuildFullPresentation)
    {
        if (rebuilding) return;
        rebuilding = true;
        try
        {
            CacheAuthoredBaseline();
            if (diggableDirtTilemap == null || permanentRockTilemap == null)
            {
                Debug.LogWarning("HandcraftedCampTerrain requires both authored Tilemaps.", this);
                return;
            }

            diggableDirtTilemap.ClearAllTiles();
            permanentRockTilemap.ClearAllTiles();

            int cellCount = authoredBounds.size.x * authoredBounds.size.y * authoredBounds.size.z;
            TileBase[] dirtTiles = new TileBase[cellCount];
            TileBase[] rockTiles = new TileBase[cellCount];
            int tileIndex = 0;
            foreach (Vector3Int position in authoredBounds.allPositionsWithin)
            {
                Vector2Int cell = new Vector2Int(position.x, position.y);
                if (baselineRock.Contains(cell))
                    rockTiles[tileIndex] = permanentRockTile;
                else if (baselineDiggable.Contains(cell))
                    dirtTiles[tileIndex] = dirtTile;
                else if (!baselineOpen.Contains(cell) && fillUnassignedCellsWithPermanentRock)
                    rockTiles[tileIndex] = permanentRockTile;
                tileIndex++;
            }
            diggableDirtTilemap.SetTilesBlock(authoredBounds, dirtTiles);
            permanentRockTilemap.SetTilesBlock(authoredBounds, rockTiles);

            CampTerrainState state = GetState(false);
            LastIgnoredSavedCellCount = 0;
            if (state != null)
            {
                state.Normalize(TotalResidentialCapacity);
                if (state.layoutRevision <= 0)
                    state.layoutRevision = layoutRevision;
                else if (state.layoutRevision == 1 && layoutRevision == 2)
                {
                    int discardedTestCells = state.clearedCellCoordinates.Count;
                    state.clearedCellCoordinates.Clear();
                    state.layoutRevision = layoutRevision;
                    if (discardedTestCells > 0)
                        Debug.LogWarning("Discarded " + discardedTestCells + " provisional Phase 2 Camp terrain coordinates while migrating to authored layout revision 2.", this);
                }
                else if (state.layoutRevision != layoutRevision)
                {
                    Debug.LogWarning("Camp terrain save revision " + state.layoutRevision + " differs from authored revision " + layoutRevision + ". Applying only coordinates valid in the current baseline and adopting the new authored revision.", this);
                    state.layoutRevision = layoutRevision;
                }

                if (forceMainChamberRevealedForCurrentCamp)
                {
                    state.layoutRevision = layoutRevision;
                    state.mainChamberRevealed = true;
                }

            }

            int establishedResidentialSlots = state != null ? state.residentialSlotsEstablished : 0;
            BuildCanonicalResidentialTerrain();

            if (state != null)
            {
                foreach (CampCellCoordinate savedCell in state.clearedCellCoordinates)
                {
                    Vector2Int cell = ToCell(savedCell);
                    if (!baselineDiggable.Contains(cell) || baselineRock.Contains(cell) ||
                        !CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(GetSpatialDigCategory(cell)))
                    {
                        LastIgnoredSavedCellCount++;
                        continue;
                    }
                    diggableDirtTilemap.SetTile(ToTileCell(cell), null);
                }

                ApplyEstablishedResidentialSlots(establishedResidentialSlots);

                if (state.mainChamberRevealed) RevealMainChamberInternal(false);
            }
            // TilemapCollider2D consumes the full authored rebuild on Unity's normal
            // collider update. Forcing thousands of cells synchronously here stalls
            // scene startup; individual digs still request an immediate refresh.
            RefreshTerrain(false);
            if (rebuildFullPresentation) terrainPresentationRenderer?.RebuildAndEnable();
            else RefreshResidentialPresentation();
        }
        finally
        {
            rebuilding = false;
        }
    }

    void RefreshResidentialPresentation()
    {
        if (terrainPresentationRenderer == null) return;
        HashSet<Vector2Int> residentialCells = new HashSet<Vector2Int>();
        foreach ((int x, int y) cell in GetResidentialCatalog().GetResidentialAuthorizationCells())
            residentialCells.Add(new Vector2Int(cell.x, cell.y));
        terrainPresentationRenderer.MarkDirty(residentialCells);
        terrainPresentationRenderer.FlushImmediate();
    }

    bool DigCell(Vector2Int cell, bool refresh)
    {
        if (!IsDiggable(cell)) return false;

        if (hasMainChamberRevealTrigger && mainChamberRevealTriggerCell != null && cell == ToCell(mainChamberRevealTriggerCell))
        {
            diggableDirtTilemap.SetTile(ToTileCell(cell), null);
            RecordClearedCell(cell);
            terrainPresentationRenderer?.MarkDirty(cell);
            RevealMainChamberInternal(true);
            if (refresh) RefreshTerrain();
            return true;
        }

        diggableDirtTilemap.SetTile(ToTileCell(cell), null);
        RecordClearedCell(cell);
        terrainPresentationRenderer?.MarkDirty(cell);
        if (refresh) RefreshTerrain();
        return true;
    }

    public List<ResidentialSlotRecord> GetResidentialSlots(int stage)
    {
        CampResidentialCatalog catalog = GetResidentialCatalog();
        List<ResidentialSlotRecord> result = new List<ResidentialSlotRecord>();
        if (catalog == null) return result;
        foreach (CampResidentialRoomDefinition room in catalog.Rooms)
            if (room.ProgressionIndex == stage)
                foreach (CampResidentialSlotDefinition slot in room.Slots) result.Add(slot.ToRecord());
        return result;
    }

    public int TotalResidentialCapacity => GetResidentialCatalog()?.TotalCapacity ?? 0;

    public CampResidentialCatalog GetResidentialCatalog()
    {
        if (residentialCatalog != null) return residentialCatalog;
        residentialCatalog = CampResidentialCatalog.CreateCurrent();
        residentialAuthorizationCells = residentialCatalog.GetResidentialAuthorizationCells();
        optionalPlayerDigCells = CampResidentialCatalog.GetOptionalPlayerDigCells();
        return residentialCatalog;
    }

    public ResidentialSlotRecord GetResidentialSlot(int slotIndex)
    {
        CampResidentialSlotDefinition slot = GetResidentialCatalog()?.GetSlot(slotIndex);
        return slot != null ? slot.ToRecord() : default;
    }

    public int GetResidentialProgressionIndexForSlot(int slotIndex)
    {
        CampResidentialCatalog catalog = GetResidentialCatalog();
        return catalog != null && catalog.TryGetRoomForSlot(slotIndex, out CampResidentialRoomDefinition room)
            ? room.ProgressionIndex : 0;
    }

    public List<Vector2Int> GetResidentialConstructionRoute(int slotIndex)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        CampResidentialSlotDefinition slot = GetResidentialCatalog()?.GetSlot(slotIndex);
        if (slot == null) return result;
        foreach ((int x, int y) waypoint in slot.ConstructionRoute)
            result.Add(new Vector2Int(waypoint.x, waypoint.y));
        return result;
    }

    public List<Vector2Int> GetResidentialSlotFootprint(int slotIndex)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        CampResidentialSlotDefinition slot = GetResidentialCatalog()?.GetSlot(slotIndex);
        if (slot == null) return result;
        foreach ((int x, int y) cell in slot.GetRequiredOpenCells(
                     CampResidentialClearanceProfile.CurrentBaby))
            result.Add(new Vector2Int(cell.x, cell.y));
        return result;
    }

    public Vector2 GetResidentialConstructionApproachWorld(int stage)
    {
        CampResidentialCatalog catalog = GetResidentialCatalog();
        if (catalog == null) return Vector2.zero;
        foreach (CampResidentialRoomDefinition room in catalog.Rooms)
            if (room.ProgressionIndex == stage && room.Slots.Count > 0)
                return CellToWorld(new Vector2Int(room.Slots[0].Approach.x, room.Slots[0].Approach.y));
        return Vector2.zero;
    }

    public List<Vector2Int> GetResidentialPresentationCells(int stage)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        CampResidentialCatalog catalog = GetResidentialCatalog();
        if (catalog == null) return result;
        foreach (CampResidentialRoomDefinition room in catalog.Rooms)
            if (room.ProgressionIndex == stage)
                foreach (CampResidentialSlotDefinition slot in room.Slots)
                    result.Add(new Vector2Int(slot.RestCell.x, slot.RestCell.y));
        return result;
    }

    public bool CompleteResidentialSlotForProgression(int stage, int slotIndex)
    {
        CampTerrainState state = GetState(true);
        CampResidentialCatalog catalog = GetResidentialCatalog();
        if (state == null || catalog == null || slotIndex != state.residentialSlotsEstablished + 1 ||
            !catalog.TryGetRoomForSlot(slotIndex, out CampResidentialRoomDefinition room) ||
            stage != room.ProgressionIndex) return false;
        foreach (Vector2Int cell in GetResidentialSlotFootprint(slotIndex))
            if (IsBlocked(cell))
            {
                Debug.LogError("[CampResidential Implementation] Refusing to commit Slot " + slotIndex +
                    ": required cell " + cell + " is still blocked. Live construction state is preserved.", this);
                return false;
            }
        state.residentialStage = Mathf.Max(state.residentialStage, stage);
        state.residentialSlotsEstablished = slotIndex;
        state.clearedCellCoordinates.RemoveAll(saved => saved != null &&
            GetSpatialDigCategory(new Vector2Int(saved.x, saved.y)) == CampDigCategory.ResidentialReserved);
        return true;
    }

    void ApplyEstablishedResidentialSlots(int establishedSlots)
    {
        CampResidentialCatalog catalog = GetResidentialCatalog();
        if (catalog == null) return;
        foreach (CampResidentialSlotDefinition definition in catalog.GetEstablishedSlots(establishedSlots))
            foreach ((int x, int y) cell in definition.GetRequiredOpenCells(
                         CampResidentialClearanceProfile.CurrentBaby))
                RemoveResidentialCell(new Vector2Int(cell.x, cell.y));
    }

    void BuildCanonicalResidentialTerrain()
    {
        if (diggableDirtTilemap == null) return;
        CampResidentialCatalog catalog = GetResidentialCatalog();
        if (catalog == null) return;
        HashSet<(int x, int y)> canonicalCells = catalog.GetResidentialAuthorizationCells();
        canonicalCells.UnionWith(CampResidentialCatalog.GetOptionalPlayerDigCells());
        foreach ((int x, int y) coordinate in canonicalCells)
        {
            Vector2Int cell = new Vector2Int(coordinate.x, coordinate.y);
            if (!Contains(authoredBounds, cell)) continue;
            permanentRockTilemap?.SetTile(ToTileCell(cell), null);
            diggableDirtTilemap.SetTile(ToTileCell(cell), dirtTile);
        }
    }

    bool RevealMainChamberInternal(bool recordState)
    {
        CampTerrainState state = GetState(recordState);
        bool alreadyRevealed = state != null && state.mainChamberRevealed;
        bool changed = false;
        foreach (Vector2Int cell in revealCells)
        {
            if (!CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(GetSpatialDigCategory(cell))) continue;
            if (diggableDirtTilemap != null && diggableDirtTilemap.HasTile(ToTileCell(cell)))
            {
                diggableDirtTilemap.SetTile(ToTileCell(cell), null);
                terrainPresentationRenderer?.MarkDirty(cell);
                changed = true;
            }
        }

        if (recordState && state != null)
        {
            state.layoutRevision = layoutRevision;
            state.mainChamberRevealed = true;
        }

        if (recordState && changed)
        {
            RefreshTerrain();
            terrainPresentationRenderer?.FlushImmediate();
        }
        return changed || !alreadyRevealed;
    }

    public TerrainVisualCellKind GetTerrainVisualKind(Vector2Int cell)
    {
        if (!Contains(authoredBounds, cell)) return TerrainVisualCellKind.OutOfBounds;
        Vector3Int position = ToTileCell(cell);
        if (permanentRockTilemap != null && permanentRockTilemap.HasTile(position)) return TerrainVisualCellKind.PermanentRock;
        if (diggableDirtTilemap != null && diggableDirtTilemap.HasTile(position)) return TerrainVisualCellKind.Dirt;
        return TerrainVisualCellKind.Open;
    }

    void RecordClearedCell(Vector2Int cell)
    {
        CampTerrainState state = GetState(true);
        if (state == null) return;
        state.layoutRevision = layoutRevision;
        foreach (CampCellCoordinate existing in state.clearedCellCoordinates)
            if (existing != null && existing.x == cell.x && existing.y == cell.y) return;
        state.clearedCellCoordinates.Add(new CampCellCoordinate(cell.x, cell.y));
    }

    CampTerrainState GetState(bool create)
    {
        GameState gameState = GameState.Instance;
        if (gameState == null) return null;
        if (gameState.campTerrainState == null && create) gameState.campTerrainState = new CampTerrainState();
        return gameState.campTerrainState;
    }

    void CacheAuthoredBaseline()
    {
        if (baselineCached) return;
        baselineCached = true;
        baselineOpen.Clear();
        baselineDiggable.Clear();
        baselineRock.Clear();
        revealCells.Clear();

        AddCells(authoredOpenCells, baselineOpen, "open");
        AddRegions(authoredOpenRegions, baselineOpen, "open");
        AddCells(authoredDiggableCells, baselineDiggable, "diggable");
        AddRegions(authoredDiggableRegions, baselineDiggable, "diggable");
        AddPaths(authoredDiggablePaths, baselineDiggable, "diggable");
        AddCells(authoredPermanentRockCells, baselineRock, "permanent rock");
        AddRegions(authoredPermanentRockRegions, baselineRock, "permanent rock");
        AddCells(mainChamberRevealCells, revealCells, "main chamber reveal");
        AddRegions(mainChamberRevealRegions, revealCells, "main chamber reveal");
        RemoveRegions(mainChamberRevealExclusionRegions, revealCells);

        foreach (Vector2Int cell in revealCells) baselineDiggable.Add(cell);
        ApplyPermanentSpatialZones();
        AlignExitFootprintToAuthoritativeTransform();
        if (reservedStationFootprints != null)
        {
            foreach (CampReservedFootprint footprint in reservedStationFootprints)
            {
                if (footprint == null) continue;
                foreach (Vector3Int position in footprint.bounds.allPositionsWithin)
                {
                    Vector2Int cell = new Vector2Int(position.x, position.y);
                    if (!Contains(authoredBounds, cell)) continue;
                    baselineOpen.Add(cell);
                    baselineDiggable.Remove(cell);
                    baselineRock.Remove(cell);
                    revealCells.Remove(cell);
                }
            }
        }
        ValidateSpatialContractOnce();
    }

    void EnsureSpatialContract()
    {
        if (spatialContract == null)
            spatialContract = Resources.Load<CampSpatialContract>("CampSpatialContract");
    }

    void ApplyPermanentSpatialZones()
    {
        EnsureSpatialContract();
        if (spatialContract == null) return;
        foreach (CampSpatialZone zone in spatialContract.zones)
        {
            if (zone == null || !CampSpatialPolicy.IsPermanent(zone.kind)) continue;
            foreach (Vector3Int position in zone.bounds.allPositionsWithin)
            {
                Vector2Int cell = new Vector2Int(position.x, position.y);
                if (!Contains(authoredBounds, cell)) continue;
                baselineOpen.Add(cell);
                baselineDiggable.Remove(cell);
                baselineRock.Remove(cell);
                revealCells.Remove(cell);
            }
        }
    }

    void ValidateSpatialContractOnce()
    {
        if (spatialContractValidated) return;
        spatialContractValidated = true;
        EnsureSpatialContract();
        if (spatialContract == null)
        {
            Debug.LogWarning("Camp spatial contract resource is missing; canonical zone protection is unavailable.", this);
            return;
        }
        foreach (string issue in spatialContract.ValidateContract())
            Debug.LogWarning("Camp spatial contract: " + issue, this);
        foreach (CampSpatialZone zone in spatialContract.zones)
        {
            if (zone == null) continue;
            if (zone.bounds.xMin < authoredBounds.xMin || zone.bounds.xMax > authoredBounds.xMax ||
                zone.bounds.yMin < authoredBounds.yMin || zone.bounds.yMax > authoredBounds.yMax)
                Debug.LogWarning("Camp spatial zone '" + zone.zoneId + "' extends outside authored terrain bounds.", this);
        }
        CampSceneController controller = Object.FindAnyObjectByType<CampSceneController>();
        ValidateAnchor(controller != null ? controller.newGameIntroSpawn : null, CampZoneKind.IntroArrivalClearance, "Intro spawn");
        ValidateAnchor(controller != null ? controller.mainCampArrivalSpawn : null, CampZoneKind.NormalArrivalClearance, "normal arrival spawn");
        CampRunPortal portal = Object.FindAnyObjectByType<CampRunPortal>();
        ValidateAnchor(portal != null ? portal.transform : null, CampZoneKind.PermanentExit, "live Exit");
        CampOldBonesWall bones = Object.FindAnyObjectByType<CampOldBonesWall>();
        ValidateAnchor(bones != null ? bones.transform : null, CampZoneKind.PermanentMemorial, "Bones Wall");
    }

    void ValidateAnchor(Transform anchor, CampZoneKind requiredZone, string label)
    {
        if (anchor != null && !IsInSpatialZone(WorldToCell(anchor.position), requiredZone))
            Debug.LogWarning(label + " is outside its canonical " + requiredZone + " zone.", anchor);
    }

    void AlignExitFootprintToAuthoritativeTransform()
    {
        if (authoritativeCampExit == null)
        {
            CampRunPortal portal = Object.FindAnyObjectByType<CampRunPortal>();
            if (portal != null) authoritativeCampExit = portal.transform;
        }
        if (grid == null || authoritativeCampExit == null || reservedStationFootprints == null) return;
        CampReservedFootprint exit = reservedStationFootprints.Find(footprint =>
            footprint != null && footprint.footprintId == authoritativeExitFootprintId);
        if (exit == null) return;

        Vector3Int center = grid.WorldToCell(authoritativeCampExit.position);
        int width = Mathf.Max(1, exit.bounds.size.x);
        int height = Mathf.Max(1, exit.bounds.size.y);
        exit.bounds = new BoundsInt(CampLifecyclePolicy.CenteredFootprintOrigin(center.x, width),
            CampLifecyclePolicy.CenteredFootprintOrigin(center.y, height), exit.bounds.z,
            width, height, Mathf.Max(1, exit.bounds.size.z));
    }

    void AddRegions(List<CampTerrainRegion> regions, HashSet<Vector2Int> destination, string category)
    {
        if (regions == null) return;
        foreach (CampTerrainRegion region in regions)
        {
            if (region == null) continue;
            foreach (Vector3Int position in region.bounds.allPositionsWithin)
            {
                Vector2Int cell = new Vector2Int(position.x, position.y);
                if (!Contains(authoredBounds, cell))
                {
                    Debug.LogWarning("Ignoring authored " + category + " region cell outside Camp bounds: " + cell, this);
                    continue;
                }
                destination.Add(cell);
            }
        }
    }

    static void RemoveRegions(List<CampTerrainRegion> regions, HashSet<Vector2Int> destination)
    {
        if (regions == null) return;
        foreach (CampTerrainRegion region in regions)
        {
            if (region == null) continue;
            foreach (Vector3Int position in region.bounds.allPositionsWithin)
                destination.Remove(new Vector2Int(position.x, position.y));
        }
    }

    void AddPaths(List<CampTerrainPath> paths, HashSet<Vector2Int> destination, string category)
    {
        if (paths == null) return;
        foreach (CampTerrainPath path in paths)
        {
            if (path == null || path.waypoints == null || path.waypoints.Count == 0) continue;
            int radius = Mathf.Max(1, path.halfWidth);
            if (path.waypoints.Count == 1)
            {
                AddPathSegment(ToCell(path.waypoints[0]), ToCell(path.waypoints[0]), radius, destination, category);
                continue;
            }
            for (int i = 1; i < path.waypoints.Count; i++)
            {
                CampCellCoordinate from = path.waypoints[i - 1];
                CampCellCoordinate to = path.waypoints[i];
                if (from == null || to == null) continue;
                AddPathSegment(ToCell(from), ToCell(to), radius, destination, category);
            }
        }
    }

    void AddPathSegment(Vector2Int from, Vector2Int to, int radius, HashSet<Vector2Int> destination, string category)
    {
        int minX = Mathf.Min(from.x, to.x) - radius;
        int maxX = Mathf.Max(from.x, to.x) + radius;
        int minY = Mathf.Min(from.y, to.y) - radius;
        int maxY = Mathf.Max(from.y, to.y) + radius;
        Vector2 a = from;
        Vector2 b = to;
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        float radiusSquared = radius * radius + 0.01f;
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (!Contains(authoredBounds, cell)) continue;
            Vector2 point = cell;
            float t = lengthSquared <= 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
            Vector2 closest = a + ab * t;
            if ((point - closest).sqrMagnitude <= radiusSquared) destination.Add(cell);
        }
    }
    void AddCells(List<CampCellCoordinate> source, HashSet<Vector2Int> destination, string category)
    {
        if (source == null) return;
        foreach (CampCellCoordinate authored in source)
        {
            if (authored == null) continue;
            Vector2Int cell = ToCell(authored);
            if (!Contains(authoredBounds, cell))
            {
                Debug.LogWarning("Ignoring authored " + category + " cell outside Camp bounds: " + cell, this);
                continue;
            }
            destination.Add(cell);
        }
    }

    void EnsureReady()
    {
        if (!baselineCached) CacheAuthoredBaseline();
    }

    void RefreshTerrain(bool processColliders = true)
    {
        if (diggableDirtTilemap != null) diggableDirtTilemap.RefreshAllTiles();
        if (permanentRockTilemap != null) permanentRockTilemap.RefreshAllTiles();
        if (processColliders)
        {
            ProcessCollider(diggableDirtTilemap);
            ProcessCollider(permanentRockTilemap);
        }
        Physics2D.SyncTransforms();
    }

    static void ProcessCollider(Tilemap tilemap)
    {
        if (tilemap == null) return;
        TilemapCollider2D collider = tilemap.GetComponent<TilemapCollider2D>();
        if (collider != null && collider.hasTilemapChanges) collider.ProcessTilemapChanges();
    }

    static Vector2Int ToCell(CampCellCoordinate cell) => new Vector2Int(cell.x, cell.y);
    static Vector3Int ToTileCell(Vector2Int cell) => new Vector3Int(cell.x, cell.y, 0);
    static bool Contains(BoundsInt bounds, Vector2Int cell) => cell.x >= bounds.xMin && cell.x < bounds.xMax && cell.y >= bounds.yMin && cell.y < bounds.yMax;
}
