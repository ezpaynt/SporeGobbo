using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RunProfile", menuName = "Spore Gobbo/Maps/Complete Run Profile")]
public class RunProfile : BranchMapProfile
{
    public enum Purpose { Production, FullPathTest, FeatureSandbox, CustomDevelopment }

    [System.Serializable]
    public class Identity
    {
        public string stableProfileId = "unassigned";
        public string displayName = "Run Profile";
        [TextArea] public string description;
        public Purpose purpose = Purpose.CustomDevelopment;
        public bool developmentOnly;
        [TextArea] public string notes;
    }

    [System.Serializable]
    public class Environment
    {
        public bool enableDirtInfluence = true;
        public MapGenerator.DirtInfluenceTuningMode dirtInfluenceTuning = MapGenerator.DirtInfluenceTuningMode.Subtle;
        public float qualifyingAreaInfluenceChance = .68f, neutralFalsePositiveChance = .12f, neighboringCategoryChance = .08f;
        public float minimumSourceStrength = .75f, maximumSourceStrength = 1.25f, minimumRadiusScale = .85f, maximumRadiusScale = 1.35f;
        public float sourceCenterOffset = .35f, maximumCombinedInfluence = .06f, influenceIrregularity = .3f;
        public Color fungalRegionTint, mineralRegionTint, disturbedRegionTint;
        public Color outerDirtTint = Color.white;
        public float outerDirtStrength, outerVariationRetention = 1f, distanceTransitionNoise;
        public float lightMaterialDensity, mediumMaterialDensity, heavyMaterialDensity, materialTransitionWidth;
        public float internalHoleChance, satellitePatchChance, fingerVeinAmount, materialCenterOffsetStrength;
        public float fungalColorStrength, mineralColorStrength, disturbedColorStrength;

        public bool enableTerrainFormations = true;
        public int formationCount = 4, minimumFormationSize = 3, maximumFormationSize = 14;
        public float formationEdgeIrregularity = .72f, formationCornerErosion = .58f, detachedChunkChance = .28f;
        public int formationPlacementPadding = 1, formationPlacementAttempts = 350;
        public Color stoneFormationTint;

        public bool enableRootFormations = true;
        public int minimumRootCount = 1, maximumRootCount = 2;
        public float rootCompanionChance, smallRootWeight, mediumRootWeight, largeRootWeight;
        public int minimumRootMainPathLength, maximumRootMainPathLength;
        public float rootDirectionalPersistence, rootTurnChance, rootKinkChance, rootBranchChance, rootBranchLengthRatio;
        public int maximumRootBranchDepth, minimumRootThickness, maximumRootThickness;
        public float rootKnotChance;
        public int minimumRootKnotSize, maximumRootKnotSize;
        public float rootTaperStrength, rootEdgeOriginPreference, rootThroughStoneChance, rootClusterChance, rootDetachedFragmentChance;
        public int rootToStoneMinimumSpacing, rootToRootMinimumSpacing, rootPlacementAttempts;
        public Color rootFormationTint;

        public bool enableFormationClearanceValidation = true;
        public float baseNavigationMargin = .12f;
        public MapGenerator.OptionalGapPolicy minimumOptionalGapPolicy;
        public int maximumErosionCellsPerFormation = 18;
        public bool validateCriticalConnectivity = true;
    }

    [System.Serializable]
    public class Content
    {
        public GameObject enemyPrefab, bossEnemyPrefab, blobSpitterPrefab, mushroomPrefab, sporePrefab;
        public GameObject shinyPrefab, exitPortalPrefab, retreatPortalPrefab;
        public bool enableSnackSpawns;
        public WorldItemPickup snackPickupPrefab;
        public List<RunContentSpawner.WeightedSnackEntry> snackLootTable = new List<RunContentSpawner.WeightedSnackEntry>();
        public float lootPocketSnackChance, combatCampSnackChance, bossCampSnackChance;
        public bool forceSnackSpawn, spawnInitialRevealedContentOnStart;
        public float objectClearRadius;
        public int placementTriesPerObject;
        public bool spawnRetreatPortalNearSpawn;
        public float retreatPortalMinDistanceFromSpawn, retreatPortalMaxDistanceFromSpawn, retreatPortalClearRadius;
        public bool overrideCampWeevilCount;
        public int weevilsPerCampMin, weevilsPerCampMax;
        public bool overrideBossCampWeevilCount;
        public int weevilsPerBossCampMin, weevilsPerBossCampMax;
        public int blobSpittersPerCampMin, blobSpittersPerCampMax;
        public float blobSpitterSpawnChance;
        public int blobSpittersPerBossCampMin, blobSpittersPerBossCampMax;
        public float blobSpitterBossSpawnChance, blobSpitterMinDistanceFromSpawn;
    }

    [System.Serializable]
    public class Difficulty
    {
        public bool enabled;
        public float enemyHealthMultiplier = 1f, enemyDamageMultiplier = 1f, playerDamageMultiplier = 1f;
        public float resourceMultiplier = 1f, xpMultiplier = 1f;
    }

    [System.Serializable]
    public class DevelopmentOverrides
    {
        public bool enabled;
        public int maximumFallbackAttempts = 160;
        public int minimumOrdinaryRooms, minimumCamps, minimumStoneFormations, minimumRootFormations;
        public bool requireFungalInfluence, requireMineralInfluence, requireDisturbedInfluence;
        public int minimumTunnelWeevils, minimumBlobSpitters, minimumMushroomCritters;
        public int minimumMushrooms, minimumSpores, minimumShinies, minimumSnacks;
        public bool requireRetreatPortal, requireExitPortal;
        public bool unsupportedXpGuarantee;
    }

    public Identity identity = new Identity();
    public Environment environment = new Environment();
    public Content content = new Content();
    public Difficulty difficulty = new Difficulty();
    public DevelopmentOverrides developmentOverrides = new DevelopmentOverrides();
}
