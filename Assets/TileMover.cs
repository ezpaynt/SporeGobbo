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

        if (MapGenerator.Instance.IsWorldPositionClearForBody(nextPos, clearanceRadius))
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        Vector2 xVel = new Vector2(desiredVelocity.x, 0f);
        Vector2 xPos = rb.position + xVel * Time.fixedDeltaTime;

        if (MapGenerator.Instance.IsWorldPositionClearForBody(xPos, clearanceRadius))
        {
            rb.linearVelocity = xVel;
            return;
        }

        Vector2 yVel = new Vector2(0f, desiredVelocity.y);
        Vector2 yPos = rb.position + yVel * Time.fixedDeltaTime;

        if (MapGenerator.Instance.IsWorldPositionClearForBody(yPos, clearanceRadius))
        {
            rb.linearVelocity = yVel;
            return;
        }

        rb.linearVelocity = Vector2.zero;
    }

    public static void KeepOutOfWalls(Rigidbody2D rb, float bodyRadius)
    {
        if (MapGenerator.Instance == null)
            return;

        float clearanceRadius = GetMapClearanceRadius(rb, bodyRadius);

        if (MapGenerator.Instance.IsWorldPositionClearForBody(rb.position, clearanceRadius))
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

                    if (MapGenerator.Instance.IsWorldPositionClearForBody(
                        testWorld,
                        clearanceRadius))
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

    private static float GetMapClearanceRadius(Rigidbody2D rb, float bodyRadius)
    {
        float radius = GetColliderBodyRadius(rb, bodyRadius);

        if (MapGenerator.Instance == null || MapGenerator.Instance.Data == null)
            return radius;

        float cellCornerPadding = MapGenerator.Instance.Data.cellSize * 0.5f * Mathf.Sqrt(2f);
        return radius + cellCornerPadding;
    }
}
