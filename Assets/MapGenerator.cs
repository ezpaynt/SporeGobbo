using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    [Header("Intro Map - DREAM_MAP_V3_SPACED_NETWORK_BUILD")]
    public int introWorldWidthCells = 420;
    public int introWorldHeightCells = 300;
    public float introStartPitRadius = 20f;
    public int introStartMushrooms = 6;
    public int introCampCount = 3;
    public float introCampRadius = 11f;
    public float introBossRadius = 15f;
    public float introRouteStepMin = 10.5f;
    public float introRouteStepMax = 17.5f;
    public float introRouteWobble = 4.2f;
    public float introRouteSegmentGapChance = 0.36f;
    public int introNearSpawnDiscoveries = 26;
    public int introOffRoutePockets = 34;
    public float introSafeDirtRadius = 126f;
    public float introMajorZoneEdgePadding = 58f;
    public float introTunnelTooCloseDistance = 7.1f;

    [Header("No-Route Guide Dirt / Soft Border")]
    public bool useNoRouteGuideDirt = true;
    public TileBase noRouteDirtNear;
    public TileBase noRouteDirtMid;
    public TileBase noRouteDirtFar;
    public float noRouteNearDistance = 30f;
    public float noRouteMidDistance = 48f;
    public float noRouteFarDistance = 68f;
    public int noRouteDistanceSampleStride = 2;

    [Header("Debug - True Reveal Overlay")]
    public bool showTrueRevealDebug = true;
    public bool hideNormalRevealCoversWhileDebugging = false;
    public Color tunnelRevealDebugColor = new Color(1f, 0.05f, 0.05f, 0.9f);
    public Color campRevealDebugColor = new Color(1f, 0.65f, 0.05f, 0.9f);
    public float trueRevealDebugLineWidth = 0.08f;
    public int trueRevealDebugCircleSegments = 24;

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
    public float tunnelAvoidCampPadding = 7.5f;

    [Header("Loose Mushrooms")]
    public int looseMushroomCount = 18;
    public float mushroomAvoidStartRadius = 4f;

    [Header("Rare Shinies")]
    public bool spawnRareShinies = true;
    public float tunnelShinyChance = 0.35f;

    private Transform revealDebugRoot;

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
        Debug.Log("DREAM_MAP_V3_SPACED_NETWORK_BUILD intro generator is running. If you do not see this, Unity is using the wrong file.");

        playerStartPosition = Vector2.zero;
        GenerateStartHub();

        CampData campA = CreateIntroCamp(ClampIntroMajorZone(PolarFromStart(82f, 145f)), introCampRadius, false);
        CampData campB = CreateIntroCamp(ClampIntroMajorZone(PolarFromStart(86f, -118f)), introCampRadius + 0.5f, false);
        CampData campC = CreateIntroCamp(ClampIntroMajorZone(PolarFromStart(84f, 34f)), introCampRadius, false);

        CampData boss = null;
        if (generateBossCamp)
            boss = CreateIntroCamp(ClampIntroMajorZone(PolarFromStart(132f, 0f)), introBossRadius, true);

        GenerateCampNeighborhood(campA, 3, false);
        GenerateCampNeighborhood(campB, 3, false);
        GenerateCampNeighborhood(campC, 3, false);
        if (boss != null)
            GenerateCampNeighborhood(boss, 4, true);

        // Four main broken routes. They should feel like reveal -> dig -> reveal, not one mega highway.
        GenerateBrokenRoute(playerStartPosition, campA.center, 1.10f, true);
        GenerateBrokenRoute(playerStartPosition, campB.center, 1.10f, true);
        GenerateBrokenRoute(playerStartPosition, campC.center, 1.10f, true);
        if (boss != null)
            GenerateBrokenRoute(playerStartPosition, boss.center, 0.92f, true);

        // Between-destination discoveries fill the map without letting routes cut through camps.
        // They are separated chunks, not continuous camp-to-camp highways.
        GenerateBetweenDestinationDiscoveries(campA.center, campB.center, 7);
        GenerateBetweenDestinationDiscoveries(campA.center, campC.center, 7);
        GenerateBetweenDestinationDiscoveries(campB.center, campC.center, 7);

        GenerateCentralDiscoveryRing(campA.center, campB.center, campC.center);
        GenerateSmallNearSpawnDiscoveries();
        GenerateOffRoutePockets();

        ApplyNoRouteGuideDirt();
    }

    void GenerateStartHub()
    {
        DigOrganicRoom(playerStartPosition, introStartPitRadius, 9);

        for (int i = 0; i < introStartMushrooms; i++)
            SpawnAround(playerStartPosition, introStartPitRadius * 0.48f, mushroomPrefab);

        // First buddy source.
        SpawnAround(playerStartPosition + Vector2.right * 1.5f, 0.35f, sporePrefab);

        // Obvious early directions so the player does not dig into blank dirt forever.
        Vector2[] dirs =
        {
            Vector2.right,
            new Vector2(-0.75f, 0.55f).normalized,
            new Vector2(-0.55f, -0.8f).normalized,
            new Vector2(0.25f, -1f).normalized,
            new Vector2(0.15f, 1f).normalized
        };

        foreach (Vector2 dir in dirs)
        {
            Vector2 start = playerStartPosition + dir * (introStartPitRadius + 4f);
            CreateTunnelSegment(start, dir, Random.Range(6f, 9f), Random.Range(2.4f, 3.4f), false, false, 0.05f, false, true, true);
        }

        Vector2 firstTunnelStart = playerStartPosition + Vector2.right * (introStartPitRadius + 7f);
        CreateTunnelSegment(firstTunnelStart, Vector2.right, 9f, 3.0f, true, false, 0.04f, false, true, true);
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

    void GenerateCampNeighborhood(CampData camp, int featureCount, bool bossZone)
    {
        int made = 0;
        int attempts = 0;

        while (made < featureCount && attempts < featureCount * 70)
        {
            attempts++;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(camp.radius + 13f, camp.radius + 24f);
            Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 pos = ClampToWorld(camp.center + radial * dist);

            // Do not let camp neighborhood chunks sit inside the camp reveal radius.
            if (IsTooCloseToCamp(pos, 8f)) continue;
            if (IsTooCloseToAnyTunnel(pos, bossZone ? 7.5f : 8.5f)) continue;

            Vector2 tangent = Vector2.Perpendicular(radial) * (Random.value < 0.5f ? 1f : -1f);
            Vector2 away = radial;
            Vector2 dir = Random.value < 0.65f ? tangent : (tangent * 0.55f + away * 0.45f).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = tangent;

            // These are camp-adjacent clues, not tunnels punching into the camp.
            if (Random.value < 0.58f)
                CreateTunnelSegment(pos, dir, Random.Range(4.5f, 7.2f), Random.Range(2.7f, 4.0f), Random.value < 0.18f || bossZone, false, 0.045f, false, true, true);
            else
                CreateTunnelSegment(pos, away, Random.Range(2.6f, 4.8f), Random.Range(3.1f, 4.7f), Random.value < 0.12f || bossZone, false, 0.035f, Random.value < 0.08f, true, true);

            made++;
        }
    }

    void GenerateBrokenRoute(Vector2 from, Vector2 to, float density, bool fromStart)
    {
        Vector2 routeDir = (to - from).normalized;
        if (routeDir.sqrMagnitude < 0.01f) routeDir = Vector2.right;

        float totalDist = Vector2.Distance(from, to);
        float traveled = fromStart ? introStartPitRadius + 13f : introCampRadius + 10f;
        int routeStep = 0;

        while (traveled < totalDist - introCampRadius - 24f)
        {
            Vector2 basePoint = Vector2.Lerp(from, to, traveled / totalDist);
            Vector2 perp = Vector2.Perpendicular(routeDir);

            float wave = Mathf.Sin(routeStep * 0.85f + totalDist * 0.025f);
            Vector2 pos = ClampToWorld(basePoint + perp * wave * introRouteWobble + Random.insideUnitCircle * 1.35f);

            Vector2 nextAim = (to - pos).normalized;
            if (nextAim.sqrMagnitude < 0.01f) nextAim = routeDir;
            nextAim = (nextAim + Random.insideUnitCircle * 0.08f).normalized;

            bool forcedFirst = fromStart && routeStep == 0;
            bool makeGap = !forcedFirst && Random.value < introRouteSegmentGapChance;

            if (!makeGap && !IsTooCloseToCamp(pos, 12f) && !IsTooCloseToAnyTunnel(pos, introTunnelTooCloseDistance))
            {
                float distFromStart = Vector2.Distance(pos, playerStartPosition);
                float nearStartBoost = Mathf.InverseLerp(95f, 15f, distFromStart);
                float radius = Random.Range(2.25f, 3.35f) + nearStartBoost * 0.20f;
                float length = Random.Range(5.8f, 9.2f) + nearStartBoost * 1.1f;
                bool enemy = Random.value < (fromStart ? 0.15f : 0.22f);
                bool mushroom = Random.value < 0.55f;

                CreateTunnelSegment(pos, nextAim, length, radius, enemy, false, 0.045f, false, mushroom, true);

                // Tiny side pocket sometimes, but not a big branching road.
                if (Random.value < 0.06f * density)
                {
                    Vector2 side = Vector2.Perpendicular(nextAim) * (Random.value < 0.5f ? 1f : -1f);
                    Vector2 sidePos = ClampToWorld(pos + side * Random.Range(6f, 9f));
                    if (!IsTooCloseToAnyTunnel(sidePos, 5.8f))
                        CreateTunnelSegment(sidePos, side, Random.Range(2.5f, 4.8f), Random.Range(2.8f, 4.2f), Random.value < 0.08f, false, 0.04f, false, Random.value < 0.65f, true);
                }
            }

            traveled += Random.Range(introRouteStepMin, introRouteStepMax) / Mathf.Max(0.45f, density);
            routeStep++;
        }
    }

    void GenerateHalfConnector(Vector2 from, Vector2 to, float tStart, float tEnd)
    {
        Vector2 a = Vector2.Lerp(from, to, tStart);
        Vector2 b = Vector2.Lerp(from, to, tEnd);
        GenerateBrokenRoute(a, b, 0.55f, false);
    }

    void GenerateBetweenDestinationDiscoveries(Vector2 a, Vector2 b, int count)
    {
        Vector2 line = (b - a);
        float dist = line.magnitude;
        if (dist < 1f) return;
        Vector2 dir = line.normalized;
        Vector2 perp = Vector2.Perpendicular(dir);

        int made = 0;
        int attempts = 0;
        while (made < count && attempts < count * 40)
        {
            attempts++;
            float t = Random.Range(0.18f, 0.82f);
            Vector2 basePos = Vector2.Lerp(a, b, t);
            Vector2 pos = ClampToWorld(basePos + perp * Random.Range(-14f, 14f) + Random.insideUnitCircle * 5f);

            // This is the important camp rule: between-camp chunks may live in the space between camps,
            // but they should never be inside or touching a camp reveal.
            if (IsTooCloseToCamp(pos, 11f)) continue;
            if (IsTooCloseToAnyTunnel(pos, introTunnelTooCloseDistance * 0.95f)) continue;

            Vector2 chunkDir;
            if (Random.value < 0.50f)
                chunkDir = (dir + Random.insideUnitCircle * 0.22f).normalized;
            else
                chunkDir = (perp * (Random.value < 0.5f ? 1f : -1f) + Random.insideUnitCircle * 0.20f).normalized;
            if (chunkDir.sqrMagnitude < 0.01f) chunkDir = dir;

            bool pocket = Random.value < 0.48f;
            if (pocket)
                CreateTunnelSegment(pos, chunkDir, Random.Range(2.8f, 5.8f), Random.Range(3.0f, 4.6f), Random.value < 0.12f, false, 0.035f, false, true, true);
            else
                CreateTunnelSegment(pos, chunkDir, Random.Range(6.0f, 10.5f), Random.Range(2.1f, 3.2f), Random.value < 0.16f, false, 0.07f, false, Random.value < 0.55f, true);

            made++;
        }
    }

    void GenerateCentralDiscoveryRing(Vector2 campA, Vector2 campB, Vector2 campC)
    {
        Vector2[] anchors =
        {
            Vector2.Lerp(playerStartPosition, campA, 0.35f),
            Vector2.Lerp(playerStartPosition, campB, 0.35f),
            Vector2.Lerp(playerStartPosition, campC, 0.35f),
            Vector2.Lerp(playerStartPosition, campA, 0.62f),
            Vector2.Lerp(playerStartPosition, campB, 0.62f),
            Vector2.Lerp(playerStartPosition, campC, 0.62f)
        };

        for (int a = 0; a < anchors.Length; a++)
        {
            int count = 4;
            float scatter = 15f;

            for (int i = 0; i < count; i++)
            {
                Vector2 pos = ClampToWorld(anchors[a] + Random.insideUnitCircle * scatter);

                if (Vector2.Distance(pos, playerStartPosition) < introStartPitRadius + 6f) continue;
                if (IsTooCloseToCamp(pos, 10f)) continue;
                if (IsTooCloseToAnyTunnel(pos, introTunnelTooCloseDistance * 0.85f)) continue;

                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

                if (Random.value < 0.62f)
                    CreateTunnelSegment(pos, dir, Random.Range(2.5f, 5.5f), Random.Range(3.2f, 5.0f), Random.value < 0.12f, false, 0.04f, false, true, true);
                else
                    CreateTunnelSegment(pos, dir, Random.Range(5.0f, 8.5f), Random.Range(2.2f, 3.2f), Random.value < 0.16f, false, 0.08f, false, Random.value < 0.55f, true);
            }
        }
    }

    void GenerateSmallNearSpawnDiscoveries()
    {
        for (int i = 0; i < introNearSpawnDiscoveries; i++)
        {
            float angle = (Mathf.PI * 2f / introNearSpawnDiscoveries) * i + Random.Range(-0.25f, 0.25f);
            float dist = Random.Range(introStartPitRadius + 14f, introStartPitRadius + 62f);
            Vector2 pos = ClampToWorld(playerStartPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist);

            if (IsTooCloseToAnyTunnel(pos, 5.2f)) continue;

            Vector2 tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            if (Random.value < 0.45f)
                CreateTunnelSegment(pos, tangent * (Random.value < 0.5f ? 1f : -1f), Random.Range(4.8f, 8.0f), Random.Range(2.2f, 3.3f), Random.value < 0.13f, false, 0.06f, false, true, true);
            else
                CreateTunnelSegment(pos, Random.insideUnitCircle.normalized, Random.Range(2.5f, 5.5f), Random.Range(3.0f, 4.9f), false, false, 0.04f, false, true, true);
        }
    }

    void GenerateOffRoutePockets()
    {
        int made = 0;
        int attempts = 0;

        while (made < introOffRoutePockets && attempts < introOffRoutePockets * 80)
        {
            attempts++;

            float dist = Random.Range(introStartPitRadius + 38f, introSafeDirtRadius + 18f);
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector2 pos = ClampToWorld(playerStartPosition + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist);

            if (Vector2.Distance(pos, playerStartPosition) < introStartPitRadius + 25f) continue;
            if (IsTooCloseToCamp(pos, 6f)) continue;
            if (IsTooCloseToAnyTunnel(pos, 6.2f)) continue;

            if (Random.value < 0.72f)
                CreateTunnelSegment(pos, Random.insideUnitCircle.normalized, Random.Range(2.7f, 5.7f), Random.Range(2.8f, 4.4f), Random.value < 0.12f, false, 0.04f, false, Random.value < 0.72f, true);
            else
                CreateTunnelSegment(pos, Random.insideUnitCircle.normalized, Random.Range(5.5f, 8.5f), Random.Range(1.8f, 2.7f), Random.value < 0.16f, false, 0.08f, false, Random.value < 0.5f, true);

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

    Vector2 ClampIntroMajorZone(Vector2 pos)
    {
        float pad = Mathf.Max(introMajorZoneEdgePadding, introBossRadius + 12f);
        float minX = Data.origin.x + pad;
        float maxX = Data.origin.x + Data.width * Data.cellSize - pad;
        float minY = Data.origin.y + pad;
        float maxY = Data.origin.y + Data.height * Data.cellSize - pad;

        return new Vector2(
            Mathf.Clamp(pos.x, minX, maxX),
            Mathf.Clamp(pos.y, minY, maxY)
        );
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

        int skippedForCamp = 0;

        for (int s = 0; s < steps; s++)
        {
            if (avoidCamps && IsTooCloseToCamp(current, radius + tunnelAvoidCampPadding))
            {
                dir = GetDirectionAwayFromCamps(current, dir);
                current = ClampToWorld(current + dir * tunnelStepSize * 1.8f);
                skippedForCamp++;
                if (skippedForCamp > 10) break;
                continue;
            }

            tunnel.points.Add(current);
            if (showTrueRevealDebug)
                SpawnTrueRevealDebugCircle(current, radius, tunnelRevealDebugColor, "TunnelRevealDebug_" + tunnel.id);

            // Cover markers are spread across the tunnel width, not only along the centerline.
            // This makes the thing you dig into closer to the real reveal area.
            SpawnTunnelRevealCover(tunnel.id, current);
            if (radius > 2.05f)
            {
                Vector2 width = Vector2.Perpendicular(dir).normalized * radius * 0.55f;
                SpawnTunnelRevealCover(tunnel.id, current + width);
                SpawnTunnelRevealCover(tunnel.id, current - width);
            }

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


    void SpawnTrueRevealDebugCircle(Vector2 center, float radius, Color color, string objectName)
    {
        if (trueRevealDebugCircleSegments < 8) trueRevealDebugCircleSegments = 8;

        if (revealDebugRoot == null)
        {
            GameObject root = new GameObject("DREAM_MAP_V3_TrueRevealDebugRoot");
            revealDebugRoot = root.transform;
        }

        GameObject go = new GameObject(objectName);
        go.transform.SetParent(revealDebugRoot);
        go.transform.position = center;

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = trueRevealDebugCircleSegments;
        lr.startWidth = trueRevealDebugLineWidth;
        lr.endWidth = trueRevealDebugLineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = 500;

        for (int i = 0; i < trueRevealDebugCircleSegments; i++)
        {
            float a = Mathf.PI * 2f * i / trueRevealDebugCircleSegments;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }

    void ClearRevealDebugObjects()
    {
        if (revealDebugRoot != null)
        {
            Destroy(revealDebugRoot.gameObject);
            revealDebugRoot = null;
        }

        GameObject existing = GameObject.Find("DREAM_MAP_V3_TrueRevealDebugRoot");
        if (existing != null)
            Destroy(existing);
    }

    void ClearOldChunksAndRuntimeObjects()
    {
        if (grid != null)
        {
            foreach (Transform child in grid.transform)
                Destroy(child.gameObject);
        }

        activeChunks.Clear();

        ClearRevealDebugObjects();

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
            case 3: return noRouteDirtNear != null ? noRouteDirtNear : dirtTile3;
            case 4: return noRouteDirtMid != null ? noRouteDirtMid : (noRouteDirtNear != null ? noRouteDirtNear : dirtTile3);
            case 5: return noRouteDirtFar != null ? noRouteDirtFar : (noRouteDirtMid != null ? noRouteDirtMid : dirtTile3);
            default: return dirtTile1;
        }
    }


    void AddIntroGuideProtectionPoints(List<Vector2> contentPoints)
    {
        float[] rings = { introStartPitRadius + 14f, introStartPitRadius + 34f, introSafeDirtRadius * 0.88f };
        for (int r = 0; r < rings.Length; r++)
        {
            float radius = rings[r];
            for (int i = 0; i < 20; i++)
            {
                float angle = Mathf.PI * 2f * i / 20f;
                contentPoints.Add(ClampToWorld(playerStartPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius));
            }
        }

        foreach (CampData camp in Data.camps)
        {
            contentPoints.Add(camp.center);
            for (int i = 0; i < 12; i++)
            {
                float angle = Mathf.PI * 2f * i / 12f;
                contentPoints.Add(ClampToWorld(camp.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (camp.radius + 20f)));
            }
        }
    }

    void ApplyNoRouteGuideDirt()
    {
        if (!useNoRouteGuideDirt || Data == null) return;

        List<Vector2> contentPoints = new List<Vector2>();
        contentPoints.Add(playerStartPosition);
        AddIntroGuideProtectionPoints(contentPoints);

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

                // Big normal-dirt basin around the start so black dirt does not appear immediately.
                if (Vector2.Distance(world, playerStartPosition) < introSafeDirtRadius)
                    continue;

                bool insideMajorZoneComfort = false;
                foreach (CampData camp in Data.camps)
                {
                    float comfort = camp.radius + 26f;
                    if (Vector2.Distance(world, camp.center) < comfort)
                    {
                        insideMajorZoneComfort = true;
                        break;
                    }
                }
                if (insideMajorZoneComfort) continue;

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

        Debug.Log("DREAM_MAP_V3 guide dirt painted using " + contentPoints.Count + " content/protection points.");
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
        if (showTrueRevealDebug)
            SpawnTrueRevealDebugCircle(camp.center, camp.radius, campRevealDebugColor, "CampRevealDebug_" + camp.id);

        if (revealCoverPrefab == null) return;
        if (hideNormalRevealCoversWhileDebugging && showTrueRevealDebug) return;

        // Real camps are intentionally a big reveal patch.
        float spacing = cellSize;
        int steps = Mathf.CeilToInt(camp.radius / spacing);

        for (int x = -steps; x <= steps; x++)
        {
            for (int y = -steps; y <= steps; y++)
            {
                Vector2 offset = new Vector2(x * spacing, y * spacing);
                if (offset.magnitude > camp.radius) continue;
                if (Random.value > 0.55f) continue;

                GameObject cover = Instantiate(revealCoverPrefab, camp.center + offset, Quaternion.identity);
                RevealCover reveal = cover.GetComponent<RevealCover>();
                if (reveal != null)
                {
                    reveal.revealType = RevealCover.RevealType.Camp;
                    reveal.revealId = camp.id;
                }
            }
        }

        Debug.Log("Spawned camp cover patch id " + camp.id + " at " + camp.center);
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
        if (hideNormalRevealCoversWhileDebugging && showTrueRevealDebug) return;

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
