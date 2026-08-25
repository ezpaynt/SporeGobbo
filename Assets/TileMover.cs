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

        float clearanceRadius = GetMapClearanceRadius(rb, bodyRadius);
        Vector2 nextPos = rb.position + desiredVelocity * Time.fixedDeltaTime;

        if (IsTerrainPositionClearForBody(terrain, nextPos, clearanceRadius))
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        Vector2 xVel = new Vector2(desiredVelocity.x, 0f);
        Vector2 xPos = rb.position + xVel * Time.fixedDeltaTime;

        if (IsTerrainPositionClearForBody(terrain, xPos, clearanceRadius))
        {
            rb.linearVelocity = xVel;
            return;
        }

        Vector2 yVel = new Vector2(0f, desiredVelocity.y);
        Vector2 yPos = rb.position + yVel * Time.fixedDeltaTime;

        if (IsTerrainPositionClearForBody(terrain, yPos, clearanceRadius))
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

        float clearanceRadius = GetMapClearanceRadius(rb, bodyRadius);

        if (IsTerrainPositionClearForBody(terrain, rb.position, clearanceRadius))
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

                    if (IsTerrainPositionClearForBody(terrain, testWorld, clearanceRadius))
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

    public static float GetMapClearanceRadius(Rigidbody2D rb, float bodyRadius)
    {
        return GetColliderBodyRadius(rb, bodyRadius);
    }

    public static bool CanOccupy(IDiggableTerrain terrain, Vector2 worldPosition, float bodyRadius)
    {
        return terrain == null || IsTerrainPositionClearForBody(terrain, worldPosition, Mathf.Max(0f, bodyRadius));
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
