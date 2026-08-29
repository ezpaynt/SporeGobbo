using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SporeGobbo.CampLifecycle;

public sealed class CampOrganicSpatialAuthoringTests
{
    const string CampScenePath = "Assets/Scenes/CampScene.unity";
    HandcraftedCampTerrain terrain;

    [SetUp]
    public void LoadCamp()
    {
        EditorSceneManager.OpenScene(CampScenePath, OpenSceneMode.Single);
        terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        Assert.That(terrain, Is.Not.Null);
    }

    [Test]
    public void ExitTransformFootprintAndPermanentZoneShareOneAuthority()
    {
        CampRunPortal portal = Object.FindAnyObjectByType<CampRunPortal>(FindObjectsInactive.Include);
        Assert.That(portal, Is.Not.Null);
        Vector2Int cell = terrain.WorldToCell(portal.transform.position);
        Assert.That(cell, Is.EqualTo(new Vector2Int(61, 54)));

        CampSpatialZone zone = terrain.SpatialContract.Find("exit-structure");
        Assert.That(zone, Is.Not.Null);
        Assert.That(zone.bounds.Contains(new Vector3Int(cell.x, cell.y, 0)), Is.True);

        CampReservedFootprint footprint = terrain.reservedStationFootprints.Find(f => f.footprintId == "run-exit");
        Assert.That(footprint, Is.Not.Null);
        Assert.That(footprint.bounds.position, Is.EqualTo(new Vector3Int(57, 52, 0)));
        Assert.That(footprint.bounds.Contains(new Vector3Int(cell.x, cell.y, 0)), Is.True);

        GameObject marker = GameObject.Find("RunExitMarker");
        Assert.That(marker, Is.Not.Null);
        Assert.That(Vector2.Distance(marker.transform.position, portal.transform.position), Is.LessThan(0.01f));
    }

    [Test]
    public void ReturnArrivalUsesSouthwestPocketAndProtectedFireRoute()
    {
        CampSceneController controller = Object.FindAnyObjectByType<CampSceneController>(FindObjectsInactive.Include);
        Assert.That(controller, Is.Not.Null);
        Vector2Int arrival = terrain.WorldToCell(controller.mainCampArrivalSpawn.position);
        Assert.That(arrival, Is.EqualTo(new Vector2Int(40, 28)));
        Assert.That(terrain.SpatialContract.Find("normal-arrival").bounds.Contains(
            new Vector3Int(arrival.x, arrival.y, 0)), Is.True);
        Assert.That(terrain.SpatialContract.Find("circulation-arrival-fire").bounds.Contains(
            new Vector3Int(arrival.x, arrival.y, 0)), Is.True);
        Assert.That(terrain.reservedStationFootprints.Find(f => f.footprintId == "arrival").bounds.Contains(
            new Vector3Int(arrival.x, arrival.y, 0)), Is.True);
    }

    [Test]
    public void BonesToExitShelfHasNoRuntimeOpeningBehindIt()
    {
        HashSet<Vector2Int> runtimeOpen = BuildRuntimeOpenAuthoring();
        for (int x = 25; x <= 67; x++)
        for (int y = 59; y < terrain.AuthoredBounds.yMax; y++)
            Assert.That(runtimeOpen.Contains(new Vector2Int(x, y)), Is.False,
                $"Cell ({x},{y}) opens playable Camp behind the terminating Bones/Exit shelf.");
    }

    [Test]
    public void CurrentRoomOneSlotsAndConstructionRouteRemainInsideCanonicalGeometry()
    {
        List<ResidentialSlotRecord> slots = terrain.GetResidentialSlots(1);
        Assert.That(slots.Count, Is.EqualTo(10));
        Assert.That(slots[0].Center, Is.EqualTo((69, 36)));
        Assert.That(slots[9].Center, Is.EqualTo((112, 35)));
        Assert.That(terrain.AuthoredBounds.size, Is.EqualTo(new Vector3Int(128, 80, 1)));
        for (int slot = 1; slot <= 10; slot++)
        {
            Assert.That(terrain.GetResidentialSlotFootprint(slot), Is.Not.Empty);
            Assert.That(terrain.GetResidentialConstructionRoute(slot), Is.Not.Empty);
        }
    }

    [Test]
    public void MasterPlanHasThirtyTwentySplitAndFutureSlotsRemainRuntimeSolid()
    {
        CampResidentialMasterPlan plan = AssetDatabase.LoadAssetAtPath<CampResidentialMasterPlan>(
            "Assets/Editor/CampResidentialMasterPlan.asset");
        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.ValidatePlan(), Is.Empty);
        Assert.That(plan.TotalCapacity, Is.EqualTo(50));
        Assert.That(Capacity(plan, "primary"), Is.EqualTo(30));
        Assert.That(Capacity(plan, "secondary"), Is.EqualTo(20));
        Assert.That(plan.plannedCampBounds, Is.EqualTo(terrain.AuthoredBounds));

        HashSet<Vector2Int> runtimeOpen = BuildRuntimeOpenAuthoring();
        foreach (CampResidentialPlanRoom room in plan.rooms)
        {
            Assert.That(terrain.AuthoredBounds.Contains(room.protectedBounds.min), Is.True);
            Assert.That(terrain.AuthoredBounds.Contains(room.protectedBounds.max - Vector3Int.one), Is.True);
            if (room.currentlyImplemented) continue;
            foreach (CampCellCoordinate slot in room.slotCenters)
                Assert.That(runtimeOpen.Contains(new Vector2Int(slot.x, slot.y)), Is.False,
                    $"Future {room.roomId} slot ({slot.x},{slot.y}) became runtime-open terrain.");
        }
    }

    [Test]
    public void ImplementedMasterPlanRoomMirrorsRuntimeCatalogWhileFutureRoomsRemainPlanningOnly()
    {
        CampResidentialMasterPlan plan = AssetDatabase.LoadAssetAtPath<CampResidentialMasterPlan>(
            "Assets/Editor/CampResidentialMasterPlan.asset");
        CampResidentialRoomDefinition runtimeRoom = terrain.GetResidentialCatalog().Rooms[0];
        CampResidentialPlanRoom implemented = plan.rooms.Find(room => room != null && room.currentlyImplemented);

        Assert.That(implemented, Is.Not.Null);
        Assert.That(implemented.capacity, Is.EqualTo(runtimeRoom.Capacity));
        Assert.That(implemented.slotCenters.Count, Is.EqualTo(runtimeRoom.Slots.Count));
        for (int i = 0; i < runtimeRoom.Slots.Count; i++)
            Assert.That(new Vector2Int(implemented.slotCenters[i].x, implemented.slotCenters[i].y),
                Is.EqualTo(new Vector2Int(runtimeRoom.Slots[i].Center.x, runtimeRoom.Slots[i].Center.y)),
                "The implemented Editor plan room must mirror the runtime catalog slot order.");

        Assert.That(plan.TotalCapacity, Is.EqualTo(50));
        Assert.That(terrain.TotalResidentialCapacity, Is.EqualTo(10));
        Assert.That(plan.rooms.Exists(room => room != null && !room.currentlyImplemented), Is.True);
    }

    [Test]
    public void TerrainStateNormalizationAndCloneUseSuppliedCatalogCapacityAboveTen()
    {
        CampTerrainState state = new CampTerrainState { residentialSlotsEstablished = 12 };
        state.Normalize(12);
        Assert.That(state.residentialSlotsEstablished, Is.EqualTo(12));
        Assert.That(state.Clone(12).residentialSlotsEstablished, Is.EqualTo(12));

        state.residentialSlotsEstablished = 13;
        state.Normalize(12);
        Assert.That(state.residentialSlotsEstablished, Is.EqualTo(12));
    }

    [Test]
    public void EveryOrganicFirstBurrowRequiredCellHasRuntimeResidentialAuthorization()
    {
        CampResidentialCatalog catalog = terrain.GetResidentialCatalog();
        foreach (CampResidentialSlotDefinition slot in catalog.Rooms[0].Slots)
        foreach ((int x, int y) required in slot.GetRequiredOpenCells(
                     CampResidentialClearanceProfile.CurrentBaby))
        {
            Vector2Int cell = new Vector2Int(required.x, required.y);
            Assert.That(terrain.GetSpatialDigCategory(cell), Is.EqualTo(CampDigCategory.ResidentialReserved),
                "Slot " + slot.GlobalSlotId + " required cell " + cell +
                " escaped runtime residential spatial authority.");
            Assert.That(CampSpatialPolicy.CanDig(terrain.GetSpatialDigCategory(cell),
                TerrainDigAuthority.ResidentialProgression, true), Is.True);
        }
    }

    static int Capacity(CampResidentialMasterPlan plan, string neighborhoodId)
    {
        int total = 0;
        foreach (CampResidentialPlanRoom room in plan.rooms)
            if (room != null && room.neighborhoodId == neighborhoodId) total += room.capacity;
        return total;
    }

    HashSet<Vector2Int> BuildRuntimeOpenAuthoring()
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        AddRegions(terrain.authoredOpenRegions, cells);
        AddRegions(terrain.mainChamberRevealRegions, cells);
        foreach (CampTerrainRegion exclusion in terrain.mainChamberRevealExclusionRegions)
            foreach (Vector3Int position in exclusion.bounds.allPositionsWithin)
                cells.Remove(new Vector2Int(position.x, position.y));
        foreach (CampReservedFootprint footprint in terrain.reservedStationFootprints)
            foreach (Vector3Int position in footprint.bounds.allPositionsWithin)
                cells.Add(new Vector2Int(position.x, position.y));
        foreach (CampSpatialZone zone in terrain.SpatialContract.zones)
            if (CampSpatialPolicy.IsPermanent(zone.kind))
                foreach (Vector3Int position in zone.bounds.allPositionsWithin)
                    cells.Add(new Vector2Int(position.x, position.y));
        return cells;
    }

    static void AddRegions(List<CampTerrainRegion> regions, HashSet<Vector2Int> cells)
    {
        foreach (CampTerrainRegion region in regions)
            foreach (Vector3Int position in region.bounds.allPositionsWithin)
                cells.Add(new Vector2Int(position.x, position.y));
    }
}
