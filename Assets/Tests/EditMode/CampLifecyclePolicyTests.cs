using NUnit.Framework;
using SporeGobbo.CampLifecycle;
using System;
using System.Collections.Generic;

public class CampLifecyclePolicyTests
{
    [Test]
    public void LeaderDeathWithSurvivorContinuesThroughSuccession()
    {
        Assert.That(CampLifecyclePolicy.IsValidSurvivor("buddy-1", false, 3, false), Is.True);
        Assert.That(CampLifecyclePolicy.DecideDeathDestination(1), Is.EqualTo(DeathDestination.SuccessionCamp));
    }

    [Test]
    public void LeaderDeathWithoutSurvivorEndsInGameOver()
    {
        Assert.That(CampLifecyclePolicy.DecideDeathDestination(0), Is.EqualTo(DeathDestination.GameOver));
    }

    [TestCase("", false, 3, false)]
    [TestCase("buddy", true, 3, false)]
    [TestCase("buddy", false, 0, false)]
    [TestCase("buddy", false, 3, true)]
    public void InvalidRecordsDoNotQualify(string id, bool dead, int health, bool leader)
    {
        Assert.That(CampLifecyclePolicy.IsValidSurvivor(id, dead, health, leader), Is.False);
    }

    [Test]
    public void FirstArrivalTerrainOnlyAppliesToIntro()
    {
        Assert.That(CampLifecyclePolicy.AppliesFirstArrivalTerrain(true), Is.True);
        Assert.That(CampLifecyclePolicy.AppliesFirstArrivalTerrain(false), Is.False);
    }

    [Test]
    public void ExitFootprintCentersOnAuthoritativeCell()
    {
        Assert.That(CampLifecyclePolicy.CenteredFootprintOrigin(61, 8), Is.EqualTo(57));
        Assert.That(CampLifecyclePolicy.CenteredFootprintOrigin(54, 4), Is.EqualTo(52));
    }

    [TestCase(CampZoneKind.HomeCore)]
    [TestCase(CampZoneKind.PermanentExit)]
    [TestCase(CampZoneKind.PermanentMemorial)]
    [TestCase(CampZoneKind.IntroArrivalClearance)]
    [TestCase(CampZoneKind.NormalArrivalClearance)]
    [TestCase(CampZoneKind.CirculationClearance)]
    public void PermanentZonesAreNeverDiggable(CampZoneKind kind)
    {
        Assert.That(CampSpatialPolicy.Classify(new[] { kind }), Is.EqualTo(CampDigCategory.NeverDiggable));
    }

    [Test]
    public void PermanentProtectionWinsOverOtherCategories()
    {
        Assert.That(CampSpatialPolicy.Classify(new[] { CampZoneKind.UnstableCollapse, CampZoneKind.PermanentExit }),
            Is.EqualTo(CampDigCategory.NeverDiggable));
    }

    [Test]
    public void CollapseAndUnreservedTerrainRetainDistinctDigCategories()
    {
        Assert.That(CampSpatialPolicy.Classify(new[] { CampZoneKind.UnstableCollapse }), Is.EqualTo(CampDigCategory.CollapseEligible));
        Assert.That(CampSpatialPolicy.Classify(new[] { CampZoneKind.GeneralUnreserved }), Is.EqualTo(CampDigCategory.NormalCampDiggable));
    }

    [Test]
    public void HomeMilestoneRequiresFirstValidBuddyAndDoesNotReplay()
    {
        Assert.That(CampLifecyclePolicy.ShouldStartHomeMilestone(0, false), Is.False);
        Assert.That(CampLifecyclePolicy.ShouldStartHomeMilestone(1, false), Is.True);
        Assert.That(CampLifecyclePolicy.ShouldStartHomeMilestone(1, true), Is.False);
    }

    [Test]
    public void ResidentialProtectionRemainsAfterCompletion()
    {
        Assert.That(CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(CampDigCategory.ResidentialReserved), Is.False);
        Assert.That(CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(CampDigCategory.NeverDiggable), Is.False);
        Assert.That(CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(CampDigCategory.NormalCampDiggable), Is.True);
    }

    [Test]
    public void ContinuingLineageWithRecordedDeathEstablishesMemorialOnce()
    {
        Assert.That(CampLifecyclePolicy.ShouldEstablishMemorial(true, false, true, false), Is.True,
            "Buddy death with a living leader, or leader death after successful succession, qualifies.");
        Assert.That(CampLifecyclePolicy.ShouldEstablishMemorial(true, false, true, true), Is.False,
            "An established memorial must not replay.");
    }

    [Test]
    public void TerminalOrUnrecordedDeathCannotEstablishMemorial()
    {
        Assert.That(CampLifecyclePolicy.ShouldEstablishMemorial(true, true, false, false), Is.False);
        Assert.That(CampLifecyclePolicy.ShouldEstablishMemorial(true, false, false, false), Is.False);
        Assert.That(CampLifecyclePolicy.ShouldEstablishMemorial(false, false, true, false), Is.False);
    }

    [Test]
    public void PromotedSuccessorMustBeAValidLivingLeader()
    {
        Assert.That(CampLifecyclePolicy.IsValidLivingLeader("successor", false, 4, true), Is.True);
        Assert.That(CampLifecyclePolicy.IsValidLivingLeader("successor", false, 4, false), Is.False);
        Assert.That(CampLifecyclePolicy.IsValidLivingLeader("successor", true, 4, true), Is.False);
        Assert.That(CampLifecyclePolicy.IsValidLivingLeader("", false, 4, true), Is.False);
    }

    static readonly CampCellRect SlotEntrance = new CampCellRect(60, 35, 2, 3);
    static CampResidentialCatalog CurrentCatalog() => CampResidentialCatalog.CreateCurrent();

    static List<ResidentialSlotRecord> CurrentSlotRecords()
    {
        var result = new List<ResidentialSlotRecord>();
        foreach (CampResidentialSlotDefinition slot in CurrentCatalog().Rooms[0].Slots)
            result.Add(slot.ToRecord());
        return result;
    }

    static List<(int x, int y)> CurrentSlotFootprint(int slotId) =>
        new List<(int x, int y)>(CurrentCatalog().GetSlot(slotId).ExcavationFootprint);

    static HashSet<(int x, int y)> CurrentEstablishedFootprint(int establishedSlots)
    {
        var result = new HashSet<(int x, int y)>();
        foreach (CampResidentialSlotDefinition slot in CurrentCatalog().GetEstablishedSlots(establishedSlots))
            foreach ((int x, int y) cell in slot.ExcavationFootprint) result.Add(cell);
        return result;
    }

    static List<(int x, int y)> CurrentConstructionRoute(int slotId) =>
        new List<(int x, int y)>(CurrentCatalog().GetSlot(slotId).ConstructionRoute);

    [Test]
    public void BuddyDigRadiusCreatesPassableThreeCellCrossSection()
    {
        ResidentialSlotRecord slot = CurrentSlotRecords()[1];
        var footprint = CurrentSlotFootprint(slot.SlotIndex);
        Assert.That(footprint, Does.Contain((67, 30)));
        Assert.That(footprint, Does.Contain((66, 30)));
        Assert.That(footprint, Does.Contain((68, 30)));
        Assert.That(CampSpatialPolicy.BuddyDigRadiusInCells, Is.GreaterThan(1.0));
    }

    [Test]
    public void SlotTwoConnectorAndCenterHaveBodyClearanceAfterAuthorizedExcavation()
    {
        var slots = CurrentSlotRecords();
        var open = CurrentEstablishedFootprint(2);
        Assert.That(CampSpatialPolicy.CanOccupyCellCenter((67, 34), open,
            CampSpatialPolicy.ResidentialClearanceRadiusInCells), Is.True);
        Assert.That(CampSpatialPolicy.CanOccupyCellCenter(slots[1].Center, open,
            CampSpatialPolicy.ResidentialClearanceRadiusInCells), Is.True);
    }

    [Test]
    public void EveryStageOneOrganicIncrementHasClearanceValidRouteToItsRestCenter()
    {
        var slots = CurrentSlotRecords();
        for (int slotIndex = 1; slotIndex <= slots.Count; slotIndex++)
        {
            ResidentialSlotRecord slot = slots[slotIndex - 1];
            var footprint = CurrentSlotFootprint(slot.SlotIndex);
            var open = CurrentEstablishedFootprint(slotIndex);
            var clearanceOpen = new HashSet<(int x, int y)>();
            foreach (var cell in open)
                if (CampSpatialPolicy.CanOccupyCellCenter(cell, open,
                    CampSpatialPolicy.ResidentialClearanceRadiusInCells)) clearanceOpen.Add(cell);

            Assert.That(CampSpatialPolicy.CanOccupyCellCenter(slot.Center, open,
                CampSpatialPolicy.ResidentialClearanceRadiusInCells), Is.True, "Slot " + slotIndex + " center");
            if (slotIndex > 1)
                Assert.That(CampSpatialPolicy.BuildOpenCellRoute(slot.Approach, slot.Center, clearanceOpen),
                    Is.Not.Empty, "Slot " + slotIndex + " has no body-clearance-valid connector route.");
            Assert.That(footprint.Count, Is.LessThan(100),
                "Slot " + slotIndex + " increment escaped its intentionally local authored hollow.");
        }
    }

    [Test]
    public void ConnectedPocketTargetsRemainWithinGenericBuddyDigReach()
    {
        var slots = CurrentSlotRecords();
        for (int slotIndex = 2; slotIndex <= slots.Count; slotIndex++)
        {
            ResidentialSlotRecord slot = slots[slotIndex - 1];
            var previous = slot.Approach;
            foreach (var target in slot.DigTargets)
            {
                int dx = System.Math.Abs(target.x - previous.x);
                int dy = System.Math.Abs(target.y - previous.y);
                Assert.That(System.Math.Max(dx, dy), Is.EqualTo(1),
                    "Slot " + slotIndex + " disconnected Dig target.");
                previous = target;
            }
            Assert.That(previous, Is.EqualTo(slot.Center));
        }
    }

    [Test]
    public void DigAuthorityProtectsPermanentAndResidentialTerrain()
    {
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.NeverDiggable, TerrainDigAuthority.Buddy, false), Is.False);
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.ResidentialReserved, TerrainDigAuthority.Player, false), Is.False);
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.ResidentialReserved, TerrainDigAuthority.Buddy, false), Is.False);
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.ResidentialReserved, TerrainDigAuthority.ResidentialProgression, true), Is.True);
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.ResidentialReserved, TerrainDigAuthority.ResidentialProgression, false), Is.False);
    }

    [Test]
    public void FirstBuddyEstablishesOnlyFirstSmallSlot()
    {
        var slots = CurrentSlotRecords();
        var first = CurrentEstablishedFootprint(1);
        var all = CurrentEstablishedFootprint(10);
        Assert.That(1, Is.EqualTo(1));
        Assert.That(first.Count, Is.LessThan(all.Count));
        Assert.That(first.Count, Is.LessThan(50), "Slot 1 must remain a Baby-scale tunnel and sleeping pocket.");
    }

    [Test]
    public void ReloadFootprintContainsExactlyEstablishedSlots()
    {
        var slots = CurrentSlotRecords();
        var four = CurrentEstablishedFootprint(4);
        var fourAgain = CurrentEstablishedFootprint(4);
        var five = CurrentEstablishedFootprint(5);
        Assert.That(fourAgain, Is.EquivalentTo(four));
        Assert.That(five.Count, Is.GreaterThan(four.Count));
    }

    [Test]
    public void MarkersAndRestPointsExistOnlyForEstablishedSlots()
    {
        Assert.That(CurrentCatalog().GetEstablishedSlots(1).ConvertAll(slot => slot.GlobalSlotId),
            Is.EqualTo(new[] { 1 }));
        Assert.That(CurrentCatalog().GetEstablishedSlots(0), Is.Empty);
    }

    [Test]
    public void FireSocialDestinationMustBeOpenFloor()
    {
        Assert.That(CampSpatialPolicy.IsFireSocialDestinationValid(true, false), Is.True);
        Assert.That(CampSpatialPolicy.IsFireSocialDestinationValid(true, true), Is.False);
    }

    [Test]
    public void FirstAndSecondBuddyRequireConstructionOnlyBeyondEstablishedCapacity()
    {
        var first = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("buddy-a", 0, true)
        }, 0, 10);
        Assert.That(first.UnassignedLivingBuddyIds, Is.EqualTo(new[] { "buddy-a" }));

        var second = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("buddy-a", 1, true),
            new ResidentialOccupantRecord("buddy-b", 0, true)
        }, 1, 10);
        Assert.That(second.Assignments["buddy-a"], Is.EqualTo(1));
        Assert.That(second.UnassignedLivingBuddyIds, Is.EqualTo(new[] { "buddy-b" }));
    }

    [Test]
    public void ValidAssignmentsSurviveAndVacantCapacityIsReusedBeforeConstruction()
    {
        var result = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("buddy-a", 3, true),
            new ResidentialOccupantRecord("buddy-new", 0, true)
        }, 5, 10);
        Assert.That(result.Assignments["buddy-a"], Is.EqualTo(3));
        Assert.That(result.Assignments["buddy-new"], Is.EqualTo(1));
        Assert.That(result.UnassignedLivingBuddyIds, Is.Empty);
        Assert.That(result.VacantEstablishedSlots, Is.EquivalentTo(new[] { 2, 4, 5 }));
    }

    [Test]
    public void DeadBuddyDoesNotOccupyOrReduceEstablishedCapacity()
    {
        int established = 10;
        var result = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("dead", 4, false),
            new ResidentialOccupantRecord("living", 2, true)
        }, established, 10);
        Assert.That(result.Assignments["dead"], Is.Zero);
        Assert.That(result.VacantEstablishedSlots, Does.Contain(4));
        Assert.That(established, Is.EqualTo(10), "Occupancy repair must not mutate physical capacity.");
    }

    [Test]
    public void DuplicateAndInvalidAssignmentsRepairDeterministically()
    {
        var result = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("buddy-b", 2, true),
            new ResidentialOccupantRecord("buddy-a", 2, true),
            new ResidentialOccupantRecord("buddy-c", 99, true)
        }, 3, 10);
        Assert.That(result.Assignments["buddy-a"], Is.EqualTo(2), "Stable identity ordering wins duplicate claims.");
        Assert.That(result.Assignments["buddy-b"], Is.EqualTo(1));
        Assert.That(result.Assignments["buddy-c"], Is.EqualTo(3));
    }

    [Test]
    public void AssignmentToUnestablishedSlotIsRejectedAndLeaderClaimIsReleased()
    {
        var result = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("promoted-leader", 1, false),
            new ResidentialOccupantRecord("buddy", 5, true)
        }, 2, 10);
        Assert.That(result.Assignments["promoted-leader"], Is.Zero);
        Assert.That(result.Assignments["buddy"], Is.EqualTo(1));
        Assert.That(result.VacantEstablishedSlots, Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public void ConstructionCannotCommitWithoutSuccessfulDirtRemovalAndOpenFootprint()
    {
        Assert.That(CampSpatialPolicy.CanCommitResidentialConstruction(1, 0, 0), Is.False);
        Assert.That(CampSpatialPolicy.CanCommitResidentialConstruction(1, 1, 1), Is.False);
        Assert.That(CampSpatialPolicy.CanCommitResidentialConstruction(1, 1, 0), Is.True);
        Assert.That(CampSpatialPolicy.CanRunResidentialSuccessCompletion(false, false), Is.False);
        Assert.That(CampSpatialPolicy.CanRunResidentialSuccessCompletion(true, false), Is.False);
        Assert.That(CampSpatialPolicy.CanRunResidentialSuccessCompletion(true, true), Is.True);
    }

    [Test]
    public void ResidentialAuthorizationRequiresCatalogProgressionAndExactExpectedSlotFootprint()
    {
        Assert.That(CampSpatialPolicy.CanAuthorizeResidentialProgression(1, 1, true), Is.True);
        Assert.That(CampSpatialPolicy.CanAuthorizeResidentialProgression(1, 1, false), Is.False,
            "A wrong slot footprint must be rejected even where pockets overlap.");
        Assert.That(CampSpatialPolicy.CanAuthorizeResidentialProgression(2, 1, true), Is.False);
        Assert.That(CampSpatialPolicy.CanAuthorizeResidentialProgression(2, 2, true), Is.True,
            "A future catalog room progression is not architecturally capped at Room 1.");
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.ResidentialReserved, TerrainDigAuthority.Buddy, true), Is.False);
    }

    [Test]
    public void SlotOneRadiusIntersectsRealCanonicalTunnelAndSlotTwoUsesSamePolicy()
    {
        var slots = CurrentSlotRecords();
        var slotOne = CurrentSlotFootprint(slots[0].SlotIndex);
        var slotTwo = CurrentSlotFootprint(slots[1].SlotIndex);
        Assert.That(slotOne, Does.Contain((60, 36)));
        Assert.That(slotOne, Does.Contain((62, 36)));
        Assert.That(slotOne, Does.Contain((69, 36)));
        Assert.That(slotTwo, Does.Contain(slots[1].Center));
        Assert.That(slotTwo, Is.Not.Empty);
    }

    [Test]
    public void SlotOneTunnelIsContinuouslyThreeCellsHighForBuddyClearance()
    {
        ResidentialSlotRecord slot = CurrentSlotRecords()[0];
        var footprint = CurrentSlotFootprint(slot.SlotIndex);
        Assert.That(slot.DigTargets[slot.DigTargets.Count - 1], Is.EqualTo((69, 36)));
        for (int x = 60; x <= 63; x++)
        {
            Assert.That(footprint, Does.Contain((x, 35)), "Tunnel ceiling clearance missing at x=" + x);
            Assert.That(footprint, Does.Contain((x, 36)), "Tunnel center missing at x=" + x);
            Assert.That(footprint, Does.Contain((x, 37)), "Tunnel floor clearance missing at x=" + x);
        }
    }

    [Test]
    public void CatalogConstructionRoutesFollowDeclaredDependencies()
    {
        CampResidentialRoomDefinition room = CurrentCatalog().Rooms[0];
        Assert.That(room.ExteriorStagingCell, Is.Not.EqualTo(room.FirstLockedEntranceCell));
        foreach (CampResidentialSlotDefinition slot in room.Slots)
        {
            Assert.That(slot.ConstructionRoute, Is.Not.Empty, "Slot " + slot.GlobalSlotId);
            Assert.That(slot.ConstructionRoute[0], Is.EqualTo(room.ExteriorStagingCell),
                "Every constructor starts at its room-owned exterior staging cell.");
            Assert.That(slot.ConstructionRoute[slot.ConstructionRoute.Count - 1], Is.EqualTo(slot.Approach));
            Assert.That(slot.DependencyGlobalSlotId, Is.LessThan(slot.GlobalSlotId));
        }
    }

    [Test]
    public void FailedConstructionIsNotPresentedAsACompletedMilestone()
    {
        Assert.That(CampSpatialPolicy.ShouldPresentResidentialMilestone(0), Is.False);
        Assert.That(CampSpatialPolicy.ShouldPresentResidentialMilestone(1), Is.True);
    }

    [Test]
    public void SlotTwoPostDigRouteUsesOpenConnectorCellsBeforeCenter()
    {
        var slots = CurrentSlotRecords();
        var open = CurrentEstablishedFootprint(1);
        foreach (var cell in CurrentSlotFootprint(slots[1].SlotIndex))
            open.Add(cell);

        var route = CampSpatialPolicy.BuildOpenCellRoute(slots[1].Approach, slots[1].Center, open);
        Assert.That(route.Count, Is.GreaterThan(1));
        Assert.That(route[route.Count - 1], Is.EqualTo(slots[1].Center));
        Assert.That(route.GetRange(0, route.Count - 1), Is.Not.Empty,
            "Slot 2 must enter through connector cells instead of steering directly to its center.");
        foreach (var waypoint in route) Assert.That(open, Does.Contain(waypoint));
    }

    [TestCase(3)]
    [TestCase(4)]
    public void BranchPostDigRoutesStayInsideActuallyOpenCanonicalCells(int slotIndex)
    {
        var slots = CurrentSlotRecords();
        ResidentialSlotRecord slot = slots[slotIndex - 1];
        var open = CurrentEstablishedFootprint(slotIndex - 1);
        foreach (var cell in CurrentSlotFootprint(slot.SlotIndex)) open.Add(cell);

        var route = CampSpatialPolicy.BuildOpenCellRoute(slot.Approach, slot.Center, open);
        Assert.That(route, Is.Not.Empty, "Slot " + slotIndex + " needs a connected post-Dig route.");
        Assert.That(route[route.Count - 1], Is.EqualTo(slot.Center));
        foreach (var waypoint in route) Assert.That(open, Does.Contain(waypoint));
    }

    [Test]
    public void FinalAdvanceIsRequiredBeforeResidentialCommit()
    {
        Assert.That(CampSpatialPolicy.CanCommitResidentialConstruction(1, 1, 0, false), Is.False);
        Assert.That(CampSpatialPolicy.CanCommitResidentialConstruction(1, 1, 0, true), Is.True);
    }

    [Test]
    public void ActiveAndReserveCampBuddiesKeepCanonicalFormScale()
    {
        Assert.That(CampBuddyPhysicalPolicy.GetScaleMultiplier(true), Is.EqualTo(1f));
        Assert.That(CampBuddyPhysicalPolicy.GetScaleMultiplier(false), Is.EqualTo(1f));
    }

    [Test]
    public void EqualSavedStatsProduceEqualActiveAndReserveCampSpeed()
    {
        const float savedMoveSpeed = 3.5f;
        float activeSpeed = CampBuddyPhysicalPolicy.GetMovementSpeed(savedMoveSpeed, true);
        float reserveSpeed = CampBuddyPhysicalPolicy.GetMovementSpeed(savedMoveSpeed, false);

        Assert.That(activeSpeed, Is.EqualTo(1.575f).Within(0.0001f));
        Assert.That(reserveSpeed, Is.EqualTo(activeSpeed));
    }

    [Test]
    public void LegitimateSavedSpeedDifferenceIsPreservedWithoutParticipationPenalty()
    {
        Assert.That(CampBuddyPhysicalPolicy.GetMovementSpeed(2f, false), Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That(CampBuddyPhysicalPolicy.GetMovementSpeed(4f, true), Is.EqualTo(1.8f).Within(0.0001f));
    }

    [Test]
    public void DirectedWalkTimeoutUsesTheSameEffectiveSpeedAsMovement()
    {
        float actualSpeed = CampBuddyPhysicalPolicy.GetMovementSpeed(3.5f, false);
        float timeout = CampBuddyPhysicalPolicy.GetDirectedWalkTimeout(3.15f, actualSpeed, 0.5f);

        Assert.That(timeout, Is.EqualTo(3.25f).Within(0.0001f));
    }

    [Test]
    public void EveryConstructionWaypointRequiresFullBodyClearance()
    {
        Assert.That(CampBuddyPhysicalPolicy.RequiresFullWaypointClearance(0, 1), Is.True);
        Assert.That(CampBuddyPhysicalPolicy.RequiresFullWaypointClearance(0, 2), Is.True);
        Assert.That(CampBuddyPhysicalPolicy.RequiresFullWaypointClearance(1, 2), Is.True);
    }

    [Test]
    public void FreshFirstBuddyRequiresSlotOneConstructionAndArrivalPhase()
    {
        var occupancy = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("first-buddy", 0, true)
        }, 0, CampResidentialCatalog.CurrentRuntimeCapacity);

        CampResidentialArrivalEvaluation arrival = CampArrivalPolicy.EvaluateResidentialWork(
            occupancy, 1, 0, 0, CampResidentialCatalog.CurrentRuntimeCapacity);

        Assert.That(arrival.EstablishedCapacity, Is.Zero);
        Assert.That(arrival.VacantEstablishedSlots, Is.Zero);
        Assert.That(arrival.UnassignedBuddies, Is.EqualTo(1));
        Assert.That(arrival.PendingConstructionCount, Is.EqualTo(1));
        Assert.That(arrival.FirstSlot, Is.EqualTo(1));
        Assert.That(arrival.ArrivalPhase, Is.True);
    }

    [Test]
    public void ResidentialArrivalWorkHandlesEmptyReloadSecondBuddyAndVacancyReuse()
    {
        var empty = CampSpatialPolicy.ResolveResidentialOccupancy(
            new ResidentialOccupantRecord[0], 0, CampResidentialCatalog.CurrentRuntimeCapacity);
        Assert.That(CampArrivalPolicy.EvaluateResidentialWork(empty, 0, 0, 0,
            CampResidentialCatalog.CurrentRuntimeCapacity).PendingConstructionCount, Is.Zero);

        var reloadedPending = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("first-buddy", 0, true)
        }, 0, CampResidentialCatalog.CurrentRuntimeCapacity);
        Assert.That(CampArrivalPolicy.EvaluateResidentialWork(reloadedPending, 1, 0, 0,
            CampResidentialCatalog.CurrentRuntimeCapacity).FirstSlot, Is.EqualTo(1));

        var completed = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("first-buddy", 1, true)
        }, 1, CampResidentialCatalog.CurrentRuntimeCapacity);
        Assert.That(CampArrivalPolicy.EvaluateResidentialWork(completed, 1, 0, 1,
            CampResidentialCatalog.CurrentRuntimeCapacity).PendingConstructionCount, Is.Zero);

        var second = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("first-buddy", 1, true),
            new ResidentialOccupantRecord("second-buddy", 0, true)
        }, 1, CampResidentialCatalog.CurrentRuntimeCapacity);
        CampResidentialArrivalEvaluation secondArrival = CampArrivalPolicy.EvaluateResidentialWork(
            second, 2, 0, 1, CampResidentialCatalog.CurrentRuntimeCapacity);
        Assert.That(secondArrival.PendingConstructionCount, Is.EqualTo(1));
        Assert.That(secondArrival.FirstSlot, Is.EqualTo(2));

        var vacancy = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("new-buddy", 0, true)
        }, 1, CampResidentialCatalog.CurrentRuntimeCapacity);
        CampResidentialArrivalEvaluation vacancyArrival = CampArrivalPolicy.EvaluateResidentialWork(
            vacancy, 1, 1, 1, CampResidentialCatalog.CurrentRuntimeCapacity);
        Assert.That(vacancyArrival.PendingConstructionCount, Is.Zero);
        Assert.That(vacancyArrival.ArrivalPhase, Is.True);
    }

    [Test]
    public void SemanticActivityPointsRespectPersonalResidentialOwnership()
    {
        Assert.That(CampArrivalPolicy.CanUseActivityPoint(true, true, 2, 2), Is.True);
        Assert.That(CampArrivalPolicy.CanUseActivityPoint(true, true, 2, 1), Is.False);
        Assert.That(CampArrivalPolicy.CanUseActivityPoint(true, false, 2, 2), Is.False);
        Assert.That(CampArrivalPolicy.CanUseActivityPoint(false, true, 0, 1), Is.True);
    }

    [Test]
    public void ArrivalPhaseOnlyRunsForVacancyClaimsOrConstruction()
    {
        Assert.That(CampArrivalPolicy.ShouldBegin(0, 0), Is.False);
        Assert.That(CampArrivalPolicy.ShouldBegin(1, 0), Is.True);
        Assert.That(CampArrivalPolicy.ShouldBegin(0, 2), Is.True);
        Assert.That(CampArrivalPolicy.ShouldReleaseToWander(true, 1), Is.False);
        Assert.That(CampArrivalPolicy.ShouldReleaseToWander(true, 0), Is.True);
    }

    [Test]
    public void OnlyActiveSquadUsesPlayerArrivalSpawn()
    {
        Assert.That(CampArrivalPolicy.ShouldSpawnAtPlayerArrival(true), Is.True);
        Assert.That(CampArrivalPolicy.ShouldSpawnAtPlayerArrival(false), Is.False);
    }

    [Test]
    public void RuntimeResidentialCatalogPreservesRoomOneGeometryAndCapacity()
    {
        CampResidentialCatalog catalog = CampResidentialCatalog.CreateCurrent();
        Assert.That(catalog.TotalCapacity, Is.EqualTo(10));
        Assert.That(catalog.Rooms.Count, Is.EqualTo(1));
        CampResidentialRoomDefinition room = catalog.Rooms[0];
        Assert.That(room.ExteriorStagingCell, Is.EqualTo((58, 36)));
        Assert.That(room.FirstLockedEntranceCell, Is.EqualTo((60, 36)));
        Assert.That(room.RoomId, Is.EqualTo("first-burrow"));
        Assert.That(room.ProgressionIndex, Is.EqualTo(1));
        Assert.That(room.RequiresBreakthrough, Is.False);
        Assert.That(room.Entrance, Is.EqualTo(SlotEntrance));
        Assert.That(room.ProtectedEnvelope, Is.EqualTo(new CampCellRect(55, 13, 63, 49)));

        (int x, int y)[] expectedCenters =
        {
            (69, 36), (70, 22), (71, 50), (86, 56), (94, 48),
            (91, 36), (99, 23), (102, 42), (86, 18), (112, 35)
        };
        for (int i = 0; i < expectedCenters.Length; i++)
        {
            CampResidentialSlotDefinition slot = room.Slots[i];
            Assert.That(slot.GlobalSlotId, Is.EqualTo(i + 1));
            Assert.That(slot.Center, Is.EqualTo(expectedCenters[i]));
            Assert.That(slot.RestCell, Is.EqualTo(expectedCenters[i]));
            Assert.That(slot.ExcavationFootprint, Is.Not.Empty);
            Assert.That(slot.ConstructionRoute, Is.Not.Empty);
        }
        Assert.That(catalog.GetSlot(11), Is.Null,
            "The Editor master plan must not create runtime capacity for Buddy 11.");
    }

    [Test]
    public void EveryCurrentSlotHasSequentialLocalDigCoverageFromItsAuthoredSpine()
    {
        CampResidentialCatalog catalog = CampResidentialCatalog.CreateCurrent();
        const double cellSize = 0.6d;
        const double terrainEffectRadius = 0.72d;
        const double livePhysicalReach = 0.72d + 0.375d;
        foreach (CampResidentialSlotDefinition slot in catalog.Rooms[0].Slots)
        {
            var stands = new List<(int x, int y)> { slot.Approach };
            stands.AddRange(slot.DigTargets);
            for (int index = 0; index < stands.Count - 1; index++)
                Assert.That(CellDistance(stands[index], stands[index + 1]) * cellSize,
                    Is.LessThanOrEqualTo(livePhysicalReach + 0.0001d),
                    "Slot " + slot.GlobalSlotId + " authored step " + index + " exceeds live Dig reach.");

            foreach ((int x, int y) required in slot.ExcavationFootprint)
            {
                bool covered = false;
                foreach ((int x, int y) stand in stands)
                foreach ((int x, int y) center in slot.ExcavationFootprint)
                    if (CellDistance(stand, center) * cellSize <= livePhysicalReach + 0.0001d &&
                        CellDistance(center, required) * cellSize <= terrainEffectRadius + 0.0001d)
                    {
                        covered = true;
                        break;
                    }
                Assert.That(covered, Is.True,
                    "Slot " + slot.GlobalSlotId + " required cell (" + required.x + "," + required.y +
                    ") has no reachable local Dig center.");
            }
        }
    }

    static double CellDistance((int x, int y) a, (int x, int y) b)
    {
        double dx = a.x - b.x;
        double dy = a.y - b.y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    [Test]
    public void EleventhBuddyRemainsUnassignedAtCatalogCapacity()
    {
        CampResidentialCatalog catalog = CampResidentialCatalog.CreateCurrent();
        List<ResidentialOccupantRecord> occupants = new List<ResidentialOccupantRecord>();
        for (int i = 1; i <= 11; i++)
            occupants.Add(new ResidentialOccupantRecord("buddy-" + i.ToString("00"), i <= 10 ? i : 0, true));

        ResidentialOccupancyResolution resolution = CampSpatialPolicy.ResolveResidentialOccupancy(
            occupants, catalog.TotalCapacity, catalog.TotalCapacity);

        Assert.That(resolution.Assignments["buddy-11"], Is.Zero);
        Assert.That(resolution.UnassignedLivingBuddyIds, Is.EquivalentTo(new[] { "buddy-11" }));
    }

    [Test]
    public void SyntheticCatalogRetainsGlobalSlotsAboveTenAndRejectsOutOfRangeIds()
    {
        CampResidentialCatalog catalog = BuildSyntheticCatalog(12);

        Assert.That(catalog.TotalCapacity, Is.EqualTo(12));
        Assert.That(catalog.IsValidGlobalSlot(11), Is.True);
        Assert.That(catalog.NormalizeGlobalSlotId(11), Is.EqualTo(11));
        Assert.That(catalog.NormalizeGlobalSlotId(13), Is.Zero);
        Assert.That(catalog.NormalizeGlobalSlotId(0), Is.Zero);
        Assert.That(catalog.TryGetRoomForSlot(11, out CampResidentialRoomDefinition room), Is.True);
        Assert.That(room.RoomId, Is.EqualTo("synthetic-room-2"));
    }

    [Test]
    public void SyntheticCapacityRepairsVacanciesDuplicatesAndNonlivingAssignmentsAboveTen()
    {
        ResidentialOccupancyResolution resolution = CampSpatialPolicy.ResolveResidentialOccupancy(new[]
        {
            new ResidentialOccupantRecord("a-retained", 11, true),
            new ResidentialOccupantRecord("b-duplicate", 11, true),
            new ResidentialOccupantRecord("c-vacancy", 0, true),
            new ResidentialOccupantRecord("d-dead", 12, false),
            new ResidentialOccupantRecord("e-leader", 10, false)
        }, 12, 12);

        Assert.That(resolution.Assignments["a-retained"], Is.EqualTo(11));
        Assert.That(resolution.Assignments["b-duplicate"], Is.EqualTo(1));
        Assert.That(resolution.Assignments["c-vacancy"], Is.EqualTo(2));
        Assert.That(resolution.Assignments["d-dead"], Is.Zero);
        Assert.That(resolution.Assignments["e-leader"], Is.Zero);
        Assert.That(resolution.VacantEstablishedSlots, Does.Contain(12));
    }

    [Test]
    public void EstablishedSlotEnumerationUsesCatalogDefinitionsBeyondTen()
    {
        CampResidentialCatalog catalog = BuildSyntheticCatalog(12);
        List<CampResidentialSlotDefinition> established = catalog.GetEstablishedSlots(11);

        Assert.That(established.Count, Is.EqualTo(11));
        Assert.That(established[10].GlobalSlotId, Is.EqualTo(11));
        Assert.That(established[10].ExcavationFootprint, Is.EquivalentTo(new[] { (111, 211) }));
        Assert.That(catalog.GetEstablishedSlots(99).Count, Is.EqualTo(12));
    }

    [Test]
    public void FirstBurrowSleepingClustersRoutesAndDefaultClearanceAreValid()
    {
        CampResidentialCatalog catalog = CampResidentialCatalog.CreateCurrent();
        Assert.That(catalog.TotalCapacity, Is.EqualTo(10));
        Assert.That(catalog.Rooms[0].SleepingClusters.Count, Is.EqualTo(4));
        Assert.That(catalog.ValidateGeometry(CampResidentialClearanceProfile.CurrentBaby), Is.Empty);

        for (int slotId = 1; slotId <= 10; slotId++)
        {
            CampResidentialSlotDefinition slot = catalog.GetSlot(slotId);
            Assert.That(slot, Is.Not.Null);
            Assert.That(slot.RestCell, Is.EqualTo(slot.Center));
            Assert.That(slot.AuthoredRouteSpine, Is.Not.Empty);
            Assert.That(catalog.GetSleepingCluster(slot.SleepingClusterId), Is.Not.Null);
            Assert.That(slot.GetRequiredOpenCells(CampResidentialClearanceProfile.CurrentBaby),
                Is.EquivalentTo(slot.ExcavationFootprint),
                "The new geometry model must preserve the exact working Small/Baby mask.");
        }
    }

    [Test]
    public void HypotheticalLargerClearanceWidensOnlyInsideAuthoredSlotEnvelopes()
    {
        CampResidentialCatalog catalog = CampResidentialCatalog.CreateCurrent();
        var hypothetical = new CampResidentialClearanceProfile(
            ResidentialClearanceTier.HypotheticalLarger, 2d);
        Assert.That(catalog.ValidateGeometry(hypothetical), Is.Empty);

        bool observedAdditionalClearance = false;
        foreach (CampResidentialSlotDefinition slot in catalog.Rooms[0].Slots)
        {
            HashSet<(int x, int y)> reserved = new HashSet<(int x, int y)>(slot.ReservedExpansionEnvelope);
            HashSet<(int x, int y)> current = new HashSet<(int x, int y)>(
                slot.GetRequiredOpenCells(CampResidentialClearanceProfile.CurrentBaby));
            List<(int x, int y)> larger = slot.GetRequiredOpenCells(hypothetical);
            foreach ((int x, int y) cell in larger) Assert.That(reserved.Contains(cell), Is.True);
            if (larger.Exists(cell => !current.Contains(cell))) observedAdditionalClearance = true;
        }
        Assert.That(observedAdditionalClearance, Is.True,
            "The hypothetical profile should prove widening without changing production gameplay.");
    }

    [Test]
    public void SharedRouteCellsRemainAuthoredGeometryWithoutSharedBedOwnership()
    {
        CampResidentialCatalog catalog = CampResidentialCatalog.CreateCurrent();
        HashSet<(int x, int y)> shared = catalog.GetSharedRouteCells();
        Assert.That(shared, Is.Not.Empty);

        HashSet<int> authoredMembers = new HashSet<int>();
        foreach (CampResidentialSleepingClusterDefinition cluster in catalog.Rooms[0].SleepingClusters)
            foreach (int slotId in cluster.MemberGlobalSlotIds)
                Assert.That(authoredMembers.Add(slotId), Is.True,
                    "A personal slot may belong to only one sleeping cluster.");
        Assert.That(authoredMembers, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
        foreach (CampResidentialSlotDefinition slot in catalog.Rooms[0].Slots)
            Assert.That(slot.RestCell, Is.EqualTo(slot.Center),
                "Shared route/chamber geometry must not merge personal rest ownership.");
    }

    [Test]
    public void EveryOrganicArrivalAddsAuthorizedGeometryWithoutFillingItsBoundingRectangle()
    {
        CampResidentialCatalog catalog = CampResidentialCatalog.CreateCurrent();
        HashSet<(int x, int y)> established = new HashSet<(int x, int y)>();
        foreach (CampResidentialSlotDefinition slot in catalog.Rooms[0].Slots)
        {
            List<(int x, int y)> required = slot.GetRequiredOpenCells(
                CampResidentialClearanceProfile.CurrentBaby);
            int added = 0;
            foreach ((int x, int y) cell in required) if (established.Add(cell)) added++;
            Assert.That(added, Is.GreaterThan(0),
                "Slot " + slot.GlobalSlotId + " must visibly extend or enlarge the neighborhood.");
        }

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach ((int x, int y) cell in established)
        {
            minX = Math.Min(minX, cell.x); maxX = Math.Max(maxX, cell.x);
            minY = Math.Min(minY, cell.y); maxY = Math.Max(maxY, cell.y);
        }
        int boundingArea = (maxX - minX + 1) * (maxY - minY + 1);
        Assert.That(established.Count, Is.LessThan(boundingArea * 0.7),
            "The final First Burrow must retain a lumpy branched silhouette rather than fill a room rectangle.");
    }

    [Test]
    public void FirstHomeMovementUsesTemporaryDoubleCampSpeed()
    {
        Assert.That(CampArrivalPolicy.GetCampMovementSpeed(1.575f, true), Is.EqualTo(3.15f).Within(0.0001f));
        Assert.That(CampArrivalPolicy.GetCampMovementSpeed(1.575f, false), Is.EqualTo(1.575f).Within(0.0001f),
            "Completing first-home arrival selects the ordinary Camp speed immediately.");
        Assert.That(CampBuddyPhysicalPolicy.GetMovementSpeed(3.5f, false), Is.EqualTo(1.575f).Within(0.0001f),
            "The saved/base Camp speed remains unchanged after the temporary profile.");
    }

    [Test]
    public void OnlyPreviouslyHomelessBuddiesRunTheFirstHomeRoutine()
    {
        Assert.That(CampArrivalPolicy.IsFirstHomeClaim(true, 3), Is.True,
            "A newcomer assigned an established vacant home still performs first-home arrival.");
        Assert.That(CampArrivalPolicy.IsFirstHomeClaim(false, 3), Is.False,
            "A returning resident must not repeat first-home arrival.");
        Assert.That(CampArrivalPolicy.IsFirstHomeClaim(true, 0), Is.False,
            "A homeless Buddy without an assigned established home remains a construction candidate.");
    }

    [Test]
    public void ConstructionReservationsAreDistinctContiguousAndDependencyGated()
    {
        Assert.That(CampArrivalPolicy.ReserveContiguousConstructionSlots(4, 3),
            Is.EqualTo(new[] { 4, 5, 6 }));
        Assert.That(CampArrivalPolicy.CanBeginReservedConstruction(4, 3, 3), Is.True);
        Assert.That(CampArrivalPolicy.CanBeginReservedConstruction(5, 4, 3), Is.False);
        Assert.That(CampArrivalPolicy.CanBeginReservedConstruction(5, 4, 4), Is.True);
        Assert.That(CampArrivalPolicy.CanBeginReservedConstruction(6, 1, 4), Is.False,
            "A later branch still cannot commit ahead of the contiguous global slot prefix.");
    }

    [Test]
    public void TenNewBuddiesReserveTenDistinctContiguousFirstHomeTargetsWithoutACap()
    {
        List<int> reservations = CampArrivalPolicy.ReserveContiguousConstructionSlots(1, 10);

        Assert.That(reservations, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
        Assert.That(new HashSet<int>(reservations).Count, Is.EqualTo(10));

        CampResidentialCatalog catalog = CampResidentialCatalog.CreateCurrent();
        foreach (int slotId in reservations)
            Assert.That(catalog.GetSlot(slotId), Is.Not.Null,
                "Every concurrently-started first-home routine must have a valid authored target.");
    }

    static CampResidentialCatalog BuildSyntheticCatalog(int capacity)
    {
        List<CampResidentialSlotDefinition> first = new List<CampResidentialSlotDefinition>();
        List<CampResidentialSlotDefinition> second = new List<CampResidentialSlotDefinition>();
        for (int slotId = 1; slotId <= capacity; slotId++)
        {
            var cell = (x: 100 + slotId, y: 200 + slotId);
            var definition = new CampResidentialSlotDefinition(slotId, Math.Max(0, slotId - 1),
                cell, cell, cell, new[] { cell }, new[] { cell }, new[] { cell });
            (slotId <= 10 ? first : second).Add(definition);
        }
        List<CampResidentialRoomDefinition> rooms = new List<CampResidentialRoomDefinition>
        {
            new CampResidentialRoomDefinition("synthetic-room-1", "Synthetic Room 1", 1,
                new CampCellRect(100, 200, 20, 20), new CampCellRect(100, 200, 1, 1), first)
        };
        if (second.Count > 0)
            rooms.Add(new CampResidentialRoomDefinition("synthetic-room-2", "Synthetic Room 2", 2,
                new CampCellRect(110, 210, 20, 20), new CampCellRect(110, 210, 1, 1), second, true));
        return new CampResidentialCatalog(rooms);
    }
}
