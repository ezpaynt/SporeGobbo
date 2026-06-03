using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public int tunnelHalfWidth = 2;
        [Range(0, 10)] public int wobble = 3;

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
        public int minGapBetweenAttachments = 8;
    }

    [Serializable]
    public class MapSettings
    {
        [Header("Seed")]
        public int seed = 0;
        public bool randomSeed = true;

        [Header("World Size")]
        public int width = 180;
        public int height = 120;
        public float cellSize = 0.75f;

        [Header("Spawn")]
        public Vector2Int spawnCenter = new Vector2Int(90, 60);
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

    private class PlannedArea
    {
        public int id;
        public AreaType type;
        public Vector2Int centerCell;
        public int radiusCells;
        public float radiusWorld;
        public HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
    }

    [Header("Profiles")]
    public bool useProfilesByRunNumber = false;
    public List<BranchMapProfile> runProfiles = new List<BranchMapProfile>();
    public BranchMapProfile selectedProfile;

    [Header("Manual Inspector Settings")]
    public MapSettings map = new MapSettings();

    [Header("Branches")]
    public List<BranchSettings> branches = new List<BranchSettings>();

    [Header("Tilemaps")]
    public Grid grid;
    public Tilemap floorTilemap;
    public Tilemap dirtTilemap;
    public Tilemap markerTilemap;

    [Header("Tiles")]
    public TileBase floorTile;
    public TileBase dirtTile1;
    public TileBase dirtTile2;
    public TileBase dirtTile3;
    public TileBase darkDirtTile;
    public TileBase darkerDirtTile;
    public TileBase revealDirtTile;

    [Header("Debug / Marker Tiles")]
    public TileBase bossMarkerTile;
    public TileBase campMarkerTile;
    public TileBase branchDebugTile;

    [Header("Debug")]
    public bool generateOnStart = true;
    public bool showBranchDebugTiles = true;
    public bool revealMainTunnelsAtStart = false;

    public MapData Data { get; private set; }

    private System.Random rng;

    private readonly HashSet<Vector2Int> spawnOpen = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> mainPathCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> plannedTunnelCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> hiddenRevealCells = new HashSet<Vector2Int>();

    private readonly Dictionary<Vector2Int, PlannedArea> revealAreaByCell = new Dictionary<Vector2Int, PlannedArea>();
    private readonly Dictionary<Vector2Int, int> revealGroupByTriggerCell = new Dictionary<Vector2Int, int>();
    private readonly List<PlannedArea> plannedAreas = new List<PlannedArea>();

    private int nextTunnelId = 1;
    private int nextCampId = 1;
    private bool defaultsInitialized = false;

    private void Reset()
    {
        SetDefaultFirstLevelInspectorSettings();
    }

    private void OnValidate()
    {
        if (map.width <= 0) map.width = 180;
        if (map.height <= 0) map.height = 120;
        if (map.cellSize <= 0f) map.cellSize = 0.75f;

        if (!defaultsInitialized && (branches == null || branches.Count == 0))
            SetDefaultFirstLevelInspectorSettings();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (branches == null || branches.Count == 0)
            SetDefaultFirstLevelInspectorSettings();
    }

    private void Start()
    {
        if (generateOnStart)
            Generate();
    }

    [ContextMenu("Set Default First Level Inspector Settings")]
    public void SetDefaultFirstLevelInspectorSettings()
    {
        defaultsInitialized = true;

        map.seed = 0;
        map.randomSeed = true;
        map.width = 180;
        map.height = 120;
        map.cellSize = 0.75f;
        map.spawnCenter = new Vector2Int(90, 60);
        map.spawnRadius = 5;
        map.darkDistance = 20;
        map.darkerDistance = 35;
        map.fillerTunnelCount = 12;
        map.fillerPocketCount = 8;
        map.fillerMinDistanceFromMainPath = 7;
        map.fillerMaxDistanceFromMainPath = 35;

        branches = CreateDefaultBranches();
    }

    [ContextMenu("Generate Map")]
    public void Generate()
    {
        ApplyProfileForThisRunIfNeeded();
        EnsureTilemaps();

        Data = new MapData(map.width, map.height, map.cellSize);

        if (grid != null)
        {
            grid.cellSize = new Vector3(map.cellSize, map.cellSize, 1f);
            grid.transform.position = new Vector3(Data.origin.x, Data.origin.y, 0f);
        }

        ClearTilemaps();
        ClearPlanData();

        rng = new System.Random(map.randomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : map.seed);

        if (branches == null || branches.Count == 0)
            branches = CreateDefaultBranches();

        Data.FillBlocked();

        BuildSpawn();
        BuildBranches();
        BuildFiller();
        PaintTilemaps();
    }

    public void GenerateMap() => Generate();
    public void Regenerate() => Generate();

    private void ApplyProfileForThisRunIfNeeded()
    {
        if (!useProfilesByRunNumber)
            return;

        BranchMapProfile profile = GetProfileForCurrentRun();
        if (profile == null)
            return;

        selectedProfile = profile;
        LoadProfileIntoInspector(profile);
    }

    private BranchMapProfile GetProfileForCurrentRun()
    {
        if (runProfiles == null || runProfiles.Count == 0)
            return selectedProfile;

        int runNumber = 1;

        Type gameStateType = Type.GetType("GameState");
        if (gameStateType != null)
        {
            object instance = null;

            var instanceProp = gameStateType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp != null)
                instance = instanceProp.GetValue(null);

            var instanceField = gameStateType.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instance == null && instanceField != null)
                instance = instanceField.GetValue(null);

            if (instance != null)
            {
                string[] possibleNames =
                {
                    "currentRunNumber", "runNumber", "RunNumber", "CurrentRunNumber",
                    "currentRun", "run", "Run", "runsCompleted"
                };

                foreach (string name in possibleNames)
                {
                    var field = gameStateType.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(int))
                    {
                        runNumber = Mathf.Max(1, (int)field.GetValue(instance));
                        break;
                    }

                    var prop = gameStateType.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (prop != null && prop.PropertyType == typeof(int))
                    {
                        runNumber = Mathf.Max(1, (int)prop.GetValue(instance));
                        break;
                    }
                }
            }
        }

        int index = Mathf.Clamp(runNumber - 1, 0, runProfiles.Count - 1);
        return runProfiles[index];
    }

    [ContextMenu("Load Selected Profile Into Inspector")]
    public void LoadSelectedProfileIntoInspector()
    {
        if (selectedProfile != null)
            LoadProfileIntoInspector(selectedProfile);
    }

    public void LoadProfileIntoInspector(BranchMapProfile profile)
    {
        if (profile == null)
            return;

        map.seed = profile.seed;
        map.randomSeed = profile.randomSeed;
        map.width = profile.width;
        map.height = profile.height;
        map.cellSize = profile.cellSize;
        map.spawnCenter = profile.spawnCenter;
        map.spawnRadius = profile.spawnRadius;
        map.darkDistance = profile.darkDistance;
        map.darkerDistance = profile.darkerDistance;
        map.fillerTunnelCount = profile.fillerTunnelCount;
        map.fillerPocketCount = profile.fillerPocketCount;
        map.fillerMinDistanceFromMainPath = profile.fillerMinDistanceFromMainPath;
        map.fillerMaxDistanceFromMainPath = profile.fillerMaxDistanceFromMainPath;

        branches = CopyBranches(profile.branches);
    }

    [ContextMenu("Save Inspector Settings To Selected Profile")]
    public void SaveInspectorSettingsToSelectedProfile()
    {
        if (selectedProfile == null)
        {
            Debug.LogWarning("No Selected Profile assigned on MapGenerator.");
            return;
        }

        selectedProfile.seed = map.seed;
        selectedProfile.randomSeed = map.randomSeed;
        selectedProfile.width = map.width;
        selectedProfile.height = map.height;
        selectedProfile.cellSize = map.cellSize;
        selectedProfile.spawnCenter = map.spawnCenter;
        selectedProfile.spawnRadius = map.spawnRadius;
        selectedProfile.darkDistance = map.darkDistance;
        selectedProfile.darkerDistance = map.darkerDistance;
        selectedProfile.fillerTunnelCount = map.fillerTunnelCount;
        selectedProfile.fillerPocketCount = map.fillerPocketCount;
        selectedProfile.fillerMinDistanceFromMainPath = map.fillerMinDistanceFromMainPath;
        selectedProfile.fillerMaxDistanceFromMainPath = map.fillerMaxDistanceFromMainPath;
        selectedProfile.branches = CopyBranches(branches);

#if UNITY_EDITOR
        EditorUtility.SetDirty(selectedProfile);
        AssetDatabase.SaveAssets();
#endif
    }

    private List<BranchSettings> CreateDefaultBranches()
    {
        return new List<BranchSettings>
        {
            new BranchSettings { branchName = "North", direction = Vector2Int.up, length = 35, tunnelHalfWidth = 2, wobble = 2, forkCount = 1, forkLengthMin = 12, forkLengthMax = 20, smallRooms = 2, camps = 0, bosses = 0, minAttachmentDistanceFromSpawn = 8, attachmentConnectorLength = 3, minGapBetweenAttachments = 8 },
            new BranchSettings { branchName = "East", direction = Vector2Int.right, length = 45, tunnelHalfWidth = 2, wobble = 4, forkCount = 2, forkLengthMin = 10, forkLengthMax = 18, smallRooms = 3, camps = 1, bosses = 0, minAttachmentDistanceFromSpawn = 10, attachmentConnectorLength = 3, minGapBetweenAttachments = 8 },
            new BranchSettings { branchName = "West", direction = Vector2Int.left, length = 28, tunnelHalfWidth = 2, wobble = 2, forkCount = 1, forkLengthMin = 8, forkLengthMax = 15, smallRooms = 2, camps = 0, bosses = 0, minAttachmentDistanceFromSpawn = 8, attachmentConnectorLength = 3, minGapBetweenAttachments = 8 },
            new BranchSettings { branchName = "Boss", direction = Vector2Int.down, length = 55, tunnelHalfWidth = 2, wobble = 3, forkCount = 2, forkLengthMin = 12, forkLengthMax = 20, smallRooms = 2, camps = 0, bosses = 1, minAttachmentDistanceFromSpawn = 15, attachmentConnectorLength = 4, minGapBetweenAttachments = 10 }
        };
    }

    private List<BranchSettings> CopyBranches(List<BranchSettings> source)
    {
        List<BranchSettings> copy = new List<BranchSettings>();
        if (source == null) return copy;

        foreach (BranchSettings b in source)
        {
            if (b == null) continue;

            copy.Add(new BranchSettings
            {
                branchName = b.branchName,
                direction = b.direction,
                length = b.length,
                tunnelHalfWidth = b.tunnelHalfWidth,
                wobble = b.wobble,
                forkCount = b.forkCount,
                forkLengthMin = b.forkLengthMin,
                forkLengthMax = b.forkLengthMax,
                smallRooms = b.smallRooms,
                camps = b.camps,
                bosses = b.bosses,
                minAttachmentDistanceFromSpawn = b.minAttachmentDistanceFromSpawn,
                attachmentConnectorLength = b.attachmentConnectorLength,
                minGapBetweenAttachments = b.minGapBetweenAttachments
            });
        }

        return copy;
    }

    private void EnsureTilemaps()
    {
        if (grid == null)
            grid = GetComponentInChildren<Grid>();

        Tilemap[] maps = GetComponentsInChildren<Tilemap>();

        foreach (Tilemap tm in maps)
        {
            string n = tm.name.ToLowerInvariant();

            if (floorTilemap == null && (n.Contains("floor") || n.Contains("ground")))
                floorTilemap = tm;

            if (dirtTilemap == null && n.Contains("dirt"))
                dirtTilemap = tm;

            if (markerTilemap == null && (n.Contains("marker") || n.Contains("debug")))
                markerTilemap = tm;
        }

        if (floorTilemap == null && maps.Length > 0)
            floorTilemap = maps[0];

        if (dirtTilemap == null && maps.Length > 1)
            dirtTilemap = maps[1];
    }

    private void ClearTilemaps()
    {
        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (dirtTilemap != null) dirtTilemap.ClearAllTiles();
        if (markerTilemap != null) markerTilemap.ClearAllTiles();
    }

    private void ClearPlanData()
    {
        spawnOpen.Clear();
        mainPathCells.Clear();
        plannedTunnelCells.Clear();
        hiddenRevealCells.Clear();
        revealAreaByCell.Clear();
        revealGroupByTriggerCell.Clear();
        plannedAreas.Clear();
        nextTunnelId = 1;
        nextCampId = 1;

        if (Data != null)
        {
            Data.camps.Clear();
            Data.tunnels.Clear();
        }
    }

    private void BuildSpawn()
    {
        foreach (Vector2Int cell in CellsInCircle(map.spawnCenter, map.spawnRadius))
        {
            spawnOpen.Add(cell);
            mainPathCells.Add(cell);
            Data.SetBlocked(cell, false);
        }

        Data.ClearCircle(Data.CellToWorld(map.spawnCenter), map.spawnRadius * map.cellSize);
    }

    private void BuildBranches()
    {
        foreach (BranchSettings branch in branches)
        {
            Vector2Int dir = NormalizeCardinal(branch.direction);
            if (dir == Vector2Int.zero) continue;

            List<Vector2Int> path = BuildWobblyPath(map.spawnCenter, dir, branch.length, branch.wobble);
            AddTunnel(path, branch.tunnelHalfWidth, AreaType.MainTunnel, revealMainTunnelsAtStart);

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
            int minLen = Mathf.Min(branch.forkLengthMin, branch.forkLengthMax);
            int maxLen = Mathf.Max(branch.forkLengthMin, branch.forkLengthMax);
            int forkLength = rng.Next(minLen, maxLen + 1);

            List<Vector2Int> forkPath = BuildWobblyPath(start, forkDir, forkLength, branch.wobble);
            AddTunnel(forkPath, branch.tunnelHalfWidth, AreaType.ForkTunnel, false);
        }
    }

    private void BuildAttachments(BranchSettings branch, List<Vector2Int> path)
    {
        List<AreaType> attachments = new List<AreaType>();

        for (int i = 0; i < branch.smallRooms; i++) attachments.Add(AreaType.SmallRoom);
        for (int i = 0; i < branch.camps; i++) attachments.Add(AreaType.Camp);
        for (int i = 0; i < branch.bosses; i++) attachments.Add(AreaType.Boss);

        Shuffle(attachments);

        List<int> usedIndices = new List<int>();

        foreach (AreaType type in attachments)
        {
            if (path.Count < 10) continue;

            int index = PickAttachmentIndex(path, branch, usedIndices);
            if (index < 0) continue;

            usedIndices.Add(index);

            Vector2Int attachPoint = path[index];
            Vector2Int sideDir = PickSideDirection(path, index);
            int connectorLength = Mathf.Max(2, branch.attachmentConnectorLength);

            Vector2Int connectorEnd = attachPoint + sideDir * connectorLength;
            List<Vector2Int> connector = StraightPath(attachPoint, connectorEnd);

            int radiusCells = GetRadiusCellsForArea(type);
            Vector2Int roomCenter = connectorEnd + sideDir * (radiusCells + 1);

            if (!Data.InBounds(roomCenter)) continue;
            if (IsTooCloseToExistingArea(roomCenter, radiusCells + 3)) continue;

            AddTunnel(connector, 1, AreaType.MainTunnel, false);
            AddRevealArea(type, roomCenter, radiusCells);
        }
    }

    private int PickAttachmentIndex(List<Vector2Int> path, BranchSettings branch, List<int> usedIndices)
    {
        int minIndex = Mathf.Clamp(branch.minAttachmentDistanceFromSpawn, 1, path.Count - 2);

        for (int tries = 0; tries < 80; tries++)
        {
            int index = rng.Next(minIndex, path.Count - 1);
            bool tooClose = false;

            foreach (int used in usedIndices)
            {
                if (Mathf.Abs(index - used) < branch.minGapBetweenAttachments)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                return index;
        }

        return rng.Next(minIndex, path.Count - 1);
    }

    private void BuildFiller()
    {
        for (int i = 0; i < map.fillerTunnelCount; i++)
        {
            Vector2Int start = FindFillerPosition();
            if (start == map.spawnCenter) continue;

            Vector2Int dir = RandomCardinal();
            int length = rng.Next(5, 16);
            List<Vector2Int> path = BuildWobblyPath(start, dir, length, 2);
            AddTunnel(path, 1, AreaType.FillerTunnel, false);
        }

        for (int i = 0; i < map.fillerPocketCount; i++)
        {
            Vector2Int pos = FindFillerPosition();
            if (pos == map.spawnCenter) continue;

            int radius = rng.Next(2, 5);
            AddRevealArea(AreaType.FillerPocket, pos, radius);
        }
    }

    private void AddTunnel(List<Vector2Int> path, int halfWidthCells, AreaType type, bool revealedNow)
    {
        if (path == null || path.Count == 0 || Data == null) return;

        TunnelData tunnel = new TunnelData
        {
            id = nextTunnelId++,
            radius = Mathf.Max(1, halfWidthCells) * map.cellSize,
            revealed = revealedNow,
            enemySpawnPoint = Data.CellToWorld(path[path.Count - 1])
        };

        foreach (Vector2Int p in path)
        {
            if (!Data.InBounds(p)) continue;

            mainPathCells.Add(p);
            tunnel.points.Add(Data.CellToWorld(p));

            foreach (Vector2Int cell in CellsInCircle(p, Mathf.Max(1, halfWidthCells)))
            {
                plannedTunnelCells.Add(cell);

                if (revealedNow)
                    Data.SetBlocked(cell, false);
            }
        }

        Data.tunnels.Add(tunnel);
    }

    private void AddRevealArea(AreaType type, Vector2Int centerCell, int radiusCells)
    {
        PlannedArea area = new PlannedArea
        {
            id = (type == AreaType.Camp || type == AreaType.Boss) ? nextCampId++ : plannedAreas.Count + 1000,
            type = type,
            centerCell = centerCell,
            radiusCells = radiusCells,
            radiusWorld = radiusCells * map.cellSize
        };

        foreach (Vector2Int cell in CellsInCircle(centerCell, radiusCells))
        {
            hiddenRevealCells.Add(cell);
            revealAreaByCell[cell] = area;
            area.cells.Add(cell);
        }

        plannedAreas.Add(area);

        if (type == AreaType.Camp || type == AreaType.Boss)
        {
            CampData camp = new CampData
            {
                id = area.id,
                center = Data.CellToWorld(centerCell),
                radius = area.radiusWorld,
                revealed = false,
                isBossCamp = type == AreaType.Boss,
                hasExitPortal = type == AreaType.Boss,
                enemyCount = type == AreaType.Boss ? 2 : 3,
                bossEnemyCount = type == AreaType.Boss ? 1 : 0,
                sporeCount = type == AreaType.Boss ? 3 : 2,
                mushroomCount = type == AreaType.Boss ? 5 : 3,
                shinyCount = type == AreaType.Boss ? 2 : 0
            };

            Data.camps.Add(camp);
        }

        PaintMarker(type, centerCell);
    }

    private void PaintTilemaps()
    {
        if (floorTilemap == null || dirtTilemap == null)
        {
            Debug.LogError("MapGenerator needs a Floor Tilemap and Dirt Tilemap assigned.");
            return;
        }

        BuildRevealTriggers();

        for (int x = 0; x < Data.width; x++)
        {
            for (int y = 0; y < Data.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                SetCellTilesFromData(cell);
            }
        }

        if (showBranchDebugTiles)
            PaintBranchDebug();
    }

    private void BuildRevealTriggers()
    {
        revealGroupByTriggerCell.Clear();

        foreach (PlannedArea area in plannedAreas)
        {
            foreach (Vector2Int cell in area.cells)
            {
                foreach (Vector2Int n in GetNeighbors4(cell))
                {
                    if (!Data.InBounds(n)) continue;
                    if (area.cells.Contains(n)) continue;
                    if (!Data.IsBlocked(n)) continue;

                    if (!revealGroupByTriggerCell.ContainsKey(n))
                        revealGroupByTriggerCell[n] = area.id;
                }
            }
        }
    }

    public void DigCircle(Vector3 worldPos, float radius)
    {
        if (Data == null) return;

        Vector2Int center = Data.WorldToCell(worldPos);
        int r = Mathf.CeilToInt(radius / map.cellSize) + 1;

        for (int x = center.x - r; x <= center.x + r; x++)
        {
            for (int y = center.y - r; y <= center.y + r; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!Data.InBounds(cell)) continue;

                Vector2 world = Data.CellToWorld(cell);
                if (Vector2.Distance(world, worldPos) <= radius)
                    DigCell(cell);
            }
        }
    }

    public void DigAtWorld(Vector3 worldPos)
    {
        if (Data == null) return;
        DigCell(Data.WorldToCell(worldPos));
    }

    public void DigAtCell(Vector2Int cell)
    {
        DigCell(cell);
    }

    public void DigCell(Vector2Int cell)
    {
        if (Data == null || !Data.InBounds(cell)) return;
        if (!Data.IsBlocked(cell)) return;

        if (revealGroupByTriggerCell.TryGetValue(cell, out int revealId))
        {
            RevealAreaById(revealId);
            return;
        }

        Data.SetBlocked(cell, false);
        SetCellTilesFromData(cell);
    }

    public void RevealCamp(int campId)
    {
        RevealAreaById(campId);
    }

    public void RevealTunnel(int tunnelId)
    {
        if (Data == null) return;

        TunnelData tunnel = Data.tunnels.Find(t => t.id == tunnelId);
        if (tunnel == null) return;

        tunnel.revealed = true;

        foreach (Vector2 point in tunnel.points)
            Data.ClearCircle(point, Mathf.Max(tunnel.radius, map.cellSize * 1.5f));

        RefreshAllTiles();
    }

    public void RevealCamp()
    {
        if (Data == null || Data.camps.Count == 0) return;
        RevealCamp(Data.camps[0].id);
    }

    public void RevealTunnel()
    {
        if (Data == null || Data.tunnels.Count == 0) return;
        RevealTunnel(Data.tunnels[0].id);
    }

    private void RevealAreaById(int areaId)
    {
        if (Data == null) return;

        PlannedArea area = plannedAreas.Find(a => a.id == areaId);
        if (area == null) return;

        foreach (Vector2Int cell in area.cells)
        {
            Data.SetBlocked(cell, false);
            SetCellTilesFromData(cell);
        }

        foreach (CampData camp in Data.camps)
        {
            if (camp.id == areaId)
            {
                camp.revealed = true;
                break;
            }
        }

        List<Vector2Int> triggersToClear = new List<Vector2Int>();

        foreach (KeyValuePair<Vector2Int, int> pair in revealGroupByTriggerCell)
        {
            if (pair.Value == areaId)
                triggersToClear.Add(pair.Key);
        }

        foreach (Vector2Int trigger in triggersToClear)
        {
            Data.SetBlocked(trigger, false);
            revealGroupByTriggerCell.Remove(trigger);
            SetCellTilesFromData(trigger);
        }
    }

    public bool IsWorldPositionClearForBody(Vector2 worldPos, float radius)
    {
        if (Data == null) return true;

        Vector2Int center = Data.WorldToCell(worldPos);
        int r = Mathf.CeilToInt(radius / map.cellSize) + 1;

        for (int x = center.x - r; x <= center.x + r; x++)
        {
            for (int y = center.y - r; y <= center.y + r; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!Data.InBounds(cell)) return false;

                Vector2 cellWorld = Data.CellToWorld(cell);
                if (Vector2.Distance(cellWorld, worldPos) <= radius && Data.IsBlocked(cell))
                    return false;
            }
        }

        return true;
    }

    public bool IsWorldPositionClearForBody(Vector3 worldPos, float radius)
    {
        return IsWorldPositionClearForBody((Vector2)worldPos, radius);
    }

    public bool IsWorldPositionClear(Vector2 worldPos)
    {
        return Data == null || !Data.IsBlocked(Data.WorldToCell(worldPos));
    }

    public bool IsWorldPositionClear(Vector3 worldPos)
    {
        return IsWorldPositionClear((Vector2)worldPos);
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return Data != null && Data.InBounds(cell) && !Data.IsBlocked(cell);
    }

    public bool IsOpen(Vector2Int cell)
    {
        return IsWalkable(cell);
    }

    public bool IsDirt(Vector2Int cell)
    {
        return Data != null && Data.InBounds(cell) && Data.IsBlocked(cell);
    }

    public bool IsDiggable(Vector2Int cell)
    {
        return IsDirt(cell);
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        if (Data == null) return Vector2Int.RoundToInt(worldPos);
        return Data.WorldToCell(worldPos);
    }

    public Vector2Int WorldToCell2D(Vector3 worldPos)
    {
        return WorldToCell(worldPos);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        if (Data == null) return new Vector3(cell.x, cell.y, 0f);
        Vector2 w = Data.CellToWorld(cell);
        return new Vector3(w.x, w.y, 0f);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        return CellToWorld(cell);
    }

    private void SetCellTilesFromData(Vector2Int cell)
    {
        if (floorTilemap == null || dirtTilemap == null || Data == null || !Data.InBounds(cell))
            return;

        Vector3Int pos = ToTilePos(cell);

        if (Data.IsBlocked(cell))
        {
            floorTilemap.SetTile(pos, null);

            if (revealGroupByTriggerCell.ContainsKey(cell) && revealDirtTile != null)
                dirtTilemap.SetTile(pos, revealDirtTile);
            else
                dirtTilemap.SetTile(pos, GetDirtTileByDistance(cell));
        }
        else
        {
            floorTilemap.SetTile(pos, floorTile);
            dirtTilemap.SetTile(pos, null);
        }
    }

    private void RefreshAllTiles()
    {
        if (Data == null) return;

        for (int x = 0; x < Data.width; x++)
        {
            for (int y = 0; y < Data.height; y++)
                SetCellTilesFromData(new Vector2Int(x, y));
        }
    }

    private TileBase GetDirtTileByDistance(Vector2Int cell)
    {
        int dist = DistanceToNearestMainPath(cell);

        if (dist >= map.darkerDistance && darkerDirtTile != null) return darkerDirtTile;
        if (dist >= map.darkDistance && darkDirtTile != null) return darkDirtTile;

        int dirtType = Data != null ? Data.GetDirtType(cell) : 0;

        if (dirtType == 1 && dirtTile2 != null) return dirtTile2;
        if (dirtType == 2 && dirtTile3 != null) return dirtTile3;

        return dirtTile1;
    }

    private IEnumerable<Vector2Int> CellsInCircle(Vector2Int center, int radiusCells)
    {
        int r = Mathf.Max(1, radiusCells);

        for (int x = center.x - r; x <= center.x + r; x++)
        {
            for (int y = center.y - r; y <= center.y + r; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (Data == null || !Data.InBounds(cell)) continue;

                if (Vector2Int.Distance(center, cell) <= r)
                    yield return cell;
            }
        }
    }

    private List<Vector2Int> BuildWobblyPath(Vector2Int start, Vector2Int direction, int length, int wobble)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int pos = start;
        Vector2Int side = new Vector2Int(-direction.y, direction.x);

        int drift = 0;

        for (int i = 0; i < length; i++)
        {
            pos += direction;

            if (wobble > 0 && rng.NextDouble() < 0.35)
                drift += rng.Next(-1, 2);

            drift = Mathf.Clamp(drift, -wobble, wobble);
            Vector2Int p = pos + side * drift;

            if (Data == null || !Data.InBounds(p)) break;
            path.Add(p);
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

            if (Data != null && Data.InBounds(p))
                path.Add(p);
        }

        return path;
    }

    private Vector2Int FindFillerPosition()
    {
        for (int tries = 0; tries < 500; tries++)
        {
            Vector2Int pos = new Vector2Int(
                rng.Next(4, Mathf.Max(5, map.width - 4)),
                rng.Next(4, Mathf.Max(5, map.height - 4))
            );

            int dist = DistanceToNearestMainPath(pos);

            if (dist >= map.fillerMinDistanceFromMainPath &&
                dist <= map.fillerMaxDistanceFromMainPath &&
                !plannedTunnelCells.Contains(pos) &&
                !hiddenRevealCells.Contains(pos))
            {
                return pos;
            }
        }

        return map.spawnCenter;
    }

    private int GetRadiusCellsForArea(AreaType type)
    {
        switch (type)
        {
            case AreaType.Boss: return rng.Next(7, 11);
            case AreaType.Camp: return rng.Next(6, 9);
            case AreaType.SmallRoom: return rng.Next(4, 7);
            default: return rng.Next(2, 5);
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
            int d = Mathf.Abs(center.x - area.centerCell.x) + Mathf.Abs(center.y - area.centerCell.y);

            if (d < distance + area.radiusCells)
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

#if UNITY_EDITOR
[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MapGenerator generator = (MapGenerator)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Map Profile Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Set Default First Level Inspector Settings"))
        {
            Undo.RecordObject(generator, "Set Default Branch Map Settings");
            generator.SetDefaultFirstLevelInspectorSettings();
            EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("Load Selected Profile Into Inspector"))
        {
            Undo.RecordObject(generator, "Load Branch Map Profile");
            generator.LoadSelectedProfileIntoInspector();
            EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("Save Inspector Settings To Selected Profile"))
        {
            generator.SaveInspectorSettingsToSelectedProfile();
        }

        if (GUILayout.Button("Generate Map Now"))
        {
            generator.Generate();
            EditorUtility.SetDirty(generator);
        }
    }
}
#endif
