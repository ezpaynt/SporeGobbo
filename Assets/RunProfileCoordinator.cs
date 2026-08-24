using System.Collections.Generic;
using UnityEngine;

public static class RunProfileCoordinator
{
    public static string ActiveProfileId { get; private set; }
    public static string ActiveDisplayName { get; private set; }
    public static string LastStatus { get; private set; }

    public static bool ValidateAndApply(BranchMapProfile selected, MapGenerator map, RunContentSpawner content)
    {
        if (selected == null) return Fail("Selected profile is missing.");
        if (map == null) return Fail("MapGenerator is missing.");
        if (selected.width <= 0 || selected.height <= 0) return Fail("Geometry dimensions must be positive.");
        if (selected.cellSize <= 0f) return Fail("Geometry cell size must be positive.");
        if (selected.spawnRadius < 0 || selected.spawnRadius * 2 >= Mathf.Min(selected.width, selected.height))
            return Fail("Spawn radius is invalid for the map dimensions.");
        if (selected.branches == null || selected.branches.Count == 0) return Fail("At least one branch is required.");

        RunProfile complete = selected as RunProfile;
        if (complete == null)
            return Fail("The selected legacy BranchMapProfile is not a complete RunProfile. Apply a migrated profile.");
        if (complete.identity == null || string.IsNullOrWhiteSpace(complete.identity.stableProfileId) ||
            complete.identity.stableProfileId == "unassigned")
            return Fail("Identity.stableProfileId is missing.");
        if (complete.environment == null || complete.content == null) return Fail("RunProfile sections are missing.");
        if (complete.difficulty != null && complete.difficulty.enabled)
            return Fail("Difficulty overrides are not implemented in this architecture batch.");
        if (complete.developmentOverrides != null && complete.developmentOverrides.enabled && complete.developmentOverrides.unsupportedXpGuarantee)
            return Fail("XP guarantees are not supported.");
        if (map.generateRunContent)
        {
            if (content == null) return Fail("RunContentSpawner is missing.");
            if (complete.content.enemyPrefab == null || complete.content.exitPortalPrefab == null ||
                complete.content.retreatPortalPrefab == null)
                return Fail("Required encounter or portal prefabs are missing.");
        }

        map.LoadProfileIntoInspector(complete);
        ApplyEnvironment(complete.environment, map);
        if (map.generateRunContent) ApplyContent(complete.content, content);
        ApplyDevelopmentOverrides(complete.developmentOverrides, map, content);
        ActiveProfileId = complete.identity.stableProfileId;
        ActiveDisplayName = string.IsNullOrWhiteSpace(complete.identity.displayName) ? complete.name : complete.identity.displayName;
        LastStatus = "Applied " + ActiveDisplayName + " (" + ActiveProfileId + ")";
        Debug.Log("RUN PROFILE APPLIED | id=" + ActiveProfileId + " displayName=" + ActiveDisplayName +
                  " purpose=" + complete.identity.purpose + " map=" + map.map.width + "x" + map.map.height +
                  " branches=" + map.branches.Count + " influence=" + map.enableDirtInfluence +
                  " stones=" + map.enableTerrainFormations + " roots=" + map.enableRootFormations, map);
        return true;
    }

    static void ApplyEnvironment(RunProfile.Environment e, MapGenerator m)
    {
        m.enableDirtInfluence=e.enableDirtInfluence; m.dirtInfluenceTuning=e.dirtInfluenceTuning;
        m.qualifyingAreaInfluenceChance=e.qualifyingAreaInfluenceChance; m.neutralFalsePositiveChance=e.neutralFalsePositiveChance;
        m.neighboringCategoryChance=e.neighboringCategoryChance; m.minimumSourceStrength=e.minimumSourceStrength;
        m.maximumSourceStrength=e.maximumSourceStrength; m.minimumRadiusScale=e.minimumRadiusScale; m.maximumRadiusScale=e.maximumRadiusScale;
        m.sourceCenterOffset=e.sourceCenterOffset; m.maximumCombinedInfluence=e.maximumCombinedInfluence; m.influenceIrregularity=e.influenceIrregularity;
        m.fungalRegionTint=e.fungalRegionTint; m.mineralRegionTint=e.mineralRegionTint; m.disturbedRegionTint=e.disturbedRegionTint;
        m.outerDirtTint=e.outerDirtTint; m.outerDirtStrength=e.outerDirtStrength;
        m.outerVariationRetention=e.outerVariationRetention; m.distanceTransitionNoise=e.distanceTransitionNoise;
        m.lightMaterialDensity=e.lightMaterialDensity; m.mediumMaterialDensity=e.mediumMaterialDensity; m.heavyMaterialDensity=e.heavyMaterialDensity;
        m.materialTransitionWidth=e.materialTransitionWidth; m.internalHoleChance=e.internalHoleChance; m.satellitePatchChance=e.satellitePatchChance;
        m.fingerVeinAmount=e.fingerVeinAmount; m.materialCenterOffsetStrength=e.materialCenterOffsetStrength;
        m.fungalColorStrength=e.fungalColorStrength; m.mineralColorStrength=e.mineralColorStrength; m.disturbedColorStrength=e.disturbedColorStrength;
        m.enableTerrainFormations=e.enableTerrainFormations; m.formationCount=e.formationCount; m.minimumFormationSize=e.minimumFormationSize;
        m.maximumFormationSize=e.maximumFormationSize; m.formationEdgeIrregularity=e.formationEdgeIrregularity;
        m.formationCornerErosion=e.formationCornerErosion; m.detachedChunkChance=e.detachedChunkChance;
        m.formationPlacementPadding=e.formationPlacementPadding; m.formationPlacementAttempts=e.formationPlacementAttempts; m.stoneFormationTint=e.stoneFormationTint;
        m.enableRootFormations=e.enableRootFormations; m.minimumRootCount=e.minimumRootCount; m.maximumRootCount=e.maximumRootCount;
        m.rootCompanionChance=e.rootCompanionChance; m.smallRootWeight=e.smallRootWeight; m.mediumRootWeight=e.mediumRootWeight; m.largeRootWeight=e.largeRootWeight;
        m.minimumRootMainPathLength=e.minimumRootMainPathLength; m.maximumRootMainPathLength=e.maximumRootMainPathLength;
        m.rootDirectionalPersistence=e.rootDirectionalPersistence; m.rootTurnChance=e.rootTurnChance; m.rootKinkChance=e.rootKinkChance;
        m.rootBranchChance=e.rootBranchChance; m.maximumRootBranchDepth=e.maximumRootBranchDepth; m.rootBranchLengthRatio=e.rootBranchLengthRatio;
        m.minimumRootThickness=e.minimumRootThickness; m.maximumRootThickness=e.maximumRootThickness; m.rootKnotChance=e.rootKnotChance;
        m.minimumRootKnotSize=e.minimumRootKnotSize; m.maximumRootKnotSize=e.maximumRootKnotSize; m.rootTaperStrength=e.rootTaperStrength;
        m.rootEdgeOriginPreference=e.rootEdgeOriginPreference; m.rootThroughStoneChance=e.rootThroughStoneChance; m.rootClusterChance=e.rootClusterChance;
        m.rootDetachedFragmentChance=e.rootDetachedFragmentChance; m.rootToStoneMinimumSpacing=e.rootToStoneMinimumSpacing;
        m.rootToRootMinimumSpacing=e.rootToRootMinimumSpacing; m.rootPlacementAttempts=e.rootPlacementAttempts; m.rootFormationTint=e.rootFormationTint;
        m.enableFormationClearanceValidation=e.enableFormationClearanceValidation; m.baseNavigationMargin=e.baseNavigationMargin;
        m.minimumOptionalGapPolicy=e.minimumOptionalGapPolicy; m.maximumErosionCellsPerFormation=e.maximumErosionCellsPerFormation;
        m.validateCriticalConnectivity=e.validateCriticalConnectivity;
    }

    static void ApplyDevelopmentOverrides(RunProfile.DevelopmentOverrides d, MapGenerator m, RunContentSpawner s)
    {
        m.activeDevelopmentOverrides = d != null && d.enabled ? d : null;
        if (s != null) s.activeDevelopmentOverrides = m.activeDevelopmentOverrides;
        if (d == null || !d.enabled) return;
        m.formationCount = Mathf.Max(m.formationCount, d.minimumStoneFormations);
        m.minimumRootCount = Mathf.Max(m.minimumRootCount, d.minimumRootFormations);
        m.maximumRootCount = Mathf.Max(m.maximumRootCount, m.minimumRootCount);
        if (d.requireFungalInfluence || d.requireMineralInfluence || d.requireDisturbedInfluence)
        {
            m.qualifyingAreaInfluenceChance = 1f;
            m.neutralFalsePositiveChance = Mathf.Max(m.neutralFalsePositiveChance, .35f);
            m.neighboringCategoryChance = 1f;
        }
        if (s != null && d.minimumBlobSpitters > 0)
        {
            s.blobSpitterSpawnChance = 1f;
            s.blobSpittersPerCampMin = Mathf.Max(s.blobSpittersPerCampMin, 1);
            s.blobSpittersPerCampMax = Mathf.Max(s.blobSpittersPerCampMax, s.blobSpittersPerCampMin);
        }
        if (s != null && d.minimumSnacks > 0) { s.enableSnackSpawns = true; s.forceSnackSpawn = true; }
        if (s != null && d.requireRetreatPortal) s.spawnRetreatPortalNearSpawn = true;
    }
    static void ApplyContent(RunProfile.Content p, RunContentSpawner s)
    {
        s.enemyPrefab=p.enemyPrefab; s.bossEnemyPrefab=p.bossEnemyPrefab; s.blobSpitterPrefab=p.blobSpitterPrefab;
        s.mushroomPrefab=p.mushroomPrefab; s.sporePrefab=p.sporePrefab; s.shinyPrefab=p.shinyPrefab;
        s.exitPortalPrefab=p.exitPortalPrefab; s.retreatPortalPrefab=p.retreatPortalPrefab; s.enableSnackSpawns=p.enableSnackSpawns;
        s.snackPickupPrefab=p.snackPickupPrefab; s.snackLootTable=new List<RunContentSpawner.WeightedSnackEntry>(p.snackLootTable);
        s.lootPocketSnackChance=p.lootPocketSnackChance; s.combatCampSnackChance=p.combatCampSnackChance;
        s.bossCampSnackChance=p.bossCampSnackChance; s.forceSnackSpawn=p.forceSnackSpawn;
        s.spawnInitialRevealedContentOnStart=p.spawnInitialRevealedContentOnStart; s.objectClearRadius=p.objectClearRadius;
        s.placementTriesPerObject=p.placementTriesPerObject; s.spawnRetreatPortalNearSpawn=p.spawnRetreatPortalNearSpawn;
        s.retreatPortalMinDistanceFromSpawn=p.retreatPortalMinDistanceFromSpawn; s.retreatPortalMaxDistanceFromSpawn=p.retreatPortalMaxDistanceFromSpawn;
        s.retreatPortalClearRadius=p.retreatPortalClearRadius; s.overrideCampWeevilCount=p.overrideCampWeevilCount;
        s.weevilsPerCampMin=p.weevilsPerCampMin; s.weevilsPerCampMax=p.weevilsPerCampMax;
        s.overrideBossCampWeevilCount=p.overrideBossCampWeevilCount; s.weevilsPerBossCampMin=p.weevilsPerBossCampMin;
        s.weevilsPerBossCampMax=p.weevilsPerBossCampMax; s.blobSpittersPerCampMin=p.blobSpittersPerCampMin;
        s.blobSpittersPerCampMax=p.blobSpittersPerCampMax; s.blobSpitterSpawnChance=p.blobSpitterSpawnChance;
        s.blobSpittersPerBossCampMin=p.blobSpittersPerBossCampMin; s.blobSpittersPerBossCampMax=p.blobSpittersPerBossCampMax;
        s.blobSpitterBossSpawnChance=p.blobSpitterBossSpawnChance; s.blobSpitterMinDistanceFromSpawn=p.blobSpitterMinDistanceFromSpawn;
    }

    static bool Fail(string message)
    {
        LastStatus = "Invalid: " + message;
        Debug.LogError("RUN PROFILE INVALID | " + message);
        return false;
    }
}
