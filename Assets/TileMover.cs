using UnityEngine;

public static class TileMover
{
    public static void Move(Rigidbody2D rb, Vector2 desiredVelocity, float bodyRadius)
    {
        if (MapGenerator.Instance == null)
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        float clearanceRadius = GetMapClearanceRadius(rb, bodyRadius);
        Vector2 nextPos = rb.position + desiredVelocity * Time.fixedDeltaTime;

        if (IsMapPositionClearForBody(nextPos, clearanceRadius))
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        Vector2 xVel = new Vector2(desiredVelocity.x, 0f);
        Vector2 xPos = rb.position + xVel * Time.fixedDeltaTime;

        if (IsMapPositionClearForBody(xPos, clearanceRadius))
        {
            rb.linearVelocity = xVel;
            return;
        }

        Vector2 yVel = new Vector2(0f, desiredVelocity.y);
        Vector2 yPos = rb.position + yVel * Time.fixedDeltaTime;

        if (IsMapPositionClearForBody(yPos, clearanceRadius))
        {
            rb.linearVelocity = yVel;
            return;
        }

        rb.linearVelocity = Vector2.zero;
    }

    public static void KeepOutOfWalls(Rigidbody2D rb, float bodyRadius)
    {
        if (MapGenerator.Instance == null || MapGenerator.Instance.Data == null)
            return;

        float clearanceRadius = GetMapClearanceRadius(rb, bodyRadius);

        if (IsMapPositionClearForBody(rb.position, clearanceRadius))
            return;

        Vector2Int cell =
            MapGenerator.Instance.Data.WorldToCell(rb.position);

        for (int r = 1; r <= 6; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    Vector2Int testCell =
                        cell + new Vector2Int(x, y);

                    Vector2 testWorld =
                        MapGenerator.Instance.Data.CellToWorld(testCell);

                    if (IsMapPositionClearForBody(testWorld, clearanceRadius))
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

    private static bool IsMapPositionClearForBody(Vector2 worldPos, float radius)
    {
        MapGenerator map = MapGenerator.Instance;
        if (map == null || map.Data == null)
            return true;

        MapData data = map.Data;
        Vector2Int center = data.WorldToCell(worldPos);
        int cellRadius = Mathf.CeilToInt((radius + data.cellSize * 0.5f) / data.cellSize) + 1;

        for (int x = center.x - cellRadius; x <= center.x + cellRadius; x++)
        {
            for (int y = center.y - cellRadius; y <= center.y + cellRadius; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!data.InBounds(cell))
                    return false;

                if (data.IsBlocked(cell) && CircleOverlapsCell(worldPos, radius, cell, data))
                    return false;
            }
        }

        return true;
    }

    private static bool CircleOverlapsCell(Vector2 worldPos, float radius, Vector2Int cell, MapData data)
    {
        Vector2 cellCenter = data.CellToWorld(cell);
        float halfSize = data.cellSize * 0.5f;

        float closestX = Mathf.Clamp(worldPos.x, cellCenter.x - halfSize, cellCenter.x + halfSize);
        float closestY = Mathf.Clamp(worldPos.y, cellCenter.y - halfSize, cellCenter.y + halfSize);
        Vector2 closestPoint = new Vector2(closestX, closestY);

        return (closestPoint - worldPos).sqrMagnitude <= radius * radius;
    }
}
