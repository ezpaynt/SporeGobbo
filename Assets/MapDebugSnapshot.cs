using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public class MapDebugSnapshot
{
    public string profileName = "Manual Inspector Settings";
    public string seedLabel = "Random";
    public int width;
    public int height;
    public float cellSize = 1f;
    public Vector2 origin;
    public Vector2Int spawnCenter;

    public List<Vector2Int> openWalkableCells = new List<Vector2Int>();
    public List<Vector2Int> hiddenTunnelCells = new List<Vector2Int>();
    public List<Vector2Int> hiddenRoomCells = new List<Vector2Int>();
    public List<Vector2Int> revealTriggerCells = new List<Vector2Int>();
    public List<Vector2Int> spawnCells = new List<Vector2Int>();
    public List<Vector2Int> bossExitCells = new List<Vector2Int>();

    public List<MapDebugAreaSnapshot> areas = new List<MapDebugAreaSnapshot>();
    public List<MapDebugTunnelSnapshot> tunnels = new List<MapDebugTunnelSnapshot>();
    public List<MapDebugBranchSnapshot> branches = new List<MapDebugBranchSnapshot>();

    public int totalNormalEnemies;
    public int totalBossEnemies;
    public int totalMushrooms;
    public int totalSpores;
    public int totalShinies;
    public int totalExitPortals;

    public Vector3 CellCenterToWorld(Vector2Int cell)
    {
        return new Vector3(
            origin.x + (cell.x + 0.5f) * cellSize,
            origin.y + (cell.y + 0.5f) * cellSize,
            0f);
    }

    public Vector3 CellSize3D(float inset = 0.08f)
    {
        float size = Mathf.Max(0.01f, cellSize - Mathf.Max(0f, inset));
        return new Vector3(size, size, 0.01f);
    }

    public string BuildTextReport()
    {
        Dictionary<string, int> areaCounts = new Dictionary<string, int>();
        foreach (MapDebugAreaSnapshot area in areas)
        {
            if (area == null) continue;
            if (!areaCounts.ContainsKey(area.areaType)) areaCounts[area.areaType] = 0;
            areaCounts[area.areaType]++;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("========== SPORE GOBBO MAP DEBUG REPORT ==========");
        builder.AppendLine("Profile: " + profileName);
        builder.AppendLine("Seed: " + seedLabel);
        builder.AppendLine("Map Size: " + width + " x " + height + " | Cell Size: " + cellSize);
        builder.AppendLine("Spawn Center: " + spawnCenter + " | Spawn Cells: " + spawnCells.Count);
        builder.AppendLine();

        builder.AppendLine("Cells");
        builder.AppendLine("-----");
        builder.AppendLine("Open Walkable Cells: " + openWalkableCells.Count);
        builder.AppendLine("Hidden Tunnel Cells: " + hiddenTunnelCells.Count);
        builder.AppendLine("Hidden Room/Area Cells: " + hiddenRoomCells.Count);
        builder.AppendLine("Reveal Trigger Cells: " + revealTriggerCells.Count);
        builder.AppendLine("Boss/Exit Cells: " + bossExitCells.Count);
        builder.AppendLine();

        builder.AppendLine("Branches");
        builder.AppendLine("--------");
        if (branches.Count == 0)
        {
            builder.AppendLine("No branch settings found.");
        }
        else
        {
            foreach (MapDebugBranchSnapshot branch in branches)
            {
                if (branch == null) continue;
                builder.AppendLine(
                    branch.branchName + " dir=" + branch.direction +
                    " length=" + branch.length +
                    " forks=" + branch.forkCount +
                    " rooms=" + branch.smallRooms +
                    " camps=" + branch.camps +
                    " bosses=" + branch.bosses);
            }
        }
        builder.AppendLine();

        builder.AppendLine("Areas");
        builder.AppendLine("-----");
        foreach (KeyValuePair<string, int> pair in areaCounts)
            builder.AppendLine(pair.Key + ": " + pair.Value);
        builder.AppendLine("Total Areas: " + areas.Count);
        builder.AppendLine("Tunnels: " + tunnels.Count);
        builder.AppendLine();

        builder.AppendLine("Content Totals");
        builder.AppendLine("--------------");
        builder.AppendLine("Normal Enemies: " + totalNormalEnemies);
        builder.AppendLine("Boss Enemies: " + totalBossEnemies);
        builder.AppendLine("Mushrooms: " + totalMushrooms);
        builder.AppendLine("Spores: " + totalSpores);
        builder.AppendLine("Shinies: " + totalShinies);
        builder.AppendLine("Exit Portals: " + totalExitPortals);
        builder.AppendLine();

        builder.AppendLine("Per-Area Summary");
        builder.AppendLine("----------------");
        foreach (MapDebugAreaSnapshot area in areas)
        {
            if (area == null) continue;
            builder.AppendLine(
                "Area " + area.id + " " + area.areaType +
                " center=" + area.centerCell +
                " cells=" + area.cells.Count +
                " revealed=" + area.revealed +
                " enemies=" + area.enemyCount +
                " boss=" + area.bossEnemyCount +
                " mushrooms=" + area.mushroomCount +
                " spores=" + area.sporeCount +
                " shinies=" + area.shinyCount +
                " exit=" + area.hasExitPortal);
        }

        builder.AppendLine("==================================================");
        return builder.ToString();
    }
}

[System.Serializable]
public class MapDebugAreaSnapshot
{
    public int id;
    public string areaType = "Unknown";
    public Vector2Int centerCell;
    public Vector2 centerWorld;
    public int radiusCells;
    public float radiusWorld;
    public bool revealed;
    public bool isBossCamp;
    public bool hasExitPortal;
    public int enemyCount;
    public int bossEnemyCount;
    public int mushroomCount;
    public int sporeCount;
    public int shinyCount;
    public List<Vector2Int> cells = new List<Vector2Int>();
}

[System.Serializable]
public class MapDebugTunnelSnapshot
{
    public int id;
    public bool revealed;
    public float radius;
    public Vector2 enemySpawnPoint;
    public List<Vector2> points = new List<Vector2>();
}

[System.Serializable]
public class MapDebugBranchSnapshot
{
    public string branchName = "Branch";
    public Vector2Int direction;
    public int length;
    public int tunnelHalfWidth;
    public int wobble;
    public int forkCount;
    public int smallRooms;
    public int camps;
    public int bosses;
}
