using System.Collections.Generic;
using UnityEngine;

public static class MapPathfinder
{
    public static float bodyRadius = 0.22f;

    class Node
    {
        public Vector2Int cell;
        public Node parent;
        public float g;
        public float h;
        public float f => g + h;

        public Node(Vector2Int cell, Node parent, float g, float h)
        {
            this.cell = cell;
            this.parent = parent;
            this.g = g;
            this.h = h;
        }
    }

    public static bool HasLineOfWalkableSight(Vector2 from, Vector2 to, float stepSize = 0.25f)
    {
        if (MapGenerator.Instance == null)
            return true;

        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / stepSize));

        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(from, to, i / (float)steps);

            if (!MapGenerator.Instance.IsWorldPositionClearForBody(point, bodyRadius))
                return false;
        }

        return true;
    }

    public static bool TryFindPath(Vector2 startWorld, Vector2 goalWorld, out List<Vector2> path)
    {
        path = new List<Vector2>();

        if (MapGenerator.Instance == null || MapGenerator.Instance.Data == null)
            return false;

        MapData data = MapGenerator.Instance.Data;

        Vector2Int start = data.WorldToCell(startWorld);
        Vector2Int goal = data.WorldToCell(goalWorld);

        if (!IsClearCell(start))
            start = FindNearestClearCell(start, 8);

        if (!IsClearCell(goal))
            goal = FindNearestClearCell(goal, 10);

        if (!IsClearCell(start) || !IsClearCell(goal))
            return false;

        List<Node> open = new List<Node>();
        Dictionary<Vector2Int, Node> nodes = new Dictionary<Vector2Int, Node>();
        HashSet<Vector2Int> closed = new HashSet<Vector2Int>();

        Node startNode = new Node(start, null, 0f, CellDistance(start, goal));
        open.Add(startNode);
        nodes[start] = startNode;

        int safety = 0;

        while (open.Count > 0 && safety < 6000)
        {
            safety++;

            Node current = GetLowestF(open);
            open.Remove(current);
            closed.Add(current.cell);

            if (current.cell == goal)
            {
                BuildWorldPath(current, data, path);
                return path.Count > 0;
            }

            foreach (Vector2Int neighbor in GetNeighbors(current.cell))
            {
                if (closed.Contains(neighbor))
                    continue;

                if (!IsClearCell(neighbor))
                    continue;

                float newG = current.g + CellDistance(current.cell, neighbor);

                if (!nodes.TryGetValue(neighbor, out Node node))
                {
                    node = new Node(neighbor, current, newG, CellDistance(neighbor, goal));
                    nodes[neighbor] = node;
                    open.Add(node);
                }
                else if (newG < node.g)
                {
                    node.parent = current;
                    node.g = newG;

                    if (!open.Contains(node))
                        open.Add(node);
                }
            }
        }

        return false;
    }

    static List<Vector2Int> GetNeighbors(Vector2Int cell)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        Vector2Int up = cell + Vector2Int.up;
        Vector2Int down = cell + Vector2Int.down;
        Vector2Int left = cell + Vector2Int.left;
        Vector2Int right = cell + Vector2Int.right;

        if (IsClearCell(up)) result.Add(up);
        if (IsClearCell(down)) result.Add(down);
        if (IsClearCell(left)) result.Add(left);
        if (IsClearCell(right)) result.Add(right);

        TryAddDiagonal(result, cell, 1, 1);
        TryAddDiagonal(result, cell, 1, -1);
        TryAddDiagonal(result, cell, -1, 1);
        TryAddDiagonal(result, cell, -1, -1);

        return result;
    }

    static void TryAddDiagonal(List<Vector2Int> result, Vector2Int cell, int x, int y)
    {
        Vector2Int horizontal = cell + new Vector2Int(x, 0);
        Vector2Int vertical = cell + new Vector2Int(0, y);
        Vector2Int diagonal = cell + new Vector2Int(x, y);

        if (!IsClearCell(horizontal)) return;
        if (!IsClearCell(vertical)) return;
        if (!IsClearCell(diagonal)) return;

        result.Add(diagonal);
    }

    static bool IsClearCell(Vector2Int cell)
    {
        if (MapGenerator.Instance == null || MapGenerator.Instance.Data == null)
            return true;

        MapData data = MapGenerator.Instance.Data;

        if (!data.InBounds(cell))
            return false;

        Vector2 world = data.CellToWorld(cell);
        return MapGenerator.Instance.IsWorldPositionClearForBody(world, bodyRadius);
    }

    static Vector2Int FindNearestClearCell(Vector2Int origin, int radius)
    {
        for (int r = 1; r <= radius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    Vector2Int test = origin + new Vector2Int(x, y);

                    if (IsClearCell(test))
                        return test;
                }
            }
        }

        return origin;
    }

    static float CellDistance(Vector2Int a, Vector2Int b)
    {
        return Vector2Int.Distance(a, b);
    }

    static Node GetLowestF(List<Node> open)
    {
        Node best = open[0];

        for (int i = 1; i < open.Count; i++)
        {
            if (open[i].f < best.f)
                best = open[i];
        }

        return best;
    }

    static void BuildWorldPath(Node end, MapData data, List<Vector2> path)
    {
        path.Clear();

        Node current = end;

        while (current != null)
        {
            path.Add(data.CellToWorld(current.cell));
            current = current.parent;
        }

        path.Reverse();

        if (path.Count > 0)
            path.RemoveAt(0);
    }
}