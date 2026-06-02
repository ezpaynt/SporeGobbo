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

    [Header("Intro Map")]
    public float introStartPitRadius = 7f;
    public int introStartMushrooms = 3;
    public int introCampCount = 3;
    public float introCampRadius = 7f;
    public int introFillerTunnelCount = 22;
    public int introFillerPocketCount = 10;
    public float introMinFeatureDistanceFromStart = 8f;
    public float introMinFeatureDistanceFromOtherFeatures = 6f;

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

        Data = new MapData(worldWidthCells, worldHeightCells, cellSize);
        Data.FillBlocked();

        nextCampId = 0;
        nextTunnelId = 0;

        RunGenerationProfile profile = forceIntroMap ? RunGenerationProfile.Intro : fallbackProfile;

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
        Data.ClearCircle(playerStartPosition, introStartPitRadius);

        for (int i = 0; i < introStartMushrooms; i++)
            SpawnAround(playerStartPosition, introStartPitRadius * 0.45f, mushroomPrefab);

        // This is the first buddy source.
        SpawnAround(playerStartPosition + Vector2.right * 1.5f, 0.35f, sporePrefab);

        // Guaranteed nearby tunnel with one normal enemy. This is intentionally close and short.
        Vector2 firstTunnelStart = playerStartPosition + Vector2.right * (introStartPitRadius + 2f);
        CreateTunnelSegment(firstTunnelStart, Vector2.right, 8f, 1.55f, true, false, 0f);

        // Three normal camp caves in the middle/near-middle of the map.
        Vector2[] campPositions =
        {
            playerStartPosition + new Vector2(18f, 11f),
            playerStartPosition + new Vector2(10f, -19f),
            playerStartPosition + new Vector2(31f, -6f)
        };

        for (int i = 0; i < introCampCount && i < campPositions.Length; i++)
        {
            CampData camp = new CampData
            {
                id = nextCampId++,
                center = ClampToWorld(campPositions[i]),
                radius = introCampRadius + Random.Range(-1f, 1.25f),
                revealed = false,
                isBossCamp = false,
                hasExitPortal = false,
                enemyCount = 3,
                bossEnemyCount = 0,
                sporeCount = 1,
                mushroomCount = Random.Range(3, 6),
                shinyCount = Random.value < 0.35f ? 1 : 0
            };

            Data.camps.Add(camp);
            SpawnCampRevealPatch(camp);
        }

        // Boss camp remains top-right for current testing. Later this can become RandomFarCornerPosition().
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
                enemyCount = 3,
                bossEnemyCount = 1,
                sporeCount = 1,
                mushroomCount = Random.Range(bossCampMushroomMin, bossCampMushroomMax + 1),
                shinyCount = bossCampShinyCount
            };

            Data.camps.Add(boss);
            SpawnCampRevealPatch(boss);
        }

        GenerateIntroFillerTunnels();
        GenerateIntroFillerPockets();
    }

    void GenerateIntroFillerTunnels()
    {
        int made = 0;
        int attempts = 0;

        while (made < introFillerTunnelCount && attempts < introFillerTunnelCount * 40)
        {
            attempts++;

            Vector2 start = PickValidFillerPosition(3f);
            if (start == Vector2.positiveInfinity) break;

            Vector2 dir = PickLooseDirectionFromStart(start);

            float roll = Random.value;
            float length;
            float radius;
            float wobble;

            if (roll < 0.55f)
            {
                // short skinny tunnel
                length = Random.Range(5f, 10f);
                radius = Random.Range(1.0f, 1.35f);
                wobble = 0.35f;
            }
            else if (roll < 0.82f)
            {
                // long skinny tunnel
                length = Random.Range(11f, 20f);
                radius = Random.Range(1.0f, 1.45f);
                wobble = 0.55f;
            }
            else
            {
                // fat short tunnel / cave-ish worm
                length = Random.Range(5f, 12f);
                radius = Random.Range(1.7f, 2.5f);
                wobble = 0.75f;
            }

            bool hasEnemy = Random.value < 0.35f;
            bool hasShiny = Random.value < 0.12f;
            CreateTunnelSegment(start, dir, length, radius, hasEnemy, false, wobble, hasShiny);
            made++;
        }
    }

    void GenerateIntroFillerPockets()
    {
        int made = 0;
        int attempts = 0;

        while (made < introFillerPocketCount && attempts < introFillerPocketCount * 40)
        {
            attempts++;

            Vector2 pos = PickValidFillerPosition(3f);
            if (pos == Vector2.positiveInfinity) break;

            float radius = Random.Range(1.8f, 3.2f);
            bool hasEnemy = Random.value < 0.2f;
            bool hasMushroom = Random.value < 0.65f;

            CreateTunnelSegment(pos, Random.insideUnitCircle.normalized, Random.Range(1.5f, 3f), radius, hasEnemy, false, 0.2f, false, hasMushroom);
            made++;
        }
    }

    Vector2 PickValidFillerPosition(float padding)
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            Vector2 pos = GetRandomWorldPosition(padding + 2f);

            if (Vector2.Distance(pos, playerStartPosition) < introMinFeatureDistanceFromStart)
                continue;

            if (IsTooCloseToCamp(pos, 1.5f))
                continue;

            bool tooCloseToTunnel = false;
            foreach (TunnelData tunnel in Data.tunnels)
            {
                foreach (Vector2 point in tunnel.points)
                {
                    if (Vector2.Distance(pos, point) < introMinFeatureDistanceFromOtherFeatures)
                    {
                        tooCloseToTunnel = true;
                        break;
                    }
                }

                if (tooCloseToTunnel) break;
            }

            if (tooCloseToTunnel) continue;

            return pos;
        }

        return Vector2.positiveInfinity;
    }

    Vector2 PickLooseDirectionFromStart(Vector2 position)
    {
        Vector2 away = (position - playerStartPosition).normalized;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle.normalized;
        Vector2 mixed = (away + Random.insideUnitCircle * 0.7f).normalized;
        if (mixed.sqrMagnitude < 0.01f) mixed = Vector2.right;
        return mixed;
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
        bool hasMushroom = false)
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
            if (IsTooCloseToCamp(current, radius + tunnelAvoidCampPadding))
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
            default: return dirtTile1;
        }
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
