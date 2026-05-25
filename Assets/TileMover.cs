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

        Vector2 nextPos = rb.position + desiredVelocity * Time.fixedDeltaTime;

        if (MapGenerator.Instance.IsWorldPositionClearForBody(nextPos, bodyRadius))
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        Vector2 xVel = new Vector2(desiredVelocity.x, 0f);
        Vector2 xPos = rb.position + xVel * Time.fixedDeltaTime;

        if (MapGenerator.Instance.IsWorldPositionClearForBody(xPos, bodyRadius))
        {
            rb.linearVelocity = xVel;
            return;
        }

        Vector2 yVel = new Vector2(0f, desiredVelocity.y);
        Vector2 yPos = rb.position + yVel * Time.fixedDeltaTime;

        if (MapGenerator.Instance.IsWorldPositionClearForBody(yPos, bodyRadius))
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

        if (MapGenerator.Instance.IsWorldPositionClearForBody(rb.position, bodyRadius))
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
                        bodyRadius))
                    {
                        rb.position = testWorld;
                        rb.linearVelocity = Vector2.zero;
                        return;
                    }
                }
            }
        }
    }
}