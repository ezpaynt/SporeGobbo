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
        Assert.That(cell, Is.EqualTo(CampEarlyMapCatalog.ExitAnchor));

        CampSpatialZone zone = terrain.SpatialContract.Find("exit-structure");
        Assert.That(zone, Is.Not.Null);
        Assert.That(zone.bounds.Contains(new Vector3Int(cell.x, cell.y, 0)), Is.True);

        CampReservedFootprint footprint = terrain.reservedStationFootprints.Find(f => f.footprintId == "run-exit");
        Assert.That(footprint, Is.Not.Null);
        Assert.That(footprint.bounds, Is.EqualTo(CampEarlyMapCatalog.ExitFootprint));
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
    public void ApprovedEarlyMapCatalogIsTheRuntimeNonResidentialOpenAuthority()
    {
        HashSet<Vector2Int> runtimeOpen = BuildRuntimeOpenAuthoring();
        Assert.That(runtimeOpen.Count, Is.EqualTo(CampEarlyMapCatalog.InitialOpenCells.Count));
        Assert.That(runtimeOpen.Contains(CampEarlyMapCatalog.FireAnchor), Is.True);
        Assert.That(runtimeOpen.Contains(CampEarlyMapCatalog.ExitAnchor), Is.True);
        Assert.That(runtimeOpen.Contains(CampEarlyMapCatalog.SquadInteractionAnchor), Is.False,
            "Squad footprint begins as staged dirt, not starting-open Camp.");
        Assert.That(CampEarlyMapCatalog.FirstBuddySquad.ContainsTerrainCell(
            (CampEarlyMapCatalog.SquadInteractionAnchor.x, CampEarlyMapCatalog.SquadInteractionAnchor.y)), Is.True);
        Assert.That(runtimeOpen.Contains(new Vector2Int(70, 70)), Is.False);
    }

    [Test]
    public void FireExitSquadAndBonesSceneObjectsMatchApprovedGridAnchors()
    {
        GameObject fire = GameObject.Find("CampFire");
        GameObject squadObject = GameObject.Find("SquadSelectSpot");
        CampSquadSelect squad = Object.FindAnyObjectByType<CampSquadSelect>(FindObjectsInactive.Include);
        GameObject bonesObject = GameObject.Find("BonesSpot");
        CampOldBonesWall bones = Object.FindAnyObjectByType<CampOldBonesWall>(FindObjectsInactive.Include);
        Assert.That(fire, Is.Not.Null);
        Assert.That(squadObject, Is.Not.Null);
        Assert.That(squad, Is.Not.Null);
        Assert.That(bonesObject, Is.Not.Null);
        Assert.That(bones, Is.Not.Null);
        Assert.That(terrain.WorldToCell(fire.transform.position), Is.EqualTo(CampEarlyMapCatalog.FireAnchor));
        Assert.That(terrain.WorldToCell(squadObject.transform.position), Is.EqualTo(CampEarlyMapCatalog.SquadInteractionAnchor));
        Assert.That(squad.campTerrain, Is.SameAs(terrain));
        Assert.That(squad.terrainFootprintId, Is.EqualTo(CampEarlyMapCatalog.SquadFootprintId));
        Assert.That(terrain.WorldToCell(bonesObject.transform.position), Is.EqualTo(CampEarlyMapCatalog.BonesInteractionAnchor));
        Assert.That(bones.campTerrain, Is.SameAs(terrain));
        Assert.That(bones.terrainFootprintId, Is.EqualTo(CampEarlyMapCatalog.BonesFootprintId));
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

        HashSet<(int x, int y)> runtimeResidential = terrain.GetResidentialCatalog()
            .GetResidentialAuthorizationCells();
        foreach (CampResidentialPlanRoom room in plan.rooms)
        {
            Assert.That(terrain.AuthoredBounds.Contains(room.protectedBounds.min), Is.True);
            Assert.That(terrain.AuthoredBounds.Contains(room.protectedBounds.max - Vector3Int.one), Is.True);
            if (room.currentlyImplemented) continue;
            foreach (CampCellCoordinate slot in room.slotCenters)
                Assert.That(runtimeResidential.Contains((slot.x, slot.y)), Is.False,
                    $"Future {room.roomId} slot ({slot.x},{slot.y}) leaked into runtime residential authority.");
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
        foreach ((int x, int y) cell in CampEarlyMapCatalog.InitialOpenCells)
            cells.Add(new Vector2Int(cell.x, cell.y));
        return cells;
    }

    static void AddRegions(List<CampTerrainRegion> regions, HashSet<Vector2Int> cells)
    {
        foreach (CampTerrainRegion region in regions)
            foreach (Vector3Int position in region.bounds.allPositionsWithin)
                cells.Add(new Vector2Int(position.x, position.y));
    }
}
