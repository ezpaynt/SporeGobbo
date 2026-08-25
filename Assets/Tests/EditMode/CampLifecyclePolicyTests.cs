using NUnit.Framework;
using SporeGobbo.CampLifecycle;
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
    public void ResidentialAndCollapseHaveDistinctDigCategories()
    {
        Assert.That(CampSpatialPolicy.Classify(new[] { CampZoneKind.ResidentialStage3 }), Is.EqualTo(CampDigCategory.ResidentialReserved));
        Assert.That(CampSpatialPolicy.Classify(new[] { CampZoneKind.UnstableCollapse }), Is.EqualTo(CampDigCategory.CollapseEligible));
        Assert.That(CampSpatialPolicy.Classify(new[] { CampZoneKind.GeneralUnreserved }), Is.EqualTo(CampDigCategory.NormalCampDiggable));
    }

    [Test]
    public void ResidentialStagesMustBeSequentiallyConnected()
    {
        var stages = new List<CampCellRect>
        {
            new CampCellRect(0, 0, 4, 4), new CampCellRect(4, 0, 4, 4), new CampCellRect(4, 4, 4, 4),
            new CampCellRect(8, 4, 4, 4), new CampCellRect(8, 0, 4, 4)
        };
        Assert.That(CampSpatialPolicy.IsOrderedAndConnected(stages), Is.True);
        stages[4] = new CampCellRect(20, 20, 4, 4);
        Assert.That(CampSpatialPolicy.IsOrderedAndConnected(stages), Is.False);
    }

    [Test]
    public void ValidationRejectsResidentialOverlapWithPermanentStructure()
    {
        var zones = new List<CampZoneRecord>
        {
            new CampZoneRecord(CampZoneKind.ResidentialStage1, new CampCellRect(0, 0, 5, 5)),
            new CampZoneRecord(CampZoneKind.PermanentMemorial, new CampCellRect(4, 4, 5, 5))
        };
        Assert.That(CampSpatialPolicy.Validate(zones), Is.Not.Empty);
    }

    [Test]
    public void HomeMilestoneRequiresFirstValidBuddyAndDoesNotReplay()
    {
        Assert.That(CampLifecyclePolicy.ShouldStartHomeMilestone(0, false), Is.False);
        Assert.That(CampLifecyclePolicy.ShouldStartHomeMilestone(1, false), Is.True);
        Assert.That(CampLifecyclePolicy.ShouldStartHomeMilestone(1, true), Is.False);
    }

    [Test]
    public void OnlyNextResidentialStageCanBeProgressionExcavated()
    {
        Assert.That(CampLifecyclePolicy.CanProgressionExcavateResidentialStage(1, 0), Is.True);
        Assert.That(CampLifecyclePolicy.CanProgressionExcavateResidentialStage(2, 0), Is.False);
        Assert.That(CampLifecyclePolicy.CanProgressionExcavateResidentialStage(1, 1), Is.False);
        Assert.That(CampLifecyclePolicy.CanProgressionExcavateResidentialStage(2, 1), Is.True);
    }

    [Test]
    public void ResidentialProtectionRemainsAfterCompletion()
    {
        Assert.That(CampSpatialPolicy.Classify(new[] { CampZoneKind.ResidentialStage1 }),
            Is.EqualTo(CampDigCategory.ResidentialReserved));
        Assert.That(CampSpatialPolicy.Classify(new[] { CampZoneKind.ResidentialStage2 }),
            Is.EqualTo(CampDigCategory.ResidentialReserved));
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

    [Test]
    public void LockedResidentialStagesAndEntranceBuildAsRealDirt()
    {
        Assert.That(CampSpatialPolicy.IsResidentialTerrainZone(CampZoneKind.ResidentialEntrance), Is.True);
        Assert.That(CampSpatialPolicy.IsResidentialTerrainZone(CampZoneKind.ResidentialStage1), Is.True);
        Assert.That(CampSpatialPolicy.IsResidentialTerrainZone(CampZoneKind.ResidentialStage5), Is.True);
    }

    static readonly CampCellRect SlotEntrance = new CampCellRect(60, 35, 2, 3);
    static readonly CampCellRect SlotChamber = new CampCellRect(62, 32, 8, 9);

    [Test]
    public void BuddyDigRadiusCreatesPassableThreeCellCrossSection()
    {
        ResidentialSlotRecord slot = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber)[1];
        var footprint = CampSpatialPolicy.BuildSlotFootprint(slot, SlotEntrance, SlotChamber);
        Assert.That(footprint, Does.Contain((65, 34)));
        Assert.That(footprint, Does.Contain((64, 34)));
        Assert.That(footprint, Does.Contain((66, 34)));
        Assert.That(CampSpatialPolicy.BuddyDigRadiusInCells, Is.GreaterThan(1.0));
    }

    [Test]
    public void SlotTwoConnectorAndCenterHaveBodyClearanceAfterAuthorizedExcavation()
    {
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        var open = CampSpatialPolicy.BuildEstablishedSlotFootprint(slots, 2, SlotEntrance, SlotChamber);
        Assert.That(CampSpatialPolicy.CanOccupyCellCenter((64, 35), open,
            CampSpatialPolicy.ResidentialClearanceRadiusInCells), Is.True);
        Assert.That(CampSpatialPolicy.CanOccupyCellCenter(slots[1].Center, open,
            CampSpatialPolicy.ResidentialClearanceRadiusInCells), Is.True);
    }

    [Test]
    public void EveryStageOnePocketHasSmallClearanceValidRouteToItsRestCenter()
    {
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        for (int slotIndex = 1; slotIndex <= slots.Count; slotIndex++)
        {
            ResidentialSlotRecord slot = slots[slotIndex - 1];
            var footprint = CampSpatialPolicy.BuildSlotFootprint(slot, SlotEntrance, SlotChamber);
            var open = CampSpatialPolicy.BuildEstablishedSlotFootprint(
                slots, slotIndex, SlotEntrance, SlotChamber);
            var clearanceOpen = new HashSet<(int x, int y)>();
            foreach (var cell in open)
                if (CampSpatialPolicy.CanOccupyCellCenter(cell, open,
                    CampSpatialPolicy.ResidentialClearanceRadiusInCells)) clearanceOpen.Add(cell);

            Assert.That(CampSpatialPolicy.CanOccupyCellCenter(slot.Center, open,
                CampSpatialPolicy.ResidentialClearanceRadiusInCells), Is.True, "Slot " + slotIndex + " center");
            if (slotIndex > 1)
                Assert.That(CampSpatialPolicy.BuildOpenCellRoute(slot.Approach, slot.Center, clearanceOpen),
                    Is.Not.Empty, "Slot " + slotIndex + " has no body-clearance-valid connector route.");
            Assert.That(footprint.Count, Is.LessThan(slotIndex == 1 ? 20 : 16),
                "Slot " + slotIndex + " pocket grew beyond its small residential allowance.");
        }
    }

    [Test]
    public void ConnectedPocketTargetsRemainWithinGenericBuddyDigReach()
    {
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        for (int slotIndex = 2; slotIndex <= slots.Count; slotIndex++)
        {
            ResidentialSlotRecord slot = slots[slotIndex - 1];
            var previous = slot.Approach;
            foreach (var target in slot.DigTargets)
            {
                int cardinalDistance = System.Math.Abs(target.x - previous.x) +
                    System.Math.Abs(target.y - previous.y);
                Assert.That(cardinalDistance, Is.EqualTo(1), "Slot " + slotIndex + " disconnected Dig target.");
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
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        var first = CampSpatialPolicy.BuildEstablishedSlotFootprint(slots, 1, SlotEntrance, SlotChamber);
        var all = CampSpatialPolicy.BuildEstablishedSlotFootprint(slots, 10, SlotEntrance, SlotChamber);
        Assert.That(CampSpatialPolicy.ResidentialStageForEstablishedSlots(0, 1), Is.EqualTo(1));
        Assert.That(first.Count, Is.LessThan(all.Count));
        Assert.That(first.Count, Is.LessThan(20), "Slot 1 must remain a small tunnel and sleeping pocket.");
    }

    [Test]
    public void ReloadFootprintContainsExactlyEstablishedSlots()
    {
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        var four = CampSpatialPolicy.BuildEstablishedSlotFootprint(slots, 4, SlotEntrance, SlotChamber);
        var fourAgain = CampSpatialPolicy.BuildEstablishedSlotFootprint(slots, 4, SlotEntrance, SlotChamber);
        var five = CampSpatialPolicy.BuildEstablishedSlotFootprint(slots, 5, SlotEntrance, SlotChamber);
        Assert.That(fourAgain, Is.EquivalentTo(four));
        Assert.That(five.Count, Is.GreaterThan(four.Count));
    }

    [Test]
    public void MarkersAndRestPointsExistOnlyForEstablishedSlots()
    {
        Assert.That(CampSpatialPolicy.ShouldExposeResidentialSlot(1, 1, 1), Is.True);
        Assert.That(CampSpatialPolicy.ShouldExposeResidentialSlot(2, 1, 1), Is.False);
        Assert.That(CampSpatialPolicy.ShouldExposeResidentialSlot(1, 0, 0), Is.False);
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
    public void ResidentialAuthorizationRequiresStageOneAndExactExpectedSlotFootprint()
    {
        Assert.That(CampSpatialPolicy.CanAuthorizeResidentialProgression(1, 1, true), Is.True);
        Assert.That(CampSpatialPolicy.CanAuthorizeResidentialProgression(1, 1, false), Is.False,
            "A wrong slot footprint must be rejected even where pockets overlap.");
        Assert.That(CampSpatialPolicy.CanAuthorizeResidentialProgression(2, 1, true), Is.False);
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.ResidentialReserved, TerrainDigAuthority.Buddy, true), Is.False);
    }

    [Test]
    public void SlotOneRadiusIntersectsRealCanonicalTunnelAndSlotTwoUsesSamePolicy()
    {
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        var slotOne = CampSpatialPolicy.BuildSlotFootprint(slots[0], SlotEntrance, SlotChamber);
        var slotTwo = CampSpatialPolicy.BuildSlotFootprint(slots[1], SlotEntrance, SlotChamber);
        Assert.That(slotOne, Does.Contain((60, 36)));
        Assert.That(slotOne, Does.Contain((62, 36)));
        Assert.That(slotOne, Does.Contain((64, 36)));
        Assert.That(slotTwo, Does.Contain(slots[1].Center));
        Assert.That(slotTwo, Is.Not.Empty);
    }

    [Test]
    public void SlotOneTunnelIsContinuouslyThreeCellsHighForBuddyClearance()
    {
        ResidentialSlotRecord slot = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber)[0];
        var footprint = CampSpatialPolicy.BuildSlotFootprint(slot, SlotEntrance, SlotChamber);
        Assert.That(slot.DigTargets, Is.EqualTo(new[]
        {
            (60, 36), (61, 36), (62, 36), (63, 36), (64, 36)
        }));
        for (int x = 60; x <= 64; x++)
        {
            Assert.That(footprint, Does.Contain((x, 35)), "Tunnel ceiling clearance missing at x=" + x);
            Assert.That(footprint, Does.Contain((x, 36)), "Tunnel center missing at x=" + x);
            Assert.That(footprint, Does.Contain((x, 37)), "Tunnel floor clearance missing at x=" + x);
        }
    }

    [Test]
    public void StageOneConstructionRoutesFollowCanonicalDependencies()
    {
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        int[] dependencies = { 0, 1, 1, 1, 2, 3, 4, 4, 2, 3 };
        for (int i = 0; i < slots.Count; i++)
            Assert.That(slots[i].DependencySlotIndex, Is.EqualTo(dependencies[i]), "Slot " + (i + 1));

        Assert.That(CampSpatialPolicy.BuildStageOneConstructionRoute(1, SlotEntrance, SlotChamber),
            Is.EqualTo(new[] { (59, 36) }));
        Assert.That(CampSpatialPolicy.BuildStageOneConstructionRoute(2, SlotEntrance, SlotChamber),
            Is.EqualTo(new[] { (59, 36), (60, 36), (61, 36), (62, 36), (63, 36), (64, 36) }));
    }

    [Test]
    public void EveryConstructionWaypointIsOpenBeforeItsTargetSlotDigs()
    {
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        for (int slotIndex = 1; slotIndex <= slots.Count; slotIndex++)
        {
            var established = CampSpatialPolicy.BuildEstablishedSlotFootprint(
                slots, slotIndex - 1, SlotEntrance, SlotChamber);
            var route = CampSpatialPolicy.BuildStageOneConstructionRoute(slotIndex, SlotEntrance, SlotChamber);
            Assert.That(route, Is.Not.Empty, "Slot " + slotIndex + " needs a route.");
            Assert.That(route[0], Is.EqualTo((59, 36)), "Every route must enter from Camp circulation.");
            for (int waypoint = 1; waypoint < route.Count; waypoint++)
                Assert.That(established, Does.Contain(route[waypoint]),
                    "Slot " + slotIndex + " waypoint " + route[waypoint] + " is not established yet.");
            Assert.That(route[route.Count - 1], Is.EqualTo(slots[slotIndex - 1].Approach));
            for (int waypoint = 1; waypoint < route.Count; waypoint++)
                Assert.That(System.Math.Abs(route[waypoint].x - route[waypoint - 1].x) +
                    System.Math.Abs(route[waypoint].y - route[waypoint - 1].y), Is.EqualTo(1),
                    "Slot " + slotIndex + " contains a non-cardinal direct segment.");
        }
    }

    [Test]
    public void EveryTargetFootprintConnectsToItsEstablishedApproach()
    {
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        for (int slotIndex = 1; slotIndex <= slots.Count; slotIndex++)
        {
            var footprint = CampSpatialPolicy.BuildSlotFootprint(
                slots[slotIndex - 1], SlotEntrance, SlotChamber);
            (int x, int y) approach = slots[slotIndex - 1].Approach;
            var established = CampSpatialPolicy.BuildEstablishedSlotFootprint(
                slots, slotIndex - 1, SlotEntrance, SlotChamber);
            established.Add(approach);
            bool connects = false;
            foreach ((int x, int y) cell in footprint)
                foreach ((int x, int y) open in established)
                    if (System.Math.Abs(cell.x - open.x) + System.Math.Abs(cell.y - open.y) <= 1)
                        connects = true;
            Assert.That(connects, Is.True, "Slot " + slotIndex + " Dig does not connect to its approach.");
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
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        var open = CampSpatialPolicy.BuildEstablishedSlotFootprint(slots, 1, SlotEntrance, SlotChamber);
        foreach (var cell in CampSpatialPolicy.BuildSlotFootprint(slots[1], SlotEntrance, SlotChamber))
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
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        ResidentialSlotRecord slot = slots[slotIndex - 1];
        var open = CampSpatialPolicy.BuildEstablishedSlotFootprint(
            slots, slotIndex - 1, SlotEntrance, SlotChamber);
        foreach (var cell in CampSpatialPolicy.BuildSlotFootprint(slot, SlotEntrance, SlotChamber)) open.Add(cell);

        var route = CampSpatialPolicy.BuildOpenCellRoute(slot.Approach, slot.Center, open);
        Assert.That(route, Is.Not.Empty, "Slot " + slotIndex + " needs a connected post-Dig route.");
        Assert.That(route[route.Count - 1], Is.EqualTo(slot.Center));
        foreach (var waypoint in route) Assert.That(open, Does.Contain(waypoint));
    }

    [Test]
    public void AllStageOnePostDigRoutesUseOnlyEstablishedOrAuthorizedCells()
    {
        var slots = CampSpatialPolicy.BuildStageOneSlots(SlotEntrance, SlotChamber);
        for (int slotIndex = 1; slotIndex <= slots.Count; slotIndex++)
        {
            ResidentialSlotRecord slot = slots[slotIndex - 1];
            var open = CampSpatialPolicy.BuildEstablishedSlotFootprint(
                slots, slotIndex - 1, SlotEntrance, SlotChamber);
            foreach (var cell in CampSpatialPolicy.BuildSlotFootprint(slot, SlotEntrance, SlotChamber)) open.Add(cell);
            open.Add(slot.Approach);

            var route = CampSpatialPolicy.BuildOpenCellRoute(slot.Approach, slot.Center, open);
            Assert.That(route, Is.Not.Empty, "Slot " + slotIndex + " has no post-Dig route.");
            foreach (var waypoint in route) Assert.That(open, Does.Contain(waypoint));
        }
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
    public void ConstructionEdgeArrivalAccountsForBodyClearanceWithoutRelaxingNormalWaypoints()
    {
        Assert.That(CampBuddyPhysicalPolicy.GetConstructionEdgeArrivalDistance(0.375f, 0.6f),
            Is.EqualTo(0.3f).Within(0.0001f));
        Assert.That(CampBuddyPhysicalPolicy.GetConstructionEdgeArrivalDistance(0.15f, 0.6f),
            Is.EqualTo(0.18f).Within(0.0001f));
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
}
