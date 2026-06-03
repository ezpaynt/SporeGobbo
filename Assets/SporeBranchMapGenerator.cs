using System;
using System.Collections.Generic;
using UnityEngine;

public class SporeBranchMapGenerator : MonoBehaviour
{
    public enum AreaType
    {
        Spawn,
        MainTunnel,
        ForkTunnel,
        SmallRoom,
        Camp,
        Boss,
        FillerTunnel,
        FillerPocket
    }

    public enum DirtMode
    {
        NormalDig,
        RevealTrigger
    }

    [Serializable]
    public class BranchSettings
    {
        public string branchName = "Branch";
        public Vector2Int direction = Vector2Int.up;

        [Header("Main Path")]
        public int length = 35;
        public int tunnelHalfWidth = 1;
        public int wobble = 3;

        [Header("Forks")]
        public int forkCount = 1;
        public int forkLengthMin = 10;
        public int forkLengthMax = 20;

        [Header("Attached Discoveries")]
        public int smallRooms = 2;
        public int camps = 0;
        public int bosses = 0;

        [Header("Spacing")]
        public int minAttachmentDistanceFromSpawn = 8;
        public int attachmentConnectorLength = 3;
    }

    [Serializable]
    public class MapSettings
    {
        public int seed = 0;
        public bool randomSeed = true;

        [Header("Map Size")]
        public int width = 120;
        public int height = 120;

        [Header("Spawn")]
        public Vector2Int spawnCenter = new Vector2Int(60, 60);
        public int spawnRadius = 5;

        [Header("Dirt Darkness")]
        public int darkDistance = 20;
        public int darkerDistance = 35;

        [Header("Filler")]
        public int fillerTunnelCount = 12;
        public int fillerPocketCount = 8;
        public int fillerMinDistanceFromMainPath = 7;
        public int fillerMaxDistanceFromMainPath = 35;
    }

    [Header("Settings")]
    public MapSettings map = new MapSettings();

    [Header("Branches")]
    public List<BranchSettings> branches = new List<BranchSettings>();

    [Header("Prefabs")]
    public GameObject normalDirtPrefab;
    public GameObject darkDirtPrefab;
    public GameObject darkerDirtPrefab;
    public GameObject revealDirtPrefab;
    public GameObject floorPrefab;
    public GameObject bossMarkerPrefab;
    public GameObject campMarkerPrefab;

    [Header("Parents")]
    public Transform dirtParent;
    public Transform floorParent;
    public Transform markerParent;

    private System.Random rng;

    private readonly HashSet<Vector2Int> plannedOpen = new();
    private readonly HashSet<Vector2Int> spawnOpen = new();
    private readonly HashSet<Vector2Int> mainPathCells = new();

    private readonly Dictionary<Vector2Int, PlannedArea> areaByCell = new();
    private readonly List<PlannedArea> plannedAreas = new();

    private class PlannedArea
    {
        public int id;
        public AreaType type;
        public Vector2Int center;
        public int radius;
        public HashSet<Vector2Int> cells = new();
        public bool revealedAtStart;
    }

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate Map")]
    public void Generate()
    {
        ClearOldMap();

        rng = new System.Random(map.randomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : map.seed);

        plannedOpen.Clear();
        spawnOpen.Clear();
        mainPathCells.Clear();
        areaByCell.Clear();
        plannedAreas.Clear();

        BuildSpawn();
        BuildBranches();
        BuildFiller();
        PaintMap();
    }

    private void ClearOldMap()
    {
        ClearChildren(dirtParent);
        ClearChildren(floorParent);
        ClearChildren(markerParent);
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(parent.GetChild(i).gameObject);
            else
                DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private void BuildSpawn()
    {
        PlannedArea spawn = CreateArea(AreaType.Spawn, map.spawnCenter, map.spawnRadius, true);

        foreach (Vector2Int cell in spawn.cells)
        {
            plannedOpen.Add(cell);
            spawnOpen.Add(cell);
            mainPathCells.Add(cell);
        }
    }

    private void BuildBranches()
    {
        foreach (BranchSettings branch in branches)
        {
            Vector2Int dir = NormalizeCardinal(branch.direction);
            if (dir == Vector2Int.zero) continue;

            List<Vector2Int> path = BuildWobblyPath(map.spawnCenter, dir, branch.length, branch.wobble);

            CarveTunnelArea(path, branch.tunnelHalfWidth, AreaType.MainTunnel, true);

            foreach (Vector2Int p in path)
                mainPathCells.Add(p);

            BuildBranchForks(branch, path);
            BuildAttachments(branch, path);
        }
    }

    private void BuildBranchForks(BranchSettings branch, List<Vector2Int> path)
    {
        for (int i = 0; i < branch.forkCount; i++)
        {
            if (path.Count < 10) continue;

            int startIndex = rng.Next(
                Mathf.Min(path.Count - 1, branch.minAttachmentDistanceFromSpawn),
                path.Count - 2
            );

            Vector2Int start = path[startIndex];
            Vector2Int forkDir = PickSideDirection(path, startIndex);
            int forkLength = rng.Next(branch.forkLengthMin, branch.forkLengthMax + 1);

            List<Vector2Int> forkPath = BuildWobblyPath(start, forkDir, forkLength, branch.wobble);
            CarveTunnelArea(forkPath, branch.tunnelHalfWidth, AreaType.ForkTunnel, false);

            foreach (Vector2Int p in forkPath)
                mainPathCells.Add(p);
        }
    }

    private void BuildAttachments(BranchSettings branch, List<Vector2Int> path)
    {
        int total = branch.smallRooms + branch.camps + branch.bosses;
        if (total <= 0 || path.Count < 10) return;

        List<AreaType> queue = new();

        for (int i = 0; i < branch.smallRooms; i++) queue.Add(AreaType.SmallRoom);
        for (int i = 0; i < branch.camps; i++) queue.Add(AreaType.Camp);
        for (int i = 0; i < branch.bosses; i++) queue.Add(AreaType.Boss);

        Shuffle(queue);

        foreach (AreaType type in queue)
        {
            int index = rng.Next(
                Mathf.Min(path.Count - 1, branch.minAttachmentDistanceFromSpawn),
                path.Count - 1
            );

            Vector2Int attachPoint = path[index];
            Vector2Int sideDir = PickSideDirection(path, index);

            Vector2Int connectorEnd = attachPoint + sideDir * branch.attachmentConnectorLength;
            List<Vector2Int> connector = StraightPath(attachPoint, connectorEnd);

            int radius = type switch
            {
                AreaType.Boss => rng.Next(6, 9),
                AreaType.Camp => rng.Next(5, 7),
                _ => rng.Next(3, 6)
            };

            Vector2Int roomCenter = connectorEnd + sideDir * (radius + 1);

            if (!InBounds(roomCenter)) continue;
            if (IsTooCloseToExistingArea(roomCenter, radius + 3)) continue;

            CarveTunnelArea(connector, 1, AreaType.MainTunnel, false);

            PlannedArea area = CreateArea(type, roomCenter, radius, false);

            foreach (Vector2Int cell in area.cells)
                plannedOpen.Add(cell);

            SpawnMarker(type, roomCenter);
        }
    }

    private void BuildFiller()
    {
        for (int i = 0; i < map.fillerTunnelCount; i++)
        {
            Vector2Int start = FindFillerPosition();
            Vector2Int dir = RandomCardinal();
            int length = rng.Next(5, 16);

            List<Vector2Int> path = BuildWobblyPath(start, dir, length, 2);
            CarveTunnelArea(path, 1, AreaType.FillerTunnel, false);
        }

        for (int i = 0; i < map.fillerPocketCount; i++)
        {
            Vector2Int pos = FindFillerPosition();
            int radius = rng.Next(2, 5);

            PlannedArea pocket = CreateArea(AreaType.FillerPocket, pos, radius, false);

            foreach (Vector2Int cell in pocket.cells)
                plannedOpen.Add(cell);
        }
    }

    private Vector2Int FindFillerPosition()
    {
        for (int tries = 0; tries < 500; tries++)
        {
            Vector2Int pos = new Vector2Int(
                rng.Next(4, map.width - 4),
                rng.Next(4, map.height - 4)
            );

            int dist = DistanceToNearestMainPath(pos);

            if (dist >= map.fillerMinDistanceFromMainPath &&
                dist <= map.fillerMaxDistanceFromMainPath &&
                !plannedOpen.Contains(pos))
            {
                return pos;
            }
        }

        return map.spawnCenter;
    }

    private void PaintMap()
    {
        for (int x = 0; x < map.width; x++)
        {
            for (int y = 0; y < map.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (spawnOpen.Contains(cell))
                {
                    SpawnPrefab(floorPrefab, cell, floorParent);
                    continue;
                }

                if (plannedOpen.Contains(cell))
                {
                    SpawnHiddenFloor(cell);
                    continue;
                }

                bool revealTrigger = IsRevealBorderCell(cell);

                if (revealTrigger)
                {
                    GameObject dirt = SpawnPrefab(revealDirtPrefab, cell, dirtParent);
                    AddRevealTriggerData(dirt, cell);
                }
                else
                {
                    SpawnPrefab(GetDirtPrefabByDistance(cell), cell, dirtParent);
                }
            }
        }
    }

    private void SpawnHiddenFloor(Vector2Int cell)
    {
        GameObject floor = SpawnPrefab(floorPrefab, cell, floorParent);
        if (floor != null)
            floor.SetActive(false);
    }

    private bool IsRevealBorderCell(Vector2Int dirtCell)
    {
        if (plannedOpen.Contains(dirtCell)) return false;
        if (spawnOpen.Contains(dirtCell)) return false;

        foreach (Vector2Int n in GetNeighbors4(dirtCell))
        {
            if (plannedOpen.Contains(n) && !spawnOpen.Contains(n))
                return true;
        }

        return false;
    }

    private void AddRevealTriggerData(GameObject dirt, Vector2Int cell)
    {
        if (dirt == null) return;

        SporeRevealDirt reveal = dirt.GetComponent<SporeRevealDirt>();
        if (reveal == null)
            reveal = dirt.AddComponent<SporeRevealDirt>();

        PlannedArea nearest = GetNearestAreaTouching(cell);

        if (nearest != null)
            reveal.revealGroupId = nearest.id;
    }

    private PlannedArea GetNearestAreaTouching(Vector2Int cell)
    {
        foreach (Vector2Int n in GetNeighbors4(cell))
        {
            if (areaByCell.TryGetValue(n, out PlannedArea area))
                return area;
        }

        return null;
    }

    private GameObject GetDirtPrefabByDistance(Vector2Int cell)
    {
        int dist = DistanceToNearestMainPath(cell);

        if (dist >= map.darkerDistance && darkerDirtPrefab != null)
            return darkerDirtPrefab;

        if (dist >= map.darkDistance && darkDirtPrefab != null)
            return darkDirtPrefab;

        return normalDirtPrefab;
    }

    private PlannedArea CreateArea(AreaType type, Vector2Int center, int radius, bool revealedAtStart)
    {
        PlannedArea area = new PlannedArea
        {
            id = plannedAreas.Count + 1,
            type = type,
            center = center,
            radius = radius,
            revealedAtStart = revealedAtStart
        };

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!InBounds(cell)) continue;

                float dx = (x - center.x) / (float)radius;
                float dy = (y - center.y) / (float)radius;

                if (dx * dx + dy * dy <= 1f)
                {
                    area.cells.Add(cell);
                    areaByCell[cell] = area;
                }
            }
        }

        plannedAreas.Add(area);
        return area;
    }

    private void CarveTunnelArea(List<Vector2Int> path, int halfWidth, AreaType type, bool revealedAtStart)
    {
        foreach (Vector2Int p in path)
        {
            PlannedArea area = CreateArea(type, p, halfWidth, revealedAtStart);

            foreach (Vector2Int cell in area.cells)
                plannedOpen.Add(cell);
        }
    }

    private List<Vector2Int> BuildWobblyPath(Vector2Int start, Vector2Int direction, int length, int wobble)
    {
        List<Vector2Int> path = new();
        Vector2Int pos = start;
        Vector2Int side = new Vector2Int(-direction.y, direction.x);

        for (int i = 0; i < length; i++)
        {
            pos += direction;

            if (wobble > 0 && rng.NextDouble() < 0.35)
                pos += side * rng.Next(-1, 2);

            if (!InBounds(pos)) break;

            path.Add(pos);
        }

        return path;
    }

    private List<Vector2Int> StraightPath(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new();

        Vector2Int pos = start;
        Vector2Int delta = end - start;
        int steps = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : i / (float)steps;
            Vector2Int p = Vector2Int.RoundToInt(Vector2.Lerp(start, end, t));

            if (InBounds(p))
                path.Add(p);
        }

        return path;
    }

    private Vector2Int PickSideDirection(List<Vector2Int> path, int index)
    {
        Vector2Int forward = Vector2Int.up;

        if (index > 0 && index < path.Count)
            forward = NormalizeCardinal(path[index] - path[index - 1]);

        Vector2Int left = new Vector2Int(-forward.y, forward.x);
        Vector2Int right = new Vector2Int(forward.y, -forward.x);

        return rng.NextDouble() < 0.5 ? left : right;
    }

    private Vector2Int NormalizeCardinal(Vector2Int v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return new Vector2Int(Math.Sign(v.x), 0);

        if (v.y != 0)
            return new Vector2Int(0, Math.Sign(v.y));

        return Vector2Int.zero;
    }

    private Vector2Int RandomCardinal()
    {
        int r = rng.Next(4);
        return r switch
        {
            0 => Vector2Int.up,
            1 => Vector2Int.down,
            2 => Vector2Int.left,
            _ => Vector2Int.right
        };
    }

    private int DistanceToNearestMainPath(Vector2Int cell)
    {
        int best = int.MaxValue;

        foreach (Vector2Int p in mainPathCells)
        {
            int d = Mathf.Abs(cell.x - p.x) + Mathf.Abs(cell.y - p.y);
            if (d < best) best = d;
        }

        return best;
    }

    private bool IsTooCloseToExistingArea(Vector2Int center, int distance)
    {
        foreach (PlannedArea area in plannedAreas)
        {
            int d = Mathf.Abs(center.x - area.center.x) + Mathf.Abs(center.y - area.center.y);
            if (d < distance + area.radius)
                return true;
        }

        return false;
    }

    private IEnumerable<Vector2Int> GetNeighbors4(Vector2Int cell)
    {
        yield return cell + Vector2Int.up;
        yield return cell + Vector2Int.down;
        yield return cell + Vector2Int.left;
        yield return cell + Vector2Int.right;
    }

    private bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < map.width && cell.y < map.height;
    }

    private GameObject SpawnPrefab(GameObject prefab, Vector2Int cell, Transform parent)
    {
        if (prefab == null) return null;

        Vector3 pos = new Vector3(cell.x, cell.y, 0);
        return Instantiate(prefab, pos, Quaternion.identity, parent);
    }

    private void SpawnMarker(AreaType type, Vector2Int cell)
    {
        if (type == AreaType.Boss)
            SpawnPrefab(bossMarkerPrefab, cell, markerParent);

        if (type == AreaType.Camp)
            SpawnPrefab(campMarkerPrefab, cell, markerParent);
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = rng.Next(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}