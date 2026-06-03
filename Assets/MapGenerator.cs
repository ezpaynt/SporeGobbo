using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance { get; private set; }

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

        [Header("Attachment Spacing")]
        public int minAttachmentDistanceFromSpawn = 8;
        public int attachmentConnectorLength = 3;
    }

    [Serializable]
    public class MapSettings
    {
        public int seed = 0;
        public bool randomSeed = true;

        public int width = 120;
        public int height = 120;

        public Vector2Int spawnCenter = new Vector2Int(60, 60);
        public int spawnRadius = 5;

        public int darkDistance = 20;
        public int darkerDistance = 35;

        public int fillerTunnelCount = 12;
        public int fillerPocketCount = 8;
        public int fillerMinDistanceFromMainPath = 7;
        public int fillerMaxDistanceFromMainPath = 35;
    }

    private class PlannedArea
    {
        public int id;
        public AreaType type;
        public Vector2Int center;
        public int radius;
        public HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
    }

    public class MapData
    {
        public HashSet<Vector2Int> openCells = new HashSet<Vector2Int>();
        public HashSet<Vector2Int> dirtCells = new HashSet<Vector2Int>();
        public HashSet<Vector2Int> revealedCells = new HashSet<Vector2Int>();

        public bool IsOpen(Vector2Int cell) => openCells.Contains(cell);
        public bool IsDirt(Vector2Int cell) => dirtCells.Contains(cell);
        public bool IsRevealed(Vector2Int cell) => revealedCells.Contains(cell);
    }

    [Header("Map Settings")]
    public MapSettings map = new MapSettings();

    [Header("Branches")]
    public List<BranchSettings> branches = new List<BranchSettings>();

    [Header("Tilemaps")]
    public Tilemap floorTilemap;
    public Tilemap dirtTilemap;
    public Tilemap markerTilemap;

    [Header("Tiles")]
    public TileBase floorTile;
    public TileBase normalDirtTile;
    public TileBase darkDirtTile;
    public TileBase darkerDirtTile;
    public TileBase revealDirtTile;

    [Header("Debug / Marker Tiles")]
    public TileBase bossMarkerTile;
    public TileBase campMarkerTile;
    public TileBase branchDebugTile;

    [Header("Debug")]
    public bool showPlannedFloorUnderDirt = false;
    public bool showBranchDebugTiles = true;

    public MapData Data { get; private set; } = new MapData();

    private System.Random rng;

    private readonly HashSet<Vector2Int> plannedOpen = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> spawnOpen = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> mainPathCells = new HashSet<Vector2Int>();

    private readonly Dictionary<Vector2Int, PlannedArea> areaByCell = new Dictionary<Vector2Int, PlannedArea>();
    private readonly Dictionary<Vector2Int, int> revealGroupByCell = new Dictionary<Vector2Int, int>();
    private readonly List<PlannedArea> plannedAreas = new List<PlannedArea>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate Map")]
    public void Generate()
    {
        ClearTilemaps();
        ClearData();

        rng = new System.Random(
            map.randomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : map.seed
        );

        if (branches == null || branches.Count == 0)
            CreateDefaultBranches();

        BuildSpawn();
        BuildBranches();
        BuildFiller();
        PaintTilemaps();
    }

    private void CreateDefaultBranches()
    {
        branches = new List<BranchSettings>
        {
            new BranchSettings { branchName = "North", direction = Vector2Int.up, length = 35, forkCount = 1, smallRooms = 2 },
            new BranchSettings { branchName = "East", direction = Vector2Int.right, length = 45, wobble = 5, forkCount = 2, smallRooms = 3, camps = 1 },
            new BranchSettings { branchName = "West", direction = Vector2Int.left, length = 22, forkCount = 0, smallRooms = 1 },
            new BranchSettings { branchName = "South Boss", direction = Vector2Int.down, length = 55, forkCount = 2, smallRooms = 2, bosses = 1 }
        };
    }

    private void ClearTilemaps()
    {
        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (dirtTilemap != null) dirtTilemap.ClearAllTiles();
        if (markerTilemap != null) markerTilemap.ClearAllTiles();
    }

    private void ClearData()
    {
        Data = new MapData();

        plannedOpen.Clear();
        spawnOpen.Clear();
        mainPathCells.Clear();
        areaByCell.Clear();
        revealGroupByCell.Clear();
        plannedAreas.Clear();
    }

    private void BuildSpawn()
    {
        PlannedArea spawn = CreateArea(AreaType.Spawn, map.spawnCenter, map.spawnRadius);

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

            CarveTunnelArea(path, branch.tunnelHalfWidth, AreaType.MainTunnel);

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

            int minIndex = Mathf.Clamp(branch.minAttachmentDistanceFromSpawn, 1, path.Count - 2);
            int startIndex = rng.Next(minIndex, path.Count - 1);

            Vector2Int start = path[startIndex];
            Vector2Int forkDir = PickSideDirection(path, startIndex);
            int forkLength = rng.Next(branch.forkLengthMin, branch.forkLengthMax + 1);

            List<Vector2Int> forkPath = BuildWobblyPath(start, forkDir, forkLength, branch.wobble);

            CarveTunnelArea(forkPath, branch.tunnelHalfWidth, AreaType.ForkTunnel);

            foreach (Vector2Int p in forkPath)
                mainPathCells.Add(p);
        }
    }

    private void BuildAttachments(BranchSettings branch, List<Vector2Int> path)
    {
        List<AreaType> attachments = new List<AreaType>();

        for (int i = 0; i < branch.smallRooms; i++) attachments.Add(AreaType.SmallRoom);
        for (int i = 0; i < branch.camps; i++) attachments.Add(AreaType.Camp);
        for (int i = 0; i < branch.bosses; i++) attachments.Add(AreaType.Boss);

        Shuffle(attachments);

        foreach (AreaType type in attachments)
        {
            if (path.Count < 10) continue;

            int minIndex = Mathf.Clamp(branch.minAttachmentDistanceFromSpawn, 1, path.Count - 2);
            int index = rng.Next(minIndex, path.Count - 1);

            Vector2Int attachPoint = path[index];
            Vector2Int sideDir = PickSideDirection(path, index);

            Vector2Int connectorEnd = attachPoint + sideDir * branch.attachmentConnectorLength;
            List<Vector2Int> connector = StraightPath(attachPoint, connectorEnd);

            int radius = GetRadiusForArea(type);
            Vector2Int roomCenter = connectorEnd + sideDir * (radius + 1);

            if (!InBounds(roomCenter)) continue;
            if (IsTooCloseToExistingArea(roomCenter, radius + 3)) continue;

            CarveTunnelArea(connector, 1, AreaType.MainTunnel);

            PlannedArea area = CreateArea(type, roomCenter, radius);

            foreach (Vector2Int cell in area.cells)
                plannedOpen.Add(cell);

            PaintMarker(type, roomCenter);
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
            CarveTunnelArea(path, 1, AreaType.FillerTunnel);
        }

        for (int i = 0; i < map.fillerPocketCount; i++)
        {
            Vector2Int pos = FindFillerPosition();
            int radius = rng.Next(2, 5);

            PlannedArea pocket = CreateArea(AreaType.FillerPocket, pos, radius);

            foreach (Vector2Int cell in pocket.cells)
                plannedOpen.Add(cell);
        }
    }

    private void PaintTilemaps()
    {
        if (floorTilemap == null || dirtTilemap == null)
        {
            Debug.LogError("MapGenerator needs Floor Tilemap and Dirt Tilemap assigned.");
            return;
        }

        for (int x = 0; x < map.width; x++)
        {
            for (int y = 0; y < map.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Vector3Int tilePos = ToTilePos(cell);

                if (spawnOpen.Contains(cell))
                {
                    floorTilemap.SetTile(tilePos, floorTile);
                    dirtTilemap.SetTile(tilePos, null);

                    Data.openCells.Add(cell);
                    Data.revealedCells.Add(cell);
                    continue;
                }

                if (plannedOpen.Contains(cell))
                {
                    if (showPlannedFloorUnderDirt)
                        floorTilemap.SetTile(tilePos, floorTile);

                    dirtTilemap.SetTile(tilePos, GetDirtTileByDistance(cell));

                    Data.dirtCells.Add(cell);
                    continue;
                }

                if (IsRevealBorderCell(cell))
                {
                    dirtTilemap.SetTile(tilePos, revealDirtTile != null ? revealDirtTile : normalDirtTile);

                    PlannedArea area = GetNearestAreaTouching(cell);
                    if (area != null)
                        revealGroupByCell[cell] = area.id;

                    Data.dirtCells.Add(cell);
                    continue;
                }

                dirtTilemap.SetTile(tilePos, GetDirtTileByDistance(cell));
                Data.dirtCells.Add(cell);
            }
        }

        if (showBranchDebugTiles)
            PaintBranchDebug();
    }

    public void DigCircle(Vector3 worldPos, float radius)
    {
        Vector3Int centerCell = dirtTilemap.WorldToCell(worldPos);
        int r = Mathf.CeilToInt(radius);

        for (int x = centerCell.x - r; x <= centerCell.x + r; x++)
        {
            for (int y = centerCell.y - r; y <= centerCell.y + r; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                float dist = Vector2.Distance(new Vector2(centerCell.x, centerCell.y), new Vector2(x, y));

                if (dist <= radius)
                    DigCell(cell);
            }
        }
    }

    public void DigAtWorld(Vector3 worldPos)
    {
        Vector3Int tilePos = dirtTilemap.WorldToCell(worldPos);
        DigCell(new Vector2Int(tilePos.x, tilePos.y));
    }

    public void DigCell(Vector2Int cell)
    {
        if (revealGroupByCell.TryGetValue(cell, out int groupId))
        {
            RevealGroup(groupId);
            return;
        }

        Vector3Int tilePos = ToTilePos(cell);

        floorTilemap.SetTile(tilePos, floorTile);
        dirtTilemap.SetTile(tilePos, null);

        Data.dirtCells.Remove(cell);
        Data.openCells.Add(cell);
        Data.revealedCells.Add(cell);
    }

    public void RevealGroup(int groupId)
    {
        PlannedArea area = plannedAreas.Find(a => a.id == groupId);
        if (area == null) return;

        foreach (Vector2Int cell in area.cells)
        {
            Vector3Int tilePos = ToTilePos(cell);

            floorTilemap.SetTile(tilePos, floorTile);
            dirtTilemap.SetTile(tilePos, null);

            Data.dirtCells.Remove(cell);
            Data.openCells.Add(cell);
            Data.revealedCells.Add(cell);
        }

        List<Vector2Int> triggerCells = new List<Vector2Int>();

        foreach (var pair in revealGroupByCell)
        {
            if (pair.Value == groupId)
                triggerCells.Add(pair.Key);
        }

        foreach (Vector2Int cell in triggerCells)
        {
            Vector3Int tilePos = ToTilePos(cell);

            floorTilemap.SetTile(tilePos, floorTile);
            dirtTilemap.SetTile(tilePos, null);

            revealGroupByCell.Remove(cell);
            Data.dirtCells.Remove(cell);
            Data.openCells.Add(cell);
            Data.revealedCells.Add(cell);
        }
    }

    public bool IsWorldPositionClearForBody(Vector3 worldPos, float radius)
    {
        if (dirtTilemap == null) return true;

        Vector3Int center = dirtTilemap.WorldToCell(worldPos);
        int r = Mathf.CeilToInt(radius);

        for (int x = center.x - r; x <= center.x + r; x++)
        {
            for (int y = center.y - r; y <= center.y + r; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (Vector2.Distance(new Vector2(center.x, center.y), new Vector2(x, y)) <= radius)
                {
                    if (IsDirt(cell))
                        return false;
                }
            }
        }

        return true;
    }

    public bool IsWorldPositionClear(Vector3 worldPos)
    {
        Vector3Int tilePos = dirtTilemap.WorldToCell(worldPos);
        return IsOpen(new Vector2Int(tilePos.x, tilePos.y));
    }

    public bool IsOpen(Vector2Int cell)
    {
        return Data.openCells.Contains(cell) && !Data.dirtCells.Contains(cell);
    }

    public bool IsDirt(Vector2Int cell)
    {
        return Data.dirtCells.Contains(cell) || dirtTilemap.HasTile(ToTilePos(cell));
    }

    public bool IsDiggable(Vector2Int cell)
    {
        return InBounds(cell) && IsDirt(cell);
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return InBounds(cell) && !IsDirt(cell);
    }

    public Vector2Int WorldToCell2D(Vector3 worldPos)
    {
        Vector3Int cell = dirtTilemap.WorldToCell(worldPos);
        return new Vector2Int(cell.x, cell.y);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        return dirtTilemap.GetCellCenterWorld(ToTilePos(cell));
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

    private TileBase GetDirtTileByDistance(Vector2Int cell)
    {
        int dist = DistanceToNearestMainPath(cell);

        if (dist >= map.darkerDistance && darkerDirtTile != null)
            return darkerDirtTile;

        if (dist >= map.darkDistance && darkDirtTile != null)
            return darkDirtTile;

        return normalDirtTile;
    }

    private PlannedArea CreateArea(AreaType type, Vector2Int center, int radius)
    {
        PlannedArea area = new PlannedArea
        {
            id = plannedAreas.Count + 1,
            type = type,
            center = center,
            radius = radius
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

    private void CarveTunnelArea(List<Vector2Int> path, int halfWidth, AreaType type)
    {
        foreach (Vector2Int p in path)
        {
            PlannedArea area = CreateArea(type, p, halfWidth);

            foreach (Vector2Int cell in area.cells)
                plannedOpen.Add(cell);
        }
    }

    private List<Vector2Int> BuildWobblyPath(Vector2Int start, Vector2Int direction, int length, int wobble)
    {
        List<Vector2Int> path = new List<Vector2Int>();
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
        List<Vector2Int> path = new List<Vector2Int>();

        Vector2Int delta = end - start;
        int steps = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : i / (float)steps;
            Vector2Int p = Vector2Int.RoundToInt(Vector2.Lerp(start, end, t));

            if (InBounds(p))
                path.Add(p);
        }

        return path;
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

    private int GetRadiusForArea(AreaType type)
    {
        switch (type)
        {
            case AreaType.Boss:
                return rng.Next(6, 9);
            case AreaType.Camp:
                return rng.Next(5, 7);
            case AreaType.SmallRoom:
                return rng.Next(3, 6);
            default:
                return rng.Next(2, 5);
        }
    }

    private void PaintMarker(AreaType type, Vector2Int cell)
    {
        if (markerTilemap == null) return;

        if (type == AreaType.Boss && bossMarkerTile != null)
            markerTilemap.SetTile(ToTilePos(cell), bossMarkerTile);

        if (type == AreaType.Camp && campMarkerTile != null)
            markerTilemap.SetTile(ToTilePos(cell), campMarkerTile);
    }

    private void PaintBranchDebug()
    {
        if (markerTilemap == null || branchDebugTile == null) return;

        foreach (Vector2Int cell in mainPathCells)
            markerTilemap.SetTile(ToTilePos(cell), branchDebugTile);
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

        switch (r)
        {
            case 0: return Vector2Int.up;
            case 1: return Vector2Int.down;
            case 2: return Vector2Int.left;
            default: return Vector2Int.right;
        }
    }

    private int DistanceToNearestMainPath(Vector2Int cell)
    {
        int best = int.MaxValue;

        foreach (Vector2Int p in mainPathCells)
        {
            int d = Mathf.Abs(cell.x - p.x) + Mathf.Abs(cell.y - p.y);
            if (d < best) best = d;
        }

        return best == int.MaxValue ? 9999 : best;
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
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < map.width &&
               cell.y < map.height;
    }

    private Vector3Int ToTilePos(Vector2Int cell)
    {
        return new Vector3Int(cell.x, cell.y, 0);
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = rng.Next(i, list.Count);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}