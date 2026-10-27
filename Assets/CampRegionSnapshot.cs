using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SporeGobbo.Geometry;
using SporeGobbo.CampCandidate;

/// <summary>Detached physical-region data. Contains no roster, inventory, render objects or jobs.</summary>
[Serializable]
public sealed class CampRegionSnapshot
{
    static readonly HashSet<string> validatedPhysical = new HashSet<string>();
    static readonly Queue<string> validationOrder = new Queue<string>();
    static readonly object validationLock = new object();
    public static int PhysicalValidationCacheEntries { get { lock(validationLock)return validatedPhysical.Count; } }
    void RememberValidated(string key)
    {
        lock(validationLock)
        {
            if (!validatedPhysical.Add(key)) return;
            validationOrder.Enqueue(key);
            while (validationOrder.Count > 8) validatedPhysical.Remove(validationOrder.Dequeue());
        }
    }
    public int schemaVersion;
    public string regionId, layoutId, rootId;
    public int width, height, terrainRevision;
    public float cellSize;
    public bool rootOpened;
    public int[] excavatedCells, establishedBeds;

    public static CampRegionSnapshot Pristine() => new CampRegionSnapshot
    {
        schemaVersion = 1, regionId = "camp", layoutId = SettlementCampLayout.LayoutId,
        rootId = SettlementCampLayout.RootId, width = 192, height = 192, cellSize = .6f,
        excavatedCells = Array.Empty<int>(), establishedBeds = Array.Empty<int>()
    };
    public CampRegionSnapshot Clone() => new CampRegionSnapshot
    {
        schemaVersion = schemaVersion, regionId = regionId, layoutId = layoutId, rootId = rootId,
        width = width, height = height, cellSize = cellSize, terrainRevision = terrainRevision,
        rootOpened = rootOpened, excavatedCells = excavatedCells?.ToArray(), establishedBeds = establishedBeds?.ToArray()
    };
    public RegionTerrainState RestoreTerrain()
    {
        ValidatePhysical();
        return new RegionTerrainState(regionId, width, height, SettlementCampLayout.Shared,
            excavatedCells.Select(id => new DualTile(id % width, id / width)).ToArray(), terrainRevision);
    }
    public void ValidatePhysical()
    {
        if (schemaVersion != 1 || regionId != "camp" || layoutId != SettlementCampLayout.LayoutId ||
            rootId != SettlementCampLayout.RootId || width != 192 || height != 192 || cellSize != .6f)
            throw new InvalidDataException("Unsupported Camp region schema/layout");
        if (excavatedCells == null || establishedBeds == null || excavatedCells.Length > width * height ||
            establishedBeds.Length > 10 || terrainRevision < 0 || terrainRevision > excavatedCells.Length || (excavatedCells.Length == 0) != (terrainRevision == 0))
            throw new InvalidDataException("Malformed Camp region state");
        // Exact content hash, recomputed for every mutable DTO call. No world, route,
        // roster or assignment is cached; campaign references are checked separately.
        string validationKey = CampaignSnapshotStorage.Hash(UnityEngine.JsonUtility.ToJson(this));
        lock(validationLock)if (validatedPhysical.Contains(validationKey)) return;
        var definition = SettlementCampLayout.Shared;
        var cuts = new HashSet<int>(); int prior = -1;
        foreach (int id in excavatedCells)
        {
            if (id < 0 || id >= width * height || id <= prior) throw new InvalidDataException("Unordered or duplicate excavation");
            prior = id; var category = definition.Category(id % width, id / width);
            if (category != CampCell.Expansion && category != CampCell.Residential && !(rootOpened && category == CampCell.FirstBuddy))
                throw new InvalidDataException("Excavation crosses authored protection");
            cuts.Add(id);
        }
        foreach (var root in SettlementCampLayout.RootCells())
            if (cuts.Contains(root.Y * width + root.X) != rootOpened) throw new InvalidDataException("Partial or contradictory root state");
        for (int i = 0; i < establishedBeds.Length; i++)
            if (establishedBeds[i] != i + 1) throw new InvalidDataException("Unsupported established-bed sequence");
        if (establishedBeds.Length == 0) { RememberValidated(validationKey); return; }
        var terrain = new RegionTerrainState(regionId, width, height, definition,
            excavatedCells.Select(id => new DualTile(id % width, id / width)).ToArray(), terrainRevision);
        var catalog = SettlementCampLayout.Catalog(definition);
        foreach (int bed in establishedBeds)
        {
            var slot = catalog.GetSlot(bed);
            if (slot.ExcavationFootprint.Any(cell => terrain.World.IsSolid(cell.x, cell.y)) ||
                terrain.Route(new Point2(slot.RestCell.x + .5, slot.RestCell.y + .5), definition.Marker("fire").Interaction, 3).Count == 0)
                throw new InvalidDataException("Established bed has incomplete geometry or no return route");
        }
        RememberValidated(validationKey);
    }
    public void ValidateCampaign(SporeSaveSlotData campaign)
    {
        ValidatePhysical();
        if (campaign?.campTerrainState == null || campaign.campTerrainState.residentialSlotsEstablished != establishedBeds.Length)
            throw new InvalidDataException("Campaign and physical bed capacity disagree");
        var assigned = new HashSet<int>();
        foreach (var unit in campaign.ownedGobbos ?? new List<GobboUnitSaveData>())
        {
            if (unit == null || unit.isDead || unit.campResidentialSlotId == 0) continue;
            if (!establishedBeds.Contains(unit.campResidentialSlotId) || !assigned.Add(unit.campResidentialSlotId))
                throw new InvalidDataException("Invalid or duplicate Gobbo-to-bed reference");
        }
    }
}

/// <summary>The active logical region supplies a detached snapshot to the existing save authority.</summary>
public interface ICampRegionSnapshotSource
{
    CampRegionSnapshot CaptureRegionSnapshot(GameState campaign);
}
