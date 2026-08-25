using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.CampLifecycle;

public class CampStartRoutineManager : MonoBehaviour
{
    public static CampStartRoutineManager Instance { get; private set; }

    [Header("Camp Start Routine")]
    public bool runRoutineOnCampOpen = true;
    public bool sendBuddiesToFireOnNormalVisits = true;
    public bool waitForFireRecoveryBeforeFreeWander = true;

    [Header("Core Scene Locations")]
    [Tooltip("Real scene Transform near the campfire. Buddies walk here after digging and on normal returns.")]
    public Transform fireGatherPoint;
    [Tooltip("Fallback Camp wander anchors used when no established ResidentialRest points exist.")]
    public Transform[] defaultWanderAnchors;

    [Header("Canonical Residential Presentation")]
    public CampResidentialPresentation residentialPresentation;

    [Header("Messages")]
    [TextArea(2, 5)] public string normalReturnPopup = "The camp is quiet enough. Go sit by the fire when you're ready.";
    [TextArea(2, 5)] public string afterRecoveryPopup = "Bellies full. Everyone starts wandering again.";

    [Header("First Buddy Home Milestone")]
    public float buddyDigDuration = 0.18f;
    public float fireArrivalStagingSeconds = 0.8f;
    [TextArea(2, 5)] public string firstHomeMessage = "Your first buddy has made a home here. Use Squad Select to choose which gobbos come on runs and which stay at Camp.";

    [Header("Movement")]
    public float startRoutineDelay = 0.25f;
    public float directedWalkSpeed = 1.5f;
    public float fireWanderRadius = 0.9f;
    public float residentialWanderRadius = 1.1f;
    public float constructionMoveTimeout = 3f;

    private bool routineStarted;
    private bool waitingForRecovery;
    private readonly List<Transform> fireWaitingPoints = new List<Transform>();
    private bool activityPointsInitialized;

    void Awake()
    {
        Instance = this;
        EnsureActivityPoints();
    }

    void Start()
    {
        EnsureActivityPoints();
        ApplyUnlockedAreaVisibility();
        if (fireGatherPoint == null) Debug.LogWarning("CampStartRoutineManager needs Fire Gather Point assigned.", this);
    }

    public void BeginCampVisit()
    {
        if (!runRoutineOnCampOpen || routineStarted) return;
        routineStarted = true;
        ApplyUnlockedAreaVisibility();
        StartCoroutine(CampOpenRoutine());
    }

    IEnumerator CampOpenRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, startRoutineDelay));
        yield return WaitForSpawnedBuddiesIfAny();
        if (GameState.Instance == null) yield break;

        bool presentedMilestone = false;
        GameState.Instance.campTerrainState ??= new CampTerrainState();
        GameState.Instance.RepairRosterState();
        HashSet<string> previouslyHomeless = new HashSet<string>();
        if (GameState.Instance.ownedGobbos != null)
            foreach (GobboUnitSaveData gobbo in GameState.Instance.ownedGobbos)
                if (CampResidentialOccupancyResolver.IsLivingBuddy(gobbo) && gobbo.campResidentialSlotId <= 0)
                    previouslyHomeless.Add(gobbo.uniqueId);
        CampResidentialOccupancyRepair occupancy = CampResidentialOccupancyResolver.Repair(GameState.Instance);
        if (occupancy.Changed)
        {
            ApplyHomePresentation();
            SporeSaveManager.SaveCurrentSlotFromGameState();
        }
        int constructionCapacity = Mathf.Max(0, CampSpatialPolicy.StageOneSlotCapacity -
            GameState.Instance.campTerrainState.residentialSlotsEstablished);
        int constructionCount = Mathf.Min(constructionCapacity, occupancy.Resolution.UnassignedLivingBuddyIds.Count);
        List<string> vacancyClaims = new List<string>();
        if (GameState.Instance.ownedGobbos != null)
            foreach (GobboUnitSaveData gobbo in GameState.Instance.ownedGobbos)
                if (gobbo != null && previouslyHomeless.Contains(gobbo.uniqueId) && gobbo.campResidentialSlotId > 0)
                    vacancyClaims.Add(gobbo.uniqueId);
        bool arrivalPhase = CampArrivalPolicy.ShouldBegin(vacancyClaims.Count, constructionCount);
        if (arrivalPhase) StageAllBuddiesAtFire();
        int completedConstructions = 0;
        if (constructionCount > 0)
        {
            yield return RunMissingResidentialSlots(occupancy.Resolution.UnassignedLivingBuddyIds, constructionCount);
            completedConstructions = residentialConstructionsCompleted;
            presentedMilestone = CampSpatialPolicy.ShouldPresentResidentialMilestone(completedConstructions);
        }
        if (arrivalPhase && constructionCount == 0) ReleaseBuddiesToResidentialOrDefaultAnchors();
        if (occupancy.Resolution.UnassignedLivingBuddyIds.Count > constructionCapacity)
            Debug.LogWarning("Living buddy population exceeds implemented Stage 1 residential capacity; later stages remain deferred.", this);

        if (ShouldEstablishMemorial())
        {
            EstablishMemorial();
            presentedMilestone = true;
        }
        else RefreshEstablishedMemorial();

        if (presentedMilestone || arrivalPhase) yield break;
        ReleaseBuddiesToResidentialOrDefaultAnchors();
    }

    bool ShouldEstablishMemorial()
    {
        if (GameState.Instance == null) return false;
        GameState.Instance.campTerrainState ??= new CampTerrainState();
        GobboUnitSaveData leader = GameState.Instance.GetLeader();
        bool validLeader = leader != null && CampLifecyclePolicy.IsValidLivingLeader(
            leader.uniqueId, leader.isDead, leader.health, leader.isLeader);
        bool hasDeath = GameState.Instance.deathHistory != null && GameState.Instance.deathHistory.Exists(record => record != null);
        return CampLifecyclePolicy.ShouldEstablishMemorial(hasDeath, GameState.Instance.lineageEnded,
            validLeader, GameState.Instance.campTerrainState.memorialEstablished);
    }

    void EstablishMemorial()
    {
        GameState.Instance.campTerrainState.memorialEstablished = true;
        CampBonesMemorialManager manager = Object.FindAnyObjectByType<CampBonesMemorialManager>(FindObjectsInactive.Include);
        if (manager != null) manager.EstablishPresentation();
        else
        {
            CampOldBonesWall wall = Object.FindAnyObjectByType<CampOldBonesWall>(FindObjectsInactive.Include);
            if (wall != null) wall.RefreshVisibility();
            CampMessageUI.Show("Someone has been lost. The Camp has made a place to remember the dead.");
        }
        SporeSaveManager.SaveCurrentSlotFromGameState();
    }

    void RefreshEstablishedMemorial()
    {
        if (GameState.Instance == null || GameState.Instance.campTerrainState == null ||
            !GameState.Instance.campTerrainState.memorialEstablished) return;
        CampBonesMemorialManager manager = Object.FindAnyObjectByType<CampBonesMemorialManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            manager.RefreshWallVisibility();
            manager.TryShowMemorialPopup();
        }
    }

    IEnumerator RunMissingResidentialSlots(List<string> unassignedBuddyIds, int constructionCount)
    {
        residentialConstructionsCompleted = 0;
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>();
        BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>();
        if (terrain == null || buddies.Length == 0)
        {
            Debug.LogWarning("Residential slot construction requires live Camp terrain and a spawned buddy.", this);
            yield break;
        }
        int firstSlot = GameState.Instance.campTerrainState.residentialSlotsEstablished + 1;
        int lastSlot = Mathf.Min(CampSpatialPolicy.StageOneSlotCapacity, firstSlot + constructionCount - 1);
        StageConstructionBuddies(unassignedBuddyIds, constructionCount, buddies);
        for (int slotIndex = firstSlot; slotIndex <= lastSlot; slotIndex++)
        {
            string gobboId = unassignedBuddyIds[slotIndex - firstSlot];
            BuddyUnit buddy = System.Array.Find(buddies, unit => unit != null && unit.unitData != null &&
                unit.unitData.uniqueId == gobboId);
            if (buddy == null)
            {
                Debug.LogWarning("Could not find spawned buddy for residential assignment " + gobboId + ".", this);
                break;
            }
            LogResidentialConstructor(buddy, slotIndex);
            yield return RunResidentialSlotArrival(terrain, buddy, slotIndex);
            if (residentialConstructionSucceeded) residentialConstructionsCompleted++;
            if (!residentialConstructionSucceeded || !residentialPostConstructionSucceeded)
            {
                break;
            }
        }
        ReleaseBuddiesToResidentialOrDefaultAnchors();
    }

    bool residentialConstructionSucceeded;
    bool residentialPostConstructionSucceeded;
    int residentialConstructionsCompleted;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    static void LogResidentialConstructor(BuddyUnit buddy, int slotIndex)
    {
        if (buddy == null || buddy.unitData == null) return;
        Rigidbody2D body = buddy.GetComponent<Rigidbody2D>();
        float colliderRadius = TileMover.GetColliderBodyRadius(body, 0.25f);
        bool active = IsActiveSquadBuddy(buddy.unitData.uniqueId);
        float directedSpeed = CampBuddyPhysicalPolicy.GetMovementSpeed(buddy.unitData.moveSpeed, active);
        Debug.Log("[CampResidential Constructor] id=" + buddy.unitData.uniqueId +
            " object=" + buddy.gameObject.name + " active=" + active +
            " type=" + buddy.unitData.gobboType + " age=" + buddy.unitData.ageStage +
            " localScale=" + buddy.transform.localScale + " lossyScale=" + buddy.transform.lossyScale +
            " colliderRadius=" + colliderRadius.ToString("0.###") +
            " savedMoveSpeed=" + buddy.unitData.moveSpeed.ToString("0.###") +
            " directedWalkSpeed=" + directedSpeed.ToString("0.###") + " slot=" + slotIndex, buddy);
    }

    IEnumerator RunResidentialSlotArrival(HandcraftedCampTerrain terrain, BuddyUnit buddy, int slotIndex)
    {
        residentialConstructionSucceeded = false;
        residentialPostConstructionSucceeded = false;
        ResidentialSlotRecord slot = terrain.GetResidentialSlot(slotIndex);
        if (slot.SlotIndex == 0 || buddy == null) yield break;
        List<Vector2Int> footprint = terrain.GetResidentialSlotFootprint(slotIndex);
        List<Vector2Int> route = terrain.GetResidentialConstructionRoute(slotIndex);
        if (route.Count == 0)
        {
            AbortResidentialConstruction(buddy, null, slotIndex, "Canonical construction route is missing.");
            yield break;
        }
        GameObject targetObject = new GameObject("ResidentialSlotConstructionTarget_RUNTIME");
        Transform target = targetObject.transform;
        CampWander wander = buddy.GetComponent<CampWander>();
        if (wander != null) wander.enabled = false;
        BuddyDigAbility dig = buddy.GetComponent<BuddyDigAbility>();
        if (dig == null) dig = buddy.gameObject.AddComponent<BuddyDigAbility>();
        dig.digDuration = Mathf.Max(0f, buddyDigDuration);
        dig.BindTerrain(terrain);
        float navigationRadius = GetNavigationRadius(buddy);
        if (!dig.enabled || !ReferenceEquals(dig.ResolvedTerrain, terrain))
        {
            AbortResidentialConstruction(buddy, targetObject, slotIndex, "BuddyDigAbility has no valid Camp terrain authority.");
            yield break;
        }
        for (int waypointIndex = 0; waypointIndex < route.Count; waypointIndex++)
        {
            Vector2Int waypoint = route[waypointIndex];
            if (terrain.IsBlocked(waypoint))
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "Construction route waypoint " + waypointIndex + " " + waypoint + " is not open.");
                yield break;
            }
            if (!TileMover.CanOccupy(terrain, terrain.CellToWorld(waypoint), navigationRadius))
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "Construction route waypoint " + waypointIndex + " " + waypoint +
                    " is cell-open but lacks body clearance for radius " +
                    navigationRadius.ToString("0.###") + ".");
                yield break;
            }
            target.position = terrain.CellToWorld(waypoint);
            LogPreDigWaypoint(buddy, slotIndex, waypointIndex, waypoint, target.position,
                terrain.IsBlocked(waypoint), GetCampSpeed(buddy));
            float arrivalDistance = waypointIndex == route.Count - 1
                ? GetConstructionEdgeArrivalDistance(buddy, terrain) : 0.18f;
            yield return MoveConstructionBuddy(buddy, target, arrivalDistance);
            if (!constructionMoveSucceeded)
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "Buddy could not reach route waypoint " + waypointIndex + " " + waypoint +
                    " before the distance-aware timeout. Final world position " + buddy.transform.position +
                    ", target " + target.position + ".");
                yield break;
            }
        }

        int requiredDigActions = 0;
        int successfulDigActions = 0;
        // Open the complete canonical pocket before entering it. Targeting remaining authorized cells
        // lets the generic 0.72 Dig radius remove diagonal clearance without expanding authorization.
        foreach (Vector2Int excavationCell in footprint)
        {
            if (!terrain.IsBlocked(excavationCell)) continue;
            requiredDigActions++;
            TerrainDigResult clearanceResult = new TerrainDigResult(0, 0, 0, TerrainDigFailureReason.None);
            Vector2 excavationWorld = terrain.CellToWorld(excavationCell);
            yield return dig.DigRoutine(excavationWorld, TerrainDigAuthority.ResidentialProgression, 1, footprint,
                result => clearanceResult = result);
            LogResidentialDig(buddy, slotIndex, excavationCell, excavationWorld, dig, clearanceResult,
                !terrain.IsBlocked(excavationCell));
            if (!clearanceResult.Changed)
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "Clearance excavation failed at " + excavationCell + ": " +
                    clearanceResult.FailureReason + ".");
                yield break;
            }
            successfulDigActions++;
        }

        Vector2Int advanceStartCell = new Vector2Int(slot.Approach.x, slot.Approach.y);
        foreach ((int x, int y) authoredTarget in slot.DigTargets)
        {
            Vector2Int targetCell = new Vector2Int(authoredTarget.x, authoredTarget.y);
            Vector2 targetWorld = terrain.CellToWorld(targetCell);
            TerrainDigResult digResult = new TerrainDigResult(0, 0, 0, TerrainDigFailureReason.None);
            if (NeedsResidentialDig(terrain, footprint, targetWorld, dig.digRadius))
            {
                requiredDigActions++;
                yield return dig.DigRoutine(targetWorld, TerrainDigAuthority.ResidentialProgression, 1, footprint,
                    result => digResult = result);
                LogResidentialDig(buddy, slotIndex, targetCell, targetWorld, dig, digResult,
                    !terrain.IsBlocked(targetCell));
                if (!digResult.Changed)
                {
                    AbortResidentialConstruction(buddy, targetObject, slotIndex,
                        "Dig failed at " + targetCell + ": " + digResult.FailureReason +
                        " (evaluated " + digResult.EvaluatedCells + ", eligible " + digResult.EligibleCells + ").");
                    yield break;
                }
                successfulDigActions++;
            }
            yield return null;
            if (terrain.IsBlocked(targetCell))
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "Intended advance cell " + targetCell + " remained blocked after Dig.");
                yield break;
            }
            List<Vector2Int> advanceRoute = BuildPostDigAdvanceRoute(
                terrain, slotIndex, footprint, route, advanceStartCell, targetCell, navigationRadius);
            LogPostDigAdvance(buddy, slotIndex, advanceStartCell, targetCell,
                digResult.RemovedCells, advanceRoute);
            if (advanceStartCell != targetCell && advanceRoute.Count == 0)
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "No open post-Dig route exists from " + advanceStartCell + " to " + targetCell + ".");
                yield break;
            }
            for (int advanceIndex = 0; advanceIndex < advanceRoute.Count; advanceIndex++)
            {
                Vector2Int advanceCell = advanceRoute[advanceIndex];
                if (terrain.IsBlocked(advanceCell))
                {
                    AbortResidentialConstruction(buddy, targetObject, slotIndex,
                        "Post-Dig waypoint " + advanceIndex + " " + advanceCell + " is blocked.");
                    yield break;
                }
                target.position = terrain.CellToWorld(advanceCell);
                yield return MoveConstructionBuddy(buddy, target);
                if (!constructionMoveSucceeded)
                {
                    float finalDistance = buddy != null
                        ? Vector2.Distance(buddy.transform.position, target.position) : float.PositiveInfinity;
                    AbortResidentialConstruction(buddy, targetObject, slotIndex,
                        "Buddy could not reach post-Dig waypoint " + advanceIndex + " " + advanceCell +
                        " on route " + FormatCells(advanceRoute) + ". Final distance " +
                        finalDistance.ToString("0.00") + ", blocked " + terrain.IsBlocked(advanceCell) + ".");
                    yield break;
                }
            }
            advanceStartCell = targetCell;
        }

        int blockedRequiredCells = 0;
        foreach (Vector2Int cell in footprint) if (terrain.IsBlocked(cell)) blockedRequiredCells++;
        Vector2Int finalStandingCell = new Vector2Int(slot.Center.x, slot.Center.y);
        bool reachedFinalStandingCell = advanceStartCell == finalStandingCell &&
            Vector2.Distance(buddy.transform.position, terrain.CellToWorld(finalStandingCell)) <= 0.18f;
        if (!CampSpatialPolicy.CanCommitResidentialConstruction(
                requiredDigActions, successfulDigActions, blockedRequiredCells, reachedFinalStandingCell))
        {
            AbortResidentialConstruction(buddy, targetObject, slotIndex,
                "Canonical footprint is incomplete (required actions " + requiredDigActions +
                ", successful " + successfulDigActions + ", blocked cells " + blockedRequiredCells +
                ", final standing reached " + reachedFinalStandingCell + ").");
            yield break;
        }
        terrain.CompleteResidentialSlotForProgression(1, slotIndex);
        bool assigned = false;
        if (buddy.unitData != null)
        {
            assigned = CampResidentialOccupancyResolver.AssignEstablishedSlot(
                GameState.Instance, buddy.unitData.uniqueId, slotIndex);
            if (assigned) buddy.unitData.campResidentialSlotId = slotIndex;
        }
        bool slotEstablished = GameState.Instance.campTerrainState.residentialSlotsEstablished >= slotIndex;
        if (!CampSpatialPolicy.CanRunResidentialSuccessCompletion(slotEstablished, assigned))
        {
            AbortResidentialConstruction(buddy, targetObject, slotIndex,
                "Slot terrain opened, but stable Gobbo assignment failed; Fire completion was withheld.");
            yield break;
        }
        ApplyHomePresentation();
        if (slotIndex == 1) CampMessageUI.Show(firstHomeMessage);
        SporeSaveManager.SaveCurrentSlotFromGameState();

        if (fireGatherPoint != null)
        {
            for (int waypointIndex = route.Count - 1; waypointIndex >= 0; waypointIndex--)
            {
                Vector2Int waypoint = route[waypointIndex];
                target.position = terrain.CellToWorld(waypoint);
                yield return MoveConstructionBuddy(buddy, target);
                if (!constructionMoveSucceeded)
                {
                    Debug.LogWarning("[CampResidential] Slot " + slotIndex +
                        " was established, but its Gobbo could not exit through route waypoint " +
                        waypointIndex + " " + waypoint + ". The valid slot remains committed.", buddy);
                    residentialConstructionSucceeded = true;
                    Destroy(targetObject);
                    yield break;
                }
            }
            target.position = GetFireWaitingPoint(slotIndex).position;
            yield return MoveConstructionBuddy(buddy, target);
            if (!constructionMoveSucceeded)
            {
                Debug.LogWarning("[CampResidential] Slot " + slotIndex +
                    " was established, but its Gobbo could not reach FireSocial. " +
                    "The valid slot remains committed and normal Camp behavior will resume.", buddy);
                residentialConstructionSucceeded = true;
                Destroy(targetObject);
                yield break;
            }
            if (fireArrivalStagingSeconds > 0f) yield return new WaitForSeconds(fireArrivalStagingSeconds);
        }
        residentialConstructionSucceeded = true;
        residentialPostConstructionSucceeded = true;
        Destroy(targetObject);
    }

    void AbortResidentialConstruction(BuddyUnit buddy, GameObject targetObject, int slotIndex, string reason)
    {
        residentialConstructionSucceeded = false;
        residentialPostConstructionSucceeded = false;
        Debug.LogWarning("[CampResidential] Slot " + slotIndex + " construction failed: " + reason +
            " Milestone remains pending.", buddy != null ? buddy : this);
        if (targetObject != null) Destroy(targetObject);
        if (buddy == null) return;
        CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
        if (walker == null) walker = buddy.gameObject.AddComponent<CampDirectedWalk>();
        CampWander wander = buddy.GetComponent<CampWander>();
        if (wander == null) wander = buddy.gameObject.AddComponent<CampWander>();
        wander.enabled = false;
        walker.destroyWhenDone = false;
        walker.enableWanderWhenDone = false;
        walker.BeginWalk(GetFireWaitingPoint(slotIndex), GetCampSpeed(buddy));
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    static void LogPreDigWaypoint(BuddyUnit buddy, int slotIndex, int waypointIndex,
        Vector2Int cell, Vector3 world, bool blocked, float speed)
    {
        Rigidbody2D body = buddy != null ? buddy.GetComponent<Rigidbody2D>() : null;
        float radius = TileMover.GetColliderBodyRadius(body, 0.25f);
        float distance = buddy != null ? Vector2.Distance(buddy.transform.position, world) : float.PositiveInfinity;
        Debug.Log("[CampResidential PreDig] id=" + (buddy?.unitData?.uniqueId ?? "unknown") +
            " slot=" + slotIndex + " waypoint=" + waypointIndex + " cell=" + cell +
            " world=" + world + " start=" + (buddy != null ? buddy.transform.position.ToString() : "missing") +
            " distance=" + distance.ToString("0.###") + " radius=" + radius.ToString("0.###") +
            " speed=" + speed.ToString("0.###") + " blocked=" + blocked, buddy);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    static void LogResidentialDig(BuddyUnit buddy, int slotIndex, Vector2Int targetCell,
        Vector2 targetWorld, BuddyDigAbility dig, TerrainDigResult result, bool advanceCellOpen)
    {
        Debug.Log("[CampResidential] Buddy " + (buddy?.unitData?.uniqueId ?? "unknown") +
            " Slot " + slotIndex + " target " + targetCell + " world " + targetWorld +
            " radius " + dig.digRadius.ToString("0.00") + " authority ResidentialProgression" +
            " evaluated " + result.EvaluatedCells + " eligible " + result.EligibleCells +
            " removed " + result.RemovedCells + " advanceOpen " + advanceCellOpen +
            " result " + result.FailureReason, buddy);
    }

    static bool NeedsResidentialDig(HandcraftedCampTerrain terrain, List<Vector2Int> footprint,
        Vector2 targetWorld, float radius)
    {
        foreach (Vector2Int cell in footprint)
            if (terrain.IsBlocked(cell) && Vector2.Distance(terrain.CellToWorld(cell), targetWorld) <= radius) return true;
        return false;
    }

    static List<Vector2Int> BuildPostDigAdvanceRoute(HandcraftedCampTerrain terrain, int slotIndex,
        List<Vector2Int> currentFootprint, List<Vector2Int> constructionRoute,
        Vector2Int start, Vector2Int goal, float navigationRadius)
    {
        HashSet<(int x, int y)> open = new HashSet<(int x, int y)>();
        for (int establishedSlot = 1; establishedSlot < slotIndex; establishedSlot++)
            foreach (Vector2Int cell in terrain.GetResidentialSlotFootprint(establishedSlot))
                if (!terrain.IsBlocked(cell)) open.Add((cell.x, cell.y));
        foreach (Vector2Int cell in currentFootprint)
            if (!terrain.IsBlocked(cell)) open.Add((cell.x, cell.y));
        foreach (Vector2Int cell in constructionRoute)
            if (!terrain.IsBlocked(cell)) open.Add((cell.x, cell.y));
        if (!terrain.IsBlocked(start)) open.Add((start.x, start.y));
        if (!terrain.IsBlocked(goal)) open.Add((goal.x, goal.y));

        open.RemoveWhere(cell => !TileMover.CanOccupy(terrain,
            terrain.CellToWorld(new Vector2Int(cell.x, cell.y)), navigationRadius));

        List<(int x, int y)> cells = CampSpatialPolicy.BuildOpenCellRoute(
            (start.x, start.y), (goal.x, goal.y), open);
        List<Vector2Int> result = new List<Vector2Int>(cells.Count);
        foreach ((int x, int y) cell in cells) result.Add(new Vector2Int(cell.x, cell.y));
        return result;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    static void LogPostDigAdvance(BuddyUnit buddy, int slotIndex, Vector2Int start,
        Vector2Int target, int removedCells, List<Vector2Int> route)
    {
        Debug.Log("[CampResidential] Slot " + slotIndex + " post-Dig advance start " + start +
            " target " + target + " removed " + removedCells + " route " + FormatCells(route), buddy);
    }

    static string FormatCells(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return "[]";
        return "[" + string.Join(" -> ", cells) + "]";
    }

    bool constructionMoveSucceeded;

    IEnumerator MoveConstructionBuddy(BuddyUnit buddy, Transform target, float confirmedArrivalDistance = 0.18f)
    {
        constructionMoveSucceeded = false;
        if (buddy == null || target == null) yield break;
        CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
        if (walker == null) walker = buddy.gameObject.AddComponent<CampDirectedWalk>();
        walker.destroyWhenDone = false;
        walker.enableWanderWhenDone = false;
        float speed = GetCampSpeed(buddy);
        walker.BeginWalk(target, speed);
        float elapsed = 0f;
        float distance = Vector2.Distance(buddy.transform.position, target.position);
        float timeout = CampBuddyPhysicalPolicy.GetDirectedWalkTimeout(distance, speed, constructionMoveTimeout);
        confirmedArrivalDistance = Mathf.Max(0.01f, confirmedArrivalDistance);
        while (buddy != null && Vector2.Distance(buddy.transform.position, target.position) > confirmedArrivalDistance && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        constructionMoveSucceeded = buddy != null &&
            Vector2.Distance(buddy.transform.position, target.position) <= confirmedArrivalDistance;
    }

    static float GetConstructionEdgeArrivalDistance(BuddyUnit buddy, HandcraftedCampTerrain terrain)
    {
        Rigidbody2D body = buddy != null ? buddy.GetComponent<Rigidbody2D>() : null;
        float radius = TileMover.GetColliderBodyRadius(body, 0.25f);
        float cellSize = terrain != null ? terrain.CellSize : 0.6f;
        return CampBuddyPhysicalPolicy.GetConstructionEdgeArrivalDistance(radius, cellSize);
    }

    static float GetNavigationRadius(BuddyUnit buddy)
    {
        Rigidbody2D body = buddy != null ? buddy.GetComponent<Rigidbody2D>() : null;
        return TileMover.GetColliderBodyRadius(body, 0.25f);
    }

    static void StageConstructionBuddies(List<string> buddyIds, int count, BuddyUnit[] liveBuddies)
    {
        if (buddyIds == null || liveBuddies == null) return;
        int stagedCount = Mathf.Min(Mathf.Max(0, count), buddyIds.Count);
        for (int i = 0; i < stagedCount; i++)
        {
            string id = buddyIds[i];
            BuddyUnit buddy = System.Array.Find(liveBuddies, unit => unit != null && unit.unitData != null &&
                unit.unitData.uniqueId == id);
            if (buddy == null) continue;
            CampWander wander = buddy.GetComponent<CampWander>();
            if (wander != null) wander.enabled = false;
            Rigidbody2D body = buddy.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
        }
    }

    void ApplyHomePresentation()
    {
        CampTerrainState state = GameState.Instance != null ? GameState.Instance.campTerrainState : null;
        int stage = state != null ? state.residentialStage : 0;
        int slots = state != null ? state.residentialSlotsEstablished : 0;
        HashSet<int> occupied = GameState.Instance != null
            ? CampResidentialOccupancyResolver.GetOccupiedEstablishedSlots(GameState.Instance) : new HashSet<int>();
        residentialPresentation?.ApplyProgress(stage, slots, occupied);
        CampSquadSelect squad = Object.FindAnyObjectByType<CampSquadSelect>(FindObjectsInactive.Include);
        if (squad != null) squad.ApplyHomeAvailability(stage >= 1 && slots >= 1);
    }

    IEnumerator WaitForSpawnedBuddiesIfAny()
    {
        if (GameState.Instance == null || GameState.Instance.ownedGobbos == null || GameState.Instance.ownedGobbos.Count == 0) yield break;

        float timer = 0f;
        while (timer < 2f)
        {
            BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
            if (buddies.Length > 0) yield break;
            timer += Time.deltaTime;
            yield return null;
        }
    }

    public void NotifyFireRecovered()
    {
        if (!waitingForRecovery) return;
        waitingForRecovery = false;
        CampMessageUI.Show(afterRecoveryPopup);
        ReleaseBuddiesToResidentialOrDefaultAnchors();
    }

    void ApplyUnlockedAreaVisibility()
    {
        ApplyHomePresentation();
    }

    float GetCampSpeed(BuddyUnit buddy)
    {
        return buddy != null && buddy.unitData != null
            ? CampBuddyPhysicalPolicy.GetMovementSpeed(buddy.unitData.moveSpeed, IsActiveSquadBuddy(buddy.unitData.uniqueId))
            : directedWalkSpeed;
    }

    static bool IsActiveSquadBuddy(string buddyId)
    {
        return GameState.Instance != null && GameState.Instance.activeSquadIds != null &&
               GameState.Instance.activeSquadIds.Contains(buddyId);
    }

    void SendAllBuddiesToTemporaryAnchor(Transform anchor, float radius, bool disableFreeWanderUntilArrived)
    {
        if (anchor == null) return;
        BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit buddy in buddies)
        {
            if (buddy == null) continue;
            CampWander wander = buddy.GetComponent<CampWander>();
            if (wander == null) wander = buddy.gameObject.AddComponent<CampWander>();
            float speed = GetCampSpeed(buddy);
            wander.SetAnchor(anchor, radius, speed);
            wander.enabled = !disableFreeWanderUntilArrived;
            CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
            if (walker == null) walker = buddy.gameObject.AddComponent<CampDirectedWalk>();
            walker.BeginWalk(anchor, speed);
        }
    }

    void ReleaseBuddiesToResidentialOrDefaultAnchors()
    {
        int stage = GameState.Instance != null && GameState.Instance.campTerrainState != null
            ? GameState.Instance.campTerrainState.residentialStage : 0;
        BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < buddies.Length; i++)
        {
            BuddyUnit buddy = buddies[i];
            if (buddy == null) continue;
            CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
            if (walker != null) Destroy(walker);
            CampWander wander = buddy.GetComponent<CampWander>();
            if (wander == null) wander = buddy.gameObject.AddComponent<CampWander>();
            wander.SetSemanticDestinations(GetCampSpeed(buddy));
            wander.enabled = true;
        }
    }

    void InitializeSemanticActivityPoints()
    {
        fireWaitingPoints.Clear();
        if (fireGatherPoint != null)
        {
            Vector2[] offsets =
            {
                new Vector2(-0.7f, 0f), new Vector2(0.7f, 0f),
                new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.75f)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject pointObject = new GameObject("FireSocialWaiting_" + (i + 1));
                pointObject.transform.SetParent(fireGatherPoint.parent, false);
                pointObject.transform.position = (Vector2)fireGatherPoint.position + offsets[i];
                CampActivityPoint point = pointObject.AddComponent<CampActivityPoint>();
                point.kind = CampActivityKind.FireSocial;
                point.available = IsOpenActivityPosition(pointObject.transform.position);
                if (point.available) fireWaitingPoints.Add(pointObject.transform);
            }
        }
        if (defaultWanderAnchors != null)
            foreach (Transform anchor in defaultWanderAnchors)
            {
                if (anchor == null) continue;
                CampActivityPoint point = anchor.GetComponent<CampActivityPoint>();
                if (point == null) point = anchor.gameObject.AddComponent<CampActivityPoint>();
                point.kind = CampActivityKind.GeneralWander;
                point.available = IsOpenActivityPosition(anchor.position);
            }
    }

    public void EnsureActivityPoints()
    {
        if (residentialPresentation == null)
            residentialPresentation = Object.FindAnyObjectByType<CampResidentialPresentation>(FindObjectsInactive.Include);
        if (residentialPresentation == null)
        {
            GameObject owner = new GameObject("CampResidentialPresentation");
            residentialPresentation = owner.AddComponent<CampResidentialPresentation>();
        }
        residentialPresentation.Initialize(Object.FindAnyObjectByType<HandcraftedCampTerrain>());
        if (activityPointsInitialized) return;
        activityPointsInitialized = true;
        InitializeSemanticActivityPoints();
    }

    bool IsOpenActivityPosition(Vector2 position)
    {
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>();
        return terrain == null || !terrain.IsBlocked(terrain.WorldToCell(position));
    }

    Transform GetFireWaitingPoint(int index)
    {
        if (fireWaitingPoints.Count == 0) return fireGatherPoint != null ? fireGatherPoint : transform;
        return fireWaitingPoints[Mathf.Abs(index) % fireWaitingPoints.Count];
    }

    void StageAllBuddiesAtFire()
    {
        BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < buddies.Length; i++)
        {
            BuddyUnit buddy = buddies[i];
            if (buddy == null) continue;
            CampWander wander = buddy.GetComponent<CampWander>();
            if (wander == null) wander = buddy.gameObject.AddComponent<CampWander>();
            wander.enabled = false;
            CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
            if (walker == null) walker = buddy.gameObject.AddComponent<CampDirectedWalk>();
            walker.destroyWhenDone = false;
            walker.enableWanderWhenDone = false;
            walker.BeginWalk(GetFireWaitingPoint(i), GetCampSpeed(buddy));
        }
    }

    Transform GetAnchor(Transform[] anchors, int index)
    {
        if (anchors == null || anchors.Length == 0) return fireGatherPoint != null ? fireGatherPoint : transform;
        int safeIndex = Mathf.Abs(index) % anchors.Length;
        return anchors[safeIndex] != null ? anchors[safeIndex] : transform;
    }

}
