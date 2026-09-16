using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using SporeGobbo.CampLifecycle;

public sealed class CampStagedTerrainPolicyTests
{
    [Test]
    public void FirstBuddySquadMaskIsExactAndDoesNotOverlapResidentialAuthority()
    {
        CampStagedTerrainDefinition stage = CampEarlyMapCatalog.FirstBuddySquad;
        Assert.That(stage.RegionId, Is.EqualTo("first-buddy-squad"));
        Assert.That(stage.Cells.Count, Is.EqualTo(425));
        Assert.That(stage.CoveredLandmarkCells.Count, Is.EqualTo(12));
        Assert.That(stage.TerrainCells.Count, Is.EqualTo(437));
        Assert.That(stage.Cells.Contains((52, 38)), Is.True);
        Assert.That(stage.Cells.Contains((46, 70)), Is.True);
        Assert.That(stage.Cells.Contains((28, 45)), Is.False,
            "Future Bones-colored terrain is not Squad terrain.");

        HashSet<(int x, int y)> residential = CampResidentialCatalog.CreateCurrent()
            .GetResidentialAuthorizationCells();
        foreach ((int x, int y) cell in stage.Cells)
            Assert.That(residential.Contains(cell), Is.False, $"Squad stage overlaps Res1 at {cell}.");
    }

    [Test]
    public void FirstBuddyMilestoneUsesExistingDurableProductionFacts()
    {
        Assert.That(CampTerrainProgressionPolicy.IsFirstBuddyRecruited(0, 0, 0), Is.False);
        Assert.That(CampTerrainProgressionPolicy.IsFirstBuddyRecruited(1, 0, 0), Is.True);
        Assert.That(CampTerrainProgressionPolicy.IsFirstBuddyRecruited(0, 1, 0), Is.True);
        Assert.That(CampTerrainProgressionPolicy.IsFirstBuddyRecruited(0, 0, 1), Is.True);
    }

    [Test]
    public void BonesAndShopMasksMatchApprovedWorkbookFills()
    {
        CampStagedTerrainDefinition bones = CampEarlyMapCatalog.FirstDeathBones;
        CampStagedTerrainDefinition shop = CampEarlyMapCatalog.FutureShop;

        Assert.That(CampEarlyMapCatalog.BonesFillHex, Is.EqualTo("#B9D5DE"));
        Assert.That(CampEarlyMapCatalog.ShopFillHex, Is.EqualTo("#9FBFCB"));
        Assert.That(CampEarlyMapCatalog.SquadFillHex, Is.EqualTo("#DDEFF3"));
        Assert.That(bones.Milestone, Is.EqualTo(CampTerrainMilestone.FirstDeath));
        Assert.That(shop.Milestone.HasValue, Is.False, "Shop has no production unlock predicate yet.");
        Assert.That(bones.Cells.Count, Is.EqualTo(169));
        Assert.That(bones.CoveredLandmarkCells.Count, Is.EqualTo(7));
        Assert.That(bones.TerrainCells.Count, Is.EqualTo(176));
        Assert.That(shop.Cells.Count, Is.EqualTo(251));
        Assert.That(shop.CoveredLandmarkCells.Count, Is.EqualTo(8));
        Assert.That(shop.TerrainCells.Count, Is.EqualTo(259));
        Assert.That(CampEarlyMapCatalog.PermanentStructureCells.Count, Is.EqualTo(293));

        Assert.That(bones.Cells.Contains((12, 62)), Is.True);
        Assert.That(bones.Cells.Contains((44, 44)), Is.True);
        Assert.That(shop.Cells.Contains((46, 17)), Is.True);
        Assert.That(shop.Cells.Contains((55, 34)), Is.True);
        Assert.That(CampEarlyMapCatalog.IsPermanentStructureCell((11, 64)), Is.True);
        Assert.That(CampEarlyMapCatalog.FindStageContaining((0, 79)), Is.Null,
            "Grey/unresolved territory must not be classified by staged terrain.");

        HashSet<(int x, int y)> residential = CampResidentialCatalog.CreateCurrent()
            .GetResidentialAuthorizationCells();
        HashSet<(int x, int y)> squad = new(CampEarlyMapCatalog.FirstBuddySquad.TerrainCells);
        foreach ((int x, int y) cell in bones.TerrainCells)
        {
            Assert.That(squad.Contains(cell), Is.False, $"Bones overlaps Squad at {cell}.");
            Assert.That(residential.Contains(cell), Is.False, $"Bones overlaps Res1 at {cell}.");
        }
        foreach ((int x, int y) cell in shop.TerrainCells)
        {
            Assert.That(squad.Contains(cell), Is.False, $"Shop overlaps Squad at {cell}.");
            Assert.That(bones.TerrainCells.Contains(cell), Is.False, $"Shop overlaps Bones at {cell}.");
            Assert.That(residential.Contains(cell), Is.False, $"Shop overlaps Res1 at {cell}.");
        }
    }

    [Test]
    public void FirstDeathUnlockUsesDurableDeathHistoryAndSavedClearPolicy()
    {
        Assert.That(CampTerrainProgressionPolicy.IsFirstDeath(0), Is.False);
        Assert.That(CampTerrainProgressionPolicy.IsFirstDeath(1), Is.True);
        Assert.That(CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(
            CampDigCategory.StagedCampDiggable, false), Is.False);
        Assert.That(CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(
            CampDigCategory.StagedCampDiggable, true), Is.True);
    }

    [Test]
    public void StagedDirtOnlyAcceptsPlayerAfterMilestone()
    {
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.StagedCampDiggable,
            TerrainDigAuthority.Player, false, false), Is.False);
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.StagedCampDiggable,
            TerrainDigAuthority.Player, false, true), Is.True);
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.StagedCampDiggable,
            TerrainDigAuthority.Buddy, false, true), Is.False);
        Assert.That(CampSpatialPolicy.CanDig(CampDigCategory.ResidentialReserved,
            TerrainDigAuthority.Player, false, true), Is.False);
        Assert.That(CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(
            CampDigCategory.StagedCampDiggable, false), Is.False);
        Assert.That(CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(
            CampDigCategory.StagedCampDiggable, true), Is.True);
        Assert.That(CampSpatialPolicy.CanApplyOrdinaryOrSavedClear(
            CampDigCategory.ResidentialReserved, true), Is.False);
    }

    [Test]
    public void ApprovedLandmarkFootprintsRemainSeparateFromSquadDirtMask()
    {
        Assert.That(CampEarlyMapCatalog.FireAnchor, Is.EqualTo(new UnityEngine.Vector2Int(24, 39)));
        Assert.That(CampEarlyMapCatalog.ExitAnchor, Is.EqualTo(new UnityEngine.Vector2Int(40, 61)));
        Assert.That(CampEarlyMapCatalog.SquadInteractionAnchor, Is.EqualTo(new UnityEngine.Vector2Int(58, 48)));
        Assert.That(CampEarlyMapCatalog.BonesInteractionAnchor, Is.EqualTo(new UnityEngine.Vector2Int(15, 63)));
        Assert.That(CampEarlyMapCatalog.ShopPlanningAnchor, Is.EqualTo(new UnityEngine.Vector2Int(49, 21)));
        Assert.That(CampEarlyMapCatalog.CampfireFootprint.size,
            Is.EqualTo(new UnityEngine.Vector3Int(3, 3, 1)));
        Assert.That(CampEarlyMapCatalog.ExitFootprint.size,
            Is.EqualTo(new UnityEngine.Vector3Int(5, 1, 1)));
        Assert.That(CampEarlyMapCatalog.SquadFootprint.size,
            Is.EqualTo(new UnityEngine.Vector3Int(4, 3, 1)));
        Assert.That(CampEarlyMapCatalog.BonesFootprint.size,
            Is.EqualTo(new UnityEngine.Vector3Int(7, 1, 1)));
        Assert.That(CampEarlyMapCatalog.ShopFootprint.size,
            Is.EqualTo(new UnityEngine.Vector3Int(4, 2, 1)));
        foreach (UnityEngine.Vector3Int cell in CampEarlyMapCatalog.SquadFootprint.allPositionsWithin)
        {
            Assert.That(CampEarlyMapCatalog.FirstBuddySquad.Cells.Contains((cell.x, cell.y)), Is.False,
                "Orange landmark underlay must not change the approved 425-cell mask identity.");
            Assert.That(CampEarlyMapCatalog.FirstBuddySquad.ContainsTerrainCell((cell.x, cell.y)), Is.True,
                "Squad underlay must begin as staged dirt governed by the same milestone.");
            Assert.That(CampEarlyMapCatalog.InitialOpenCells.Contains((cell.x, cell.y)), Is.False,
                "Squad footprint must not begin as an automatically excavated island.");
        }
    }
}
