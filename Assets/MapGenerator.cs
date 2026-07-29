using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        FillerPocket,
        FillerPocketLoot
    }

    public enum DirtInfluenceCategory
    {
        FungalRegion,
        DisturbedRegion,
        MineralRegion
    }

    public enum DirtInfluenceTuningMode
    {
        BarelyVisible = 0,
        Subtle = 1,
        StrongTest = 3,
        VisibleTest = 4,
        DebugObvious = 2
    }

    public enum TerrainFormationType
    {
        Stone = 0,
        Root = 1
    }

    public enum OptionalGapPolicy
    {
        Widen = 0,
        Merge = 1,
        RejectFormation = 2
    }

    public enum RootOriginType
    {
        Edge = 0,
        Stone = 1,
        Internal = 2,
        Companion = 3
    }

    [Serializable]
    public class TerrainFormation
    {
        public TerrainFormationType type;
        public Vector2Int centerCell;
        public List<Vector2Int> cells = new List<Vector2Int>();
        public List<Vector2Int> mainPathCells = new List<Vector2Int>();
        public List<Vector2Int> branchCells = new List<Vector2Int>();
        public List<Vector2Int> knotCells = new List<Vector2Int>();
        public int branchCount;
        public int removedBranchCount;
        public int knotCount;
        public int formationId;
        public List<Vector2Int> majorBranchCells = new List<Vector2Int>();
        public List<Vector2Int> minorBranchCells = new List<Vector2Int>();
        public List<Vector2Int> stoneOverlapCells = new List<Vector2Int>();
        public Vector2 dominantDirection;
        public RootOriginType rootOriginType;
        public bool isCompanion;
        public int trunkTargetLength;
    }

    [Serializable]
    private class DirtInfluenceSource
    {
        public DirtInfluenceCategory category;
        public int sourceAreaId;
        public Vector2 centerCell;
        public float sourceRadius;
        public float outerHintRadius;
        public float strength;
        public float irregularity;
        public int deterministicSeed;
        public bool qualifying;
        public bool falsePositive;
        public bool substituted;
        public bool suppressed;
    }

    private struct DirtInfluenceSample
    {
        public Color tintMultiplier;
        public float totalStrength;
        public DirtInfluenceCategory strongestCategory;
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

        [Header("Generation Bounds")]
        public int width = 180;
        public int height = 120;
        public float cellSize = 0.75f;

        [Header("Spawn")]
        public bool autoCenterSpawn = true;

        [Header("Spawn")]
        [HideInInspector] public Vector2Int spawnCenter = new Vector2Int(90, 60);
        public int spawnRadius = 5;

        [Header("Dirt Darkness")]
        public int darkDistance = 20;
        public int darkerDistance = 35;

        [Header("Filler")]
        public int fillerTunnelCount = 12;
        public int fillerPocketCount = 8;
        public int fillerLootPocketCount = 2;
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
    public Tilemap dirtTilemap;

    [Header("Tiles")]
    public TileBase dirtTile1;
    public TileBase dirtTile2;
    public TileBase dirtTile3;
    public TileBase darkDirtTile;
    public TileBase darkerDirtTile;
    public TileBase revealDirtTile;

    [Header("Dirt Environmental Influence")]
    public bool enableDirtInfluence = true;
    public DirtInfluenceTuningMode dirtInfluenceTuning = DirtInfluenceTuningMode.Subtle;
    [Range(0f, 1f)] public float qualifyingAreaInfluenceChance = 0.68f;
    [Range(0f, 1f)] public float neutralFalsePositiveChance = 0.12f;
    [Range(0f, 1f)] public float neighboringCategoryChance = 0.08f;
    [Range(0.1f, 2f)] public float minimumSourceStrength = 0.75f;
    [Range(0.1f, 2f)] public float maximumSourceStrength = 1.25f;
    [Range(0.5f, 2f)] public float minimumRadiusScale = 0.85f;
    [Range(0.5f, 2f)] public float maximumRadiusScale = 1.35f;
    [Range(0f, 1f)] public float sourceCenterOffset = 0.35f;
    [Range(0f, 0.25f)] public float maximumCombinedInfluence = 0.06f;
    [Range(0f, 1f)] public float influenceIrregularity = 0.3f;
    public Color fungalRegionTint = new Color(0.985f, 0.995f, 0.965f, 1f);
    public Color mineralRegionTint = new Color(0.995f, 1.005f, 1.02f, 1f);
    public Color disturbedRegionTint = new Color(0.97f, 0.95f, 0.945f, 1f);
    public Color outerDirtTint = new Color(0.72f, 0.76f, 0.82f, 1f);
    [Range(0f, 1f)] public float outerDirtStrength = 0f;
    [Range(0f, 1f)] public float outerVariationRetention = 1f;
    [Range(0f, 0.25f)] public float distanceTransitionNoise = 0f;

    [Header("Visible Test Material Pattern")]
    [Range(0f, 1f)] public float lightMaterialDensity = 0.16f;
    [Range(0f, 1f)] public float mediumMaterialDensity = 0.38f;
    [Range(0f, 1f)] public float heavyMaterialDensity = 0.62f;
    [Range(0f, 0.5f)] public float materialTransitionWidth = 0.12f;
    [Range(0f, 0.5f)] public float internalHoleChance = 0.12f;
    [Range(0f, 0.5f)] public float satellitePatchChance = 0.09f;
    [Range(0f, 1f)] public float fingerVeinAmount = 0.42f;
    [Range(0f, 1f)] public float materialCenterOffsetStrength = 0.35f;
    [Range(0f, 1f)] public float fungalColorStrength = 0.82f;
    [Range(0f, 1f)] public float mineralColorStrength = 0.78f;
    [Range(0f, 1f)] public float disturbedColorStrength = 0.76f;

    [Header("Terrain Formations")]
    public bool enableTerrainFormations = true;
    [Range(0, 12)] public int formationCount = 4;
    [Range(3, 20)] public int minimumFormationSize = 3;
    [Range(3, 24)] public int maximumFormationSize = 14;
    [Range(0f, 1f)] public float formationEdgeIrregularity = 0.72f;
    [Range(0f, 1f)] public float formationCornerErosion = 0.58f;
    [Range(0f, 1f)] public float detachedChunkChance = 0.28f;
    [Range(0, 8)] public int formationPlacementPadding = 1;
    [Range(1, 500)] public int formationPlacementAttempts = 350;
    public Color stoneFormationTint = new Color(0.56f, 0.58f, 0.60f, 1f);

    [Header("Root Formations")]
    public bool enableRootFormations = true;
    [Range(0, 4)] public int minimumRootCount = 1;
    [Range(0, 4)] public int maximumRootCount = 2;
    [Range(0f, 1f)] public float rootCompanionChance = 0.35f;
    [Range(0f, 1f)] public float smallRootWeight = 0.25f;
    [Range(0f, 1f)] public float mediumRootWeight = 0.50f;
    [Range(0f, 1f)] public float largeRootWeight = 0.25f;
    [Range(8, 90)] public int minimumRootMainPathLength = 16;
    [Range(16, 120)] public int maximumRootMainPathLength = 75;
    [Range(0f, 1f)] public float rootDirectionalPersistence = 0.88f;
    [Range(0f, 1f)] public float rootTurnChance = 0.10f;
    [Range(0f, 1f)] public float rootKinkChance = 0.035f;
    [Range(0f, 1f)] public float rootBranchChance = 0.12f;
    [Range(0, 2)] public int maximumRootBranchDepth = 2;
    [Range(0.15f, 0.8f)] public float rootBranchLengthRatio = 0.42f;
    [Range(1, 3)] public int minimumRootThickness = 1;
    [Range(2, 4)] public int maximumRootThickness = 3;
    [Range(0f, 1f)] public float rootKnotChance = 0.04f;
    [Range(2, 5)] public int minimumRootKnotSize = 3;
    [Range(2, 6)] public int maximumRootKnotSize = 4;
    [Range(0f, 1f)] public float rootTaperStrength = 0.72f;
    [Range(0f, 1f)] public float rootEdgeOriginPreference = 0.78f;
    [Range(0f, 1f)] public float rootThroughStoneChance = 0.30f;
    [Range(0f, 1f)] public float rootClusterChance = 0.30f;
    [Range(0f, 0.25f)] public float rootDetachedFragmentChance = 0.04f;
    [Range(0, 8)] public int rootToStoneMinimumSpacing = 2;
    [Range(0, 8)] public int rootToRootMinimumSpacing = 2;
    [Range(1, 800)] public int rootPlacementAttempts = 240;
    public Color rootFormationTint = new Color(0.38f, 0.235f, 0.16f, 1f);


    [Header("Formation Traversal Clearance")]
    public bool enableFormationClearanceValidation = true;
    [Range(0f, 0.5f)] public float baseNavigationMargin = 0.12f;
    public OptionalGapPolicy minimumOptionalGapPolicy = OptionalGapPolicy.Widen;
    [Range(0, 100)] public int maximumErosionCellsPerFormation = 18;
    public bool validateCriticalConnectivity = true;
    public bool showFormationClearanceDebug = false;
    public bool logFormationClearanceReport = false;

    [Header("Dirt Influence Debug")]
    public bool showDirtInfluenceSources = false;
    public bool showDirtInfluenceCells = false;
    public bool showInfluenceOnlyPreview = false;
    public float dirtInfluenceDebugZ = -1f;

    [Header("Debug")]
    public bool generateOnStart = true;
    public bool revealMainTunnelsAtStart = false;

    [Header("Content Spawning")]
    public RunContentSpawner contentSpawner;

    public MapData Data { get; private set; }
    [NonSerialized] public RunProfile.DevelopmentOverrides activeDevelopmentOverrides;

    private System.Random rng;

    private readonly HashSet<Vector2Int> spawnOpen = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> mainPathCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> generatedAreaCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> plannedTunnelCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> hiddenRevealCells = new HashSet<Vector2Int>();

    private readonly Dictionary<Vector2Int, PlannedArea> revealAreaByCell = new Dictionary<Vector2Int, PlannedArea>();
    private readonly Dictionary<Vector2Int, int> revealGroupByTriggerCell = new Dictionary<Vector2Int, int>();
    private readonly List<PlannedArea> plannedAreas = new List<PlannedArea>();
    private readonly List<DirtInfluenceSource> dirtInfluenceSources = new List<DirtInfluenceSource>();
    private readonly List<TerrainFormation> terrainFormations = new List<TerrainFormation>();
    private readonly HashSet<Vector2Int> terrainFormationCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> stoneFormationCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> rootStoneOverlapCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> fusedRootSafetyCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> removedRootBranchCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> rootFormationCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> trimmedRootCells = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> rejectedRootOrigins = new List<Vector2Int>();
    private int rejectedRootCount;
    private int correctedRootCount;
    private readonly HashSet<int> correctedRootIds = new HashSet<int>();
    private int unsafeRootVCount;
    private int unsafeRootPocketCount;
    private int rootSafetyFusionCount;
    private int rejectedRootOverlapCount;
    private readonly HashSet<Vector2Int> requiredTraversalCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> formationClearanceBlockedCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> invalidFormationGapCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> erodedFormationCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> clearanceReachableCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> criticalTraversalTargets = new HashSet<Vector2Int>();
    private int rejectedFormationCount;
    private int correctedFormationCount;
    private int mergedOptionalGapCount;
    private int requiredFailuresBeforeCorrection;
    private int requiredFailuresAfterCorrection;
    private float minimumMeasuredPassageWidth;

    private DirtInfluenceSample[,] dirtInfluenceField;
    private int runtimeMapSeed;
    private int tileColorUnlockAttempts;
    private int tileColorUnlockSuccesses;

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

        if (map.autoCenterSpawn)
            map.spawnCenter = new Vector2Int(map.width / 2, map.height / 2);

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

    private RunContentSpawner GetContentSpawner()
    {
        if (contentSpawner == null)
            contentSpawner = UnityEngine.Object.FindAnyObjectByType<RunContentSpawner>(FindObjectsInactive.Include);

        return contentSpawner;
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
        map.autoCenterSpawn = true;
        map.spawnCenter = new Vector2Int(map.width / 2, map.height / 2);
        map.spawnRadius = 5;
        map.darkDistance = 20;
        map.darkerDistance = 35;
        map.fillerTunnelCount = 5;
        map.fillerPocketCount = 3;
        map.fillerLootPocketCount = 2;
        map.fillerMinDistanceFromMainPath = 7;
        map.fillerMaxDistanceFromMainPath = 35;

        branches = CreateDefaultBranches();
    }

    private void ApplyAutoSpawnCenter()
    {
        if (map.autoCenterSpawn)
            map.spawnCenter = new Vector2Int(map.width / 2, map.height / 2);
    }

    [ContextMenu("Generate Map")]
    public void Generate()
    {
        ApplyProfileForThisRunIfNeeded();
        if (selectedProfile != null && !RunProfileCoordinator.ValidateAndApply(selectedProfile, this, GetContentSpawner()))
            return;
        ApplyAutoSpawnCenter();
        EnsureTilemaps();

        Data = new MapData(map.width, map.height, map.cellSize);

        if (grid != null)
        {
            grid.cellSize = new Vector3(map.cellSize, map.cellSize, 1f);
            grid.transform.position = new Vector3(Data.origin.x, Data.origin.y, 0f);
        }

        ClearTilemaps();
        ClearPlanData();

        runtimeMapSeed = map.randomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : map.seed;
        rng = new System.Random(runtimeMapSeed);

        if (branches == null || branches.Count == 0)
            branches = CreateDefaultBranches();

        Data.FillBlocked();

        BuildSpawn();
        BuildBranches();
        BuildFiller();
        ApplyDevelopmentAreaMinimums();
        EnsureExitPortalExists();
        ApplyDevelopmentContentMinimums();
        BuildDirtInfluenceField();
        PaintTilemaps();

        if (showDirtInfluenceSources || showDirtInfluenceCells || showInfluenceOnlyPreview)
            LogDirtInfluenceReport();

        RunContentSpawner spawner = GetContentSpawner();
        if (spawner != null)
        {
            spawner.ResetSpawnedContentTracking();
            spawner.SpawnInitialContent();
        }

        BuildTerrainFormations();
        ValidateAndCorrectFormationClearance();
        BuildRootFormations();
        ValidateAndCorrectFormationClearance();
        RefreshTerrainFormationTiles();

        LogGenerationSummary();
    }

    public void GenerateMap() => Generate();
    public void Regenerate() => Generate();

    private void LogGenerationSummary()
    {
        int smallRooms = 0;
        int camps = 0;
        int bosses = 0;
        int fillerPockets = 0;
        int fillerLootPockets = 0;

        foreach (PlannedArea area in plannedAreas)
        {
            switch (area.type)
            {
                case AreaType.SmallRoom: smallRooms++; break;
                case AreaType.Camp: camps++; break;
                case AreaType.Boss: bosses++; break;
                case AreaType.FillerPocket: fillerPockets++; break;
                case AreaType.FillerPocketLoot: fillerLootPockets++; break;
            }
        }

        int normalEnemies = 0;
        int bossEnemies = 0;
        int mushrooms = 0;
        int spores = 0;
        int shinies = 0;
        int exits = 0;

        if (Data != null)
        {
            foreach (CampData camp in Data.camps)
            {
                normalEnemies += camp.enemyCount;
                bossEnemies += camp.bossEnemyCount;
                mushrooms += camp.mushroomCount;
                spores += camp.sporeCount;
                shinies += camp.shinyCount;
                if (camp.hasExitPortal) exits++;
            }
        }

        string profileName = !string.IsNullOrEmpty(RunProfileCoordinator.ActiveDisplayName) ? RunProfileCoordinator.ActiveDisplayName : (selectedProfile != null ? selectedProfile.name : "Manual Inspector Settings");
        int tunnelCount = Data != null ? Data.tunnels.Count : 0;
        int campDataCount = Data != null ? Data.camps.Count : 0;
        int revealTriggers = revealGroupByTriggerCell.Count;

        Debug.Log(
            "\n========== SPORE GOBBO MAP REPORT ==========\n" +
            $"Profile: {profileName}\n" +
            $"Seed: {(map.randomSeed ? "Random" : map.seed.ToString())}\n" +
            $"Map Size: {map.width} x {map.height} | Cell Size: {map.cellSize}\n" +
            $"Spawn Cell: {map.spawnCenter} | Spawn Radius: {map.spawnRadius}\n\n" +

            "Structure\n" +
            "---------\n" +
            $"Branches: {branches.Count}\n" +
            $"Tunnels Registered: {tunnelCount}\n" +
            $"Reveal Triggers: {revealTriggers}\n" +
            $"Generated Area Cells: {generatedAreaCells.Count}\n\n" +

            "Rooms\n" +
            "-----\n" +
            $"Small Rooms: {smallRooms}\n" +
            $"Camps: {camps}\n" +
            $"Boss Rooms: {bosses}\n" +
            $"Filler Pockets: {fillerPockets}\n" +
            $"Filler Loot Pockets: {fillerLootPockets}\n" +
            $"Content Areas Registered: {campDataCount}\n\n" +

            "Planned Content\n" +
            "---------------\n" +
            $"Normal Enemies: {normalEnemies}\n" +
            $"Boss Enemies: {bossEnemies}\n" +
            $"Mushrooms: {mushrooms}\n" +
            $"Spores: {spores}\n" +
            $"Shinies: {shinies}\n" +
            $"Exit Portals: {exits}\n" +
            "===========================================\n",
            this
        );
    }

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
        map.autoCenterSpawn = profile.autoCenterSpawn;
        map.spawnCenter = profile.autoCenterSpawn ? new Vector2Int(profile.width / 2, profile.height / 2) : profile.spawnCenter;
        map.spawnRadius = profile.spawnRadius;
        map.darkDistance = profile.darkDistance;
        map.darkerDistance = profile.darkerDistance;
        map.fillerTunnelCount = profile.fillerTunnelCount;
        map.fillerPocketCount = profile.fillerPocketCount;
        map.fillerLootPocketCount = profile.fillerLootPocketCount;
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
        selectedProfile.autoCenterSpawn = map.autoCenterSpawn;
        selectedProfile.spawnCenter = map.spawnCenter;
        selectedProfile.spawnRadius = map.spawnRadius;
        selectedProfile.darkDistance = map.darkDistance;
        selectedProfile.darkerDistance = map.darkerDistance;
        selectedProfile.fillerTunnelCount = map.fillerTunnelCount;
        selectedProfile.fillerPocketCount = map.fillerPocketCount;
        selectedProfile.fillerLootPocketCount = map.fillerLootPocketCount;
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

            if (dirtTilemap == null && n.Contains("dirt"))
                dirtTilemap = tm;
        }

        if (dirtTilemap == null && maps.Length > 0)
            dirtTilemap = maps[0];
    }

    private void ClearTilemaps()
    {
        if (dirtTilemap != null) dirtTilemap.ClearAllTiles();
    }

    private void ClearPlanData()
    {
        spawnOpen.Clear();
        mainPathCells.Clear();
        generatedAreaCells.Clear();
        plannedTunnelCells.Clear();
        hiddenRevealCells.Clear();
        revealAreaByCell.Clear();
        revealGroupByTriggerCell.Clear();
        plannedAreas.Clear();
        dirtInfluenceSources.Clear();
        terrainFormations.Clear();
        terrainFormationCells.Clear();
        stoneFormationCells.Clear();
        rootStoneOverlapCells.Clear();
        fusedRootSafetyCells.Clear();
        removedRootBranchCells.Clear();
        rootFormationCells.Clear();
        trimmedRootCells.Clear();
        rejectedRootOrigins.Clear();
        rejectedRootCount = 0;
        correctedRootCount = 0;
        unsafeRootVCount = 0;
        unsafeRootPocketCount = 0;
        rootSafetyFusionCount = 0;
        rejectedRootOverlapCount = 0;
        requiredTraversalCells.Clear();
        formationClearanceBlockedCells.Clear();
        invalidFormationGapCells.Clear();
        erodedFormationCells.Clear();
        clearanceReachableCells.Clear();
        criticalTraversalTargets.Clear();
        correctedRootIds.Clear();
        rejectedFormationCount = 0;
        correctedFormationCount = 0;
        mergedOptionalGapCount = 0;
        requiredFailuresBeforeCorrection = 0;
        requiredFailuresAfterCorrection = 0;
        minimumMeasuredPassageWidth = float.PositiveInfinity;
        dirtInfluenceField = null;
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
            generatedAreaCells.Add(cell);
            Data.SetBlocked(cell, false);
        }

        Data.ClearCircle(Data.CellToWorld(map.spawnCenter), map.spawnRadius * map.cellSize);
    }

    private void BuildBranches()
    {
        foreach (BranchSettings branch in branches)
        {
            Vector2Int dir = NormalizeCardinal(branch.direction);

            if (dir == Vector2Int.zero)
            {
                dir = Vector2Int.up;
                Debug.LogWarning(
                    $"MapGenerator: Branch '{branch.branchName}' had direction (0,0). Defaulting to Up so the branch can generate. Set Direction to X/Y like (1,0), (-1,0), (0,1), or (0,-1).",
                    this
                );
            }

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

    private void ApplyDevelopmentAreaMinimums()
    {
        RunProfile.DevelopmentOverrides d = activeDevelopmentOverrides;
        if (d == null || !d.enabled) return;
        EnsureAreaMinimum(AreaType.SmallRoom, Mathf.Max(0, d.minimumOrdinaryRooms), d.maximumFallbackAttempts);
        EnsureAreaMinimum(AreaType.Camp, Mathf.Max(0, d.minimumCamps), d.maximumFallbackAttempts);
    }

    private void EnsureAreaMinimum(AreaType type, int minimum, int maximumAttempts)
    {
        int existing = plannedAreas.FindAll(a => a.type == type).Count;
        if (existing >= minimum || mainPathCells.Count == 0) return;
        List<Vector2Int> anchors = new List<Vector2Int>(mainPathCells);
        anchors.Sort((a,b) => StableHash(runtimeMapSeed, a.x, a.y).CompareTo(StableHash(runtimeMapSeed, b.x, b.y)));
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        int attempts = Mathf.Max(1, maximumAttempts);
        for (int i = 0; i < attempts && existing < minimum; i++)
        {
            Vector2Int anchor = anchors[i % anchors.Count];
            Vector2Int direction = directions[Mathf.Abs(StableHash(runtimeMapSeed, (int)type, i)) % directions.Length];
            int radius = GetRadiusCellsForArea(type);
            Vector2Int center = anchor + direction * (radius + 4);
            if (!Data.InBounds(center) || Vector2Int.Distance(center, map.spawnCenter) < map.spawnRadius + radius + 3) continue;
            if (IsTooCloseToExistingArea(center, radius + 3)) continue;
            AddTunnel(StraightPath(anchor, center - direction * (radius + 1)), 1, AreaType.MainTunnel, false);
            AddRevealArea(type, center, radius);
            existing++;
        }
        if (existing < minimum) Debug.LogError("RUN PROFILE MINIMUM UNMET | area=" + type + " required=" + minimum + " actual=" + existing, this);
    }

    private void ApplyDevelopmentContentMinimums()
    {
        RunProfile.DevelopmentOverrides d = activeDevelopmentOverrides;
        if (d == null || !d.enabled || Data == null || Data.camps.Count == 0) return;
        List<CampData> eligible = Data.camps.FindAll(c => !c.isBossCamp);
        if (eligible.Count == 0) eligible.AddRange(Data.camps);
        int mushrooms=0, spores=0, shinies=0;
        foreach (CampData c in Data.camps) { mushrooms += c.mushroomCount; spores += c.sporeCount; shinies += c.shinyCount; }
        int index=0;
        while (mushrooms < Mathf.Max(d.minimumMushrooms, d.minimumMushroomCritters)) { eligible[index++ % eligible.Count].mushroomCount++; mushrooms++; }
        index=0; while (spores < d.minimumSpores) { eligible[index++ % eligible.Count].sporeCount++; spores++; }
        index=0; while (shinies < d.minimumShinies) { eligible[index++ % eligible.Count].shinyCount++; shinies++; }
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

        for (int i = 0; i < map.fillerLootPocketCount; i++)
        {
            Vector2Int pos = FindFillerPosition();
            if (pos == map.spawnCenter) continue;

            int radius = rng.Next(2, 4);
            AddRevealArea(AreaType.FillerPocketLoot, pos, radius);
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
                generatedAreaCells.Add(cell);

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
            generatedAreaCells.Add(cell);
            revealAreaByCell[cell] = area;
            area.cells.Add(cell);
        }

        plannedAreas.Add(area);

        if (ShouldCreateContentData(type))
        {
            CampData camp = CreateContentDataForArea(type, area);
            Data.camps.Add(camp);
        }

    }

    private bool ShouldCreateContentData(AreaType type)
    {
        return type == AreaType.SmallRoom ||
               type == AreaType.Camp ||
               type == AreaType.Boss ||
               type == AreaType.FillerPocketLoot;
    }

    private CampData CreateContentDataForArea(AreaType type, PlannedArea area)
    {
        CampData camp = new CampData
        {
            id = area.id,
            center = Data.CellToWorld(area.centerCell),
            radius = area.radiusWorld,
            revealed = false,
            isBossCamp = type == AreaType.Boss,
            hasExitPortal = type == AreaType.Boss,
            enemyCount = 0,
            bossEnemyCount = 0,
            sporeCount = 0,
            mushroomCount = 0,
            shinyCount = 0
        };

        switch (type)
        {
            case AreaType.SmallRoom:
                camp.enemyCount = rng.Next(0, 2);
                if (rng.NextDouble() < 0.5)
                    camp.mushroomCount = 1;
                else
                    camp.sporeCount = 1;
                break;

            case AreaType.Camp:
                camp.enemyCount = 3;
                camp.mushroomCount = 3;
                camp.sporeCount = rng.Next(1, 3);
                break;

            case AreaType.Boss:
                camp.enemyCount = 2;
                camp.bossEnemyCount = 1;
                camp.hasExitPortal = true;
                camp.mushroomCount = 5;
                camp.sporeCount = 1;
                break;

            case AreaType.FillerPocketLoot:
                camp.mushroomCount = rng.Next(1, 3);
                break;
        }

        return camp;
    }

    private void EnsureExitPortalExists()
    {
        if (Data == null) return;

        foreach (CampData camp in Data.camps)
        {
            if (camp.hasExitPortal)
                return;
        }

        // If a boss room was created but somehow lost its portal flag, fix that first.
        foreach (CampData camp in Data.camps)
        {
            if (camp.isBossCamp || camp.bossEnemyCount > 0)
            {
                camp.isBossCamp = true;
                camp.hasExitPortal = true;
                if (camp.bossEnemyCount <= 0) camp.bossEnemyCount = 1;
                Debug.LogWarning($"MapGenerator repaired missing exit portal on existing boss camp id:{camp.id}.", this);
                return;
            }
        }

        bool created = TryCreateFallbackBossExitRoom();

        if (!created)
        {
            Debug.LogError("MapGenerator could not create a fallback exit portal room. Check map bounds/spawn settings.", this);
            return;
        }

        Debug.LogWarning("MapGenerator created a fallback boss exit room because normal boss placement failed.", this);
    }

    private bool TryCreateFallbackBossExitRoom()
    {
        if (Data == null) return false;

        List<Vector2Int> candidates = new List<Vector2Int>(mainPathCells);
        if (candidates.Count == 0)
            candidates.Add(map.spawnCenter);

        candidates.Sort((a, b) =>
            Vector2Int.Distance(b, map.spawnCenter).CompareTo(Vector2Int.Distance(a, map.spawnCenter)));

        int radiusCells = 8;
        int connectorLength = 4;

        foreach (Vector2Int attachPoint in candidates)
        {
            Vector2Int away = NormalizeCardinal(attachPoint - map.spawnCenter);
            if (away == Vector2Int.zero) away = Vector2Int.up;

            Vector2Int left = new Vector2Int(-away.y, away.x);
            Vector2Int right = new Vector2Int(away.y, -away.x);

            Vector2Int[] dirs = new Vector2Int[]
            {
                left,
                right,
                away,
                -away,
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            foreach (Vector2Int dir in dirs)
            {
                if (dir == Vector2Int.zero) continue;

                Vector2Int connectorEnd = attachPoint + dir * connectorLength;
                Vector2Int roomCenter = connectorEnd + dir * (radiusCells + 1);

                if (!CircleFitsInBounds(roomCenter, radiusCells))
                    continue;

                AddTunnel(StraightPath(attachPoint, connectorEnd), 1, AreaType.MainTunnel, false);
                AddRevealArea(AreaType.Boss, roomCenter, radiusCells);
                return true;
            }
        }

        // Last resort: put the exit room in-bounds near the farthest reachable path cell.
        Vector2Int fallback = candidates[0];
        fallback.x = Mathf.Clamp(fallback.x, radiusCells + 2, map.width - radiusCells - 3);
        fallback.y = Mathf.Clamp(fallback.y, radiusCells + 2, map.height - radiusCells - 3);

        if (!CircleFitsInBounds(fallback, radiusCells))
            return false;

        AddRevealArea(AreaType.Boss, fallback, radiusCells);
        return true;
    }

    private bool CircleFitsInBounds(Vector2Int center, int radiusCells)
    {
        if (Data == null) return false;

        return center.x - radiusCells >= 1 &&
               center.y - radiusCells >= 1 &&
               center.x + radiusCells < Data.width - 1 &&
               center.y + radiusCells < Data.height - 1;
    }

    private void PaintTilemaps()
    {
        if (dirtTilemap == null)
        {
            Debug.LogError("MapGenerator needs a Dirt Tilemap assigned.");
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
        if (terrainFormationCells.Contains(cell)) return;

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

        RunContentSpawner spawner = GetContentSpawner();
        if (spawner != null)
            spawner.SpawnTunnel(tunnel);
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

                RunContentSpawner spawner = GetContentSpawner();
                if (spawner != null)
                    spawner.SpawnCamp(camp);

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
        return IsDirt(cell) && !terrainFormationCells.Contains(cell);
    }

    public bool IsTerrainFormationCell(Vector2Int cell)
    {
        return terrainFormationCells.Contains(cell);
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
        if (dirtTilemap == null || Data == null || !Data.InBounds(cell))
            return;

        Vector3Int pos = ToTilePos(cell);

        if (Data.IsBlocked(cell))
        {
            if (revealGroupByTriggerCell.ContainsKey(cell) && revealDirtTile != null)
                dirtTilemap.SetTile(pos, revealDirtTile);
            else
                dirtTilemap.SetTile(pos, GetDirtTileByDistance(cell));

            dirtTilemap.RemoveTileFlags(pos, TileFlags.LockColor);
            tileColorUnlockAttempts++;
            if ((dirtTilemap.GetTileFlags(pos) & TileFlags.LockColor) == 0)
                tileColorUnlockSuccesses++;
            Color presentationTint = rootFormationCells.Contains(cell) ? rootFormationTint :
                terrainFormationCells.Contains(cell) ? stoneFormationTint : GetDirtInfluenceTint(cell);
            dirtTilemap.SetColor(pos, MultiplyTint(presentationTint, GetDirtDistanceTint(cell)));
        }
        else
        {
            // Revealed/open space is now just empty dirt, so the background art shows through.
            dirtTilemap.SetTile(pos, null);
            dirtTilemap.SetColor(pos, Color.white);
        }
    }

    private void RefreshTerrainFormationTiles()
    {
        foreach (Vector2Int cell in terrainFormationCells)
            SetCellTilesFromData(cell);
    }

    private void BuildTerrainFormations()
    {
        terrainFormations.Clear();
        terrainFormationCells.Clear();
        stoneFormationCells.Clear();
        if (!enableTerrainFormations || Data == null || formationCount <= 0) return;

        int minimumSize = Mathf.Clamp(Mathf.Min(minimumFormationSize, maximumFormationSize), 3, 24);
        int maximumSize = Mathf.Clamp(Mathf.Max(minimumFormationSize, maximumFormationSize), minimumSize, 24);
        System.Random formationRng = new System.Random(StableHash(runtimeMapSeed, 2017));
        HashSet<Vector2Int> reserved = BuildFormationReservedCells();
        int attemptsPerFormation = Mathf.Max(1, formationPlacementAttempts);

        for (int formationIndex = 0; formationIndex < formationCount; formationIndex++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < attemptsPerFormation && !placed; attempt++)
            {
                int span = ChooseFormationSpan(formationIndex, minimumSize, maximumSize, formationRng);
                int margin = Mathf.Max(2, span / 2 + 2);
                if (Data.width <= margin * 2 || Data.height <= margin * 2) break;

                Vector2Int center = new Vector2Int(
                    formationRng.Next(margin, Data.width - margin),
                    formationRng.Next(margin, Data.height - margin));
                int footprintSeed = StableHash(runtimeMapSeed, formationIndex, attempt, center.x, center.y, span);
                List<Vector2Int> footprint = BuildFracturedFormationFootprint(center, span, footprintSeed);
                footprint = ClipFormationFootprintToTopology(footprint, reserved, footprintSeed);
                int minimumFootprint = span <= 5 ? 5 : span * 2;
                if (footprint.Count < minimumFootprint || !CanPlaceFormation(footprint, reserved)) continue;

                TerrainFormation formation = new TerrainFormation
                {
                    type = TerrainFormationType.Stone,
                    centerCell = center,
                    cells = footprint
                };
                terrainFormations.Add(formation);
                foreach (Vector2Int cell in footprint)
                {
                    terrainFormationCells.Add(cell);
                    stoneFormationCells.Add(cell);
                    AddFormationReservedCell(reserved, cell, Mathf.Max(1, formationPlacementPadding));
                }
                placed = true;
            }
        }
    }

    private void BuildRootFormations()
    {
        rootFormationCells.Clear();
        rootStoneOverlapCells.Clear();
        if (!enableRootFormations || Data == null) return;

        int minCount = Mathf.Max(0, Mathf.Min(minimumRootCount, maximumRootCount));
        int maxCount = Mathf.Max(minCount, Mathf.Max(minimumRootCount, maximumRootCount));
        System.Random familyRng = new System.Random(StableHash(runtimeMapSeed, 9049));
        int majorCount = familyRng.Next(minCount, maxCount + 1);
        int companionCount = familyRng.NextDouble() < rootCompanionChance ? 1 : 0;
        HashSet<Vector2Int> reserved = BuildFormationReservedCells();

        for (int rootIndex = 0; rootIndex < majorCount + companionCount; rootIndex++)
        {
            bool companion = rootIndex >= majorCount;
            bool accepted = false;
            for (int attempt = 0; attempt < Mathf.Max(1, rootPlacementAttempts) && !accepted; attempt++)
            {
                int seed = StableHash(runtimeMapSeed, 9049, rootIndex, attempt);
                System.Random local = new System.Random(seed);
                RootOriginType originType;
                Vector2Int origin = ChooseRootOrigin(rootIndex, companion, local, reserved, out originType);
                bool throughStone = !companion && local.NextDouble() < rootThroughStoneChance && stoneFormationCells.Count > 0;
                Vector2 direction = ChooseRootDirection(origin, originType, throughStone, local);
                int targetLength = ChooseRootMainPathLength(seed);
                if (companion) targetLength = Mathf.Max(12, Mathf.RoundToInt(targetLength * 0.48f));

                TerrainFormation root = GrowDirectionalRoot(rootIndex, origin, direction, targetLength,
                    seed, originType, companion, throughStone, reserved);
                int minimumAccepted = companion ? 10 : Mathf.Max(18, targetLength / 2);
                if (root.mainPathCells.Count < minimumAccepted || !IsRootCoherent(root))
                {
                    rejectedRootOrigins.Add(origin);
                    if (throughStone) rejectedRootOverlapCount++;
                    continue;
                }

                terrainFormations.Add(root);
                foreach (Vector2Int cell in root.cells)
                {
                    terrainFormationCells.Add(cell);
                    rootFormationCells.Add(cell);
                    if (stoneFormationCells.Contains(cell))
                    {
                        rootStoneOverlapCells.Add(cell);
                        root.stoneOverlapCells.Add(cell);
                    }
                }
                accepted = true;
            }
            if (!accepted) rejectedRootCount++;
        }

        CorrectRootInteriorTraps(reserved);
    }

    private int ChooseRootMainPathLength(int seed)
    {
        System.Random local = new System.Random(seed);
        float total = Mathf.Max(0.001f, smallRootWeight + mediumRootWeight + largeRootWeight);
        float roll = (float)local.NextDouble() * total;
        int configuredMin = Mathf.Min(minimumRootMainPathLength, maximumRootMainPathLength);
        int configuredMax = Mathf.Max(minimumRootMainPathLength, maximumRootMainPathLength);
        if (roll < smallRootWeight)
            return local.Next(Mathf.Max(configuredMin, 16), Mathf.Min(configuredMax, 28) + 1);
        if (roll < smallRootWeight + mediumRootWeight)
            return local.Next(Mathf.Max(configuredMin, 28), Mathf.Min(configuredMax, 48) + 1);
        int largeMin = Mathf.Max(configuredMin, 45);
        int largeMax = Mathf.Max(largeMin, Mathf.Min(configuredMax, 75));
        return local.Next(largeMin, largeMax + 1);
    }

    private Vector2Int ChooseRootOrigin(int rootIndex, bool companion, System.Random local,
        HashSet<Vector2Int> reserved, out RootOriginType type)
    {
        if (companion && terrainFormations.Exists(f => f.type == TerrainFormationType.Root))
        {
            TerrainFormation parent = terrainFormations.FindLast(f => f.type == TerrainFormationType.Root);
            Vector2Int anchor = parent.mainPathCells[parent.mainPathCells.Count / 3];
            type = RootOriginType.Companion;
            return ClampRootCell(anchor + new Vector2Int(local.Next(-4, 5), local.Next(-4, 5)), 1);
        }
        if (stoneFormationCells.Count > 0 && local.NextDouble() > rootEdgeOriginPreference)
        {
            List<Vector2Int> stones = new List<Vector2Int>(stoneFormationCells);
            type = RootOriginType.Stone;
            return stones[local.Next(stones.Count)];
        }
        if (local.NextDouble() < rootEdgeOriginPreference)
        {
            type = RootOriginType.Edge;
            for (int sample = 0; sample < 40; sample++)
            {
                int edge = (rootIndex + sample + local.Next(4)) % 4;
                Vector2Int candidate;
                if (edge == 0) candidate = new Vector2Int(1, local.Next(2, Data.height - 2));
                else if (edge == 1) candidate = new Vector2Int(Data.width - 2, local.Next(2, Data.height - 2));
                else if (edge == 2) candidate = new Vector2Int(local.Next(2, Data.width - 2), 1);
                else candidate = new Vector2Int(local.Next(2, Data.width - 2), Data.height - 2);
                if (Data.IsBlocked(candidate) && !reserved.Contains(candidate)) return candidate;
            }
        }
        type = RootOriginType.Internal;
        for (int sample = 0; sample < 40; sample++)
        {
            Vector2Int candidate = new Vector2Int(local.Next(3, Data.width - 3), local.Next(3, Data.height - 3));
            if (Data.IsBlocked(candidate) && !reserved.Contains(candidate)) return candidate;
        }
        return new Vector2Int(Data.width / 2, Data.height / 2);
    }


    private Vector2 ChooseRootDirection(Vector2Int origin, RootOriginType type, bool throughStone, System.Random local)
    {
        Vector2 target = new Vector2(Data.width * 0.5f, Data.height * 0.5f);
        if (throughStone && stoneFormationCells.Count > 0)
        {
            float best = float.MaxValue;
            foreach (Vector2Int stone in stoneFormationCells)
            {
                float score = (stone - origin).sqrMagnitude + Stable01(runtimeMapSeed, stone.x, stone.y) * 24f;
                if (score < best && score > 16f) { best = score; target = stone; }
            }
        }
        else if (type == RootOriginType.Internal || type == RootOriginType.Companion)
        {
            target = new Vector2(local.Next(2, Data.width - 2), local.Next(2, Data.height - 2));
        }
        Vector2 direction = (target - (Vector2)origin).normalized;
        float angleJitter = Mathf.Lerp(-18f, 18f, (float)local.NextDouble());
        return RotateRootDirection(direction, angleJitter).normalized;
    }

    private TerrainFormation GrowDirectionalRoot(int rootId, Vector2Int origin, Vector2 initialDirection,
        int targetLength, int seed, RootOriginType originType, bool companion, bool throughStone,
        HashSet<Vector2Int> reserved)
    {
        TerrainFormation root = new TerrainFormation
        {
            type = TerrainFormationType.Root,
            formationId = rootId,
            centerCell = origin,
            dominantDirection = initialDirection,
            rootOriginType = originType,
            isCompanion = companion,
            trunkTargetLength = targetLength
        };
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        System.Random local = new System.Random(seed);
        Vector2 position = origin;
        Vector2 heading = initialDirection.normalized;
        int branchCooldown = 6;

        for (int step = 0; step < targetLength; step++)
        {
            Vector2Int cell = Vector2Int.RoundToInt(position);
            if (!TryResolveRootStep(cell, heading, throughStone, reserved, out cell, out heading)) break;
            if (!cells.Contains(cell))
            {
                root.mainPathCells.Add(cell);
                AddDirectionalRootSection(root, cells, cell, heading, step, targetLength, 0, local, reserved, throughStone);
            }

            bool meaningfulBranch = step >= 8 && step <= targetLength - 8 && branchCooldown <= 0 &&
                (Mathf.Abs(step - targetLength / 3) <= 1 ||
                 Mathf.Abs(step - targetLength * 2 / 3) <= 1 ||
                 local.NextDouble() < rootBranchChance);
            if (meaningfulBranch)
            {
                float side = local.Next(2) == 0 ? -1f : 1f;
                float branchAngle = side * Mathf.Lerp(38f, 68f, (float)local.NextDouble());
                int branchLength = Mathf.Max(5, Mathf.RoundToInt((targetLength - step) * rootBranchLengthRatio));
                GrowHierarchicalBranch(root, cells, cell, RotateRootDirection(heading, branchAngle), branchLength,
                    1, StableHash(seed, step, root.branchCount), reserved, throughStone);
                branchCooldown = local.Next(7, 13);
                AddRootKnot(root, cells, cell, heading, local, reserved, throughStone);
            }
            branchCooldown--;

            double motionRoll = local.NextDouble();
            if (motionRoll > rootDirectionalPersistence)
            {
                float maxAngle = motionRoll < rootDirectionalPersistence + rootKinkChance ? 42f : 18f;
                heading = RotateRootDirection(heading, Mathf.Lerp(-maxAngle, maxAngle, (float)local.NextDouble())).normalized;
            }
            else if (local.NextDouble() < rootTurnChance)
            {
                heading = RotateRootDirection(heading, Mathf.Lerp(-10f, 10f, (float)local.NextDouble())).normalized;
            }
            position = cell + heading;
        }

        root.cells = new List<Vector2Int>(cells);
        root.cells.Sort(CompareFormationCells);
        return root;
    }

    private void GrowHierarchicalBranch(TerrainFormation root, HashSet<Vector2Int> cells, Vector2Int origin,
        Vector2 heading, int length, int depth, int seed, HashSet<Vector2Int> reserved, bool throughStone)
    {
        if (depth > maximumRootBranchDepth || length < 3) return;
        System.Random local = new System.Random(seed);
        Vector2 position = origin + heading.normalized;
        int added = 0;
        for (int step = 0; step < length; step++)
        {
            Vector2Int cell = Vector2Int.RoundToInt(position);
            if (!TryResolveRootStep(cell, heading, throughStone, reserved, out cell, out heading)) break;
            if (cells.Add(cell))
            {
                root.branchCells.Add(cell);
                if (depth == 1) root.majorBranchCells.Add(cell); else root.minorBranchCells.Add(cell);
                AddDirectionalRootSection(root, cells, cell, heading, step, length, depth, local, reserved, throughStone);
                added++;
            }
            if (depth < maximumRootBranchDepth && step > 5 && step < length - 4 &&
                (Mathf.Abs(step - length / 2) <= 1 ||
                 local.NextDouble() < rootBranchChance * 0.45f))
            {
                float angle = (local.Next(2) == 0 ? -1f : 1f) * Mathf.Lerp(32f, 55f, (float)local.NextDouble());
                GrowHierarchicalBranch(root, cells, cell, RotateRootDirection(heading, angle),
                    Mathf.Max(3, Mathf.RoundToInt((length - step) * rootBranchLengthRatio)), depth + 1,
                    StableHash(seed, step, depth), reserved, throughStone);
            }
            if (local.NextDouble() > rootDirectionalPersistence)
                heading = RotateRootDirection(heading, Mathf.Lerp(-16f, 16f, (float)local.NextDouble())).normalized;
            position = cell + heading;
        }
        if (added > 2) root.branchCount++;
    }

    private void AddDirectionalRootSection(TerrainFormation root, HashSet<Vector2Int> cells, Vector2Int center,
        Vector2 heading, int step, int length, int depth, System.Random local, HashSet<Vector2Int> reserved,
        bool throughStone)
    {
        cells.Add(center);
        float progress = length > 1 ? step / (float)(length - 1) : 1f;
        bool thickBase = depth == 0 && progress < 0.22f && maximumRootThickness >= 3 && local.NextDouble() < 0.72;
        int baseThickness = depth == 0 ? Mathf.Min(thickBase ? 3 : 2, maximumRootThickness) : depth == 1 ? 2 : 1;
        int thickness = progress > rootTaperStrength ? 1 : Mathf.Clamp(baseThickness, minimumRootThickness, maximumRootThickness);
        Vector2 perpendicular = new Vector2(-heading.y, heading.x).normalized;
        for (int offset = 1; offset < thickness; offset++)
        {
            float signed = offset % 2 == 0 ? -(offset + 1) / 2f : (offset + 1) / 2f;
            Vector2Int side = Vector2Int.RoundToInt((Vector2)center + perpendicular * signed);
            if (CanOccupyRootCell(side, throughStone, reserved)) cells.Add(side);
        }
        if (depth == 0 && step > 5 && step < length - 5 && local.NextDouble() < rootKnotChance)
            AddRootKnot(root, cells, center, heading, local, reserved, throughStone);
    }

    private void AddRootKnot(TerrainFormation root, HashSet<Vector2Int> cells, Vector2Int center, Vector2 heading,
        System.Random local, HashSet<Vector2Int> reserved, bool throughStone)
    {
        root.knotCount++;
        int diameter = local.Next(Mathf.Min(minimumRootKnotSize, maximumRootKnotSize),
            Mathf.Max(minimumRootKnotSize, maximumRootKnotSize) + 1);
        int radius = Mathf.Max(1, diameter / 2);
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radius * radius + local.Next(0, 2)) continue;
                Vector2Int cell = center + new Vector2Int(x, y);
                if (!CanOccupyRootCell(cell, throughStone, reserved)) continue;
                cells.Add(cell);
                root.knotCells.Add(cell);
            }
    }

    private bool TryResolveRootStep(Vector2Int requested, Vector2 heading, bool throughStone,
        HashSet<Vector2Int> reserved, out Vector2Int resolved, out Vector2 resolvedHeading)
    {
        float[] redirects = { 0f, -12f, 12f, -24f, 24f, -38f, 38f };
        foreach (float angle in redirects)
        {
            Vector2 candidateHeading = RotateRootDirection(heading, angle).normalized;
            Vector2Int candidate = requested;
            if (angle != 0f) candidate = Vector2Int.RoundToInt((Vector2)requested + candidateHeading);
            if (!CanOccupyRootCell(candidate, throughStone, reserved)) continue;
            resolved = candidate;
            resolvedHeading = candidateHeading;
            return true;
        }
        resolved = requested;
        resolvedHeading = heading;
        return false;
    }

    private bool CanOccupyRootCell(Vector2Int cell, bool throughStone, HashSet<Vector2Int> reserved)
    {
        if (!Data.InBounds(cell) || !Data.IsBlocked(cell) || reserved.Contains(cell)) return false;
        if (rootFormationCells.Contains(cell) || IsNearFormationCells(cell, rootFormationCells, rootToRootMinimumSpacing)) return false;
        if (stoneFormationCells.Contains(cell)) return throughStone;
        if (!throughStone && IsNearFormationCells(cell, stoneFormationCells, rootToStoneMinimumSpacing)) return false;
        return true;
    }

    private void CorrectRootInteriorTraps(HashSet<Vector2Int> reserved)
    {
        List<Vector2Int> fuse = new List<Vector2Int>();
        for (int x = 1; x < Data.width - 1; x++)
            for (int y = 1; y < Data.height - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (terrainFormationCells.Contains(cell) || reserved.Contains(cell) || !Data.IsBlocked(cell)) continue;
                int cardinal = 0, diagonal = 0, rootAdjacent = 0;
                Vector2Int[] cardinals = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
                Vector2Int[] diagonals = { new Vector2Int(1,1), new Vector2Int(-1,1), new Vector2Int(1,-1), new Vector2Int(-1,-1) };
                foreach (Vector2Int d in cardinals)
                {
                    if (rootFormationCells.Contains(cell + d)) rootAdjacent++;
                    if (rootFormationCells.Contains(cell + d) || stoneFormationCells.Contains(cell + d)) cardinal++;
                }
                foreach (Vector2Int d in diagonals)
                {
                    if (rootFormationCells.Contains(cell + d)) rootAdjacent++;
                    if (rootFormationCells.Contains(cell + d) || stoneFormationCells.Contains(cell + d)) diagonal++;
                }
                if (rootAdjacent == 0) continue;
                if (cardinal >= 3) { unsafeRootPocketCount++; fuse.Add(cell); }
                else if (cardinal >= 2 && diagonal >= 2) { unsafeRootVCount++; fuse.Add(cell); }
            }
        foreach (Vector2Int cell in fuse)
        {
            TerrainFormation nearest = FindNearestRootFormation(cell);
            if (nearest == null) continue;
            terrainFormationCells.Add(cell);
            rootFormationCells.Add(cell);
            fusedRootSafetyCells.Add(cell);
            rootSafetyFusionCount++;
            if (!nearest.cells.Contains(cell)) nearest.cells.Add(cell);
        }
    }

    private TerrainFormation FindNearestRootFormation(Vector2Int cell)
    {
        TerrainFormation best = null;
        float distance = float.MaxValue;
        foreach (TerrainFormation root in terrainFormations)
        {
            if (root.type != TerrainFormationType.Root) continue;
            float candidate = (root.centerCell - cell).sqrMagnitude;
            if (candidate < distance) { distance = candidate; best = root; }
        }
        return best;
    }

    private Vector2Int ClampRootCell(Vector2Int cell, int margin)
    {
        return new Vector2Int(Mathf.Clamp(cell.x, margin, Data.width - margin - 1),
            Mathf.Clamp(cell.y, margin, Data.height - margin - 1));
    }

    private static Vector2 RotateRootDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine);
    }

    private bool IsNearFormationCells(Vector2Int cell, HashSet<Vector2Int> occupied, int distance)
    {
        int radius = Mathf.Max(0, distance);
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                if (occupied.Contains(cell + new Vector2Int(x, y))) return true;
        return false;
    }

    private bool IsRootCoherent(TerrainFormation root)
    {
        if (root.cells.Count == 0) return false;
        HashSet<Vector2Int> all = new HashSet<Vector2Int>(root.cells);
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> open = new Queue<Vector2Int>();
        open.Enqueue(root.cells[0]); visited.Add(root.cells[0]);
        Vector2Int[] neighbors = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();
            foreach (Vector2Int direction in neighbors)
            {
                Vector2Int next = current + direction;
                if (all.Contains(next) && visited.Add(next)) open.Enqueue(next);
            }
        }
        return visited.Count == all.Count;
    }

    private static int CompareFormationCells(Vector2Int a, Vector2Int b)
    {
        int x = a.x.CompareTo(b.x);
        return x != 0 ? x : a.y.CompareTo(b.y);
    }

    private int ChooseFormationSpan(int formationIndex, int minimumSize, int maximumSize,
                                    System.Random formationRng)
    {
        int smallMax = Mathf.Min(maximumSize, 5);
        int mediumMin = Mathf.Max(minimumSize, 6);
        int mediumMax = Mathf.Min(maximumSize, 10);
        int largeMin = Mathf.Max(minimumSize, 12);

        int sizeClass = formationIndex % 3;
        if (sizeClass == 0 && minimumSize <= smallMax)
            return formationRng.Next(minimumSize, smallMax + 1);
        if (sizeClass == 1 && mediumMin <= mediumMax)
            return formationRng.Next(mediumMin, mediumMax + 1);
        if (sizeClass == 2 && largeMin <= maximumSize)
            return formationRng.Next(largeMin, maximumSize + 1);

        return formationRng.Next(minimumSize, maximumSize + 1);
    }

    private HashSet<Vector2Int> BuildFormationReservedCells()
    {
        HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();
        int padding = Mathf.Max(0, formationPlacementPadding);

        for (int x = 0; x < Data.width; x++)
        {
            for (int y = 0; y < Data.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!Data.IsBlocked(cell))
                    AddFormationReservedCell(reserved, cell, padding);
            }
        }

        foreach (Vector2Int trigger in revealGroupByTriggerCell.Keys)
            AddFormationReservedCell(reserved, trigger, padding + 1);
        foreach (Vector2Int cell in plannedTunnelCells)
            AddFormationReservedCell(reserved, cell, padding);
        foreach (Vector2Int cell in hiddenRevealCells)
            AddFormationReservedCell(reserved, cell, padding + 1);
        foreach (Vector2Int cell in revealAreaByCell.Keys)
            AddFormationReservedCell(reserved, cell, padding);
        foreach (PlannedArea area in plannedAreas)
        {
            foreach (Vector2Int cell in area.cells)
                AddFormationReservedCell(reserved, cell, padding);
        }
        foreach (Vector2Int cell in spawnOpen)
            AddFormationReservedCell(reserved, cell, padding + 1);

        return reserved;
    }

    private void AddFormationReservedCell(HashSet<Vector2Int> reserved, Vector2Int center, int padding)
    {
        for (int x = -padding; x <= padding; x++)
        {
            for (int y = -padding; y <= padding; y++)
                reserved.Add(center + new Vector2Int(x, y));
        }
    }

    private List<Vector2Int> BuildFracturedFormationFootprint(Vector2Int center, int span, int seed)
    {
        System.Random shapeRng = new System.Random(seed);
        HashSet<Vector2Int> localCells = new HashSet<Vector2Int>();
        int halfWidth = Mathf.Max(1, span / 2);
        int halfHeight = Mathf.Max(1, Mathf.RoundToInt(span * Mathf.Lerp(0.55f, 1f,
            (float)shapeRng.NextDouble())) / 2);
        int walkerCount = span >= 12 ? 5 : span >= 6 ? 3 : 2;
        int stepsPerWalker = Mathf.Max(4, span * 2 + shapeRng.Next(span + 1));
        Vector2Int[] directions =
        {
            Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
        };

        for (int walker = 0; walker < walkerCount; walker++)
        {
            Vector2Int cursor = new Vector2Int(shapeRng.Next(-1, 2), shapeRng.Next(-1, 2));
            Vector2Int direction = directions[shapeRng.Next(directions.Length)];

            for (int step = 0; step < stepsPerWalker; step++)
            {
                int thickness = span >= 11 && shapeRng.NextDouble() < 0.48 ? 2 : 1;
                AddFracturedBrush(localCells, cursor, thickness, halfWidth, halfHeight, shapeRng);

                if (shapeRng.NextDouble() < Mathf.Lerp(0.22f, 0.62f, formationEdgeIrregularity))
                    direction = directions[shapeRng.Next(directions.Length)];

                Vector2Int next = cursor + direction;
                if (Mathf.Abs(next.x) > halfWidth || Mathf.Abs(next.y) > halfHeight)
                {
                    direction = directions[shapeRng.Next(directions.Length)];
                    next = cursor + direction;
                }
                if (Mathf.Abs(next.x) <= halfWidth && Mathf.Abs(next.y) <= halfHeight)
                    cursor = next;
            }
        }

        if (shapeRng.NextDouble() < detachedChunkChance)
        {
            Vector2Int chunkDirection = directions[shapeRng.Next(directions.Length)];
            Vector2Int chunkCenter = chunkDirection * (Mathf.Max(halfWidth, halfHeight) + 2);
            int chunkSize = span >= 10 ? 2 : 1;
            AddFracturedBrush(localCells, chunkCenter, chunkSize, halfWidth + 4, halfHeight + 4, shapeRng);
        }

        List<Vector2Int> footprint = new List<Vector2Int>(localCells.Count);
        foreach (Vector2Int local in localCells)
            footprint.Add(center + local);
        return footprint;
    }

    private void AddFracturedBrush(HashSet<Vector2Int> cells, Vector2Int center, int thickness,
                                   int halfWidth, int halfHeight, System.Random shapeRng)
    {
        for (int x = -thickness; x <= thickness; x++)
        {
            for (int y = -thickness; y <= thickness; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) > thickness + 1) continue;
                Vector2Int candidate = center + new Vector2Int(x, y);
                if (Mathf.Abs(candidate.x) > halfWidth || Mathf.Abs(candidate.y) > halfHeight) continue;

                bool nearCorner = Mathf.Abs(candidate.x) >= halfWidth - 1 &&
                                  Mathf.Abs(candidate.y) >= halfHeight - 1;
                if (nearCorner && shapeRng.NextDouble() < formationCornerErosion) continue;
                bool fringe = Mathf.Abs(candidate.x) == halfWidth || Mathf.Abs(candidate.y) == halfHeight;
                if (fringe && shapeRng.NextDouble() < formationEdgeIrregularity * 0.55f) continue;
                cells.Add(candidate);
            }
        }
    }

    private bool CanPlaceFormation(List<Vector2Int> footprint, HashSet<Vector2Int> reserved)
    {
        if (footprint == null || footprint.Count < 5) return false;
        foreach (Vector2Int cell in footprint)
        {
            if (!Data.InBounds(cell) || !Data.IsBlocked(cell) || reserved.Contains(cell) ||
                revealGroupByTriggerCell.ContainsKey(cell))
                return false;
        }
        return true;
    }

    private List<Vector2Int> ClipFormationFootprintToTopology(List<Vector2Int> footprint,
                                                               HashSet<Vector2Int> reserved, int seed)
    {
        HashSet<Vector2Int> allowed = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in footprint)
        {
            if (Data.InBounds(cell) && Data.IsBlocked(cell) && !reserved.Contains(cell) &&
                !revealGroupByTriggerCell.ContainsKey(cell))
                allowed.Add(cell);
        }

        List<List<Vector2Int>> components = new List<List<Vector2Int>>();
        Vector2Int[] cardinal =
        {
            Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
        };
        while (allowed.Count > 0)
        {
            Vector2Int start = default;
            foreach (Vector2Int cell in allowed)
            {
                start = cell;
                break;
            }

            List<Vector2Int> component = new List<Vector2Int>();
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            frontier.Enqueue(start);
            allowed.Remove(start);
            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                component.Add(current);
                foreach (Vector2Int direction in cardinal)
                {
                    Vector2Int neighbor = current + direction;
                    if (!allowed.Remove(neighbor)) continue;
                    frontier.Enqueue(neighbor);
                }
            }
            components.Add(component);
        }

        components.Sort((a, b) => b.Count.CompareTo(a.Count));
        if (components.Count == 0) return new List<Vector2Int>();

        List<Vector2Int> result = new List<Vector2Int>(components[0]);
        if (components.Count > 1 && components[1].Count >= 2 &&
            Stable01(seed, 2053) < detachedChunkChance)
        {
            int maximumDetachedSize = Mathf.Max(2, components[0].Count / 4);
            int cellsToKeep = Mathf.Min(maximumDetachedSize, components[1].Count);
            for (int i = 0; i < cellsToKeep; i++)
                result.Add(components[1][i]);
        }
        return result;
    }

    private float GetBasePlayerTraversalRadius() => 0.5f;
    private float GetBaseBuddyTraversalRadius() => 0.28f;

    private float GetAuthoritativeTraversalRadius()
    {
        float bodyRadius = Mathf.Max(GetBasePlayerTraversalRadius(), GetBaseBuddyTraversalRadius());
        return bodyRadius + Mathf.Max(0f, baseNavigationMargin) * 0.5f;
    }

    private void ValidateAndCorrectFormationClearance()
    {
        BuildRequiredTraversalCells();
        erodedFormationCells.Clear();
        invalidFormationGapCells.Clear();
        clearanceReachableCells.Clear();
        rejectedFormationCount = 0;
        correctedFormationCount = 0;
        mergedOptionalGapCount = 0;
        minimumMeasuredPassageWidth = float.PositiveInfinity;

        if (!enableFormationClearanceValidation || terrainFormations.Count == 0)
        {
            RebuildFormationClearanceDebug();
            return;
        }

        float radius = GetAuthoritativeTraversalRadius();
        requiredFailuresBeforeCorrection = CountRequiredClearanceFailures(radius);
        HashSet<TerrainFormation> corrected = new HashSet<TerrainFormation>();

        List<TerrainFormation> formationSnapshot = new List<TerrainFormation>(terrainFormations);
        foreach (TerrainFormation formation in formationSnapshot)
        {
            HashSet<Vector2Int> conflicts = new HashSet<Vector2Int>();
            foreach (Vector2Int routeCell in requiredTraversalCells)
            {
                foreach (Vector2Int formationCell in formation.cells)
                {
                    if (CircleOverlapsMapCell(Data.CellToWorld(routeCell), radius, formationCell))
                        conflicts.Add(formationCell);
                }
            }

            if (conflicts.Count == 0) continue;
            if (conflicts.Count > maximumErosionCellsPerFormation ||
                formation.cells.Count - conflicts.Count < 5)
            {
                RejectTerrainFormation(formation);
                continue;
            }

            foreach (Vector2Int cell in conflicts)
            {
                formation.cells.Remove(cell);
                RemoveFormationCellOwnership(formation, cell);
                if (formation.type == TerrainFormationType.Root)
                {
                    trimmedRootCells.Add(cell);
                    correctedRootIds.Add(formation.formationId);
                }
                erodedFormationCells.Add(cell);
            }
            corrected.Add(formation);
        }

        CorrectOptionalFormationGaps(radius, corrected);
        correctedFormationCount = corrected.Count;

        requiredFailuresAfterCorrection = CountRequiredClearanceFailures(radius);
        if (validateCriticalConnectivity)
        {
            int connectivityFailures = BuildClearanceReachableArea(radius);
            while (connectivityFailures > 0 && terrainFormations.Count > 0)
            {
                TerrainFormation rejected = terrainFormations[terrainFormations.Count - 1];
                RejectTerrainFormation(rejected);
                connectivityFailures = BuildClearanceReachableArea(radius);
            }
            requiredFailuresAfterCorrection = Mathf.Max(requiredFailuresAfterCorrection, connectivityFailures);
        }

        RebuildFormationClearanceDebug();
        MeasureMinimumFormationPassageWidth();
        if (logFormationClearanceReport)
            LogFormationClearanceReport();
    }

    private void BuildRequiredTraversalCells()
    {
        requiredTraversalCells.Clear();
        criticalTraversalTargets.Clear();
        requiredTraversalCells.UnionWith(spawnOpen);
        requiredTraversalCells.UnionWith(mainPathCells);
        requiredTraversalCells.UnionWith(generatedAreaCells);
        requiredTraversalCells.UnionWith(plannedTunnelCells);
        requiredTraversalCells.UnionWith(hiddenRevealCells);
        foreach (Vector2Int cell in revealGroupByTriggerCell.Keys) requiredTraversalCells.Add(cell);
        foreach (Vector2Int cell in revealAreaByCell.Keys) requiredTraversalCells.Add(cell);
        foreach (PlannedArea area in plannedAreas)
        {
            foreach (Vector2Int cell in area.cells)
                requiredTraversalCells.Add(cell);
            if (Data.InBounds(area.centerCell)) criticalTraversalTargets.Add(area.centerCell);
        }

        if (Data.InBounds(map.spawnCenter)) criticalTraversalTargets.Add(map.spawnCenter);
        foreach (TunnelData tunnel in Data.tunnels)
        {
            if (tunnel.points == null || tunnel.points.Count == 0) continue;
            Vector2Int first = Data.WorldToCell(tunnel.points[0]);
            Vector2Int last = Data.WorldToCell(tunnel.points[tunnel.points.Count - 1]);
            if (Data.InBounds(first)) criticalTraversalTargets.Add(first);
            if (Data.InBounds(last)) criticalTraversalTargets.Add(last);
        }
        requiredTraversalCells.RemoveWhere(cell => !Data.InBounds(cell));
        float traversalRadius = GetAuthoritativeTraversalRadius();
        criticalTraversalTargets.RemoveWhere(cell => !IsInsideWorldClearance(cell, traversalRadius));
    }

    private int CountRequiredClearanceFailures(float radius)
    {
        int failures = 0;
        foreach (Vector2Int cell in requiredTraversalCells)
            if (!IsFormationClearAt(cell, radius)) failures++;
        return failures;
    }

    private bool IsInsideWorldClearance(Vector2Int cell, float radius)
    {
        if (!Data.InBounds(cell)) return false;
        Vector2 world = Data.CellToWorld(cell);
        float minimumX = Data.origin.x;
        float minimumY = Data.origin.y;
        float maximumX = Data.origin.x + Data.width * Data.cellSize;
        float maximumY = Data.origin.y + Data.height * Data.cellSize;
        return world.x - radius >= minimumX && world.x + radius <= maximumX &&
               world.y - radius >= minimumY && world.y + radius <= maximumY;
    }

    private bool IsFormationClearAt(Vector2Int cell, float radius)
    {
        if (!Data.InBounds(cell)) return false;
        Vector2 world = Data.CellToWorld(cell);
        foreach (Vector2Int formationCell in terrainFormationCells)
            if (CircleOverlapsMapCell(world, radius, formationCell)) return false;
        return true;
    }

    private bool IsTraversalCenterClear(Vector2Int cell, float radius)
    {
        return IsInsideWorldClearance(cell, radius) && IsFormationClearAt(cell, radius);
    }

    private bool CircleOverlapsMapCell(Vector2 world, float radius, Vector2Int cell)
    {
        Vector2 center = Data.CellToWorld(cell);
        float half = Data.cellSize * 0.5f;
        float closestX = Mathf.Clamp(world.x, center.x - half, center.x + half);
        float closestY = Mathf.Clamp(world.y, center.y - half, center.y + half);
        return (new Vector2(closestX, closestY) - world).sqrMagnitude < radius * radius;
    }

    private void CorrectOptionalFormationGaps(float radius, HashSet<TerrainFormation> corrected)
    {
        for (int pass = 0; pass < 5; pass++)
        {
            bool changed = false;
            for (int x = 0; x < Data.width; x++)
            {
                for (int y = 0; y < Data.height; y++)
                {
                    Vector2Int gapCell = new Vector2Int(x, y);
                    if (terrainFormationCells.Contains(gapCell) || requiredTraversalCells.Contains(gapCell)) continue;
                    List<TerrainFormation> owners = GetOverlappingFormations(gapCell, radius);
                    if (owners.Count < 2) continue;
                    invalidFormationGapCells.Add(gapCell);

                    TerrainFormation target = ChooseFormationCorrectionTarget(owners);
                    if (minimumOptionalGapPolicy == OptionalGapPolicy.Merge)
                    {
                        owners[0].cells.Add(gapCell);
                        terrainFormationCells.Add(gapCell);
                        mergedOptionalGapCount++;
                        changed = true;
                        continue;
                    }
                    if (minimumOptionalGapPolicy == OptionalGapPolicy.RejectFormation)
                    {
                        RejectTerrainFormation(target);
                        changed = true;
                        break;
                    }

                    Vector2Int cellToErode = FindClosestFormationCell(target, gapCell, radius);
                    if (cellToErode == new Vector2Int(int.MinValue, int.MinValue) ||
                        CountErodedCells(target) >= maximumErosionCellsPerFormation || target.cells.Count <= 5)
                    {
                        RejectTerrainFormation(target);
                    }
                    else
                    {
                        target.cells.Remove(cellToErode);
                        RemoveFormationCellOwnership(target, cellToErode);
                        if (target.type == TerrainFormationType.Root)
                        {
                            trimmedRootCells.Add(cellToErode);
                            correctedRootIds.Add(target.formationId);
                        }
                        erodedFormationCells.Add(cellToErode);
                        corrected.Add(target);
                    }
                    changed = true;
                }
                if (changed && minimumOptionalGapPolicy == OptionalGapPolicy.RejectFormation) break;
            }
            if (!changed) break;
        }
    }

    private TerrainFormation ChooseFormationCorrectionTarget(List<TerrainFormation> owners)
    {
        for (int i = owners.Count - 1; i >= 0; i--)
            if (owners[i].type == TerrainFormationType.Root) return owners[i];
        return owners[owners.Count - 1];
    }

    private List<TerrainFormation> GetOverlappingFormations(Vector2Int centerCell, float radius)
    {
        List<TerrainFormation> result = new List<TerrainFormation>();
        Vector2 world = Data.CellToWorld(centerCell);
        foreach (TerrainFormation formation in terrainFormations)
        {
            foreach (Vector2Int cell in formation.cells)
            {
                if (!CircleOverlapsMapCell(world, radius, cell)) continue;
                result.Add(formation);
                break;
            }
        }
        return result;
    }

    private Vector2Int FindClosestFormationCell(TerrainFormation formation, Vector2Int gapCell, float radius)
    {
        Vector2Int result = new Vector2Int(int.MinValue, int.MinValue);
        float best = float.MaxValue;
        Vector2 gapWorld = Data.CellToWorld(gapCell);
        foreach (Vector2Int cell in formation.cells)
        {
            if (!CircleOverlapsMapCell(gapWorld, radius, cell)) continue;
            float distance = (Data.CellToWorld(cell) - gapWorld).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            result = cell;
        }
        return result;
    }

    private int CountErodedCells(TerrainFormation formation)
    {
        int count = 0;
        foreach (Vector2Int cell in erodedFormationCells)
        {
            if (Vector2Int.Distance(cell, formation.centerCell) <= maximumFormationSize + 4) count++;
        }
        return count;
    }

    private void RemoveFormationCellOwnership(TerrainFormation formation, Vector2Int cell)
    {
        if (formation.type == TerrainFormationType.Stone)
            stoneFormationCells.Remove(cell);
        else
            rootFormationCells.Remove(cell);
        rootStoneOverlapCells.Remove(cell);
        if (!stoneFormationCells.Contains(cell) && !rootFormationCells.Contains(cell))
            terrainFormationCells.Remove(cell);
    }

    private void RejectTerrainFormation(TerrainFormation formation)
    {
        if (formation == null || !terrainFormations.Remove(formation)) return;
        foreach (Vector2Int cell in formation.cells)
            RemoveFormationCellOwnership(formation, cell);
        if (formation.type == TerrainFormationType.Root)
        {
            rejectedRootCount++;
            rejectedRootOrigins.Add(formation.centerCell);
        }
        rejectedFormationCount++;
    }

    private int BuildClearanceReachableArea(float radius)
    {
        clearanceReachableCells.Clear();
        Vector2Int start = map.spawnCenter;
        if (!IsTraversalCenterClear(start, radius)) return criticalTraversalTargets.Count;
        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        frontier.Enqueue(start);
        clearanceReachableCells.Add(start);
        Vector2Int[] cardinal = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            foreach (Vector2Int direction in cardinal)
            {
                Vector2Int next = current + direction;
                if (clearanceReachableCells.Contains(next) || !Data.InBounds(next)) continue;
                if (!IsTraversalCenterClear(next, radius)) continue;
                clearanceReachableCells.Add(next);
                frontier.Enqueue(next);
            }
        }

        int failures = 0;
        foreach (Vector2Int target in criticalTraversalTargets)
            if (!clearanceReachableCells.Contains(target)) failures++;
        return failures;
    }

    private void RebuildFormationClearanceDebug()
    {
        formationClearanceBlockedCells.Clear();
        float radius = GetAuthoritativeTraversalRadius();
        for (int x = 0; x < Data.width; x++)
            for (int y = 0; y < Data.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!IsTraversalCenterClear(cell, radius)) formationClearanceBlockedCells.Add(cell);
            }
    }

    private void MeasureMinimumFormationPassageWidth()
    {
        minimumMeasuredPassageWidth = float.PositiveInfinity;
        for (int i = 0; i < terrainFormations.Count; i++)
            for (int j = i + 1; j < terrainFormations.Count; j++)
                foreach (Vector2Int a in terrainFormations[i].cells)
                    foreach (Vector2Int b in terrainFormations[j].cells)
                    {
                        float gap = Mathf.Max(0f, Vector2.Distance(Data.CellToWorld(a), Data.CellToWorld(b)) - Data.cellSize);
                        if (gap + 0.001f < GetAuthoritativeTraversalRadius() * 2f) continue;
                        minimumMeasuredPassageWidth = Mathf.Min(minimumMeasuredPassageWidth, gap);
                    }
        if (float.IsPositiveInfinity(minimumMeasuredPassageWidth)) minimumMeasuredPassageWidth = 0f;
    }

    public void LogFormationClearanceReport()
    {
        Debug.Log("FORMATION CLEARANCE REPORT | profile=" + RunProfileCoordinator.ActiveDisplayName + " playerRadius=" + GetBasePlayerTraversalRadius().ToString("0.000") +
            " buddyRadius=" + GetBaseBuddyTraversalRadius().ToString("0.000") +
            " traversalRadius=" + GetAuthoritativeTraversalRadius().ToString("0.000") +
            " minimumWidth=" + (GetAuthoritativeTraversalRadius() * 2f).ToString("0.000") +
            " formations=" + terrainFormations.Count + " rejected=" + rejectedFormationCount +
            " corrected=" + correctedFormationCount + " erodedCells=" + erodedFormationCells.Count +
            " mergedOptionalGaps=" + mergedOptionalGapCount +
            " routeFailuresBefore=" + requiredFailuresBeforeCorrection +
            " routeFailuresAfter=" + requiredFailuresAfterCorrection +
            " reachableTargets=" + Mathf.Max(0, criticalTraversalTargets.Count - requiredFailuresAfterCorrection) +
            "/" + criticalTraversalTargets.Count +
            " minimumMeasuredPassage=" + minimumMeasuredPassageWidth.ToString("0.000"), this);
    }

    public void LogTerrainFormationReport()
    {
        int totalCells = terrainFormationCells.Count;
        int blockedCells = 0;
        if (Data != null)
        {
            for (int x = 0; x < Data.width; x++)
                for (int y = 0; y < Data.height; y++)
                    if (Data.IsBlocked(new Vector2Int(x, y))) blockedCells++;
        }
        float average = terrainFormations.Count > 0 ? totalCells / (float)terrainFormations.Count : 0f;
        float percent = blockedCells > 0 ? totalCells * 100f / blockedCells : 0f;
        StringBuilder footprintSizes = new StringBuilder();
        for (int i = 0; i < terrainFormations.Count; i++)
        {
            if (i > 0) footprintSizes.Append(",");
            footprintSizes.Append(terrainFormations[i].cells.Count);
        }
        Debug.Log("TERRAIN FORMATION REPORT | profile=" + RunProfileCoordinator.ActiveDisplayName + " count=" + terrainFormations.Count +
            " totalCells=" + totalCells + " averageFootprint=" + average.ToString("0.0") +
            " blockedCellPercent=" + percent.ToString("0.00") +
            " footprintSizes=[" + footprintSizes + "]" +
            " fingerprint=" + GetTerrainFormationFingerprint(), this);
    }


    public void LogRootFormationReport()
    {
        int stones = 0, roots = 0, companions = 0, edgeRoots = 0, stoneRoots = 0;
        int totalCells = 0, minRoot = int.MaxValue, maxRoot = 0, trunkCells = 0, maxTrunk = 0;
        int majorBranches = 0, minorBranches = 0, knots = 0, removedBranches = 0;
        foreach (TerrainFormation formation in terrainFormations)
        {
            if (formation.type == TerrainFormationType.Stone) { stones++; continue; }
            roots++;
            if (formation.isCompanion) companions++;
            if (formation.rootOriginType == RootOriginType.Edge) edgeRoots++;
            if (formation.rootOriginType == RootOriginType.Stone || formation.stoneOverlapCells.Count > 0) stoneRoots++;
            totalCells += formation.cells.Count;
            minRoot = Mathf.Min(minRoot, formation.cells.Count);
            maxRoot = Mathf.Max(maxRoot, formation.cells.Count);
            trunkCells += formation.mainPathCells.Count;
            maxTrunk = Mathf.Max(maxTrunk, formation.mainPathCells.Count);
            majorBranches += formation.majorBranchCells.Count;
            minorBranches += formation.minorBranchCells.Count;
            knots += formation.knotCount;
            removedBranches += formation.removedBranchCount;
        }
        float averageSize = roots > 0 ? totalCells / (float)roots : 0f;
        float averageTrunk = roots > 0 ? trunkCells / (float)roots : 0f;
        Debug.Log("ROOT FORMATION REPORT | profile=" + RunProfileCoordinator.ActiveDisplayName + " stones=" + stones + " roots=" + roots +
            " majorRoots=" + (roots - companions) + " companionRoots=" + companions +
            " edgeRoots=" + edgeRoots + " stoneInteractingRoots=" + stoneRoots +
            " rejectedRoots=" + rejectedRootCount + " rejectedOverlaps=" + rejectedRootOverlapCount +
            " correctedRoots=" + correctedRootIds.Count + " rootCells=" + totalCells +
            " averageRootSize=" + averageSize.ToString("0.0") + " minRootSize=" + (roots > 0 ? minRoot : 0) +
            " maxRootSize=" + maxRoot + " averageTrunk=" + averageTrunk.ToString("0.0") +
            " maxTrunk=" + maxTrunk + " majorBranchCells=" + majorBranches +
            " minorBranchCells=" + minorBranches + " knots=" + knots +
            " rootStoneOverlap=" + rootStoneOverlapCells.Count + " unsafeVs=" + unsafeRootVCount +
            " unsafePockets=" + unsafeRootPocketCount + " safetyFusions=" + rootSafetyFusionCount +
            " trimmedRootCells=" + trimmedRootCells.Count + " removedBranches=" + removedBranches +
            " routeFailuresBefore=" + requiredFailuresBeforeCorrection +
            " routeFailuresAfter=" + requiredFailuresAfterCorrection +
            " minimumPassage=" + minimumMeasuredPassageWidth.ToString("0.000") +
            " reachableTargets=" + Mathf.Max(0, criticalTraversalTargets.Count - requiredFailuresAfterCorrection) +
            "/" + criticalTraversalTargets.Count + " stoneFingerprint=" + GetFormationFingerprint(TerrainFormationType.Stone) +
            " rootFingerprint=" + GetFormationFingerprint(TerrainFormationType.Root), this);
    }

    public string GetFormationFingerprint(TerrainFormationType filter)
    {
        int count = 0, cells = 0;
        int fingerprint = StableHash(runtimeMapSeed, (int)filter);
        foreach (TerrainFormation formation in terrainFormations)
        {
            if (formation.type != filter) continue;
            count++; cells += formation.cells.Count;
            fingerprint = StableHash(fingerprint, formation.centerCell.x, formation.centerCell.y, formation.cells.Count);
            foreach (Vector2Int cell in formation.cells) fingerprint = StableHash(fingerprint, cell.x, cell.y);
        }
        return StableHash(fingerprint, count, cells).ToString("X8");
    }

    public string GetTerrainFormationFingerprint()
    {
        int fingerprint = StableHash(runtimeMapSeed, terrainFormations.Count, terrainFormationCells.Count);
        foreach (TerrainFormation formation in terrainFormations)
        {
            fingerprint = StableHash(fingerprint, (int)formation.type, formation.centerCell.x,
                formation.centerCell.y, formation.cells.Count);
            foreach (Vector2Int cell in formation.cells)
                fingerprint = StableHash(fingerprint, cell.x, cell.y);
        }
        return fingerprint.ToString("X8");
    }

    public bool ValidateTerrainFormationState()
    {
        if (!enableTerrainFormations) return terrainFormations.Count == 0 && terrainFormationCells.Count == 0;
        if (terrainFormations.Count == 0 || terrainFormationCells.Count == 0 || Data == null) return false;

        HashSet<Vector2Int> protectedCells = BuildFormationReservedCells();
        foreach (TerrainFormation formation in terrainFormations)
        {
            if (formation.cells == null || formation.cells.Count < 5)
            {
                Debug.LogError("TERRAIN FORMATION SAFETY FAILURE | footprint too small", this);
                return false;
            }
            foreach (Vector2Int cell in formation.cells)
            {
                if (!Data.InBounds(cell) || !Data.IsBlocked(cell) || IsDiggable(cell) ||
                    protectedCells.Contains(cell) || revealGroupByTriggerCell.ContainsKey(cell))
                {
                    Debug.LogError("TERRAIN FORMATION SAFETY FAILURE | cell=" + cell +
                        " inBounds=" + Data.InBounds(cell) +
                        " blocked=" + Data.IsBlocked(cell) +
                        " diggable=" + IsDiggable(cell) +
                        " protected=" + protectedCells.Contains(cell) +
                        " revealTrigger=" + revealGroupByTriggerCell.ContainsKey(cell), this);
                    return false;
                }
            }
        }

        Vector2Int guardedCell = terrainFormations[0].cells[0];
        bool blockedBefore = Data.IsBlocked(guardedCell);
        DigCell(guardedCell);
        bool blockedAfter = Data.IsBlocked(guardedCell);
        bool valid = blockedBefore && blockedAfter && terrainFormationCells.Contains(guardedCell);
        Debug.Log("TERRAIN FORMATION SAFETY | valid=" + valid + " guardedCell=" + guardedCell +
            " blockedBefore=" + blockedBefore + " blockedAfter=" + blockedAfter, this);
        return valid;
    }

    private void BuildDirtInfluenceField()
    {
        dirtInfluenceSources.Clear();
        tileColorUnlockAttempts = 0;
        tileColorUnlockSuccesses = 0;
        dirtInfluenceField = Data != null ? new DirtInfluenceSample[Data.width, Data.height] : null;
        if (Data == null) return;

        for (int x = 0; x < Data.width; x++)
        {
            for (int y = 0; y < Data.height; y++)
            {
                dirtInfluenceField[x, y] = new DirtInfluenceSample
                {
                    tintMultiplier = Color.white,
                    strongestCategory = DirtInfluenceCategory.FungalRegion
                };
            }
        }

        if (!enableDirtInfluence) return;

        Color[,] accumulatedOffsets = new Color[Data.width, Data.height];
        float[,] strongestWeights = new float[Data.width, Data.height];

        foreach (PlannedArea area in plannedAreas)
        {
            if (!TryCreateDirtInfluenceSource(area, out DirtInfluenceSource source)) continue;
            dirtInfluenceSources.Add(source);
            if (!source.suppressed)
                RasterizeDirtInfluence(source, accumulatedOffsets, strongestWeights);
        }

        EnsureRequiredInfluenceSources(accumulatedOffsets, strongestWeights);
        FinalizeDirtInfluenceField(accumulatedOffsets);
    }

    private void EnsureRequiredInfluenceSources(Color[,] accumulatedOffsets, float[,] strongestWeights)
    {
        RunProfile.DevelopmentOverrides d = activeDevelopmentOverrides;
        if (d == null || !d.enabled) return;
        if (d.requireFungalInfluence) EnsureRequiredInfluenceSource(DirtInfluenceCategory.FungalRegion, accumulatedOffsets, strongestWeights);
        if (d.requireMineralInfluence) EnsureRequiredInfluenceSource(DirtInfluenceCategory.MineralRegion, accumulatedOffsets, strongestWeights);
        if (d.requireDisturbedInfluence) EnsureRequiredInfluenceSource(DirtInfluenceCategory.DisturbedRegion, accumulatedOffsets, strongestWeights);
    }

    private void EnsureRequiredInfluenceSource(DirtInfluenceCategory category, Color[,] accumulatedOffsets, float[,] strongestWeights)
    {
        if (dirtInfluenceSources.Exists(s => !s.suppressed && s.category == category)) return;
        DirtInfluenceSource candidate = dirtInfluenceSources.Find(s => s.suppressed);
        if (candidate == null)
        {
            foreach (PlannedArea area in plannedAreas)
                if (TryCreateDirtInfluenceSource(area, out candidate)) { dirtInfluenceSources.Add(candidate); break; }
        }
        if (candidate == null) { Debug.LogError("RUN PROFILE MINIMUM UNMET | influence=" + category, this); return; }
        candidate.category = category; candidate.suppressed = false; candidate.substituted = true;
        candidate.deterministicSeed = StableHash(candidate.deterministicSeed, (int)category, 911);
        RasterizeDirtInfluence(candidate, accumulatedOffsets, strongestWeights);
    }
    private bool TryCreateDirtInfluenceSource(PlannedArea area, out DirtInfluenceSource source)
    {
        source = null;
        if (area == null) return false;

        CampData content = Data.camps.Find(c => c != null && c.id == area.id);
        bool hasCombat = area.type == AreaType.Camp || area.type == AreaType.Boss ||
                         (content != null && (content.enemyCount > 0 || content.bossEnemyCount > 0));
        bool hasFungalContent = content != null && content.enemyCount <= 0 &&
                                content.bossEnemyCount <= 0 &&
                                (content.mushroomCount > 0 || content.sporeCount > 0);
        bool qualifying = false;
        bool neutralCandidate = false;
        DirtInfluenceCategory category = DirtInfluenceCategory.FungalRegion;

        if (hasCombat)
        {
            qualifying = true;
            category = DirtInfluenceCategory.DisturbedRegion;
        }
        else if (area.type == AreaType.FillerPocketLoot)
        {
            qualifying = true;
            category = DirtInfluenceCategory.MineralRegion;
        }
        else if (area.type == AreaType.SmallRoom && hasFungalContent)
        {
            qualifying = true;
            category = DirtInfluenceCategory.FungalRegion;
        }
        else if (area.type == AreaType.SmallRoom || area.type == AreaType.FillerPocket)
        {
            neutralCandidate = true;
        }

        if (!qualifying && !neutralCandidate) return false;

        int decisionSeed = StableHash(runtimeMapSeed, area.id, (int)area.type,
                                      area.centerCell.x, area.centerCell.y);
        bool falsePositive = !qualifying;
        bool visibleTest = dirtInfluenceTuning == DirtInfluenceTuningMode.VisibleTest;
        float appearanceChance = qualifying
            ? visibleTest ? 0.93f : qualifyingAreaInfluenceChance
            : visibleTest ? 0.15f : neutralFalsePositiveChance;
        bool suppressed = Stable01(decisionSeed, 11) >= appearanceChance;

        bool substituted = false;
        if (falsePositive)
            category = PickStableCategory(decisionSeed, 17);
        else if (!suppressed && Stable01(decisionSeed, 19) < neighboringCategoryChance)
        {
            category = PickNeighboringCategory(category, decisionSeed);
            substituted = true;
        }

        float strength = Mathf.Lerp(Mathf.Min(minimumSourceStrength, maximumSourceStrength),
                                    Mathf.Max(minimumSourceStrength, maximumSourceStrength),
                                    Stable01(decisionSeed, 23));
        float radiusScale = Mathf.Lerp(Mathf.Min(minimumRadiusScale, maximumRadiusScale),
                                       Mathf.Max(minimumRadiusScale, maximumRadiusScale),
                                       Stable01(decisionSeed, 29));
        float sourceRadius = Mathf.Max(1f, area.radiusCells);
        float presetRadiusScale = visibleTest ? 0.88f : 1f;
        float outerRadius = Mathf.Max(sourceRadius + 1f,
            sourceRadius * 2.1f * radiusScale * presetRadiusScale);
        float offsetDistance = sourceRadius * Mathf.Clamp01(sourceCenterOffset) * Stable01(decisionSeed, 31);
        float offsetAngle = Stable01(decisionSeed, 37) * Mathf.PI * 2f;
        Vector2 offset = new Vector2(Mathf.Cos(offsetAngle), Mathf.Sin(offsetAngle)) * offsetDistance;

        source = new DirtInfluenceSource
        {
            category = category,
            sourceAreaId = area.id,
            centerCell = (Vector2)area.centerCell + offset,
            sourceRadius = sourceRadius,
            outerHintRadius = outerRadius,
            strength = strength,
            irregularity = Mathf.Clamp01(influenceIrregularity),
            deterministicSeed = StableHash(decisionSeed, (int)category, 41),
            qualifying = qualifying,
            falsePositive = falsePositive,
            substituted = substituted,
            suppressed = suppressed
        };
        return true;
    }

    private void RasterizeDirtInfluence(DirtInfluenceSource source, Color[,] accumulatedOffsets,
                                        float[,] strongestWeights)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(source.centerCell.x - source.outerHintRadius - 2f));
        int maxX = Mathf.Min(Data.width - 1, Mathf.CeilToInt(source.centerCell.x + source.outerHintRadius + 2f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(source.centerCell.y - source.outerHintRadius - 2f));
        int maxY = Mathf.Min(Data.height - 1, Mathf.CeilToInt(source.centerCell.y + source.outerHintRadius + 2f));
        float axisAngle = Stable01(source.deterministicSeed, 43) * Mathf.PI * 2f;
        Vector2 longAxis = new Vector2(Mathf.Cos(axisAngle), Mathf.Sin(axisAngle));
        Vector2 shortAxis = new Vector2(-longAxis.y, longAxis.x);
        float stretch = Mathf.Lerp(1.05f, 1.3f, Stable01(source.deterministicSeed, 47));
        Color themeOffset = GetCategoryTint(source.category) - Color.white;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2 delta = new Vector2(x, y) - source.centerCell;
                float axisX = Vector2.Dot(delta, longAxis) / stretch;
                float axisY = Vector2.Dot(delta, shortAxis) * stretch;
                float shapedDistance = Mathf.Sqrt(axisX * axisX + axisY * axisY);
                float noise = SmoothStableNoise(source.deterministicSeed, x, y, 5);
                float noiseScale = 1f + (noise - 0.5f) * 2f * source.irregularity;
                float normalizedDistance = shapedDistance * noiseScale / source.outerHintRadius;
                if (normalizedDistance >= 1f) continue;

                float falloff = 1f - Mathf.SmoothStep(0.12f, 1f, normalizedDistance);
                float weight = falloff * source.strength;
                if (weight <= 0f) continue;

                accumulatedOffsets[x, y] += themeOffset * weight;
                if (weight > strongestWeights[x, y])
                {
                    strongestWeights[x, y] = weight;
                    DirtInfluenceSample sample = dirtInfluenceField[x, y];
                    sample.strongestCategory = source.category;
                    dirtInfluenceField[x, y] = sample;
                }
            }
        }
    }

    private void FinalizeDirtInfluenceField(Color[,] accumulatedOffsets)
    {
        float modeMultiplier = GetDirtInfluenceModeMultiplier();
        float cap = GetDirtInfluenceCap();

        for (int x = 0; x < Data.width; x++)
        {
            for (int y = 0; y < Data.height; y++)
            {
                Color offset = accumulatedOffsets[x, y] * modeMultiplier;
                float strongestChannel = Mathf.Max(Mathf.Abs(offset.r),
                    Mathf.Abs(offset.g), Mathf.Abs(offset.b));
                if (strongestChannel > cap && strongestChannel > 0f)
                    offset *= cap / strongestChannel;

                DirtInfluenceSample sample = dirtInfluenceField[x, y];
                sample.tintMultiplier = new Color(Mathf.Max(0f, 1f + offset.r),
                    Mathf.Max(0f, 1f + offset.g), Mathf.Max(0f, 1f + offset.b), 1f);
                sample.totalStrength = Mathf.Min(strongestChannel, cap);
                dirtInfluenceField[x, y] = sample;
            }
        }
    }

    private Color GetDirtInfluenceTint(Vector2Int cell)
    {
        if (!enableDirtInfluence || dirtInfluenceField == null || Data == null || !Data.InBounds(cell))
            return Color.white;

        DirtInfluenceSample sample = dirtInfluenceField[cell.x, cell.y];
        if (dirtInfluenceTuning == DirtInfluenceTuningMode.VisibleTest && !showInfluenceOnlyPreview)
            return GetVisibleTestMaterialTint(cell, sample);

        if (!showInfluenceOnlyPreview)
            return sample.tintMultiplier;

        if (sample.totalStrength <= 0.001f)
            return new Color(0.22f, 0.22f, 0.22f, 1f);

        Color categoryColor = GetDebugCategoryColor(sample.strongestCategory);
        float normalizedStrength = Mathf.Clamp01(sample.totalStrength / Mathf.Max(0.001f, GetDirtInfluenceCap()));
        return Color.Lerp(new Color(0.18f, 0.18f, 0.18f, 1f), categoryColor,
            Mathf.Lerp(0.65f, 1f, normalizedStrength));
    }

    private Color GetDebugCategoryColor(DirtInfluenceCategory category)
    {
        if (category == DirtInfluenceCategory.FungalRegion) return Color.green;
        if (category == DirtInfluenceCategory.MineralRegion) return Color.cyan;
        return Color.red;
    }

    private int GetVisibleTestInfluenceBand(DirtInfluenceSample sample)
    {
        if (sample.totalStrength <= 0.001f) return 0;
        if (sample.totalStrength >= 0.115f) return 3;
        if (sample.totalStrength >= 0.06f) return 2;
        return 1;
    }

    private Color GetVisibleTestMaterialTint(Vector2Int cell, DirtInfluenceSample sample)
    {
        int band = GetVisibleTestInfluenceBand(sample);
        if (band == 0 || !ShouldShowThemedMaterial(cell, sample, band))
            return Color.white;

        Color target;
        float colorStrength;

        switch (sample.strongestCategory)
        {
            case DirtInfluenceCategory.MineralRegion:
                target = band == 3 ? new Color(0.68f, 0.76f, 0.84f, 1f) :
                    band == 2 ? new Color(0.82f, 0.87f, 0.92f, 1f) :
                    new Color(0.94f, 0.97f, 1.01f, 1f);
                colorStrength = mineralColorStrength;
                break;

            case DirtInfluenceCategory.DisturbedRegion:
                target = band == 3 ? new Color(0.82f, 0.60f, 0.45f, 1f) :
                    band == 2 ? new Color(0.92f, 0.76f, 0.62f, 1f) :
                    new Color(1f, 0.94f, 0.87f, 1f);
                colorStrength = disturbedColorStrength;
                break;

            default:
                target = band == 3 ? new Color(0.72f, 0.84f, 0.55f, 1f) :
                    band == 2 ? new Color(0.84f, 0.92f, 0.72f, 1f) :
                    new Color(0.95f, 0.99f, 0.90f, 1f);
                colorStrength = fungalColorStrength;
                break;
        }

        return Color.Lerp(Color.white, target, Mathf.Clamp01(colorStrength));
    }

    private bool ShouldShowThemedMaterial(Vector2Int cell, DirtInfluenceSample sample, int band)
    {
        float targetDensity = band == 3 ? heavyMaterialDensity :
            band == 2 ? mediumMaterialDensity : lightMaterialDensity;
        float transition = Mathf.Clamp01(materialTransitionWidth);
        float bandStart = band == 3 ? 0.115f : band == 2 ? 0.06f : 0.001f;
        float bandEnd = band == 3 ? GetDirtInfluenceCap() : band == 2 ? 0.115f : 0.06f;
        float bandProgress = Mathf.InverseLerp(bandStart, Mathf.Max(bandStart + 0.001f, bandEnd),
            sample.totalStrength);
        float previousDensity = band == 3 ? mediumMaterialDensity : band == 2 ? lightMaterialDensity : 0f;
        if (transition > 0f && bandProgress < transition)
            targetDensity = Mathf.Lerp(previousDensity, targetDensity, bandProgress / transition);

        int category = (int)sample.strongestCategory;
        int patternSeed = StableHash(runtimeMapSeed, category, 1709);
        int offsetRange = Mathf.RoundToInt(14f * Mathf.Clamp01(materialCenterOffsetStrength));
        int offsetX = Mathf.RoundToInt((Stable01(patternSeed, 1711) * 2f - 1f) * offsetRange);
        int offsetY = Mathf.RoundToInt((Stable01(patternSeed, 1717) * 2f - 1f) * offsetRange);
        int px = cell.x + offsetX;
        int py = cell.y + offsetY;

        float broadClusters = SmoothStableNoise(patternSeed, px, py, 9);
        float brokenPatches = SmoothStableNoise(patternSeed + 31, px, py, 4);
        float pores = SmoothStableNoise(patternSeed + 67, px, py, 2);
        float angle = Stable01(patternSeed, 1723) * Mathf.PI;
        float projected = px * Mathf.Cos(angle) + py * Mathf.Sin(angle);
        float bend = (SmoothStableNoise(patternSeed + 97, px, py, 7) - 0.5f) * 7f;
        float vein = 0.5f + 0.5f * Mathf.Sin(projected * 0.82f + bend);
        vein = Mathf.Pow(vein, 2.4f);

        float clustered = broadClusters * 0.52f + brokenPatches * 0.34f + pores * 0.14f;
        float pattern = Mathf.Lerp(clustered, Mathf.Max(clustered * 0.82f, vein),
            Mathf.Clamp01(fingerVeinAmount));
        float grain = Stable01(patternSeed, cell.x, cell.y, 1733);
        pattern = Mathf.Clamp01(pattern * 0.82f + grain * 0.18f);

        float holeSignal = SmoothStableNoise(patternSeed + 131, px, py, 2);
        bool internalHole = holeSignal > 1f - Mathf.Clamp01(internalHoleChance) &&
            Stable01(patternSeed, cell.x, cell.y, 1741) < 0.72f;
        if (internalHole) return false;

        float satelliteSignal = SmoothStableNoise(patternSeed + 173, px, py, 3);
        bool satellite = satelliteSignal > 0.74f &&
            Stable01(patternSeed, cell.x, cell.y, 1753) < Mathf.Clamp01(satellitePatchChance);
        float clusterFactor = Mathf.Clamp(1f + (pattern - 0.5f) * 1.6f, 0.2f, 1.8f);
        float themedChance = Mathf.Clamp01(targetDensity * clusterFactor);
        float selectionRoll = Stable01(patternSeed, cell.x, cell.y, 1769);
        return selectionRoll < themedChance || satellite;
    }

    private Color GetCategoryTint(DirtInfluenceCategory category)
    {
        if (dirtInfluenceTuning == DirtInfluenceTuningMode.VisibleTest)
        {
            switch (category)
            {
                case DirtInfluenceCategory.DisturbedRegion: return new Color(0.94f, 0.90f, 0.89f, 1f);
                case DirtInfluenceCategory.MineralRegion: return new Color(0.99f, 1.01f, 1.04f, 1f);
                default: return new Color(0.97f, 0.99f, 0.93f, 1f);
            }
        }

        switch (category)
        {
            case DirtInfluenceCategory.DisturbedRegion: return disturbedRegionTint;
            case DirtInfluenceCategory.MineralRegion: return mineralRegionTint;
            default: return fungalRegionTint;
        }
    }

    private float GetDirtInfluenceModeMultiplier()
    {
        switch (dirtInfluenceTuning)
        {
            case DirtInfluenceTuningMode.BarelyVisible: return 0.5f;
            case DirtInfluenceTuningMode.StrongTest: return 2.5f;
            case DirtInfluenceTuningMode.VisibleTest: return 6.7f;
            case DirtInfluenceTuningMode.DebugObvious: return 4f;
            default: return 1f;
        }
    }

    private float GetDirtInfluenceCap()
    {
        float configuredCap = Mathf.Max(0f, maximumCombinedInfluence);
        switch (dirtInfluenceTuning)
        {
            case DirtInfluenceTuningMode.BarelyVisible: return Mathf.Min(configuredCap, 0.03f);
            case DirtInfluenceTuningMode.StrongTest: return Mathf.Max(configuredCap, 0.15f);
            case DirtInfluenceTuningMode.VisibleTest: return 0.17f;
            case DirtInfluenceTuningMode.DebugObvious: return Mathf.Max(configuredCap, 0.24f);
            default: return configuredCap;
        }
    }

    private DirtInfluenceCategory PickStableCategory(int seed, int salt)
    {
        int index = Mathf.FloorToInt(Stable01(seed, salt) * 3f);
        return (DirtInfluenceCategory)Mathf.Clamp(index, 0, 2);
    }

    private DirtInfluenceCategory PickNeighboringCategory(DirtInfluenceCategory current, int seed)
    {
        int offset = Stable01(seed, 53) < 0.5f ? 1 : 2;
        return (DirtInfluenceCategory)(((int)current + offset) % 3);
    }

    private float SmoothStableNoise(int seed, int x, int y, int scale)
    {
        int safeScale = Mathf.Max(1, scale);
        float scaledX = x / (float)safeScale;
        float scaledY = y / (float)safeScale;
        int x0 = Mathf.FloorToInt(scaledX);
        int y0 = Mathf.FloorToInt(scaledY);
        float tx = Mathf.SmoothStep(0f, 1f, scaledX - x0);
        float ty = Mathf.SmoothStep(0f, 1f, scaledY - y0);
        float a = Stable01(seed, x0, y0);
        float b = Stable01(seed, x0 + 1, y0);
        float c = Stable01(seed, x0, y0 + 1);
        float d = Stable01(seed, x0 + 1, y0 + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private float Stable01(params int[] values)
    {
        uint hash = 2166136261u;
        unchecked
        {
            foreach (int value in values)
            {
                hash ^= (uint)value;
                hash *= 16777619u;
                hash ^= hash >> 13;
            }
        }
        return (hash & 0x00FFFFFFu) / 16777216f;
    }

    private int StableHash(params int[] values)
    {
        uint hash = 2166136261u;
        unchecked
        {
            foreach (int value in values)
            {
                hash ^= (uint)value;
                hash *= 16777619u;
                hash ^= hash >> 13;
            }
        }
        return (int)hash;
    }

    [ContextMenu("Rebuild Dirt Influence")]
    public void RebuildDirtInfluence()
    {
        if (Data == null)
        {
            Debug.LogWarning("Dirt influence cannot rebuild before a map has been generated.", this);
            return;
        }

        BuildDirtInfluenceField();
        RefreshAllTiles();
    }

    [ContextMenu("Refresh Dirt Tint")]
    public void RefreshDirtTint()
    {
        RebuildDirtInfluence();
    }

    [ContextMenu("Run Dirt Influence Color Diagnostic")]
    public void RunDirtInfluenceColorDiagnostic()
    {
        if (!TryFindDiagnosticInfluenceCell(out Vector2Int cell))
        {
            Debug.LogWarning("Dirt influence diagnostic could not find an influenced blocked cell.", this);
            return;
        }

        if (Application.isPlaying)
            StartCoroutine(RunDirtInfluenceColorDiagnosticRoutine(cell));
        else
            RunDirtInfluenceColorDiagnosticImmediate(cell);
    }

    private bool TryFindDiagnosticInfluenceCell(out Vector2Int cell)
    {
        cell = default;
        if (Data == null || dirtInfluenceField == null || dirtTilemap == null) return false;

        for (int x = 0; x < Data.width; x++)
        {
            for (int y = 0; y < Data.height; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (!Data.IsBlocked(candidate) || revealGroupByTriggerCell.ContainsKey(candidate)) continue;
                if (dirtInfluenceField[x, y].totalStrength <= 0.001f) continue;
                cell = candidate;
                return true;
            }
        }
        return false;
    }

    private IEnumerator RunDirtInfluenceColorDiagnosticRoutine(Vector2Int cell)
    {
        RunDirtInfluenceColorTest(cell, false);
        yield return new WaitForSecondsRealtime(0.75f);
        RunDirtInfluenceColorTest(cell, true);
    }

    private void RunDirtInfluenceColorDiagnosticImmediate(Vector2Int cell)
    {
        RunDirtInfluenceColorTest(cell, false);
        RunDirtInfluenceColorTest(cell, true);
    }

    private void RunDirtInfluenceColorTest(Vector2Int cell, bool restoreAndDig)
    {
        Vector3Int tilePosition = ToTilePos(cell);
        if (!restoreAndDig)
        {
            TileBase assignedTile = dirtTilemap.GetTile(tilePosition);
            dirtTilemap.SetTile(tilePosition, assignedTile);
            TileFlags flagsBefore = dirtTilemap.GetTileFlags(tilePosition);
            dirtTilemap.RemoveTileFlags(tilePosition, TileFlags.LockColor);
            TileFlags flagsAfter = dirtTilemap.GetTileFlags(tilePosition);
            Color obviousTestColor = Color.magenta;
            dirtTilemap.SetColor(tilePosition, obviousTestColor);
            Color readback = dirtTilemap.GetColor(tilePosition);
            Debug.Log("Dirt influence color diagnostic TEST | cell=" + cell +
                " tile=" + (assignedTile != null ? assignedTile.name : "<none>") +
                " flagsBefore=" + flagsBefore + " flagsAfter=" + flagsAfter +
                " requested=" + obviousTestColor + " readback=" + readback, this);
            return;
        }

        SetCellTilesFromData(cell);
        Color calculated = GetDirtInfluenceTint(cell);
        Color restored = dirtTilemap.GetColor(tilePosition);
        RefreshAllTiles();
        Color afterRefresh = dirtTilemap.GetColor(tilePosition);
        DigCell(cell);
        bool tileRemoved = dirtTilemap.GetTile(tilePosition) == null;
        Color colorAfterDig = dirtTilemap.GetColor(tilePosition);
        Data.SetBlocked(cell, true);
        SetCellTilesFromData(cell);
        Debug.Log("Dirt influence color diagnostic RESTORE | cell=" + cell +
            " calculated=" + calculated + " restored=" + restored +
            " afterRefresh=" + afterRefresh + " tileRemovedAfterDig=" + tileRemoved +
            " colorAfterDig=" + colorAfterDig + " runtimeCellRestored=true", this);
    }

    [ContextMenu("Log Dirt Influence Report")]
    public void LogDirtInfluenceReport()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("========== DIRT INFLUENCE REPORT ==========");
        report.AppendLine("Profile: " + RunProfileCoordinator.ActiveDisplayName);
        report.AppendLine("Runtime map seed: " + runtimeMapSeed);
        report.AppendLine("Enabled: " + enableDirtInfluence);
        report.AppendLine("Preset: " + dirtInfluenceTuning);
        report.AppendLine("Influence-only preview: " + showInfluenceOnlyPreview);
        report.AppendLine("Configured cap: " + maximumCombinedInfluence.ToString("0.000"));
        report.AppendLine("Effective cap: " + GetDirtInfluenceCap().ToString("0.000"));
        report.AppendLine("Fungal tint: " + fungalRegionTint);
        report.AppendLine("Mineral tint: " + mineralRegionTint);
        report.AppendLine("Disturbed tint: " + disturbedRegionTint);

        int qualifyingCount = 0;
        int suppressedCount = 0;
        int falsePositiveCount = 0;
        int substitutedCount = 0;
        int fungalCount = 0;
        int mineralCount = 0;
        int disturbedCount = 0;

        foreach (DirtInfluenceSource source in dirtInfluenceSources)
        {
            if (source.qualifying) qualifyingCount++;
            if (source.suppressed) suppressedCount++;
            if (source.falsePositive) falsePositiveCount++;
            if (source.substituted) substitutedCount++;
            if (source.category == DirtInfluenceCategory.FungalRegion) fungalCount++;
            else if (source.category == DirtInfluenceCategory.MineralRegion) mineralCount++;
            else disturbedCount++;
        }

        report.AppendLine("Sources: " + dirtInfluenceSources.Count +
            " | qualifying=" + qualifyingCount +
            " suppressed=" + suppressedCount +
            " falsePositive=" + falsePositiveCount +
            " substituted=" + substitutedCount);
        report.AppendLine("Source categories: fungal=" + fungalCount +
            " mineral=" + mineralCount + " disturbed=" + disturbedCount);

        int blockedCount = 0;
        int influencedCount = 0;
        int lightBandCount = 0;
        int mediumBandCount = 0;
        int heavyBandCount = 0;
        float minimumStrength = float.MaxValue;
        int lightInfluenceCount = 0;
        int mediumInfluenceCount = 0;
        int heavyInfluenceCount = 0;
        float totalStrength = 0f;
        float maximumStrength = 0f;
        List<string> samples = new List<string>();

        if (Data != null && dirtInfluenceField != null)
        {
            for (int x = 0; x < Data.width; x++)
            {
                for (int y = 0; y < Data.height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!Data.IsBlocked(cell)) continue;
                    blockedCount++;

                    DirtInfluenceSample sample = dirtInfluenceField[x, y];
                    if (sample.totalStrength <= 0.001f) continue;
                    influencedCount++;
                    int influenceBand = GetVisibleTestInfluenceBand(sample);
                    if (influenceBand == 3)
                        heavyInfluenceCount++;
                    else if (influenceBand == 2)
                        mediumInfluenceCount++;
                    else
                        lightInfluenceCount++;

                    if (ShouldShowThemedMaterial(cell, sample, influenceBand))
                    {
                        if (influenceBand == 3)
                            heavyBandCount++;
                        else if (influenceBand == 2)
                            mediumBandCount++;
                        else
                            lightBandCount++;
                    }
                    minimumStrength = Mathf.Min(minimumStrength, sample.totalStrength);
                    maximumStrength = Mathf.Max(maximumStrength, sample.totalStrength);
                    totalStrength += sample.totalStrength;

                    if (samples.Count < 8 && dirtTilemap != null)
                    {
                        Vector3Int tilePosition = ToTilePos(cell);
                        samples.Add("  " + cell + " category=" + sample.strongestCategory +
                            " strength=" + sample.totalStrength.ToString("0.0000") +
                            " calculated=" + sample.tintMultiplier +
                            " tilemap=" + dirtTilemap.GetColor(tilePosition));
                    }
                }
            }
        }

        float averageStrength = influencedCount > 0 ? totalStrength / influencedCount : 0f;
        float influencedPercent = blockedCount > 0 ? influencedCount * 100f / blockedCount : 0f;
        if (influencedCount == 0) minimumStrength = 0f;
        report.AppendLine("Blocked dirt cells: " + blockedCount);
        report.AppendLine("Influenced blocked cells: " + influencedCount +
            " (" + influencedPercent.ToString("0.0") + "%)");
        report.AppendLine("Visible Test themed cells (% of blocked dirt): normal=" +
            (blockedCount > 0 ? (blockedCount - lightBandCount - mediumBandCount - heavyBandCount) * 100f / blockedCount : 0f).ToString("0.0") +
            " light=" + (blockedCount > 0 ? lightBandCount * 100f / blockedCount : 0f).ToString("0.0") +
            " medium=" + (blockedCount > 0 ? mediumBandCount * 100f / blockedCount : 0f).ToString("0.0") +
            " heavy=" + (blockedCount > 0 ? heavyBandCount * 100f / blockedCount : 0f).ToString("0.0"));
        report.AppendLine("Influence strength min/avg/max: " +
            minimumStrength.ToString("0.0000") + " / " + averageStrength.ToString("0.0000") +
            " / " + maximumStrength.ToString("0.0000"));
        report.AppendLine("Themed density within each influence band: light=" +
            (lightInfluenceCount > 0 ? lightBandCount * 100f / lightInfluenceCount : 0f).ToString("0.0") +
            " medium=" + (mediumInfluenceCount > 0 ? mediumBandCount * 100f / mediumInfluenceCount : 0f).ToString("0.0") +
            " heavy=" + (heavyInfluenceCount > 0 ? heavyBandCount * 100f / heavyInfluenceCount : 0f).ToString("0.0"));

        TilemapRenderer tilemapRenderer = dirtTilemap != null ? dirtTilemap.GetComponent<TilemapRenderer>() : null;
        string shaderName = tilemapRenderer != null && tilemapRenderer.sharedMaterial != null &&
                            tilemapRenderer.sharedMaterial.shader != null
            ? tilemapRenderer.sharedMaterial.shader.name
            : "<none>";
        report.AppendLine("Tilemap: " + (dirtTilemap != null ? dirtTilemap.name : "<missing>"));
        report.AppendLine("Tilemap shader: " + shaderName);
        report.AppendLine("LockColor cleared: " + tileColorUnlockSuccesses + "/" + tileColorUnlockAttempts);
        report.AppendLine("Per-cell color readback active: " +
            (tileColorUnlockAttempts > 0 && tileColorUnlockSuccesses == tileColorUnlockAttempts));
        report.AppendLine("Influenced-cell samples:");
        foreach (string sample in samples) report.AppendLine(sample);
        report.AppendLine("===========================================");
        Debug.Log(report.ToString(), this);
    }

    public string GetDirtInfluenceDiagnosticFingerprint()
    {
        int fingerprint = StableHash(runtimeMapSeed, dirtInfluenceSources.Count,
            Data != null ? Data.width : 0, Data != null ? Data.height : 0);

        foreach (DirtInfluenceSource source in dirtInfluenceSources)
        {
            fingerprint = StableHash(fingerprint, source.sourceAreaId, (int)source.category,
                Mathf.RoundToInt(source.centerCell.x * 1000f),
                Mathf.RoundToInt(source.centerCell.y * 1000f),
                Mathf.RoundToInt(source.outerHintRadius * 1000f),
                Mathf.RoundToInt(source.strength * 1000f),
                source.falsePositive ? 1 : 0, source.substituted ? 1 : 0, source.suppressed ? 1 : 0);
        }

        if (Data != null && dirtInfluenceField != null)
        {
            for (int x = 0; x < Data.width; x++)
            {
                for (int y = 0; y < Data.height; y++)
                {
                    DirtInfluenceSample sample = dirtInfluenceField[x, y];
                    if (sample.totalStrength <= 0.001f) continue;
                    fingerprint = StableHash(fingerprint, x, y, (int)sample.strongestCategory,
                        Mathf.RoundToInt(sample.totalStrength * 100000f),
                        Mathf.RoundToInt(sample.tintMultiplier.r * 100000f),
                        Mathf.RoundToInt(sample.tintMultiplier.g * 100000f),
                        Mathf.RoundToInt(sample.tintMultiplier.b * 100000f));
                }
            }
        }

        return fingerprint.ToString("X8");
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

    private float GetDirtDistanceFactor(Vector2Int cell)
    {
        int distance = DistanceToNearestGeneratedArea(cell);
        float start = Mathf.Min(map.darkDistance, map.darkerDistance);
        float end = Mathf.Max(start + 1f, Mathf.Max(map.darkDistance, map.darkerDistance));
        float factor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, distance));
        float noise = (SmoothStableNoise(runtimeMapSeed, cell.x, cell.y, 17) - 0.5f) * 2f * distanceTransitionNoise;
        return Mathf.Clamp01(factor + noise * factor * (1f - factor));
    }

    private Color GetDirtDistanceTint(Vector2Int cell)
    {
        float factor = GetDirtDistanceFactor(cell) * Mathf.Clamp01(outerDirtStrength);
        Color tint = Color.Lerp(Color.white, outerDirtTint, factor);
        tint.a = 1f;
        return tint;
    }

    private static Color MultiplyTint(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    }

    private TileBase GetDirtTileByDistance(Vector2Int cell)
    {
        float factor = GetDirtDistanceFactor(cell);
        if (factor >= 0.78f && darkerDirtTile != null) return darkerDirtTile;
        if (factor >= 0.28f && darkDirtTile != null) return darkDirtTile;
        int dirtType = Data != null ? Data.GetDirtType(cell) : 0;
        float retainedVariation = Mathf.Lerp(1f, Mathf.Clamp01(outerVariationRetention), factor);
        if (Stable01(runtimeMapSeed, cell.x, cell.y, 1703) > retainedVariation) dirtType = 0;
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
            case AreaType.FillerPocketLoot: return rng.Next(2, 4);
            default: return rng.Next(2, 5);
        }
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

    private int DistanceToNearestGeneratedArea(Vector2Int cell)
    {
        int best = int.MaxValue;

        foreach (Vector2Int p in generatedAreaCells)
        {
            int d = Mathf.Abs(cell.x - p.x) + Mathf.Abs(cell.y - p.y);
            if (d < best) best = d;
        }

        return best == int.MaxValue ? 9999 : best;
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

    private void DrawFormationClearanceGizmos()
    {
        if (!showFormationClearanceDebug || Data == null) return;
        Vector3 cellSize = new Vector3(map.cellSize * 0.72f, map.cellSize * 0.72f, 0.02f);

        Gizmos.color = new Color(0.28f, 0.3f, 0.34f, 0.9f);
        foreach (Vector2Int cell in terrainFormationCells)
            Gizmos.DrawCube(CellToWorld(cell), cellSize);
        Gizmos.color = new Color(0.55f, 0.27f, 0.08f, 0.92f);
        foreach (Vector2Int cell in rootFormationCells)
            Gizmos.DrawCube(CellToWorld(cell), cellSize * 0.9f);
        foreach (TerrainFormation formation in terrainFormations)
        {
            if (formation.type != TerrainFormationType.Root) continue;
            Gizmos.color = new Color(1f, 0.62f, 0.05f, 0.95f);
            foreach (Vector2Int cell in formation.mainPathCells)
                if (rootFormationCells.Contains(cell)) Gizmos.DrawWireCube(CellToWorld(cell), cellSize * 0.72f);
            Gizmos.color = new Color(1f, 0.82f, 0.12f, 0.92f);
            foreach (Vector2Int cell in formation.branchCells)
                if (rootFormationCells.Contains(cell)) Gizmos.DrawWireCube(CellToWorld(cell), cellSize * 0.55f);
            Gizmos.color = new Color(0.34f, 0.12f, 0.04f, 0.95f);
            foreach (Vector2Int cell in formation.knotCells)
                if (rootFormationCells.Contains(cell)) Gizmos.DrawSphere(CellToWorld(cell), map.cellSize * 0.12f);
            Gizmos.color = Color.white;
            Vector3 originWorld = CellToWorld(formation.centerCell);
            Gizmos.DrawWireSphere(originWorld, map.cellSize * 0.28f);
            Gizmos.DrawLine(originWorld, originWorld + (Vector3)(formation.dominantDirection.normalized * map.cellSize * 2f));
            Gizmos.color = new Color(1f, 0.35f, 0.02f, 0.95f);
            foreach (Vector2Int cell in formation.majorBranchCells)
                if (rootFormationCells.Contains(cell)) Gizmos.DrawWireCube(CellToWorld(cell), cellSize * 0.62f);
            Gizmos.color = new Color(1f, 0.92f, 0.25f, 0.95f);
            foreach (Vector2Int cell in formation.minorBranchCells)
                if (rootFormationCells.Contains(cell)) Gizmos.DrawWireCube(CellToWorld(cell), cellSize * 0.42f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(originWorld + Vector3.up * map.cellSize * 0.6f,
                "Root " + formation.formationId + "  source=" + formation.rootOriginType +
                "  cells=" + formation.cells.Count + "  trunk=" + formation.mainPathCells.Count +
                "  branches=" + formation.branchCount + "  stone=" + formation.stoneOverlapCells.Count);
#endif
        }
        Gizmos.color = Color.red;
        foreach (Vector2Int origin in rejectedRootOrigins)
            Gizmos.DrawWireSphere(CellToWorld(origin), map.cellSize * 0.42f);

        Gizmos.color = Color.cyan;
        foreach (Vector2Int cell in rootStoneOverlapCells)
            Gizmos.DrawWireCube(CellToWorld(cell), cellSize * 1.08f);
        Gizmos.color = new Color(0.75f, 0.1f, 1f, 0.95f);
        foreach (Vector2Int cell in fusedRootSafetyCells)
            Gizmos.DrawWireSphere(CellToWorld(cell), map.cellSize * 0.25f);


        Gizmos.color = new Color(1f, 0.08f, 0.08f, 0.18f);
        foreach (Vector2Int cell in formationClearanceBlockedCells)
            Gizmos.DrawCube(CellToWorld(cell), cellSize * 0.76f);

        Gizmos.color = new Color(1f, 0.9f, 0.05f, 0.45f);
        foreach (Vector2Int cell in requiredTraversalCells)
            Gizmos.DrawCube(CellToWorld(cell), cellSize * 0.46f);

        Gizmos.color = new Color(1f, 0f, 1f, 0.9f);
        foreach (Vector2Int cell in invalidFormationGapCells)
            Gizmos.DrawCube(CellToWorld(cell), cellSize * 0.62f);

        Gizmos.color = new Color(1f, 0.45f, 0f, 0.95f);
        foreach (Vector2Int cell in erodedFormationCells)
            Gizmos.DrawWireCube(CellToWorld(cell), cellSize);

        Gizmos.color = new Color(0.1f, 1f, 0.25f, 0.16f);
        foreach (Vector2Int cell in clearanceReachableCells)
            Gizmos.DrawCube(CellToWorld(cell), cellSize * 0.28f);

        Vector3 spawnWorld = CellToWorld(map.spawnCenter);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(spawnWorld, GetBasePlayerTraversalRadius());
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(spawnWorld, GetBaseBuddyTraversalRadius());
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(spawnWorld, GetAuthoritativeTraversalRadius());
    }

    private void OnDrawGizmosSelected()
    {
        if (Data == null) return;
        DrawFormationClearanceGizmos();

        if (showDirtInfluenceSources)
        {
            foreach (DirtInfluenceSource source in dirtInfluenceSources)
            {
                Color debugColor = GetDebugCategoryColor(source.category);
                debugColor.a = source.suppressed ? 0.3f : source.falsePositive ? 0.65f : 1f;
                Gizmos.color = debugColor;

                Vector3 center = new Vector3(
                    Data.origin.x + (source.centerCell.x + 0.5f) * map.cellSize,
                    Data.origin.y + (source.centerCell.y + 0.5f) * map.cellSize,
                    dirtInfluenceDebugZ);
                Gizmos.DrawSphere(center, map.cellSize * (source.falsePositive ? 0.5f : 0.38f));

                float axisAngle = Stable01(source.deterministicSeed, 43) * 360f;
                float stretch = Mathf.Lerp(1.05f, 1.3f, Stable01(source.deterministicSeed, 47));
                Matrix4x4 previousMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, axisAngle),
                    new Vector3(source.outerHintRadius * map.cellSize * stretch,
                                source.outerHintRadius * map.cellSize / stretch, 1f));
                Gizmos.DrawWireSphere(Vector3.zero, 1f);
                Gizmos.matrix = previousMatrix;

#if UNITY_EDITOR
                string label = source.category + "  strength=" + source.strength.ToString("0.00") +
                               "  falsePositive=" + source.falsePositive +
                               "  substituted=" + source.substituted +
                               "  suppressed=" + source.suppressed;
                UnityEditor.Handles.color = debugColor;
                UnityEditor.Handles.Label(center + Vector3.up * map.cellSize * 0.7f, label);
#endif
            }
        }

        if (!showDirtInfluenceCells || dirtInfluenceField == null) return;

        for (int x = 0; x < Data.width; x++)
        {
            for (int y = 0; y < Data.height; y++)
            {
                DirtInfluenceSample sample = dirtInfluenceField[x, y];
                if (sample.totalStrength <= 0.001f) continue;

                float normalizedStrength = Mathf.Clamp01(sample.totalStrength /
                    Mathf.Max(0.001f, GetDirtInfluenceCap()));
                Color cellColor = GetDebugCategoryColor(sample.strongestCategory);
                cellColor.a = Mathf.Lerp(0.28f, 0.82f, normalizedStrength);
                Gizmos.color = cellColor;
                Vector3 center = CellToWorld(new Vector2Int(x, y));
                center.z = dirtInfluenceDebugZ + 0.02f;
                Gizmos.DrawCube(center,
                    new Vector3(map.cellSize * 0.82f, map.cellSize * 0.82f, 0.02f));
            }
        }
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
    public static void RunDirtInfluenceBatchValidation()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath,
            UnityEditor.SceneManagement.OpenSceneMode.Single);
        MapGenerator generator = UnityEngine.Object.FindAnyObjectByType<MapGenerator>();
        if (generator == null)
            throw new InvalidOperationException("Dirt influence validation could not find MapGenerator in SampleScene.");

        bool originalRandomSeed = generator.map.randomSeed;
        int originalSeed = generator.map.seed;
        bool originalGenerateOnStart = generator.generateOnStart;
        MapGenerator.DirtInfluenceTuningMode originalTuning = generator.dirtInfluenceTuning;
        int[] validationSeeds = { 24681357, 97531, 424242, 104729, 8675309, 31415926, 1945411817, 501405637, 700038825 };
        bool originalRootFormations = generator.enableRootFormations;
        RunProfile selectedRunProfile = generator.selectedProfile as RunProfile;
        bool originalProfileRandomSeed = selectedRunProfile != null && selectedRunProfile.randomSeed;
        int originalProfileSeed = selectedRunProfile != null ? selectedRunProfile.seed : 0;
        MapGenerator.DirtInfluenceTuningMode originalProfileTuning = selectedRunProfile != null
            ? selectedRunProfile.environment.dirtInfluenceTuning
            : originalTuning;
        bool originalProfileRootFormations = selectedRunProfile != null
            ? selectedRunProfile.environment.enableRootFormations
            : originalRootFormations;

        try
        {
            generator.generateOnStart = false;
            generator.map.randomSeed = false;
            generator.dirtInfluenceTuning = MapGenerator.DirtInfluenceTuningMode.VisibleTest;
            if (selectedRunProfile != null)
            {
                selectedRunProfile.randomSeed = false;
                selectedRunProfile.environment.dirtInfluenceTuning = MapGenerator.DirtInfluenceTuningMode.VisibleTest;
            }

            for (int i = 0; i < validationSeeds.Length; i++)
            {
                int validationSeed = validationSeeds[i];
                generator.map.seed = validationSeed;
                if (selectedRunProfile != null) selectedRunProfile.seed = validationSeed;
                generator.enableRootFormations = false;
                if (selectedRunProfile != null) selectedRunProfile.environment.enableRootFormations = false;
                generator.Generate();
                string stoneWithoutRoots = generator.GetFormationFingerprint(MapGenerator.TerrainFormationType.Stone);
                generator.enableRootFormations = true;
                if (selectedRunProfile != null) selectedRunProfile.environment.enableRootFormations = true;
                generator.Generate();
                string firstInfluenceFingerprint = generator.GetDirtInfluenceDiagnosticFingerprint();
                string firstTopology = generator.BuildDebugTextReport();
                string firstFormationFingerprint = generator.GetTerrainFormationFingerprint();
                generator.LogDirtInfluenceReport();
                generator.LogTerrainFormationReport();
                string firstStoneFingerprint = generator.GetFormationFingerprint(MapGenerator.TerrainFormationType.Stone);
                generator.LogRootFormationReport();
                generator.LogFormationClearanceReport();
                bool formationSafetyValid = generator.ValidateTerrainFormationState();
                if (!formationSafetyValid)
                    throw new InvalidOperationException("Terrain formation safety validation failed for seed " + validationSeed + ".");

                generator.Generate();
                string secondInfluenceFingerprint = generator.GetDirtInfluenceDiagnosticFingerprint();
                string secondTopology = generator.BuildDebugTextReport();
                string secondFormationFingerprint = generator.GetTerrainFormationFingerprint();
                bool influenceMatches = firstInfluenceFingerprint == secondInfluenceFingerprint;
                bool formationsMatch = firstFormationFingerprint == secondFormationFingerprint;
                string secondStoneFingerprint = generator.GetFormationFingerprint(MapGenerator.TerrainFormationType.Stone);
                bool topologyMatches = firstTopology == secondTopology;
                bool stonesMatch = stoneWithoutRoots == firstStoneFingerprint && firstStoneFingerprint == secondStoneFingerprint;
                Debug.Log("VISIBLE TEST MAP " + (i + 1) + " | seed=" + validationSeed +
                    " topologyMatches=" + topologyMatches +
                    " influenceMatches=" + influenceMatches +
                    " formationsMatch=" + formationsMatch +
                    " stonesUnchanged=" + stonesMatch +
                    " formationFingerprint=" + firstFormationFingerprint +
                    " fingerprint=" + firstInfluenceFingerprint, generator);
                if (!influenceMatches || !formationsMatch || !topologyMatches || !stonesMatch)
                    throw new InvalidOperationException(
                        "Visible Test fixed-seed validation did not reproduce exactly for seed " + validationSeed + ".");
            }
        }
        finally
        {
            generator.map.randomSeed = originalRandomSeed;
            generator.map.seed = originalSeed;
            generator.generateOnStart = originalGenerateOnStart;
            generator.dirtInfluenceTuning = originalTuning;
            generator.enableRootFormations = originalRootFormations;
            if (selectedRunProfile != null)
            {
                selectedRunProfile.randomSeed = originalProfileRandomSeed;
                selectedRunProfile.seed = originalProfileSeed;
                selectedRunProfile.environment.dirtInfluenceTuning = originalProfileTuning;
                selectedRunProfile.environment.enableRootFormations = originalProfileRootFormations;
            }
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MapGenerator generator = (MapGenerator)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Map Profile Tools", EditorStyles.boldLabel);
        string selectedLabel = generator.selectedProfile != null ? generator.selectedProfile.name : "None";
        EditorGUILayout.HelpBox("Selected: " + selectedLabel + "\nActive: " + (RunProfileCoordinator.ActiveDisplayName ?? "Not applied") + "\nStatus: " + (RunProfileCoordinator.LastStatus ?? "Not applied"), MessageType.Info);

        if (GUILayout.Button("Set Default First Level Inspector Settings"))
        {
            Undo.RecordObject(generator, "Set Default Branch Map Settings");
            generator.SetDefaultFirstLevelInspectorSettings();
            EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("Apply Selected Complete Run Profile To Inspector"))
        {
            Undo.RecordObject(generator, "Load Branch Map Profile");
            RunProfileCoordinator.ValidateAndApply(generator.selectedProfile, generator, UnityEngine.Object.FindAnyObjectByType<RunContentSpawner>(FindObjectsInactive.Include));
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

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Dirt Influence Diagnostics", EditorStyles.boldLabel);

        if (GUILayout.Button("Rebuild Dirt Influence"))
        {
            generator.RebuildDirtInfluence();
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Refresh Dirt Tint"))
        {
            generator.RefreshDirtTint();
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Log Dirt Influence Report"))
            generator.LogDirtInfluenceReport();

        if (GUILayout.Button("Run Dirt Influence Color Diagnostic"))
        {
            generator.RunDirtInfluenceColorDiagnostic();
            SceneView.RepaintAll();
        }
    }
}
#endif
