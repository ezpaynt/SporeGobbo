using System.Collections.Generic;
using UnityEngine;

public static class PersistentTestCaveRules
{
    public static CampData CreatePocketContent(PersistentTestCaveLayout.Pocket pocket, MapData map, int id)
    {
        return new CampData
        {
            id = id,
            center = map.CellToWorld(pocket.center),
            radius = pocket.radius * map.cellSize,
            revealed = pocket.startsRevealed,
            enemyCount = pocket.content == PersistentTestCaveLayout.PocketContent.Weevils ? pocket.count : 0,
            exactBlobSpitterCount = pocket.content == PersistentTestCaveLayout.PocketContent.Blobs ? pocket.count : 0,
            mushroomCount = pocket.content == PersistentTestCaveLayout.PocketContent.Mushrooms ? pocket.count : 0,
            shinyCount = pocket.content == PersistentTestCaveLayout.PocketContent.Shinies ? pocket.count : 0,
            sporeCount = 0,
            hasExitPortal = pocket.content == PersistentTestCaveLayout.PocketContent.Exit
        };
    }

    public static HashSet<Vector2Int> BuildResetCells(PersistentTestCaveLayout layout, int width, int height)
    {
        HashSet<Vector2Int> result = new HashSet<Vector2Int>();
        if (layout == null || layout.pockets == null) return result;
        foreach (PersistentTestCaveLayout.Pocket pocket in layout.pockets)
        {
            if (pocket == null || pocket.startsRevealed) continue;
            int radius = Mathf.Max(2, pocket.radius) + 1;
            for (int x = pocket.center.x - radius; x <= pocket.center.x + radius; x++)
            for (int y = pocket.center.y - radius; y <= pocket.center.y + radius; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                if (Vector2.Distance(cell, pocket.center) <= radius) result.Add(cell);
            }
        }
        return result;
    }

    public static void ApplySavedExcavation(MapData map, PersistentTestCaveTerrainState state,
        IReadOnlyCollection<Vector2Int> resetCells)
    {
        if (map == null || state == null) return;
        HashSet<Vector2Int> reset = resetCells as HashSet<Vector2Int> ??
            new HashSet<Vector2Int>(resetCells ?? new Vector2Int[0]);
        state.Normalize();
        foreach (Vector2Int cell in state.clearedCells)
            if (map.InBounds(cell) && !reset.Contains(cell)) map.SetBlocked(cell, false);
    }

    public static bool ShouldPersist(Vector2Int cell, IReadOnlyCollection<Vector2Int> resetCells)
    {
        if (resetCells == null) return true;
        foreach (Vector2Int resetCell in resetCells)
            if (resetCell == cell) return false;
        return true;
    }
}
