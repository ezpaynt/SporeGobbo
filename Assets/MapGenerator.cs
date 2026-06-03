using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// ROUTE_V4_GUIDE_DIRT_BUILD - route dense intro + no-route warning dirt zones

public enum RunGenerationProfile
{
    Intro,
    EarlyCaves
}

public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance;

    public MapData Data { get; private set; }

    private Transform playerTransform;
    private Vector2Int lastPlayerChunk;
    private bool hasLastPlayerChunk = false;

    [Header("Grid / Dirt Tiles")]
    public Grid grid;
    public TileBase dirtTile1;
    public TileBase dirtTile2;
    public TileBase dirtTile3;

    [Header("No-Route Guide Dirt / Soft Border")]
    public bool useNoRouteGuideDirt = true;
    public TileBase noRouteDirtNear;
    public TileBase noRouteDirtMid;
    public TileBase noRouteDirtFar;
    public float noRouteNearDistance = 10f;
    public float noRouteMidDistance = 17f;
    public float noRouteFarDistance = 25f;
    public int noRouteDistanceSampleStride = 2;

    [Header("World Size")]
    public int worldWidthCells = 180;
    public int worldHeightCells = 120;
    public float cellSize = 0.75f;

    [Header("Chunks")]
    public int chunkSize = 16;
    public int activeChunkRadius = 3;

    [Header("Run Profile Testing")]
    public bool forceIntroMap = true;
    public RunGenerationProfile fallbackProfile = RunGenerationProfile.EarlyCaves;

    [Header("Start Area")]
    public Vector2 playerStartPosition = Vector2.zero;
    public float startClearRadius = 4f;

    [Header("Intro Map - ROUTE_V4_GUIDE_DIRT_BUILD")]
    public int introWorldWidthCells = 260;
    public int introWorldHeightCells = 180;
    public float introStartPitRadius = 15f;
    public int introStartMushrooms = 6;
    public int introCampCount = 3;
    public float introCampRadius = 10f;
    public float introBossRadius = 12f;
    public float introRouteStepMin = 4.25f;
    public float introRouteStepMax = 6.25f;
    public float introRouteWobble = 3.25f;
    public float introCampZoneRadius = 23f;
    public int introNearSpawnDiscoveries = 18;
    public int introSparseOffRoutePockets = 6;
    public float introRouteSegmentGapChance = 0.06f;

    [Header("Player / Buddy")]
    public GameObject gobboPrefab;
    public GameObject buddyPrefab;
    public bool spawnPlayer = true;
    public bool spawnTestBuddy = false;
    public float buddySpawnDistance = 1.2f;

    [Header("Reveal Cover")]
    public GameObject revealCoverPrefab;

    [Header("Camp Generation")]
    public GameObject enemyPrefab;
    public GameObject sporePrefab;
    public GameObject mushroomPrefab;
    public GameObject shinyPrefab;
    public int campCount = 3;
    public float campRadius = 4f;
    public float minCampDistanceFromStart = 10f;
    public float minCampDistanceFromOtherCamps = 10f;

    [Header("Boss Camp")]
    public bool generateBossCamp = true;
    public float bossCampRadius = 8f;
    public GameObject bossEnemyPrefab;
    public GameObject exitPortalPrefab;
    public int bossCampNormalEnemyCount = 3;
    public int bossCampMushroomMin = 5;
    public int bossCampMushroomMax = 8;
    public int bossCampShinyCount = 2;
    public float bossEnemyScaleMultiplier = 1.45f;
    public int bossEnemyHealthMultiplier = 3;
    public int bossEnemyDamageBonus = 2;

    [Header("Hidden Tunnels")]
    public GameObject tunnelEnemyPrefab;
    public int tunnelCount = 8;
    public float tunnelMinLength = 8f;
    public float tunnelMaxLength = 22f;
    public float tunnelRadius = 1.4f;
    public float tunnelStepSize = 0.75f;
    public float tunnelWobble = 0.7f;
    public float tunnelAvoidCampPadding = 2f;

    [Header("Loose Mushrooms")]
    public int looseMushroomCount = 18;
    public float mushroomAvoidStartRadius = 4f;

    [Header("Rare Shinies")]
    public bool spawnRareShinies = true;
    public float tunnelShinyChance = 0.35f;

    private Dictionary<Vector2Int, MapChunk> activeChunks = new Dictionary<Vector2Int, MapChunk>();
    private GameObject spawnedPlayer;
    private int nextCampId = 0;
    private int nextTunnelId = 0;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GenerateMap();
    }

    void Update()
    {
        UpdateActiveChunksAroundPlayer();
    }

    public void GenerateMap()
    {
        if (grid == null)
        {
            Debug.LogError("MapGenerator needs Grid assigned.");
            return;
        }

        ClearOldChunksAndRuntimeObjects();

        RunGenerationProfile profile = forceIntroMap ? RunGenerationProfile.Intro : fallbackProfile;

        // Intro uses its own dimensions so old inspector world size cannot secretly keep the map tiny.
        int activeWorldWidth = profile == RunGenerationProfile.Intro ? introWorldWidthCells : worldWidthCells;
        int activeWorldHeight = profile == RunGenerationProfile.Intro ? introWorldHeightCells : worldHeightCells;
        worldWidthCells = activeWorldWidth;
        worldHeightCells = activeWorldHeight;

        Data = new MapData(activeWorldWidth, activeWorldHeight, cellSize);
        Data.FillBlocked();

        nextCampId = 0;
        nextTunnelId = 0;

        if (profile == RunGenerationProfile.Intro)
            GenerateIntroMap();
        else
            GenerateEarlyCavesMap();

        SpawnPlayer();
        SpawnTestBuddy();
        UpdateActiveChunksAroundPlayer(true);

        Debug.Log("Generated map profile: " + profile);
    }

    void GenerateEarlyCavesMap()
    {
        Data.ClearCircle(playerStartPosition, startClearRadius);
        GenerateCamps();
        GenerateHiddenTunnels();
        SpawnLooseMushrooms();
    }

    void GenerateIntroMap()
    {
        Debug.Log("ROUTE_V4_GUIDE_DIRT_BUILD intro generator is running. If you do not see this, Unity is using the wrong file.");

        // 1. Big playable tutorial hub.
        GenerateStartHub();

        // 2. Destination zones. These are intentionally arranged, not randomly scattered.
        CampData campA = CreateIntroCamp(PolarFromStart(32f, 38f), introCampRadius, false);
        CampData campB = CreateIntroCamp(PolarFromStart(34f, -58f), introCampRadius, false);
        CampData campC = CreateIntroCamp(PolarFromStart(48f, 2f), introCampRadius + 0.75f, false);

        // Boss remains top-right for testing, but now participates as a route destination.
        CampData boss = null;
        if (generateBossCamp)
            boss = CreateIntroCamp(GetFarCornerPosition(), introBossRadius, true);

        GenerateCampZone(campA);
        GenerateCampZone(campB);
        GenerateCampZone(campC);
        if (boss != null) GenerateBossZone(boss);

        // Extra dense central discoveries stop the intro from feeling like a huge empty dirt rectangle.
        GenerateCentralDiscoveryRing(campA.center, campB.center, campC.center);

        // 3. Actual route network: start -> camps, camps -> camp, camp -> boss.
        GenerateBrokenRoute(playerStartPosition, campA.center, 1.75f, true);
        GenerateBrokenRoute(playerStartPosition, campB.center, 1.65f, true);
        GenerateBrokenRoute(playerStartPosition, campC.center, 1.45f, true);
        GenerateBrokenRoute(campA.center, campC.center, 1.15f, false);
        GenerateBrokenRoute(campB.center, campC.center, 1.15f, false);
        GenerateBrokenRoute(campA.center, campB.center, 0.85f, false);
        if (boss != null)
            GenerateBrokenRoute(campC.center, boss.center, 1.25f, false);

        // 4. Tutorial/feel-good filler near start, then sparse optional off-route stuff.
        GenerateSmallNearSpawnDiscoveries();
        GenerateSparseOffRoutePockets();

        // Paint blocked dirt based on distance from real generated content.
        // This gives the player a soft visual warning when they are digging into a dead direction.
        ApplyNoRouteGuideDirt();
    }

    void GenerateStartHub()
    {
        DigOrganicRoom(playerStartPosition, introStartPitRadius, 8);

        for (int i = 0; i < introStartMushrooms; i++)
            SpawnAround(playerStartPosition, introStartPitRadius * 0.45f, mushroomPrefab);

        // This is the first buddy source.
        SpawnAround(playerStartPosition + Vector2.right * 1.5f, 0.35f, sporePrefab);

        // A few almost-exits around the hub so it feels like a dug place, not a lonely circle.
        Vector2[] dirs = { Vector2.right, Vector2.up, Vector2.down, new Vector2(1f, 0.45f).normalized };
        foreach (Vector2 dir in dirs)
        {
            Vector2 start = playerStartPosition + dir * (introStartPitRadius + 2.5f);
            CreateTunnelSegment(start, dir, Random.Range(8f, 12f), Random.Range(1.8f, 2.8f), false, false, 0.25f, false, true, false);
        }

        // Guaranteed first combat tunnel: close, easy to find, one enemy.
        Vector2 firstTunnelStart = playerStartPosition + Vector2.right * (introStartPitRadius + 3f);
        CreateTunnelSegment(firstTunnelStart, Vector2.right, 13f, 2.35f, true, false, 0.18f, false, true, false);
    }

    CampData CreateIntroCamp(Vector2 center, float radius, bool isBoss)
    {
        CampData camp = new CampData
        {
            id = nextCampId++,
            center = ClampToWorld(center),
            radius = radius,
            revealed = false,
            isBossCamp = isBoss,
            hasExitPortal = isBoss,
            enemyCount = isBoss ? 3 : 3,
            bossEnemyCount = isBoss ? 1 : 0,
            sporeCount = 1,
            mushroomCount = isBoss ? Random.Range(bossCampMushroomMin, bossCampMushroomMax + 1) : Random.Range(3, 6),
            shinyCount = isBoss ? bossCampShinyCount : (Random.value < 0.35f ? 1 : 0)
        };

        Data.camps.Add(camp);
        SpawnCampRevealPatch(camp);
        return camp;
    }

    void GenerateCampZone(CampData camp)
    {
        GenerateCampNeighborhood(camp, 14, false);
    }

    void GenerateBossZone(CampData boss)
    {
        GenerateCampNeighborhood(boss, 18, true);
    }

    void GenerateCampNeighborhood(CampData camp, int featureCount, bool bossZone)
    {
        for (int i = 0; i < featureCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(camp.radius + 4f, introCampZoneRadius);
            Vector2 pos = ClampToWorld(camp.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist);

            Vector2 dirToCamp = (camp.center - pos).normalized;
            if (dirToCamp.sqrMagnitude < 0.01f) dirToCamp = Random.insideUnitCircle.normalized;

            float roll = Random.value;
            if (roll < 0.45f)
            {
                // Approach bite: points toward the camp but does not reveal the camp.
                CreateTunnelSegment(pos, dirToCamp, Random.Range(7f, 13f), Random.Range(1.7f, 2.6f), Random.value < 0.35f, false, 0.35f, false, true, false);
            }
            else if (roll < 0.8f)
            {
                // Cavelet / pocket near the camp zone.
                CreateTunnelSegment(pos, Random.insideUnitCircle.normalized, Random.Range(3.5f, 7f), Random.Range(2.6f, 4.25f), Random.value < 0.25f || bossZone, false, 0.2f, Random.value < 0.12f, true, false);
            }
            else
            {
                // Side fork around the destination.
                Vector2 side = Vector2.Perpendicular(dirToCamp) * (Random.value < 0.5f ? 1f : -1f);
                CreateTunnelSegment(pos, side, Random.Range(8f, 15f), Random.Range(1.35f, 2.15f), Random.value < 0.3f, false, 0.45f, false, false, false);
            }
        }
    }


    void GenerateCentralDiscoveryRing(Vector2 campA, Vector2 campB, Vector2 campC)
    {
        // This is the "do not let the player dig forever into nothing" layer.
        // It fills the middle of the intro map with useful short chunks between the start and camp triangle.
        Vector2[] anchors =
        {
            playerStartPosition,
            Vector2.Lerp(playerStartPosition, campA, 0.45f),
            Vector2.Lerp(playerStartPosition, campB, 0.45f),
            Vector2.Lerp(playerStartPosition, campC, 0.35f),
            Vector2.Lerp(campA, campC, 0.5f),
            Vector2.Lerp(campB, campC, 0.5f)
        };

        for (int a = 0; a < anchors.Length; a++)
        {
            int count = a == 0 ? 10 : 7;
            float radius = a == 0 ? introStartPitRadius + 34f : 18f;

            for (int i = 0; i < count; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(7f, radius);
                Vector2 pos = ClampToWorld(anchors[a] + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist);

                if (Vector2.Distance(pos, playerStartPosition) < introStartPitRadius + 3f) continue;
                if (IsTooCloseToCamp(pos, 3f)) continue;
                if (IsTooCloseToAnyTunnel(pos, 3.5f)) continue;

                Vector2 dir = (Random.value < 0.65f) ? (playerStartPosition - pos).normalized : Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

                bool fatPocket = Random.value < 0.42f;
                if (fatPocket)
                    CreateTunnelSegment(pos, dir, Random.Range(3.5f, 7.5f), Random.Range(2.5f, 4.3f), Random.value < 0.18f, false, 0.22f, false, Random.value < 0.7f, false);
                else
                    CreateTunnelSegment(pos, dir, Random.Range(6.5f, 12f), Random.Range(1.55f, 2.5f), Random.value < 0.22f, false, 0.38f, false, Random.value < 0.55f, false);
            }
        }
    }

    void GenerateBrokenRoute(Vector2 from, Vector2 to, float density, bool fromStart)
    {
        Vector2 routeDir = (to - from).normalized;
        if (routeDir.sqrMagnitude < 0.01f) routeDir = Vector2.right;

        float totalDist = Vector2.Distance(from, to);
        float traveled = fromStart ? introStartPitRadius + 5f : introCampRadius + 7f;
        Vector2 lastChunkPos = from + routeDir * traveled;

        while (traveled < totalDist - introCampRadius - 8f)
        {
            Vector2 basePoint = Vector2.Lerp(from, to, traveled / totalDist);
            Vector2 perp = Vector2.Perpendicular(routeDir);
            Vector2 wobble = perp * Random.Range(-introRouteWobble, introRouteWobble) + Random.insideUnitCircle * 1.5f;
            Vector2 pos = ClampToWorld(basePoint + wobble);

            Vector2 nextAim = (to - pos).normalized;
            if (nextAim.sqrMagnitude < 0.01f) nextAim = routeDir;
            nextAim = (nextAim + Random.insideUnitCircle * 0.22f).normalized;

            // Sometimes leave a dirt gap so revealing one tunnel chunk does not open the whole path.
            bool makeGap = Random.value < introRouteSegmentGapChance && !fromStart;
            if (!makeGap)
            {
                float distFromStart = Vector2.Distance(pos, playerStartPosition);
                float nearStartBoost = Mathf.InverseLerp(80f, 10f, distFromStart);
                float radius = Random.Range(1.65f, 2.45f) + nearStartBoost * 0.55f;
                float length = Random.Range(7.5f, 12.5f) + nearStartBoost * 3f;
                bool enemy = Random.value < (fromStart ? 0.25f : 0.4f);
                bool mushroom = Random.value < 0.35f;

                CreateTunnelSegment(pos, nextAim, length, radius, enemy, false, 0.28f, false, mushroom, false);

                // Little side branch sometimes, so the route feels cave-ish instead of dashed lines only.
                if (Random.value < 0.65f * density)
                {
                    Vector2 side = Vector2.Perpendicular(nextAim) * (Random.value < 0.5f ? 1f : -1f);
                    CreateTunnelSegment(pos + side * Random.Range(2f, 4f), side, Random.Range(4.5f, 9f), Random.Range(1.45f, 2.35f), Random.value < 0.2f, false, 0.35f, false, Random.value < 0.4f, false);
                }
            }

            lastChunkPos = pos;
            traveled += Random.Range(introRouteStepMin, introRouteStepMax) / Mathf.Max(0.2f, density);
        }
    }

    void GenerateSmallNearSpawnDiscoveries()
    {
        for (int i = 0; i < introNearSpawnDiscoveries; i++)
        {
            float angle = (Mathf.PI * 2f / introNearSpawnDiscoveries) * i + Random.Range(-0.35f, 0.35f);
            float dist = Random.Range(introStartPitRadius + 3f, introStartPitRadius + 30f);
            Vector2 pos = ClampToWorld(playerStartPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist);
            Vector2 tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));

            if (Random.value < 0.55f)
                CreateTunnelSegment(pos, tangent * (Random.value < 0.5f ? 1f : -1f), Random.Range(6f, 11f), Random.Range(1.7f, 2.8f), Random.value < 0.2f, false, 0.35f, false, true, false);
            else
                CreateTunnelSegment(pos, Random.insideUnitCircle.normalized, Random.Range(3f, 6f), Random.Range(2.5f, 4f), false, false, 0.15f, false, true, false);
        }
    }

    void GenerateSparseOffRoutePockets()
    {
        int made = 0;
        int attempts = 0;

        while (made < introSparseOffRoutePockets && attempts < introSparseOffRoutePockets * 80)
        {
            attempts++;
            Vector2 pos = GetRandomWorldPosition(8f);

            if (Vector2.Distance(pos, playerStartPosition) < introStartPitRadius + 20f) continue;
            if (IsTooCloseToCamp(pos, 6f)) continue;
            if (IsTooCloseToAnyTunnel(pos, 7f)) continue;

            float roll = Random.value;
            if (roll < 0.6f)
                CreateTunnelSegment(pos, Random.insideUnitCircle.normalized, Random.Range(2f, 5f), Random.Range(2f, 3.25f), Random.value < 0.18f, false, 0.2f, false, Random.value < 0.55f, false);
            else
                CreateTunnelSegment(pos, Random.insideUnitCircle.normalized, Random.Range(5f, 10f), Random.Range(1.05f, 1.55f), Random.value < 0.25f, false, 0.5f, false, Random.value < 0.35f, false);

            made++;
        }
    }

    bool IsTooCloseToAnyTunnel(Vector2 pos, float distance)
    {
        foreach (TunnelData tunnel in Data.tunnels)
        {
            foreach (Vector2 point in tunnel.points)
            {
                if (Vector2.Distance(pos, point) < distance)
                    return true;
            }
        }
        return false;
    }

    Vector2 PolarFromStart(float distance, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return ClampToWorld(playerStartPosition + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * distance);
    }

    void CreateTunnelSegment(
        Vector2 start,
        Vector2 dir,
        float length,
        float radius,
        bool hasEnemy,
        bool enemyAtEnd,
        float wobble,
        bool hasShiny = false,
        bool hasMushroom = false,
        bool avoidCamps = true)
    {
        TunnelData tunnel = new TunnelData();
        tunnel.id = nextTunnelId++;
        tunnel.radius = radius;

        Vector2 current = ClampToWorld(start);
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
        dir.Normalize();

        int steps = Mathf.Max(1, Mathf.CeilToInt(length / tunnelStepSize));

        for (int s = 0; s < steps; s++)
        {
            if (avoidCamps && IsTooCloseToCamp(current, radius + tunnelAvoidCampPadding))
            {
                dir = GetDirectionAwayFromCamps(current, dir);
                current = ClampToWorld(current + dir * tunnelStepSize);
                continue;
            }

            tunnel.points.Add(current);
            SpawnTunnelRevealCover(tunnel.id, current);

            if (wobble > 0f)
            {
                dir = (dir + Random.insideUnitCircle * wobble).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Random.insideUnitCircle.normalized;
            }

            current = ClampToWorld(current + dir * tunnelStepSize);
        }

        if (tunnel.points.Count == 0) return;

        tunnel.enemySpawnPoint = enemyAtEnd ? tunnel.points[tunnel.points.Count - 1] : tunnel.points[tunnel.points.Count / 2];
        Data.tunnels.Add(tunnel);

        // These extra spawns are dormant until reveal because RevealTunnel uses this tunnel's id.
        // Enemy/shiny/mushroom decisions are encoded by name through tunnel id lookup in RevealTunnel.
        // To keep MapData unchanged, we spawn the optional mushroom/shiny on reveal by random chance below.
        if (hasEnemy)
            tunnel.enemySpawnPoint += Random.insideUnitCircle * Mathf.Min(radius * 0.4f, 0.8f);

        // Marker objects are not stored in MapData yet; reveal uses random fallback chances.
        // Keeping this method parameterized so it is easy to move into TunnelData later.
    }

    void ClearOldChunksAndRuntimeObjects()
    {
        if (grid != null)
        {
            foreach (Transform child in grid.transform)
                Destroy(child.gameObject);
        }

        activeChunks.Clear();

        RevealCover[] oldCovers = Object.FindObjectsByType<RevealCover>(FindObjectsSortMode.None);
        foreach (RevealCover cover in oldCovers)
            Destroy(cover.gameObject);
    }

    // -------------------------
    // CHUNKS
    // -------------------------

    void UpdateActiveChunksAroundPlayer(bool force = false)
    {
        if (Data == null) return;

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return;
            playerTransform = playerObj.transform;
        }

        Vector2Int playerCell = Data.WorldToCell(playerTransform.position);
        Vector2Int playerChunk = CellToChunk(playerCell);

        if (!force && hasLastPlayerChunk && playerChunk == lastPlayerChunk) return;

        lastPlayerChunk = playerChunk;
        hasLastPlayerChunk = true;

        HashSet<Vector2Int> needed = new HashSet<Vector2Int>();

        for (int x = -activeChunkRadius; x <= activeChunkRadius; x++)
        {
            for (int y = -activeChunkRadius; y <= activeChunkRadius; y++)
            {
                Vector2Int coord = playerChunk + new Vector2Int(x, y);
                if (!ChunkCouldContainMapCells(coord)) continue;

                needed.Add(coord);

                if (!activeChunks.ContainsKey(coord))
                    CreateChunk(coord);
            }
        }

        List<Vector2Int> remove = new List<Vector2Int>();
        foreach (Vector2Int coord in activeChunks.Keys)
        {
            if (!needed.Contains(coord)) remove.Add(coord);
        }

        foreach (Vector2Int coord in remove)
        {
            Destroy(activeChunks[coord].gameObject);
            activeChunks.Remove(coord);
        }
    }

    bool ChunkCouldContainMapCells(Vector2Int chunkCoord)
    {
        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;
        int endX = startX + chunkSize - 1;
        int endY = startY + chunkSize - 1;

        if (endX < 0 || endY < 0) return false;
        if (startX >= worldWidthCells || startY >= worldHeightCells) return false;
        return true;
    }

    void CreateChunk(Vector2Int coord)
    {
        GameObject chunkObj = new GameObject("Chunk_" + coord.x + "_" + coord.y);
        chunkObj.transform.SetParent(grid.transform);

        MapChunk chunk = chunkObj.AddComponent<MapChunk>();
        chunk.Init(this, coord, chunkSize);
        activeChunks.Add(coord, chunk);
    }

    Vector2Int CellToChunk(Vector2Int cell)
    {
        return new Vector2Int(
            Mathf.FloorToInt(cell.x / (float)chunkSize),
            Mathf.FloorToInt(cell.y / (float)chunkSize)
        );
    }

    public void RefreshChunksNearWorldCircle(Vector2 center, float radius)
    {
        if (Data == null) return;

        Vector2Int centerCell = Data.WorldToCell(center);
        int cellRadius = Mathf.CeilToInt(radius / Data.cellSize) + 2;

        Vector2Int minChunk = CellToChunk(centerCell - new Vector2Int(cellRadius, cellRadius));
        Vector2Int maxChunk = CellToChunk(centerCell + new Vector2Int(cellRadius, cellRadius));

        for (int x = minChunk.x; x <= maxChunk.x; x++)
        {
            for (int y = minChunk.y; y <= maxChunk.y; y++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                if (activeChunks.TryGetValue(coord, out MapChunk chunk))
                    chunk.Refresh();
            }
        }
    }

    // -------------------------
    // DIRT / PATH TRUTH
    // -------------------------

    public TileBase GetRandomDirtTile()
    {
        float roll = Random.value;
        if (roll < 0.5f) return dirtTile1;
        if (roll < 0.85f) return dirtTile2;
        return dirtTile3;
    }

    public TileBase GetDirtTileByType(int type)
    {
        switch (type)
        {
            case 0: return dirtTile1;
            case 1: return dirtTile2;
            case 2: return dirtTile3;

            // These are optional guide/soft-border dirt tiles.
            // Assign darker/weirder dirt sprites here in the inspector.
            case 3: return noRouteDirtNear != null ? noRouteDirtNear : dirtTile3;
            case 4: return noRouteDirtMid != null ? noRouteDirtMid : (noRouteDirtNear != null ? noRouteDirtNear : dirtTile3);
            case 5: return noRouteDirtFar != null ? noRouteDirtFar : (noRouteDirtMid != null ? noRouteDirtMid : dirtTile3);
            default: return dirtTile1;
        }
    }

    void ApplyNoRouteGuideDirt()
    {
        if (!useNoRouteGuideDirt || Data == null) return;

        // Build a list of places that count as "real map": start, camps, and every tunnel reveal point.
        // Dirt far from these points gets painted with warning dirt.
        List<Vector2> contentPoints = new List<Vector2>();
        contentPoints.Add(playerStartPosition);

        foreach (CampData camp in Data.camps)
            contentPoints.Add(camp.center);

        foreach (TunnelData tunnel in Data.tunnels)
        {
            if (tunnel.points == null) continue;
            int stride = Mathf.Max(1, noRouteDistanceSampleStride);
            for (int i = 0; i < tunnel.points.Count; i += stride)
                contentPoints.Add(tunnel.points[i]);
        }

        if (contentPoints.Count == 0) return;

        float nearSqr = noRouteNearDistance * noRouteNearDistance;
        float midSqr = noRouteMidDistance * noRouteMidDistance;
        float farSqr = noRouteFarDistance * noRouteFarDistance;

        for (int x = 0; x < Data.width; x++)
        {
            for (int y = 0; y < Data.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!Data.IsBlocked(cell)) continue;

                Vector2 world = Data.CellToWorld(cell);
                float closestSqr = float.MaxValue;

                for (int i = 0; i < contentPoints.Count; i++)
                {
                    float d = (contentPoints[i] - world).sqrMagnitude;
                    if (d < closestSqr) closestSqr = d;
                    if (closestSqr <= nearSqr) break;
                }

                if (closestSqr >= farSqr)
                    Data.SetDirtType(cell, 5);
                else if (closestSqr >= midSqr)
                    Data.SetDirtType(cell, 4);
                else if (closestSqr >= nearSqr)
                    Data.SetDirtType(cell, 3);
            }
        }

        Debug.Log("ROUTE_V4 guide dirt painted using " + contentPoints.Count + " content points.");
    }

    public void DigCircle(Vector2 worldCenter, float radius)
    {
        if (Data == null) return;

        Data.ClearCircle(worldCenter, radius);
        RefreshChunksNearWorldCircle(worldCenter, radius);
    }

    public void MarkWorldCircleWalkable(Vector2 center, float radius)
    {
        DigCircle(center, radius);
    }

    public bool IsWorldPositionWalkable(Vector2 worldPos)
    {
        if (Data == null) return true;
        Vector2Int cell = Data.WorldToCell(worldPos);
        return !Data.IsBlocked(cell);
    }

    public bool IsWorldPositionClearForBody(Vector2 worldPos, float bodyRadius)
    {
        Vector2[] checks =
        {
            Vector2.zero,
            Vector2.up * bodyRadius,
            Vector2.down * bodyRadius,
            Vector2.left * bodyRadius,
            Vector2.right * bodyRadius,
            new Vector2(1, 1).normalized * bodyRadius,
            new Vector2(1, -1).normalized * bodyRadius,
            new Vector2(-1, 1).normalized * bodyRadius,
            new Vector2(-1, -1).normalized * bodyRadius
        };

        foreach (Vector2 check in checks)
        {
            if (!IsWorldPositionWalkable(worldPos + check)) return false;
        }

        return true;
    }

    // -------------------------
    // CAMPS
    // -------------------------

    void GenerateCamps()
    {
        // Make the boss camp first so normal camps avoid it when choosing positions.
        if (generateBossCamp)
        {
            CampData boss = new CampData
            {
                id = nextCampId++,
                center = GetFarCornerPosition(),
                radius = bossCampRadius,
                revealed = false,
                isBossCamp = true,
                hasExitPortal = true,
                enemyCount = bossCampNormalEnemyCount,
                bossEnemyCount = 1,
                sporeCount = 1,
                mushroomCount = Random.Range(bossCampMushroomMin, bossCampMushroomMax + 1),
                shinyCount = bossCampShinyCount
            };

            Data.camps.Add(boss);
            SpawnCampRevealPatch(boss);
        }

        for (int i = 0; i < campCount; i++)
        {
            Vector2 pos = PickValidCampPosition(campRadius);

            CampData camp = new CampData
            {
                id = nextCampId++,
                center = pos,
                radius = campRadius,
                revealed = false,
                isBossCamp = false,
                hasExitPortal = false,
                enemyCount = 3,
                bossEnemyCount = 0,
                sporeCount = 2,
                mushroomCount = Random.Range(2, 5),
                shinyCount = Random.value < 0.45f ? 1 : 0
            };

            Data.camps.Add(camp);
            SpawnCampRevealPatch(camp);
        }
    }

    Vector2 PickValidCampPosition(float radius)
    {
        for (int attempt = 0; attempt < 500; attempt++)
        {
            Vector2 pos = GetRandomWorldPosition(radius + 2f);

            if (Vector2.Distance(pos, playerStartPosition) < minCampDistanceFromStart)
                continue;

            bool tooClose = false;
            foreach (CampData camp in Data.camps)
            {
                float needed = radius + camp.radius + minCampDistanceFromOtherCamps;
                if (Vector2.Distance(pos, camp.center) < needed)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose) return pos;
        }

        return GetRandomWorldPosition(radius + 2f);
    }

    void SpawnCampRevealPatch(CampData camp)
    {
        if (revealCoverPrefab == null) return;

        // ROUTE_V2_BUILD: Camp reveal covers are blobby, not perfect meatball circles.
        List<Vector2> lobeCenters = new List<Vector2>();
        List<float> lobeRadii = new List<float>();
        lobeCenters.Add(camp.center);
        lobeRadii.Add(camp.radius);

        int lobes = camp.isBossCamp ? 8 : 5;
        for (int i = 0; i < lobes; i++)
        {
            Vector2 offset = Random.insideUnitCircle * camp.radius * Random.Range(0.3f, 0.85f);
            lobeCenters.Add(camp.center + offset);
            lobeRadii.Add(camp.radius * Random.Range(0.3f, 0.58f));
        }

        float spacing = cellSize;
        int steps = Mathf.CeilToInt(camp.radius * 1.35f / spacing);

        for (int x = -steps; x <= steps; x++)
        {
            for (int y = -steps; y <= steps; y++)
            {
                Vector2 pos = camp.center + new Vector2(x * spacing, y * spacing);

                bool insideBlob = false;
                for (int i = 0; i < lobeCenters.Count; i++)
                {
                    if (Vector2.Distance(pos, lobeCenters[i]) <= lobeRadii[i])
                    {
                        insideBlob = true;
                        break;
                    }
                }

                if (!insideBlob) continue;
                if (Random.value > 0.52f) continue;

                GameObject cover = Instantiate(revealCoverPrefab, pos, Quaternion.identity);
                RevealCover reveal = cover.GetComponent<RevealCover>();
                if (reveal != null)
                {
                    reveal.revealType = RevealCover.RevealType.Camp;
                    reveal.revealId = camp.id;
                }
            }
        }

        Debug.Log("Spawned ROUTE_V3 blobby camp cover patch id " + camp.id + " at " + camp.center);
    }

    public void RevealCamp(int campId)
    {
        if (Data == null) return;

        CampData camp = Data.camps.Find(c => c.id == campId);
        if (camp == null || camp.revealed) return;

        camp.revealed = true;

        DigOrganicRoom(camp.center, camp.radius, camp.isBossCamp ? 9 : 6);
        DestroyRevealCovers(RevealCover.RevealType.Camp, campId, camp.center, camp.radius + 2f);

        for (int i = 0; i < camp.enemyCount; i++)
            SpawnAround(camp.center, camp.radius * 0.45f, enemyPrefab);

        for (int i = 0; i < camp.bossEnemyCount; i++)
            SpawnBossEnemy(camp);

        for (int i = 0; i < camp.sporeCount; i++)
            SpawnAround(camp.center, camp.radius * 0.35f, sporePrefab);

        for (int i = 0; i < camp.mushroomCount; i++)
            SpawnAround(camp.center, camp.radius * 0.6f, mushroomPrefab);

        for (int i = 0; i < camp.shinyCount; i++)
            SpawnAround(camp.center, camp.radius * 0.55f, shinyPrefab);

        if (camp.hasExitPortal)
            SpawnExitPortal(camp);

        Debug.Log("Revealed camp " + campId + (camp.isBossCamp ? " (boss)" : ""));
    }

    void DigOrganicRoom(Vector2 center, float radius, int extraLobes)
    {
        DigCircle(center, radius);

        for (int i = 0; i < extraLobes; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius * Random.Range(0.25f, 0.75f);
            float lobeRadius = radius * Random.Range(0.28f, 0.55f);
            DigCircle(center + offset, lobeRadius);
        }
    }

    void SpawnBossEnemy(CampData camp)
    {
        GameObject prefab = bossEnemyPrefab != null ? bossEnemyPrefab : enemyPrefab;
        if (prefab == null) return;

        Vector2 spawnPos = camp.center + Random.insideUnitCircle * (camp.radius * 0.25f);
        GameObject boss = Instantiate(prefab, spawnPos, Quaternion.identity);
        boss.name = "Boss " + boss.name;
        boss.transform.localScale *= bossEnemyScaleMultiplier;

        EnemyHealth enemyHealth = boss.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.maxHealth *= bossEnemyHealthMultiplier;
            enemyHealth.health = enemyHealth.maxHealth;
            enemyHealth.xpDropValue *= bossEnemyHealthMultiplier;
        }

        TunnelWeevilEnemy weevil = boss.GetComponent<TunnelWeevilEnemy>();
        if (weevil != null)
        {
            weevil.enemyName = "Boss " + weevil.enemyName;
            weevil.maxHealth *= bossEnemyHealthMultiplier;
            weevil.health = weevil.maxHealth;
            weevil.attackDamage += bossEnemyDamageBonus;
            weevil.noticeRange *= 1.2f;
        }
    }

    void SpawnExitPortal(CampData camp)
    {
        if (exitPortalPrefab == null)
        {
            Debug.LogWarning("Boss camp revealed, but no exitPortalPrefab is assigned on MapGenerator.");
            return;
        }

        Vector2 exitPos = camp.center + Vector2.right * (camp.radius * 0.45f);
        Instantiate(exitPortalPrefab, exitPos, Quaternion.identity);
    }

    // -------------------------
    // TUNNELS
    // -------------------------

    void GenerateHiddenTunnels()
    {
        for (int i = 0; i < tunnelCount; i++)
        {
            Vector2 current = GetRandomWorldPosition(4f);
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

            float length = Random.Range(tunnelMinLength, tunnelMaxLength);
            CreateTunnelSegment(current, dir, length, tunnelRadius, true, false, tunnelWobble);
        }
    }

    void SpawnTunnelRevealCover(int tunnelId, Vector2 position)
    {
        if (revealCoverPrefab == null) return;

        GameObject cover = Instantiate(revealCoverPrefab, position, Quaternion.identity);
        RevealCover reveal = cover.GetComponent<RevealCover>();
        if (reveal != null)
        {
            reveal.revealType = RevealCover.RevealType.Tunnel;
            reveal.revealId = tunnelId;
        }
    }

    public void RevealTunnel(int tunnelId)
    {
        if (Data == null) return;

        TunnelData tunnel = Data.tunnels.Find(t => t.id == tunnelId);
        if (tunnel == null || tunnel.revealed) return;

        tunnel.revealed = true;

        foreach (Vector2 point in tunnel.points)
            DigCircle(point, tunnel.radius);

        DestroyRevealCovers(RevealCover.RevealType.Tunnel, tunnelId, tunnel.enemySpawnPoint, tunnelMaxLength + 4f);

        // Most tunnel chunks get some kind of tiny reward or threat. This keeps filler from feeling empty.
        if (tunnelEnemyPrefab != null && Random.value < 0.65f)
            Instantiate(tunnelEnemyPrefab, tunnel.enemySpawnPoint, Quaternion.identity);

        if (mushroomPrefab != null && Random.value < 0.45f)
            SpawnAround(tunnel.enemySpawnPoint, tunnel.radius * 0.8f, mushroomPrefab);

        if (spawnRareShinies && shinyPrefab != null && Random.value < tunnelShinyChance)
            SpawnAround(tunnel.enemySpawnPoint, tunnel.radius * 0.6f, shinyPrefab);

        Debug.Log("Revealed tunnel " + tunnelId);
    }

    void DestroyRevealCovers(RevealCover.RevealType type, int id, Vector2 center, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (Collider2D hit in hits)
        {
            RevealCover cover = hit.GetComponent<RevealCover>();
            if (cover == null) continue;
            if (cover.revealType == type && cover.revealId == id)
                Destroy(cover.gameObject);
        }
    }

    bool IsTooCloseToCamp(Vector2 pos, float padding)
    {
        foreach (CampData camp in Data.camps)
        {
            if (Vector2.Distance(pos, camp.center) < camp.radius + padding)
                return true;
        }

        return false;
    }

    Vector2 GetDirectionAwayFromCamps(Vector2 pos, Vector2 fallback)
    {
        Vector2 away = Vector2.zero;

        foreach (CampData camp in Data.camps)
        {
            float dist = Vector2.Distance(pos, camp.center);
            if (dist < camp.radius + tunnelAvoidCampPadding + 4f)
                away += (pos - camp.center).normalized;
        }

        if (away.sqrMagnitude < 0.01f) return fallback.normalized;
        return away.normalized;
    }

    // -------------------------
    // LOOSE SPAWNS
    // -------------------------

    void SpawnLooseMushrooms()
    {
        if (mushroomPrefab == null) return;

        int spawned = 0;
        int attempts = 0;

        while (spawned < looseMushroomCount && attempts < looseMushroomCount * 20)
        {
            attempts++;

            Vector2 pos = GetRandomWorldPosition(3f);
            if (Vector2.Distance(pos, playerStartPosition) < mushroomAvoidStartRadius) continue;
            if (IsTooCloseToCamp(pos, 1f)) continue;

            SpawnAround(pos, 0.2f, mushroomPrefab);
            spawned++;
        }
    }

    void SpawnAround(Vector2 center, float radius, GameObject prefab)
    {
        if (prefab == null) return;

        Vector2 pos = center + Random.insideUnitCircle * radius;
        Instantiate(prefab, pos, Quaternion.identity);
    }

    // -------------------------
    // PLAYER / BUDDY
    // -------------------------

    void SpawnPlayer()
    {
        if (!spawnPlayer || gobboPrefab == null) return;

        GameObject existing = GameObject.FindGameObjectWithTag("Player");
        if (existing != null) Destroy(existing);

        spawnedPlayer = Instantiate(gobboPrefab, playerStartPosition, Quaternion.identity);
        playerTransform = spawnedPlayer.transform;
        hasLastPlayerChunk = false;

        spawnedPlayer.name = "Gobbo";
        spawnedPlayer.tag = "Player";

        CameraFollow cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cam != null) cam.target = spawnedPlayer.transform;
    }

    void SpawnTestBuddy()
    {
        if (!spawnTestBuddy || buddyPrefab == null || spawnedPlayer == null) return;

        Vector2 pos = playerStartPosition + Vector2.right * buddySpawnDistance;
        if (!IsWorldPositionClearForBody(pos, 0.3f))
            pos = playerStartPosition + Vector2.left * buddySpawnDistance;

        GameObject buddy = Instantiate(buddyPrefab, pos, Quaternion.identity);

        BuddyFollow follow = buddy.GetComponent<BuddyFollow>();
        if (follow != null)
        {
            follow.SetPlayer(spawnedPlayer.transform);
            follow.SetFormationOffset(Vector2.zero);
        }

        BuddyCombat combat = buddy.GetComponent<BuddyCombat>();
        if (combat != null)
            combat.SetPlayer(spawnedPlayer.transform);
    }

    // -------------------------
    // WORLD HELPERS
    // -------------------------

    Vector2 GetRandomWorldPosition(float edgePadding = 0f)
    {
        float minX = Data.origin.x + edgePadding;
        float maxX = Data.origin.x + Data.width * Data.cellSize - edgePadding;
        float minY = Data.origin.y + edgePadding;
        float maxY = Data.origin.y + Data.height * Data.cellSize - edgePadding;

        return new Vector2(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY)
        );
    }

    Vector2 GetFarCornerPosition()
    {
        float maxX = Data.origin.x + Data.width * Data.cellSize - bossCampRadius - 3f;
        float maxY = Data.origin.y + Data.height * Data.cellSize - bossCampRadius - 3f;
        return new Vector2(maxX, maxY);
    }

    Vector2 ClampToWorld(Vector2 pos)
    {
        float minX = Data.origin.x + 2f;
        float maxX = Data.origin.x + Data.width * Data.cellSize - 2f;
        float minY = Data.origin.y + 2f;
        float maxY = Data.origin.y + Data.height * Data.cellSize - 2f;

        return new Vector2(
            Mathf.Clamp(pos.x, minX, maxX),
            Mathf.Clamp(pos.y, minY, maxY)
        );
    }

    void OnDrawGizmosSelected()
    {
        if (Data == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerStartPosition, forceIntroMap ? introStartPitRadius : startClearRadius);

        Gizmos.color = Color.red;
        foreach (CampData camp in Data.camps)
            Gizmos.DrawWireSphere(camp.center, camp.radius);

        Gizmos.color = Color.cyan;
        foreach (TunnelData tunnel in Data.tunnels)
        {
            foreach (Vector2 point in tunnel.points)
                Gizmos.DrawWireSphere(point, tunnel.radius * 0.25f);
        }
    }
}
