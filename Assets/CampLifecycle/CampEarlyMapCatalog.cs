using System;
using System.Collections.Generic;
using UnityEngine;

namespace SporeGobbo.CampLifecycle
{
    public enum CampTerrainMilestone
    {
        FirstBuddyRecruited,
        FirstDeath
    }

    public readonly struct CampCellRun
    {
        public readonly int Y;
        public readonly int XStart;
        public readonly int XEnd;

        public CampCellRun(int y, int xStart, int xEnd)
        {
            Y = y;
            XStart = Math.Min(xStart, xEnd);
            XEnd = Math.Max(xStart, xEnd);
        }
    }

    public sealed class CampStagedTerrainDefinition
    {
        public string RegionId { get; }
        public CampTerrainMilestone? Milestone { get; }
        public IReadOnlyCollection<(int x, int y)> Cells { get; }
        public IReadOnlyCollection<(int x, int y)> CoveredLandmarkCells { get; }
        public IReadOnlyCollection<(int x, int y)> TerrainCells { get; }

        public CampStagedTerrainDefinition(string regionId, CampTerrainMilestone? milestone,
            IReadOnlyCollection<(int x, int y)> cells,
            IReadOnlyCollection<(int x, int y)> coveredLandmarkCells = null)
        {
            RegionId = regionId ?? "";
            Milestone = milestone;
            Cells = cells ?? Array.Empty<(int x, int y)>();
            CoveredLandmarkCells = coveredLandmarkCells ?? Array.Empty<(int x, int y)>();
            HashSet<(int x, int y)> terrainCells = new(Cells);
            terrainCells.UnionWith(CoveredLandmarkCells);
            TerrainCells = terrainCells;
        }

        public bool ContainsTerrainCell((int x, int y) cell) =>
            TerrainCells is HashSet<(int x, int y)> set ? set.Contains(cell) : false;
    }

    public static class CampTerrainProgressionPolicy
    {
        public static bool IsFirstBuddyRecruited(int ownedBuddyCount, int establishedHomes, int deadBuddyCount) =>
            ownedBuddyCount > 0 || establishedHomes > 0 || deadBuddyCount > 0;

        public static bool IsFirstDeath(int deathHistoryCount) => deathHistoryCount > 0;
    }

    /// <summary>
    /// Exact current early-Camp mechanical map transcribed from the approved planning grid.
    /// Res1 is deliberately absent: CampResidentialCatalog remains its sole authority.
    /// </summary>
    public static class CampEarlyMapCatalog
    {
        public const string FirstBuddySquadRegionId = "first-buddy-squad";
        public const string FirstDeathBonesRegionId = "first-death-bones";
        public const string FutureShopRegionId = "future-shop-reserved";
        public const string BonesFootprintId = "bones-wall";
        public const string ShopFootprintId = "future-shop";
        public const string SquadFillHex = "#DDEFF3";
        public const string BonesFillHex = "#B9D5DE";
        public const string ShopFillHex = "#9FBFCB";
        public const string PermanentStructureFillHex = "#08090B";
        public const string CampfireFootprintId = "campfire";
        public const string ExitFootprintId = "run-exit";
        public const string SquadFootprintId = "squad-selection";

        public static readonly BoundsInt CampfireFootprint = new BoundsInt(23, 38, 0, 3, 3, 1);
        public static readonly BoundsInt ExitFootprint = new BoundsInt(38, 61, 0, 5, 1, 1);
        public static readonly BoundsInt SquadFootprint = new BoundsInt(57, 47, 0, 4, 3, 1);
        public static readonly BoundsInt BonesFootprint = new BoundsInt(12, 63, 0, 7, 1, 1);
        public static readonly BoundsInt ShopFootprint = new BoundsInt(48, 21, 0, 4, 2, 1);
        public static readonly Vector2Int FireAnchor = new Vector2Int(24, 39);
        public static readonly Vector2Int ExitAnchor = new Vector2Int(40, 61);
        public static readonly Vector2Int SquadInteractionAnchor = new Vector2Int(58, 48);
        public static readonly Vector2Int BonesInteractionAnchor = new Vector2Int(15, 63);
        public static readonly Vector2Int ShopPlanningAnchor = new Vector2Int(49, 21);

        static readonly CampCellRun[] InitialOpenRuns =
        {
            new(25,25,25), new(25,29,29), new(26,22,26), new(26,28,30),
            new(27,22,31), new(27,38,38), new(28,11,32), new(28,34,42),
            new(29,11,43), new(30,11,44), new(31,11,44), new(32,11,45),
            new(33,11,44), new(34,11,51), new(35,10,59), new(36,10,59),
            new(37,10,59), new(38,10,22), new(38,26,51), new(39,10,22),
            new(39,26,51), new(40,10,22), new(40,26,51), new(41,10,47),
            new(42,10,48), new(43,10,40), new(43,44,48), new(44,13,40),
            new(44,45,48), new(45,21,48), new(46,27,48), new(47,28,46),
            new(48,28,43), new(49,27,40), new(50,28,39), new(51,29,38),
            new(52,31,38), new(53,32,38), new(54,32,38), new(55,33,39),
            new(56,33,41), new(57,35,41), new(58,35,41), new(59,35,41),
            new(60,35,41)
        };

        static readonly CampCellRun[] FirstBuddySquadRuns =
        {
            new(38,52,59), new(39,52,59), new(40,52,59), new(41,48,60),
            new(42,49,62), new(43,49,63), new(44,49,63), new(45,49,64),
            new(46,26,26), new(46,49,64), new(47,26,27), new(47,47,56), new(47,61,65),
            new(48,26,27), new(48,44,56), new(48,61,65),
            new(49,26,26), new(49,41,45), new(49,47,56), new(49,61,65),
            new(50,26,27), new(50,40,45), new(50,47,65),
            new(51,25,28), new(51,39,45), new(51,47,65),
            new(52,26,30), new(52,39,44), new(52,47,64),
            new(53,27,31), new(53,39,43), new(53,47,55), new(53,58,63),
            new(54,28,31), new(54,39,42), new(54,48,54), new(54,60,62),
            new(55,30,32), new(55,40,41), new(55,50,51),
            new(56,30,32), new(56,50,51), new(57,29,34), new(57,50,51),
            new(58,29,34), new(58,50,51), new(59,28,34), new(59,50,51),
            new(60,28,34), new(60,50,51), new(61,29,34), new(61,50,51),
            new(62,29,31), new(62,49,51), new(63,28,30), new(63,48,50),
            new(64,46,49), new(65,45,49), new(66,44,51), new(67,41,51),
            new(68,42,53), new(69,38,49), new(70,41,46)
        };

        static readonly CampCellRun[] FirstDeathBonesRuns =
        {
            new(43,41,43), new(44,41,44), new(46,21,25), new(47,21,25),
            new(48,20,25), new(49,20,25), new(50,19,25), new(51,19,24),
            new(52,18,25), new(53,17,24), new(54,16,25), new(55,12,25),
            new(56,12,25), new(57,12,25), new(58,12,24), new(59,12,24),
            new(60,12,24), new(61,12,22), new(62,12,20)
        };

        static readonly CampCellRun[] FutureShopRuns =
        {
            new(15,58,58), new(15,61,63), new(16,54,66), new(17,46,66), new(17,71,71),
            new(18,46,67), new(19,46,65), new(20,46,64), new(21,46,47), new(21,52,64),
            new(22,46,47), new(22,52,64), new(23,46,63), new(24,46,63),
            new(25,49,63), new(26,49,63), new(27,50,60), new(28,50,57),
            new(29,50,55), new(30,49,55), new(31,49,55), new(32,50,55),
            new(33,49,54), new(34,52,55)
        };

        // Exact black cells inside the one-cell local envelopes around the authored Bones and Shop territories.
        static readonly CampCellRun[] LocalPermanentStructureRuns =
        {
            new(14,57,67), new(15,53,57), new(15,59,60), new(15,64,66),
            new(16,45,53), new(16,72,72), new(17,45,45), new(17,72,72),
            new(18,45,45), new(18,68,72), new(19,45,45), new(19,66,68),
            new(20,45,45), new(20,65,66), new(21,45,45), new(21,65,66),
            new(22,45,45), new(22,65,66), new(23,45,45), new(23,64,65),
            new(24,45,45), new(24,64,65), new(25,45,48), new(25,64,66),
            new(26,47,48), new(26,64,65), new(27,48,49), new(27,61,64),
            new(28,45,46), new(28,49,49), new(28,58,64),
            new(29,45,49), new(29,56,63), new(30,45,48), new(30,56,63), new(30,72,72),
            new(31,45,48), new(31,56,60), new(31,71,72),
            new(32,46,49), new(32,56,60), new(32,72,72),
            new(33,45,48), new(33,55,59), new(34,56,59),
            new(52,45,45), new(53,25,26), new(53,44,45),
            new(54,26,27), new(54,43,45), new(55,26,29), new(55,42,45),
            new(56,26,29), new(56,42,45), new(57,26,28), new(57,42,45),
            new(58,25,28), new(58,42,45), new(59,25,27), new(59,42,45),
            new(60,25,27), new(60,42,45), new(61,23,28), new(61,35,37), new(61,43,45),
            new(62,21,28), new(62,32,45), new(63,19,27), new(63,31,45), new(64,11,45)
        };

        static readonly HashSet<(int x, int y)> initialOpenCells = BuildInitialOpenCells();
        static readonly HashSet<(int x, int y)> firstBuddySquadCells = BuildCells(FirstBuddySquadRuns);
        static readonly HashSet<(int x, int y)> squadCoveredCells = BuildBounds(SquadFootprint);
        static readonly HashSet<(int x, int y)> firstDeathBonesCells = BuildCells(FirstDeathBonesRuns);
        static readonly HashSet<(int x, int y)> bonesCoveredCells = BuildBounds(BonesFootprint);
        static readonly HashSet<(int x, int y)> futureShopCells = BuildCells(FutureShopRuns);
        static readonly HashSet<(int x, int y)> shopCoveredCells = BuildBounds(ShopFootprint);
        static readonly HashSet<(int x, int y)> permanentStructureCells = BuildCells(LocalPermanentStructureRuns);
        static readonly CampStagedTerrainDefinition firstBuddySquad = new(
            FirstBuddySquadRegionId, CampTerrainMilestone.FirstBuddyRecruited,
            firstBuddySquadCells, squadCoveredCells);
        static readonly CampStagedTerrainDefinition firstDeathBones = new(
            FirstDeathBonesRegionId, CampTerrainMilestone.FirstDeath,
            firstDeathBonesCells, bonesCoveredCells);
        static readonly CampStagedTerrainDefinition futureShop = new(
            FutureShopRegionId, null, futureShopCells, shopCoveredCells);
        static readonly CampStagedTerrainDefinition[] stages =
        {
            firstBuddySquad, firstDeathBones, futureShop
        };

        public static IReadOnlyCollection<(int x, int y)> InitialOpenCells => initialOpenCells;
        public static CampStagedTerrainDefinition FirstBuddySquad => firstBuddySquad;
        public static CampStagedTerrainDefinition FirstDeathBones => firstDeathBones;
        public static CampStagedTerrainDefinition FutureShop => futureShop;
        public static IReadOnlyCollection<CampStagedTerrainDefinition> Stages => stages;
        public static IReadOnlyCollection<(int x, int y)> PermanentStructureCells => permanentStructureCells;

        public static CampStagedTerrainDefinition FindStageContaining((int x, int y) cell)
        {
            foreach (CampStagedTerrainDefinition stage in stages)
                if (stage.ContainsTerrainCell(cell)) return stage;
            return null;
        }

        public static bool IsPermanentStructureCell((int x, int y) cell) =>
            permanentStructureCells.Contains(cell);

        public static bool TryGetLandmarkFootprint(string footprintId, out BoundsInt bounds)
        {
            switch (footprintId)
            {
                case CampfireFootprintId: bounds = CampfireFootprint; return true;
                case ExitFootprintId: bounds = ExitFootprint; return true;
                case SquadFootprintId: bounds = SquadFootprint; return true;
                case BonesFootprintId: bounds = BonesFootprint; return true;
                case ShopFootprintId: bounds = ShopFootprint; return true;
                default: bounds = default; return false;
            }
        }

        static HashSet<(int x, int y)> BuildInitialOpenCells()
        {
            HashSet<(int x, int y)> cells = BuildCells(InitialOpenRuns);
            AddBounds(cells, CampfireFootprint);
            AddBounds(cells, ExitFootprint);
            return cells;
        }

        static HashSet<(int x, int y)> BuildBounds(BoundsInt bounds)
        {
            HashSet<(int x, int y)> cells = new();
            AddBounds(cells, bounds);
            return cells;
        }

        static HashSet<(int x, int y)> BuildCells(IEnumerable<CampCellRun> runs)
        {
            HashSet<(int x, int y)> cells = new();
            foreach (CampCellRun run in runs)
                for (int x = run.XStart; x <= run.XEnd; x++) cells.Add((x, run.Y));
            return cells;
        }

        static void AddBounds(HashSet<(int x, int y)> cells, BoundsInt bounds)
        {
            foreach (Vector3Int position in bounds.allPositionsWithin) cells.Add((position.x, position.y));
        }
    }
}
