using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class MapGeneratorDebugExtensions
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    public static MapDebugSnapshot BuildDebugSnapshot(this MapGenerator generator)
    {
        MapDebugSnapshot snapshot = new MapDebugSnapshot();
        if (generator == null || generator.Data == null)
            return snapshot;

        MapData data = generator.Data;
        snapshot.profileName = generator.selectedProfile != null ? generator.selectedProfile.name : "Manual Inspector Settings";
        snapshot.seedLabel = generator.map.randomSeed ? "Random" : generator.map.seed.ToString();
        snapshot.width = data.width;
        snapshot.height = data.height;
        snapshot.cellSize = data.cellSize;
        snapshot.origin = data.origin;
        snapshot.spawnCenter = generator.map.spawnCenter;

        CopyBranchSettings(generator, snapshot);
        CopyOpenCells(data, snapshot);
        CopyCellSet(generator, "spawnOpen", snapshot.spawnCells);
        CopyHiddenTunnelCells(generator, data, snapshot);
        CopyRevealTriggers(generator, snapshot);
        CopyAreas(generator, data, snapshot);
        CopyTunnels(data, snapshot);
        CalculateTotals(snapshot);

        return snapshot;
    }

    public static string BuildDebugTextReport(this MapGenerator generator)
    {
        return generator.BuildDebugSnapshot().BuildTextReport();
    }

    public static void LogDebugTextReport(this MapGenerator generator)
    {
        if (generator == null)
        {
            Debug.LogWarning("Map debug report requested, but no MapGenerator was provided.");
            return;
        }

        Debug.Log(generator.BuildDebugTextReport(), generator);
    }

    private static void CopyBranchSettings(MapGenerator generator, MapDebugSnapshot snapshot)
    {
        if (generator.branches == null) return;

        foreach (MapGenerator.BranchSettings branch in generator.branches)
        {
            if (branch == null) continue;
            snapshot.branches.Add(new MapDebugBranchSnapshot
            {
                branchName = branch.branchName,
                direction = branch.direction,
                length = branch.length,
                tunnelHalfWidth = branch.tunnelHalfWidth,
                wobble = branch.wobble,
                forkCount = branch.forkCount,
                smallRooms = branch.smallRooms,
                camps = branch.camps,
                bosses = branch.bosses
            });
        }
    }

    private static void CopyOpenCells(MapData data, MapDebugSnapshot snapshot)
    {
        for (int x = 0; x < data.width; x++)
        {
            for (int y = 0; y < data.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!data.IsBlocked(cell))
                    snapshot.openWalkableCells.Add(cell);
            }
        }
    }

    private static void CopyHiddenTunnelCells(MapGenerator generator, MapData data, MapDebugSnapshot snapshot)
    {
        HashSet<Vector2Int> plannedTunnelCells = GetFieldValue<HashSet<Vector2Int>>(generator, "plannedTunnelCells");
        if (plannedTunnelCells == null) return;

        foreach (Vector2Int cell in plannedTunnelCells)
        {
            if (data.InBounds(cell) && data.IsBlocked(cell))
                snapshot.hiddenTunnelCells.Add(cell);
        }
    }

    private static void CopyRevealTriggers(MapGenerator generator, MapDebugSnapshot snapshot)
    {
        Dictionary<Vector2Int, int> triggers = GetFieldValue<Dictionary<Vector2Int, int>>(generator, "revealGroupByTriggerCell");
        if (triggers == null) return;

        foreach (Vector2Int cell in triggers.Keys)
            snapshot.revealTriggerCells.Add(cell);
    }

    private static void CopyAreas(MapGenerator generator, MapData data, MapDebugSnapshot snapshot)
    {
        IList<object> plannedAreas = GetPlannedAreas(generator);
        if (plannedAreas == null) return;

        foreach (object plannedArea in plannedAreas)
        {
            MapDebugAreaSnapshot areaSnapshot = BuildAreaSnapshot(plannedArea, data);
            if (areaSnapshot == null) continue;

            CampData content = FindCampData(data, areaSnapshot.id);
            if (content != null)
            {
                areaSnapshot.revealed = content.revealed;
                areaSnapshot.isBossCamp = content.isBossCamp;
                areaSnapshot.hasExitPortal = content.hasExitPortal;
                areaSnapshot.enemyCount = content.enemyCount;
                areaSnapshot.bossEnemyCount = content.bossEnemyCount;
                areaSnapshot.mushroomCount = content.mushroomCount;
                areaSnapshot.sporeCount = content.sporeCount;
                areaSnapshot.shinyCount = GetEffectiveShinyCount(content);
            }

            foreach (Vector2Int cell in areaSnapshot.cells)
            {
                if (data.InBounds(cell) && data.IsBlocked(cell))
                    snapshot.hiddenRoomCells.Add(cell);

                if (areaSnapshot.hasExitPortal || areaSnapshot.isBossCamp || areaSnapshot.bossEnemyCount > 0)
                    snapshot.bossExitCells.Add(cell);
            }

            snapshot.areas.Add(areaSnapshot);
        }
    }

    private static void CopyTunnels(MapData data, MapDebugSnapshot snapshot)
    {
        if (data.tunnels == null) return;
        foreach (TunnelData tunnel in data.tunnels)
        {
            if (tunnel == null) continue;
            snapshot.tunnels.Add(new MapDebugTunnelSnapshot
            {
                id = tunnel.id,
                revealed = tunnel.revealed,
                radius = tunnel.radius,
                enemySpawnPoint = tunnel.enemySpawnPoint,
                points = tunnel.points != null ? new List<Vector2>(tunnel.points) : new List<Vector2>()
            });
        }
    }

    private static void CalculateTotals(MapDebugSnapshot snapshot)
    {
        foreach (MapDebugAreaSnapshot area in snapshot.areas)
        {
            if (area == null) continue;
            snapshot.totalNormalEnemies += area.enemyCount;
            snapshot.totalBossEnemies += area.bossEnemyCount;
            snapshot.totalMushrooms += area.mushroomCount;
            snapshot.totalSpores += area.sporeCount;
            snapshot.totalShinies += area.shinyCount;
            if (area.hasExitPortal) snapshot.totalExitPortals++;
        }
    }

    private static MapDebugAreaSnapshot BuildAreaSnapshot(object plannedArea, MapData data)
    {
        if (plannedArea == null || data == null) return null;
        System.Type type = plannedArea.GetType();
        int id = GetMemberValue<int>(plannedArea, type, "id");
        Vector2Int centerCell = GetMemberValue<Vector2Int>(plannedArea, type, "centerCell");
        int radiusCells = GetMemberValue<int>(plannedArea, type, "radiusCells");
        float radiusWorld = GetMemberValue<float>(plannedArea, type, "radiusWorld");
        object areaType = GetMemberValue<object>(plannedArea, type, "type");
        HashSet<Vector2Int> cells = GetMemberValue<HashSet<Vector2Int>>(plannedArea, type, "cells");

        MapDebugAreaSnapshot snapshot = new MapDebugAreaSnapshot
        {
            id = id,
            areaType = areaType != null ? areaType.ToString() : "Unknown",
            centerCell = centerCell,
            centerWorld = data.CellToWorld(centerCell),
            radiusCells = radiusCells,
            radiusWorld = radiusWorld,
            cells = cells != null ? new List<Vector2Int>(cells) : new List<Vector2Int>()
        };

        return snapshot;
    }

    private static IList<object> GetPlannedAreas(MapGenerator generator)
    {
        object value = GetFieldValue<object>(generator, "plannedAreas");
        System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
        if (enumerable == null) return null;

        List<object> result = new List<object>();
        foreach (object item in enumerable)
            if (item != null) result.Add(item);
        return result;
    }

    private static CampData FindCampData(MapData data, int id)
    {
        if (data == null || data.camps == null) return null;
        foreach (CampData camp in data.camps)
        {
            if (camp != null && camp.id == id)
                return camp;
        }
        return null;
    }

    private static int GetEffectiveShinyCount(CampData camp)
    {
        if (camp == null) return 0;
        if (camp.shinyCount > 0) return camp.shinyCount;

        bool isBossCamp = camp.isBossCamp || camp.bossEnemyCount > 0 || camp.hasExitPortal;
        bool isResourceCamp = camp.enemyCount >= 3 && camp.mushroomCount >= 3;
        return isBossCamp || isResourceCamp ? 1 : 0;
    }

    private static void CopyCellSet(MapGenerator generator, string fieldName, List<Vector2Int> target)
    {
        HashSet<Vector2Int> source = GetFieldValue<HashSet<Vector2Int>>(generator, fieldName);
        if (source == null || target == null) return;
        foreach (Vector2Int cell in source)
            target.Add(cell);
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        if (target == null || string.IsNullOrWhiteSpace(fieldName)) return default;
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        if (field == null) return default;
        object value = field.GetValue(target);
        if (value is T typed) return typed;
        return default;
    }

    private static T GetMemberValue<T>(object target, System.Type type, string memberName)
    {
        FieldInfo field = type.GetField(memberName, InstancePrivate);
        if (field == null) field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (field == null) return default;

        object value = field.GetValue(target);
        if (value is T typed) return typed;
        return default;
    }
}
