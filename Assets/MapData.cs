using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CampData
{
    public int id;
    public Vector2 center;
    public float radius = 4f;
    public bool revealed = false;

    public bool isBossCamp = false;
    public bool hasExitPortal = false;

    // Normal enemies use enemyPrefab. Boss camps can also spawn one buffed boss enemy.
    public int enemyCount = 3;
    public int bossEnemyCount = 0;

    public int sporeCount = 2;
    public int mushroomCount = 3;
    public int shinyCount = 0;
}

[System.Serializable]
public class TunnelData
{
    public int id;
    public List<Vector2> points = new List<Vector2>();
    public float radius = 1.4f;
    public bool revealed = false;
    public Vector2 enemySpawnPoint;
}

public class MapData
{
    public int width;
    public int height;
    public float cellSize;
    public Vector2 origin;

    private bool[,] blocked;
    private int[,] dirtType;

    public List<CampData> camps = new List<CampData>();
    public List<TunnelData> tunnels = new List<TunnelData>();

    public MapData(int width, int height, float cellSize)
    {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;

        origin = new Vector2(-width * cellSize * 0.5f, -height * cellSize * 0.5f);

        blocked = new bool[width, height];
        dirtType = new int[width, height];
    }

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public Vector2Int WorldToCell(Vector2 world)
    {
        int x = Mathf.FloorToInt((world.x - origin.x) / cellSize);
        int y = Mathf.FloorToInt((world.y - origin.y) / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        return origin + new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
    }

    public bool IsBlocked(Vector2Int cell)
    {
        if (!InBounds(cell))
            return true;

        return blocked[cell.x, cell.y];
    }

    public void SetBlocked(Vector2Int cell, bool value)
    {
        if (!InBounds(cell))
            return;

        blocked[cell.x, cell.y] = value;
    }

    public int GetDirtType(Vector2Int cell)
    {
        if (!InBounds(cell))
            return 0;

        return dirtType[cell.x, cell.y];
    }

    public void SetDirtType(Vector2Int cell, int type)
    {
        if (!InBounds(cell))
            return;

        dirtType[cell.x, cell.y] = type;
    }

    public void FillBlocked()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                blocked[x, y] = true;
                dirtType[x, y] = Random.Range(0, 3);
            }
        }
    }

    public void ClearCircle(Vector2 center, float radius)
    {
        Vector2Int centerCell = WorldToCell(center);
        int cellRadius = Mathf.CeilToInt(radius / cellSize) + 1;

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                Vector2Int cell = centerCell + new Vector2Int(x, y);
                Vector2 world = CellToWorld(cell);

                if (Vector2.Distance(world, center) <= radius)
                    SetBlocked(cell, false);
            }
        }

        FixDiagonalPinches(center, radius);
    }

    void FixDiagonalPinches(Vector2 center, float radius)
    {
        Vector2Int centerCell = WorldToCell(center);
        int cellRadius = Mathf.CeilToInt(radius / cellSize) + 2;

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                Vector2Int cell = centerCell + new Vector2Int(x, y);

                Vector2Int right = cell + Vector2Int.right;
                Vector2Int up = cell + Vector2Int.up;
                Vector2Int diagonal = cell + Vector2Int.right + Vector2Int.up;

                // Pattern:
                // open blocked
                // blocked open
                if (!IsBlocked(cell) && IsBlocked(right) && IsBlocked(up) && !IsBlocked(diagonal))
                {
                    SetBlocked(right, false);
                    SetBlocked(up, false);
                }

                // Pattern:
                // blocked open
                // open blocked
                if (IsBlocked(cell) && !IsBlocked(right) && !IsBlocked(up) && IsBlocked(diagonal))
                {
                    SetBlocked(cell, false);
                    SetBlocked(diagonal, false);
                }
            }
        }
    }
}