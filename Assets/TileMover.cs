using UnityEngine;

public static class TileMover
{
    public static void Move(Rigidbody2D rb, Vector2 desiredVelocity, float bodyRadius)
    {
        IDiggableTerrain terrain = DiggableTerrainService.Active;
        if (terrain == null)
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        Vector2 clearanceExtents = GetMapClearanceExtents(rb, bodyRadius);
        Vector2 nextPos = rb.position + desiredVelocity * Time.fixedDeltaTime;

        if (IsTerrainPositionClearForBox(terrain, nextPos, clearanceExtents))
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        Vector2 xVel = new Vector2(desiredVelocity.x, 0f);
        Vector2 xPos = rb.position + xVel * Time.fixedDeltaTime;

        if (IsTerrainPositionClearForBox(terrain, xPos, clearanceExtents))
        {
            rb.linearVelocity = xVel;
            return;
        }

        Vector2 yVel = new Vector2(0f, desiredVelocity.y);
        Vector2 yPos = rb.position + yVel * Time.fixedDeltaTime;

        if (IsTerrainPositionClearForBox(terrain, yPos, clearanceExtents))
        {
            rb.linearVelocity = yVel;
            return;
        }

        rb.linearVelocity = Vector2.zero;
    }

    public static void KeepOutOfWalls(Rigidbody2D rb, float bodyRadius)
    {
        IDiggableTerrain terrain = DiggableTerrainService.Active;
        if (terrain == null)
            return;

        Vector2 clearanceExtents = GetMapClearanceExtents(rb, bodyRadius);

        if (IsTerrainPositionClearForBox(terrain, rb.position, clearanceExtents))
            return;

        Vector2Int cell =
            terrain.WorldToCell(rb.position);

        for (int r = 1; r <= 6; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    Vector2Int testCell =
                        cell + new Vector2Int(x, y);

                    Vector2 testWorld =
                        terrain.CellToWorld(testCell);

                    if (IsTerrainPositionClearForBox(terrain, testWorld, clearanceExtents))
                    {
                        rb.position = testWorld;
                        rb.linearVelocity = Vector2.zero;
                        return;
                    }
                }
            }
        }
    }

    public static float GetColliderBodyRadius(Rigidbody2D rb, float fallbackRadius)
    {
        float radius = Mathf.Max(0f, fallbackRadius);

        if (rb == null)
            return radius;

        Collider2D collider = rb.GetComponent<Collider2D>();
        if (collider == null || !collider.enabled || collider.isTrigger)
            return radius;

        Vector2 extents = collider.bounds.extents;
        return Mathf.Max(radius, extents.x, extents.y);
    }

    public static Vector2 GetMapClearanceExtents(Rigidbody2D rb, float fallbackRadius)
    {
        Vector2 fallback = Vector2.one * Mathf.Max(0f, fallbackRadius);
        if (rb == null) return fallback;
        Collider2D collider = rb.GetComponent<Collider2D>();
        if (collider == null || !collider.enabled || collider.isTrigger) return fallback;
        Vector2 extents = collider.bounds.extents;
        return new Vector2(Mathf.Max(fallback.x, extents.x), Mathf.Max(fallback.y, extents.y));
    }

    public static float GetMapClearanceRadius(Rigidbody2D rb, float bodyRadius)
    {
        return GetColliderBodyRadius(rb, bodyRadius);
    }

    public static bool CanOccupy(IDiggableTerrain terrain, Vector2 worldPosition, float bodyRadius)
    {
        return terrain == null || IsTerrainPositionClearForBody(terrain, worldPosition, Mathf.Max(0f, bodyRadius));
    }

    public static bool CanTraverse(IDiggableTerrain terrain, Vector2 start, Vector2 end, float bodyRadius)
    {
        if (terrain == null) return true;
        float distance = Vector2.Distance(start, end);
        float spacing = Mathf.Max(0.01f, Mathf.Min(terrain.CellSize * 0.125f,
            Mathf.Max(0.01f, bodyRadius) * 0.5f));
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
        for (int step = 0; step <= steps; step++)
            if (!CanOccupy(terrain, Vector2.Lerp(start, end, step / (float)steps), bodyRadius))
                return false;
        return true;
    }

    public static bool CanOccupyBox(IDiggableTerrain terrain, Vector2 worldPosition, Vector2 halfExtents) =>
        terrain == null || IsTerrainPositionClearForBox(terrain, worldPosition,
            new Vector2(Mathf.Max(0f, halfExtents.x), Mathf.Max(0f, halfExtents.y)));

    public static bool CanTraverseBox(IDiggableTerrain terrain, Vector2 start, Vector2 end, Vector2 halfExtents)
    {
        if (terrain == null) return true;
        float distance = Vector2.Distance(start, end);
        float minExtent = Mathf.Max(0.01f, Mathf.Min(halfExtents.x, halfExtents.y));
        float spacing = Mathf.Max(0.01f, Mathf.Min(terrain.CellSize * 0.125f, minExtent * 0.5f));
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
        for (int step = 0; step <= steps; step++)
            if (!CanOccupyBox(terrain, Vector2.Lerp(start, end, step / (float)steps), halfExtents)) return false;
        return true;
    }

    static bool IsTerrainPositionClearForBox(IDiggableTerrain terrain, Vector2 worldPos, Vector2 halfExtents)
    {
        float cellSize = terrain.CellSize;
        Vector2Int center = terrain.WorldToCell(worldPos);
        int rangeX = Mathf.CeilToInt((halfExtents.x + cellSize * 0.5f) / cellSize) + 1;
        int rangeY = Mathf.CeilToInt((halfExtents.y + cellSize * 0.5f) / cellSize) + 1;
        for (int x = center.x - rangeX; x <= center.x + rangeX; x++)
        for (int y = center.y - rangeY; y <= center.y + rangeY; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (!terrain.IsBlocked(cell)) continue;
            Vector2 cellCenter = terrain.CellToWorld(cell);
            float halfCell = cellSize * 0.5f;
            if (Mathf.Abs(worldPos.x - cellCenter.x) <= halfExtents.x + halfCell &&
                Mathf.Abs(worldPos.y - cellCenter.y) <= halfExtents.y + halfCell) return false;
        }
        return true;
    }

    private static bool IsTerrainPositionClearForBody(IDiggableTerrain terrain, Vector2 worldPos, float radius)
    {
        if (terrain == null)
            return true;
        float cellSize = terrain.CellSize;
        Vector2Int center = terrain.WorldToCell(worldPos);
        int cellRadius = Mathf.CeilToInt((radius + cellSize * 0.5f) / cellSize) + 1;

        for (int x = center.x - cellRadius; x <= center.x + cellRadius; x++)
        {
            for (int y = center.y - cellRadius; y <= center.y + cellRadius; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (terrain.IsBlocked(cell) && CircleOverlapsCell(worldPos, radius, cell, terrain))
                    return false;
            }
        }

        return true;
    }

    private static bool CircleOverlapsCell(Vector2 worldPos, float radius, Vector2Int cell, IDiggableTerrain terrain)
    {
        Vector2 cellCenter = terrain.CellToWorld(cell);
        float halfSize = terrain.CellSize * 0.5f;

        float closestX = Mathf.Clamp(worldPos.x, cellCenter.x - halfSize, cellCenter.x + halfSize);
        float closestY = Mathf.Clamp(worldPos.y, cellCenter.y - halfSize, cellCenter.y + halfSize);
        Vector2 closestPoint = new Vector2(closestX, closestY);

        return (closestPoint - worldPos).sqrMagnitude <= radius * radius;
    }
}
