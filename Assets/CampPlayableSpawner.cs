using System.Collections.Generic;
using UnityEngine;

public class CampPlayableSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject activeBuddyPrefab;
    public GameObject reserveBuddyPrefab;

    [Header("Spawn Points")]
    public Transform playerSpawnPoint;
    public Transform[] activeBuddySpots;
    public Transform[] reserveBuddySpots;

    [Header("Fallback Layout")]
    public float activeCircleRadius = 2f;
    public float reserveCircleRadius = 4f;

    [Header("Camp Arrival Spawn")]
    [Tooltip("For camp starts, spawn buddies near the player first so the start routine can visibly send them to beds/fire.")]
    public bool spawnBuddiesNearPlayerFirst = true;
    public float activeArrivalRadius = 0.85f;
    public float reserveArrivalRadius = 1.25f;

    [Header("Camp Wander")]
    public float activeWanderRadius = 1.4f;
    public float reserveWanderRadius = 1.8f;
    public float activeWanderSpeedMultiplier = 0.45f;
    public float reserveWanderSpeedMultiplier = 0.25f;

    [Header("Reserve Look")]
    public float reserveScaleMultiplier = 0.55f;
    public int activeSortingOrder = 8;
    public int reserveSortingOrder = 5;
    public int playerSortingOrder = 10;

    [Header("Camera")]
    public CameraFollow cameraFollow;
    public bool assignMainCameraIfMissing = true;

    [Header("Behavior")]
    public bool spawnOnStart = false;
    public bool clearExistingBeforeSpawn = true;
    public bool disablePlayerCombatInCamp = false;
    public bool disablePlayerDiggingInCamp = false;
    public bool faceMovementInCamp = true;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private GobboController spawnedPlayer;

    void Start()
    {
        if (spawnOnStart)
            SpawnPlayableCamp();
    }

    public void SpawnPlayableCamp()
    {
        if (GameState.Instance == null)
        {
            Debug.LogWarning("No GameState found for playable camp spawn.");
            return;
        }

        if (clearExistingBeforeSpawn)
            ClearSpawnedCampObjects();

        spawnedPlayer = SpawnPlayer();
        SpawnActiveBuddies();
        SpawnReserveBuddies();

        Debug.Log("Playable camp spawned. Player: " + (spawnedPlayer != null) + ", active buddies: " + GameState.Instance.GetActiveSquad().Count + ", reserve buddies: " + GameState.Instance.GetReserveBuddies().Count);
    }

    public void ClearSpawnedCampObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
                Destroy(spawnedObjects[i]);
        }

        spawnedObjects.Clear();
        spawnedPlayer = null;
    }

    GobboController SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("CampPlayableSpawner needs Player Prefab assigned.");
            return null;
        }

        Vector3 pos = playerSpawnPoint != null ? playerSpawnPoint.position : transform.position;
        pos.z = 0f;

        GameObject playerObject = Instantiate(playerPrefab, pos, Quaternion.identity);
        playerObject.name = "Camp Player Gobbo";
        playerObject.tag = "Player";
        spawnedObjects.Add(playerObject);

        Rigidbody2D rb = playerObject.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        GobboController controller = playerObject.GetComponent<GobboController>();
        if (controller != null)
        {
            GameState.Instance.ApplyToPlayer(controller);
            // Do not heal here. Camp recovery happens at the fire so the return-to-camp
            // ritual can show hurt/tired gobbos before Eat/Rest.
            controller.followersFollowing = false;
            controller.followersAggressive = false;

            if (faceMovementInCamp)
            {
                controller.faceCursor = false;
                controller.faceMovementWhenNotFacingCursor = true;
            }

            if (disablePlayerCombatInCamp)
                controller.enemyLayers = 0;

            if (disablePlayerDiggingInCamp)
                controller.diggableLayers = 0;

            controller.enabled = true;
            GameState.Instance.SavePlayer(controller);
        }
        else
        {
            Debug.LogWarning("Player prefab is missing GobboController.");
        }

        ForceVisible(playerObject, playerSortingOrder);
        AssignCamera(playerObject.transform);
        return controller;
    }

    void SpawnActiveBuddies()
    {
        if (activeBuddyPrefab == null)
        {
            Debug.LogWarning("CampPlayableSpawner needs Active Buddy Prefab assigned.");
            return;
        }

        List<BuddyData> active = GameState.Instance.GetActiveSquad();
        for (int i = 0; i < active.Count; i++)
        {
            Vector3 pos = spawnBuddiesNearPlayerFirst
                ? GetArrivalSpot(i, active.Count, activeArrivalRadius)
                : GetSpot(activeBuddySpots, i, activeCircleRadius, i, active.Count);

            SpawnCampBuddy(activeBuddyPrefab, active[i], pos, true, activeSortingOrder, activeWanderRadius, activeWanderSpeedMultiplier, i);
        }
    }

    void SpawnReserveBuddies()
    {
        GameObject prefab = reserveBuddyPrefab != null ? reserveBuddyPrefab : activeBuddyPrefab;
        if (prefab == null)
            return;

        List<BuddyData> reserve = GameState.Instance.GetReserveBuddies();
        for (int i = 0; i < reserve.Count; i++)
        {
            Vector3 pos = spawnBuddiesNearPlayerFirst
                ? GetArrivalSpot(i + GameState.Instance.GetActiveSquad().Count, reserve.Count + GameState.Instance.GetActiveSquad().Count, reserveArrivalRadius)
                : GetSpot(reserveBuddySpots, i, reserveCircleRadius, i, reserve.Count);

            SpawnCampBuddy(prefab, reserve[i], pos, false, reserveSortingOrder, reserveWanderRadius, reserveWanderSpeedMultiplier, i);
        }
    }

    void SpawnCampBuddy(GameObject prefab, BuddyData data, Vector3 position, bool activeSquad, int sortingOrder, float wanderRadius, float speedMultiplier, int spotIndex)
    {
        if (prefab == null || data == null)
            return;

        data.EnsureId();
        data.EnsureRuntimeDefaults();
        // Do not heal here. The campfire recovery owns healing.

        position.z = 0f;
        GameObject buddyObject = Instantiate(prefab, position, Quaternion.identity);
        buddyObject.name = activeSquad ? data.buddyName + " Camp Buddy" : data.buddyName + " Camp Reserve";
        spawnedObjects.Add(buddyObject);

        Rigidbody2D rb = buddyObject.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        BuddyUnit unit = buddyObject.GetComponent<BuddyUnit>();
        if (unit != null)
            unit.Initialize(data);

        DisableRunBuddyBehavior(buddyObject);

        if (!activeSquad)
            buddyObject.transform.localScale *= reserveScaleMultiplier;

        CampWander wander = buddyObject.GetComponent<CampWander>();
        if (wander == null)
            wander = buddyObject.AddComponent<CampWander>();

        Transform anchor = activeSquad
            ? GetAnchorTransform(activeBuddySpots, spotIndex)
            : GetAnchorTransform(reserveBuddySpots, spotIndex);

        wander.SetAnchor(anchor, wanderRadius, Mathf.Max(0.2f, data.moveSpeed * speedMultiplier));
        ForceVisible(buddyObject, sortingOrder);
    }

    void DisableRunBuddyBehavior(GameObject obj)
    {
        BuddyBrain brain = obj.GetComponent<BuddyBrain>();
        if (brain != null) brain.enabled = false;

        BuddyFollow follow = obj.GetComponent<BuddyFollow>();
        if (follow != null) follow.enabled = false;

        BuddyCombat combat = obj.GetComponent<BuddyCombat>();
        if (combat != null) combat.enabled = false;

        BuddyScavenger scavenger = obj.GetComponent<BuddyScavenger>();
        if (scavenger != null) scavenger.enabled = false;
    }

    void AssignCamera(Transform target)
    {
        if (cameraFollow == null && assignMainCameraIfMissing && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();

        if (cameraFollow != null)
            cameraFollow.target = target;
    }

    void ForceVisible(GameObject obj, int sortingOrder)
    {
        SpriteRenderer[] renderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sr in renderers)
        {
            sr.enabled = true;
            sr.sortingOrder = sortingOrder;
        }
    }


    Vector3 GetArrivalSpot(int index, int count, float radius)
    {
        Vector3 center = playerSpawnPoint != null ? playerSpawnPoint.position : transform.position;
        int safeCount = Mathf.Max(1, count);
        float angle = (index / (float)safeCount) * Mathf.PI * 2f;
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Mathf.Max(0.1f, radius);
        Vector3 pos = center + offset;
        pos.z = 0f;
        return pos;
    }

    Vector3 GetSpot(Transform[] spots, int index, float fallbackRadius, int circleIndex, int circleCount)
    {
        if (spots != null && index < spots.Length && spots[index] != null)
            return spots[index].position;

        float angle = circleCount <= 0 ? 0f : (circleIndex / (float)circleCount) * Mathf.PI * 2f;
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * fallbackRadius;
        return transform.position + offset;
    }

    Transform GetAnchorTransform(Transform[] spots, int index)
    {
        if (spots == null || spots.Length == 0)
            return null;

        int safeIndex = Mathf.Abs(index) % spots.Length;
        return spots[safeIndex];
    }
}
