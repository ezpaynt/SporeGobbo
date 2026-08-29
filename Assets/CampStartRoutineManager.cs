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
    private bool routineRunning;
    private string currentResidentialConstructorId = "";

    public bool IsCampVisitRoutineRunning => routineRunning;
    public string CurrentResidentialConstructorId => currentResidentialConstructorId;
    public string CurrentResidentialRoomId { get; private set; } = "";
    public int CurrentResidentialSlot { get; private set; }
    public Vector2Int CurrentResidentialStagingCell { get; private set; }
    public Vector2Int CurrentResidentialDigCell { get; private set; }
    public CampDirectedWalkResult CurrentResidentialMovementResult => constructionMoveResult;
    public int LastResidentialDigRemovedCells { get; private set; }
    public string LastResidentialFailureReason { get; private set; } = "";
    public int LastResidentialCompletedCount { get; private set; }
    private bool waitingForRecovery;
    private readonly List<Transform> fireWaitingPoints = new List<Transform>();
    private bool activityPointsInitialized;
    private readonly HashSet<string> firstHomeBuddyIds = new HashSet<string>();
    private readonly Dictionary<string, FirstHomeMoveOperation> firstHomeApproaches =
        new Dictionary<string, FirstHomeMoveOperation>();

    sealed class FirstHomeMoveOperation
    {
        public bool Complete;
        public bool Succeeded;
        public string Failure = "";
    }

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
        StartCoroutine(TrackedCampOpenRoutine());
    }

    IEnumerator TrackedCampOpenRoutine()
    {
        routineRunning = true;
        yield return CampOpenRoutine();
        currentResidentialConstructorId = "";
        CurrentResidentialRoomId = "";
        routineRunning = false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool RestartCampVisitForDevelopment()
    {
        if (routineRunning) return false;
        routineStarted = false;
        BeginCampVisit();
        return routineRunning || routineStarted;
    }

#endif

    IEnumerator CampOpenRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, startRoutineDelay));
        yield return WaitForSpawnedBuddiesIfAny();
        if (GameState.Instance == null) yield break;

        bool presentedMilestone = false;
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>();
        int residentialCapacity = terrain != null
            ? terrain.TotalResidentialCapacity : CampResidentialCatalog.CurrentRuntimeCapacity;
        GameState.Instance.campTerrainState ??= new CampTerrainState();
        GameState.Instance.RepairRosterState();
        HashSet<string> previouslyHomeless = new HashSet<string>();
        int livingBuddyCount = 0;
        if (GameState.Instance.ownedGobbos != null)
            foreach (GobboUnitSaveData gobbo in GameState.Instance.ownedGobbos)
                if (CampResidentialOccupancyResolver.IsLivingBuddy(gobbo))
                {
                    livingBuddyCount++;
                    if (gobbo.campResidentialSlotId <= 0) previouslyHomeless.Add(gobbo.uniqueId);
                }
        CampResidentialOccupancyRepair occupancy = CampResidentialOccupancyResolver.Repair(
            GameState.Instance, residentialCapacity);
        if (occupancy.Changed)
        {
            ApplyHomePresentation();
            SporeSaveManager.SaveCurrentSlotFromGameState();
        }
        List<string> vacancyClaims = new List<string>();
        if (GameState.Instance.ownedGobbos != null)
            foreach (GobboUnitSaveData gobbo in GameState.Instance.ownedGobbos)
                if (gobbo != null && CampArrivalPolicy.IsFirstHomeClaim(
                        previouslyHomeless.Contains(gobbo.uniqueId), gobbo.campResidentialSlotId))
                    vacancyClaims.Add(gobbo.uniqueId);
        CampResidentialArrivalEvaluation arrival = CampArrivalPolicy.EvaluateResidentialWork(
            occupancy.Resolution, livingBuddyCount, vacancyClaims.Count,
            GameState.Instance.campTerrainState.residentialSlotsEstablished,
            residentialCapacity);
        int constructionCapacity = Mathf.Max(0, residentialCapacity - arrival.EstablishedCapacity);
        int constructionCount = arrival.PendingConstructionCount;
        bool arrivalPhase = arrival.ArrivalPhase;
        LogResidentialArrival(arrival, occupancy.Resolution.UnassignedLivingBuddyIds,
            Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None));
        firstHomeBuddyIds.Clear();
        foreach (string id in vacancyClaims) firstHomeBuddyIds.Add(id);
        int reservedConstructionCount = Mathf.Min(constructionCount,
            occupancy.Resolution.UnassignedLivingBuddyIds.Count);
        for (int i = 0; i < reservedConstructionCount; i++)
            firstHomeBuddyIds.Add(occupancy.Resolution.UnassignedLivingBuddyIds[i]);

        if (arrivalPhase)
        {
            bool staged = false;
            yield return RunConcurrentFirstHomeApproaches(terrain, vacancyClaims,
                occupancy.Resolution.UnassignedLivingBuddyIds, reservedConstructionCount,
                result => staged = result);
            if (!staged) yield break;
        }
        int completedConstructions = 0;
        LastResidentialCompletedCount = 0;
        if (constructionCount > 0)
        {
            yield return RunMissingResidentialSlots(occupancy.Resolution.UnassignedLivingBuddyIds, constructionCount);
            completedConstructions = residentialConstructionsCompleted;
            LastResidentialCompletedCount = completedConstructions;
            presentedMilestone = CampSpatialPolicy.ShouldPresentResidentialMilestone(completedConstructions);
        }
        if (arrivalPhase)
        {
            bool approachesSucceeded = false;
            yield return WaitForAllFirstHomeApproaches(result => approachesSucceeded = result);
            if (!approachesSucceeded) yield break;
        }
        if (arrivalPhase && constructionCount == 0)
            ReleaseNonFirstHomeBuddiesToNormalBehavior();
        if (occupancy.Resolution.UnassignedLivingBuddyIds.Count > constructionCapacity)
            Debug.LogWarning("Living buddy population exceeds currently authored residential capacity; later rooms remain deferred.", this);

        if (ShouldEstablishMemorial())
        {
            EstablishMemorial();
            presentedMilestone = true;
        }
        else RefreshEstablishedMemorial();

        if (presentedMilestone || arrivalPhase) yield break;
        ReleaseBuddiesToResidentialOrDefaultAnchors();
    }

    IEnumerator RunConcurrentFirstHomeApproaches(HandcraftedCampTerrain terrain,
        List<string> vacancyClaims, List<string> constructionBuddyIds, int constructionCount,
        System.Action<bool> completed)
    {
        BuddyUnit[] liveBuddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        firstHomeApproaches.Clear();

        foreach (string id in vacancyClaims)
        {
            BuddyUnit buddy = FindLiveBuddy(liveBuddies, id);
            GobboUnitSaveData saved = GameState.Instance?.FindOwnedGobbo(id);
            if (buddy == null || saved == null || saved.campResidentialSlotId <= 0)
            {
                LastResidentialFailureReason = "Vacant-home claimant " + id + " has no live Buddy or assigned slot.";
                Debug.LogError("[CampResidential Implementation] " + LastResidentialFailureReason, this);
                completed(false);
                yield break;
            }
            FirstHomeMoveOperation operation = new FirstHomeMoveOperation();
            firstHomeApproaches.Add(id, operation);
            StartCoroutine(RunFirstHomeRoute(terrain, buddy, saved.campResidentialSlotId, true, operation));
        }

        int firstSlot = GameState.Instance.campTerrainState.residentialSlotsEstablished + 1;
        List<int> reservedSlots = CampArrivalPolicy.ReserveContiguousConstructionSlots(
            firstSlot, constructionCount);
        for (int i = 0; i < constructionCount; i++)
        {
            string id = constructionBuddyIds[i];
            BuddyUnit buddy = FindLiveBuddy(liveBuddies, id);
            if (buddy == null)
            {
                LastResidentialFailureReason = "Reserved constructor " + id + " has no spawned Buddy.";
                Debug.LogError("[CampResidential Implementation] " + LastResidentialFailureReason, this);
                completed(false);
                yield break;
            }
            FirstHomeMoveOperation operation = new FirstHomeMoveOperation();
            firstHomeApproaches.Add(id, operation);
            StartCoroutine(RunFirstHomeRoute(terrain, buddy, reservedSlots[i], false, operation));
        }
        completed(true);
        yield break;
    }

    IEnumerator WaitForAllFirstHomeApproaches(System.Action<bool> completed)
    {
        foreach (FirstHomeMoveOperation operation in firstHomeApproaches.Values)
            while (!operation.Complete) yield return null;
        foreach (FirstHomeMoveOperation operation in firstHomeApproaches.Values)
            if (!operation.Succeeded)
            {
                LastResidentialFailureReason = operation.Failure;
                Debug.LogError("[CampResidential Implementation] Concurrent first-home approach stopped: " +
                    operation.Failure + " No retry or fallback was attempted.", this);
                completed(false);
                yield break;
            }
        completed(true);
    }

    IEnumerator RunFirstHomeRoute(HandcraftedCampTerrain terrain, BuddyUnit buddy, int slotId,
        bool moveToEstablishedHome, FirstHomeMoveOperation operation)
    {
        List<Vector2Int> route = terrain != null
            ? terrain.GetResidentialConstructionRoute(slotId) : new List<Vector2Int>();
        if (moveToEstablishedHome && terrain?.GetResidentialCatalog()?.GetSlot(slotId) is
            CampResidentialSlotDefinition establishedSlot)
            foreach ((int x, int y) cell in establishedSlot.DigTargets)
                route.Add(new Vector2Int(cell.x, cell.y));

        List<Vector2Int> openRoute = new List<Vector2Int>();
        foreach (Vector2Int cell in route)
        {
            if (terrain.IsBlocked(cell)) break;
            openRoute.Add(cell);
        }
        if (moveToEstablishedHome && openRoute.Count != route.Count)
        {
            operation.Failure = "Established home route for slot " + slotId + " contains blocked terrain.";
            operation.Complete = true;
            yield break;
        }
        if (!moveToEstablishedHome && openRoute.Count == 0)
        {
            operation.Failure = "Constructor route for slot " + slotId + " has no currently-open approach cell.";
            operation.Complete = true;
            yield break;
        }

        foreach (Vector2Int cell in openRoute)
        {
            FirstHomeMoveOperation move = new FirstHomeMoveOperation();
            yield return MoveFirstHomeBuddy(buddy, terrain.CellToWorld(cell), move);
            if (!move.Succeeded)
            {
                operation.Failure = "Buddy " + buddy.unitData.uniqueId + " could not reach first-home route cell " +
                    cell + " for slot " + slotId + ": " + move.Failure;
                operation.Complete = true;
                yield break;
            }
        }

        if (moveToEstablishedHome)
            CompleteFirstHomeMovement(buddy);
        operation.Succeeded = true;
        operation.Complete = true;
    }

    IEnumerator MoveFirstHomeBuddy(BuddyUnit buddy, Vector2 worldTarget, FirstHomeMoveOperation operation)
    {
        GameObject targetObject = new GameObject("FirstHomeMoveTarget_RUNTIME");
        targetObject.transform.position = worldTarget;
        Rigidbody2D body = buddy != null ? buddy.GetComponent<Rigidbody2D>() : null;
        CampDirectedWalk walker = buddy != null ? buddy.GetComponent<CampDirectedWalk>() : null;
        if (buddy == null || body == null)
        {
            operation.Failure = "missing Buddy physics body";
            operation.Complete = true;
            Destroy(targetObject);
            yield break;
        }
        if (walker == null) walker = buddy.gameObject.AddComponent<CampDirectedWalk>();
        CampWander wander = buddy.GetComponent<CampWander>();
        if (wander != null) wander.enabled = false;
        walker.destroyWhenDone = false;
        walker.enableWanderWhenDone = false;
        walker.bodyRadius = TileMover.GetColliderBodyRadius(body, walker.bodyRadius);
        float speed = GetFirstHomeSpeed(buddy);
        float distance = Vector2.Distance(body.position, worldTarget);
        walker.BeginWalk(targetObject.transform, speed, 0.18f,
            CampBuddyPhysicalPolicy.GetDirectedWalkTimeout(distance, speed, constructionMoveTimeout));
        while (buddy != null && walker.IsWalking) yield return null;
        operation.Succeeded = buddy != null && walker.Result == CampDirectedWalkResult.Arrived;
        operation.Failure = buddy != null ? walker.Result + " / " + walker.BlockingColliderDescription : "Buddy destroyed";
        operation.Complete = true;
        Destroy(targetObject);
    }

    static BuddyUnit FindLiveBuddy(BuddyUnit[] buddies, string id) =>
        System.Array.Find(buddies, unit => unit != null && unit.unitData != null && unit.unitData.uniqueId == id);

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
        int lastSlot = Mathf.Min(terrain.TotalResidentialCapacity, firstSlot + constructionCount - 1);
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
            if (firstHomeApproaches.TryGetValue(gobboId, out FirstHomeMoveOperation approach))
            {
                while (!approach.Complete) yield return null;
                if (!approach.Succeeded)
                {
                    LastResidentialFailureReason = approach.Failure;
                    Debug.LogError("[CampResidential Implementation] Constructor " + gobboId +
                        " failed its own first-home approach. No retry or fallback was attempted.", buddy);
                    break;
                }
            }
            CampResidentialSlotDefinition definition = terrain.GetResidentialCatalog()?.GetSlot(slotIndex);
            int established = GameState.Instance.campTerrainState.residentialSlotsEstablished;
            if (definition == null || !CampArrivalPolicy.CanBeginReservedConstruction(
                    slotIndex, definition.DependencyGlobalSlotId, established))
            {
                LastResidentialFailureReason = "Reserved slot " + slotIndex +
                    " reached execution before its contiguous/dependency gate was satisfied.";
                Debug.LogError("[CampResidential Implementation] " + LastResidentialFailureReason, buddy);
                break;
            }
            currentResidentialConstructorId = gobboId;
            LogResidentialConstructor(buddy, slotIndex);
            yield return RunResidentialSlotArrival(terrain, buddy, slotIndex);
            if (residentialConstructionSucceeded) residentialConstructionsCompleted++;
            if (!residentialConstructionSucceeded || !residentialPostConstructionSucceeded)
            {
                break;
            }
        }
        currentResidentialConstructorId = "";
        if (residentialConstructionSucceeded && residentialPostConstructionSucceeded)
            ReleaseBuddiesToResidentialOrDefaultAnchors();
    }

    bool residentialConstructionSucceeded;
    bool residentialPostConstructionSucceeded;
    int residentialConstructionsCompleted;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    static void LogResidentialArrival(CampResidentialArrivalEvaluation evaluation,
        List<string> unassignedBuddyIds, BuddyUnit[] runtimeBuddies)
    {
        string constructorId = unassignedBuddyIds != null && unassignedBuddyIds.Count > 0
            ? unassignedBuddyIds[0] : "none";
        int mappedRuntimeBuddies = 0;
        if (runtimeBuddies != null && unassignedBuddyIds != null)
            foreach (string id in unassignedBuddyIds)
                if (System.Array.Exists(runtimeBuddies, unit => unit != null && unit.unitData != null &&
                    unit.unitData.uniqueId == id)) mappedRuntimeBuddies++;
        Debug.Log("[CampResidential Arrival] stage=" +
            (GameState.Instance?.campTerrainState?.residentialStage ?? 0) +
            " established=" + evaluation.EstablishedCapacity +
            " livingBuddies=" + evaluation.LivingBuddyCount +
            " vacancies=" + evaluation.VacantEstablishedSlots +
            " vacancyClaims=" + evaluation.VacancyClaims +
            " unassigned=" + evaluation.UnassignedBuddies +
            " pendingConstruction=" + evaluation.PendingConstructionCount +
            " arrivalPhase=" + evaluation.ArrivalPhase +
            " firstSlot=" + evaluation.FirstSlot +
            " constructor=" + constructorId +
            " runtimeBuddies=" + (runtimeBuddies?.Length ?? 0) +
            " mappedUnassigned=" + mappedRuntimeBuddies);
    }

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
        CurrentResidentialSlot = slotIndex;
        CurrentResidentialRoomId = terrain.GetResidentialCatalog() != null &&
            terrain.GetResidentialCatalog().TryGetRoomForSlot(slotIndex, out CampResidentialRoomDefinition currentRoom)
            ? currentRoom.RoomId : "";
        LastResidentialFailureReason = "";
        LastResidentialDigRemovedCells = 0;
        residentialConstructionSucceeded = false;
        residentialPostConstructionSucceeded = false;
        ResidentialSlotRecord slot = terrain.GetResidentialSlot(slotIndex);
        if (slot.SlotIndex == 0 || buddy == null) yield break;
        int residentialProgression = terrain.GetResidentialProgressionIndexForSlot(slotIndex);
        if (residentialProgression <= 0) yield break;
        List<Vector2Int> footprint = terrain.GetResidentialSlotFootprint(slotIndex);
        List<Vector2Int> route = terrain.GetResidentialConstructionRoute(slotIndex);
        CurrentResidentialStagingCell = route.Count > 0 ? route[route.Count - 1] : default;
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
        Vector2 navigationExtents = GetNavigationExtents(buddy);
        if (!dig.enabled || !ReferenceEquals(dig.ResolvedTerrain, terrain))
        {
            AbortResidentialConstruction(buddy, targetObject, slotIndex, "BuddyDigAbility has no valid Camp terrain authority.");
            yield break;
        }
        if (!ResidentialConstructionPlan.TryBuild(terrain, slotIndex, navigationExtents,
                dig.digRadius, out ResidentialConstructionPlan plan, out string planFailure,
                buddy.gameObject.layer))
        {
            AbortResidentialConstruction(buddy, targetObject, slotIndex,
                "Deterministic plan validation failed: " + planFailure);
            yield break;
        }
        route = new List<Vector2Int>(plan.ApproachRoute);
        for (int waypointIndex = 0; waypointIndex < route.Count; waypointIndex++)
        {
            Vector2Int waypoint = route[waypointIndex];
            if (terrain.IsBlocked(waypoint))
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "Construction route waypoint " + waypointIndex + " " + waypoint + " is not open.");
                yield break;
            }
            if (!TileMover.CanOccupyBox(terrain, terrain.CellToWorld(waypoint), navigationExtents))
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "Construction route waypoint " + waypointIndex + " " + waypoint +
                    " is cell-open but lacks body clearance for radius " +
                    navigationRadius.ToString("0.###") + ".");
                yield break;
            }
            if (waypointIndex > 0 && !TileMover.CanTraverseBox(terrain,
                    terrain.CellToWorld(route[waypointIndex - 1]), terrain.CellToWorld(waypoint), navigationExtents))
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "Construction route segment into waypoint " + waypointIndex + " " + waypoint +
                    " lacks continuous body clearance.");
                yield break;
            }
            target.position = terrain.CellToWorld(waypoint);
            LogPreDigWaypoint(buddy, slotIndex, waypointIndex, waypoint, target.position,
                terrain.IsBlocked(waypoint), GetCampSpeed(buddy));
            yield return MoveConstructionBuddy(buddy, target);
            if (!constructionMoveSucceeded)
            {
                    AbortResidentialConstruction(buddy, targetObject, slotIndex,
                        "Buddy could not reach route waypoint " + waypointIndex + " " + waypoint +
                        ". Directed-walk result " + constructionMoveResult +
                        ", final physics position " + GetMovementPosition(buddy.GetComponent<Rigidbody2D>(), buddy) +
                        ", target " + target.position + ".");
                yield break;
            }
        }

        int requiredDigActions = 0;
        int successfulDigActions = 0;
        Vector2Int advanceStartCell = new Vector2Int(slot.Approach.x, slot.Approach.y);
        for (int stepIndex = 0; stepIndex < plan.DigSteps.Count; stepIndex++)
        {
            ResidentialDigStep step = plan.DigSteps[stepIndex];
            Vector2Int targetCell = step.AdvanceCell;
            Vector2 targetWorld = terrain.CellToWorld(targetCell);
            int removedForStep = 0;
            for (int actionIndex = 0; actionIndex < step.DigCenters.Count; actionIndex++)
            {
                Vector2Int localTarget = step.DigCenters[actionIndex];
                requiredDigActions++;
                HashSet<Vector2Int> before = new HashSet<Vector2Int>();
                foreach (Vector2Int cell in footprint) if (terrain.IsBlocked(cell)) before.Add(cell);
                TerrainDigResult digResult = new TerrainDigResult(0, 0, 0, TerrainDigFailureReason.None);
                Vector2 localTargetWorld = terrain.CellToWorld(localTarget);
                CurrentResidentialDigCell = localTarget;
                yield return dig.DigRoutine(localTargetWorld, TerrainDigAuthority.ResidentialProgression,
                    residentialProgression, footprint,
                    result => digResult = result);
                LogResidentialDig(buddy, slotIndex, localTarget, localTargetWorld, dig, digResult,
                    !terrain.IsBlocked(targetCell));
                if (!digResult.Changed)
                {
                    AbortResidentialConstruction(buddy, targetObject, slotIndex,
                        "Local Dig failed at " + localTarget + " while advancing toward " + targetCell +
                        ": " + digResult.FailureReason +
                        " (evaluated " + digResult.EvaluatedCells + ", eligible " + digResult.EligibleCells + ").");
                    yield break;
                }
                successfulDigActions++;
                removedForStep += digResult.RemovedCells;
                LastResidentialDigRemovedCells = digResult.RemovedCells;
                HashSet<Vector2Int> actualRemoved = new HashSet<Vector2Int>();
                foreach (Vector2Int cell in before) if (!terrain.IsBlocked(cell)) actualRemoved.Add(cell);
                if (!actualRemoved.SetEquals(step.ExpectedRemovedCells[actionIndex]))
                {
                    AbortResidentialConstruction(buddy, targetObject, slotIndex,
                        "Step " + stepIndex + " Dig " + actionIndex + " at " + localTarget +
                        " removed " + FormatCells(new List<Vector2Int>(actualRemoved)) +
                        " but plan expected " + FormatCells(new List<Vector2Int>(step.ExpectedRemovedCells[actionIndex])) + ".");
                    yield break;
                }
            }
            if (terrain.IsBlocked(targetCell) ||
                !TileMover.CanOccupyBox(terrain, targetWorld, navigationExtents) ||
                !TileMover.CanTraverseBox(terrain,
                    GetMovementPosition(buddy.GetComponent<Rigidbody2D>(), buddy), targetWorld, navigationExtents))
            {
                AbortResidentialConstruction(buddy, targetObject, slotIndex,
                    "Step " + stepIndex + " exact advance target " + targetCell +
                    " failed its validated post-Dig body-clearance contract.");
                yield break;
            }
            List<Vector2Int> advanceRoute = advanceStartCell == targetCell
                ? new List<Vector2Int>() : new List<Vector2Int> { targetCell };
            LogPostDigAdvance(buddy, slotIndex, advanceStartCell, targetCell,
                removedForStep, advanceRoute);
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
                Vector2Int segmentStart = advanceIndex == 0 ? advanceStartCell : advanceRoute[advanceIndex - 1];
                if (!TileMover.CanOccupyBox(terrain, terrain.CellToWorld(advanceCell), navigationExtents))
                {
                    AbortResidentialConstruction(buddy, targetObject, slotIndex,
                        "Post-Dig waypoint " + advanceIndex + " " + advanceCell +
                        " lacks full body clearance.");
                    yield break;
                }
                Vector2 segmentEnd = terrain.CellToWorld(advanceCell);
                if (!TileMover.CanTraverseBox(terrain,
                        GetMovementPosition(buddy.GetComponent<Rigidbody2D>(), buddy),
                        segmentEnd, navigationExtents))
                {
                    AbortResidentialConstruction(buddy, targetObject, slotIndex,
                        "Post-Dig segment " + segmentStart + " -> " + advanceCell +
                        " lacks continuous body clearance.");
                    yield break;
                }
                target.position = terrain.CellToWorld(advanceCell);
                yield return MoveConstructionBuddy(buddy, target);
                if (!constructionMoveSucceeded)
                {
                    float finalDistance = buddy != null
                        ? Vector2.Distance(GetMovementPosition(buddy.GetComponent<Rigidbody2D>(), buddy),
                            target.position) : float.PositiveInfinity;
                    AbortResidentialConstruction(buddy, targetObject, slotIndex,
                        "Buddy could not reach post-Dig waypoint " + advanceIndex + " " + advanceCell +
                        " on route " + FormatCells(advanceRoute) + ". Final distance " +
                        finalDistance.ToString("0.00") + ", result " + constructionMoveResult +
                        ", blocked " + terrain.IsBlocked(advanceCell) + ", physical blocker " +
                        (buddy.GetComponent<CampDirectedWalk>()?.BlockingColliderDescription ?? "walker missing") + ".");
                    yield break;
                }
            }
            advanceStartCell = targetCell;
        }

        int blockedRequiredCells = 0;
        foreach (Vector2Int cell in footprint) if (terrain.IsBlocked(cell)) blockedRequiredCells++;
        Vector2Int finalStandingCell = new Vector2Int(slot.Center.x, slot.Center.y);
        bool reachedFinalStandingCell = advanceStartCell == finalStandingCell &&
            constructionMoveResult == CampDirectedWalkResult.Arrived;
        if (!CampSpatialPolicy.CanCommitResidentialConstruction(
                requiredDigActions, successfulDigActions, blockedRequiredCells, reachedFinalStandingCell))
        {
            AbortResidentialConstruction(buddy, targetObject, slotIndex,
                "Canonical footprint is incomplete (required actions " + requiredDigActions +
                ", successful " + successfulDigActions + ", blocked cells " + blockedRequiredCells +
                ", final standing reached " + reachedFinalStandingCell + ").");
            yield break;
        }
        if (buddy.unitData == null || !CampResidentialOccupancyResolver.CanAssignNextSlot(
                GameState.Instance, buddy.unitData.uniqueId, slotIndex))
        {
            AbortResidentialConstruction(buddy, targetObject, slotIndex,
                "Intended resident cannot claim the next canonical slot; committed terrain was not changed.");
            yield break;
        }
        if (!terrain.CompleteResidentialSlotForProgression(residentialProgression, slotIndex))
        {
            AbortResidentialConstruction(buddy, targetObject, slotIndex,
                "Terrain authority refused the validated canonical slot commit.");
            yield break;
        }
        bool assigned = CampResidentialOccupancyResolver.AssignEstablishedSlot(
            GameState.Instance, buddy.unitData.uniqueId, slotIndex);
        if (!assigned)
        {
            Debug.LogError("[CampResidential Implementation] Slot " + slotIndex +
                " committed after assignment prevalidation, but synchronous assignment failed. " +
                "This is an implementation invariant violation; no fallback was attempted.", buddy);
            residentialConstructionSucceeded = false;
            residentialPostConstructionSucceeded = false;
            Destroy(targetObject);
            yield break;
        }
        buddy.unitData.campResidentialSlotId = slotIndex;
        ApplyHomePresentation();
        if (slotIndex == 1) CampMessageUI.Show(firstHomeMessage);
        SporeSaveManager.SaveCurrentSlotFromGameState();
        residentialConstructionSucceeded = true;
        residentialPostConstructionSucceeded = true;
        CompleteFirstHomeMovement(buddy);
        Destroy(targetObject);
    }

    void AbortResidentialConstruction(BuddyUnit buddy, GameObject targetObject, int slotIndex, string reason)
    {
        residentialConstructionSucceeded = false;
        residentialPostConstructionSucceeded = false;
        LastResidentialFailureReason = reason ?? "Unknown residential construction failure.";
        if (buddy != null && buddy.unitData != null)
            firstHomeBuddyIds.Remove(buddy.unitData.uniqueId);
        Debug.LogError("[CampResidential Implementation] Slot " + slotIndex +
            " construction stopped: " + reason +
            " No save, assignment, retry, rollback, or fallback movement was performed; live state is preserved.",
            buddy != null ? buddy : this);
        if (targetObject != null) Destroy(targetObject);
        if (buddy == null) return;
        Rigidbody2D body = buddy.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        CampWander wander = buddy.GetComponent<CampWander>();
        if (wander != null) wander.enabled = false;
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
    CampDirectedWalkResult constructionMoveResult = CampDirectedWalkResult.None;

    IEnumerator MoveConstructionBuddy(BuddyUnit buddy, Transform target, float confirmedArrivalDistance = 0.18f)
    {
        constructionMoveSucceeded = false;
        constructionMoveResult = CampDirectedWalkResult.InvalidTarget;
        if (buddy == null || target == null) yield break;
        confirmedArrivalDistance = Mathf.Max(0.01f, confirmedArrivalDistance);
        Rigidbody2D body = buddy.GetComponent<Rigidbody2D>();
        CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
        if (walker == null) walker = buddy.gameObject.AddComponent<CampDirectedWalk>();
        walker.destroyWhenDone = false;
        walker.enableWanderWhenDone = false;
        walker.bodyRadius = TileMover.GetColliderBodyRadius(body, walker.bodyRadius);
        float speed = GetFirstHomeSpeed(buddy);
        float distance = Vector2.Distance(GetMovementPosition(body, buddy), target.position);
        float timeout = CampBuddyPhysicalPolicy.GetDirectedWalkTimeout(distance, speed, constructionMoveTimeout);
        walker.BeginWalk(target, speed, confirmedArrivalDistance, timeout);
        while (buddy != null && walker.IsWalking) yield return null;
        constructionMoveResult = buddy != null ? walker.Result : CampDirectedWalkResult.Cancelled;
        constructionMoveSucceeded = constructionMoveResult == CampDirectedWalkResult.Arrived;
    }

    static Vector2 GetMovementPosition(Rigidbody2D body, BuddyUnit buddy) =>
        body != null ? body.position : (Vector2)buddy.transform.position;

    static float GetNavigationRadius(BuddyUnit buddy)
    {
        Rigidbody2D body = buddy != null ? buddy.GetComponent<Rigidbody2D>() : null;
        return TileMover.GetColliderBodyRadius(body, 0.25f);
    }

    static Vector2 GetNavigationExtents(BuddyUnit buddy)
    {
        Rigidbody2D body = buddy != null ? buddy.GetComponent<Rigidbody2D>() : null;
        return TileMover.GetMapClearanceExtents(body, 0.25f);
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
        int slots = state != null ? state.residentialSlotsEstablished : 0;
        HashSet<int> occupied = GameState.Instance != null
            ? CampResidentialOccupancyResolver.GetOccupiedEstablishedSlots(GameState.Instance) : new HashSet<int>();
        residentialPresentation?.ApplyProgress(slots, occupied);
        CampSquadSelect squad = Object.FindAnyObjectByType<CampSquadSelect>(FindObjectsInactive.Include);
        if (squad != null) squad.ApplyHomeAvailability(slots >= 1);
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

    float GetFirstHomeSpeed(BuddyUnit buddy) =>
        CampArrivalPolicy.GetCampMovementSpeed(GetCampSpeed(buddy), true);

    void CompleteFirstHomeMovement(BuddyUnit buddy)
    {
        if (buddy == null) return;
        if (buddy.unitData != null) firstHomeBuddyIds.Remove(buddy.unitData.uniqueId);
        CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
        if (walker != null) Destroy(walker);
        CampWander wander = buddy.GetComponent<CampWander>();
        if (wander == null) wander = buddy.gameObject.AddComponent<CampWander>();
        wander.SetSemanticDestinations(GetCampSpeed(buddy));
        wander.enabled = true;
    }

    void ReleaseNonFirstHomeBuddiesToNormalBehavior()
    {
        foreach (BuddyUnit buddy in Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None))
        {
            if (buddy == null || buddy.unitData != null && firstHomeBuddyIds.Contains(buddy.unitData.uniqueId))
                continue;
            CompleteFirstHomeMovement(buddy);
        }
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
