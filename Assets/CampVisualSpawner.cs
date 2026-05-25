using System.Collections.Generic;
using UnityEngine;

public class CampVisualSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject gobboCampPrefab;
    public GameObject activeBuddyPrefab;
    public GameObject reserveBuddyPrefab;

    [Header("Spawn Points")]
    public Transform gobboSpawnPoint;
    public Transform[] activeBuddySpots;
    public Transform[] reserveBuddySpots;

    [Header("Fallback Layout")]
    public float activeCircleRadius = 2f;
    public float reserveCircleRadius = 4f;

    [Header("Behavior")]
    public bool spawnOnStart = false;
    public bool clearExistingVisualsBeforeSpawn = true;

    private readonly List<GameObject> spawnedVisuals = new List<GameObject>();

    void Start()
    {
        if (spawnOnStart)
            SpawnCampVisuals();
    }

    public void SpawnCampVisuals()
    {
        if (GameState.Instance == null)
        {
            Debug.LogWarning("No GameState found for camp visuals.");
            return;
        }

        if (clearExistingVisualsBeforeSpawn)
            ClearSpawnedVisuals();

        SpawnGobbo();
        SpawnActiveSquad();
        SpawnReserveBuddies();
    }

    public void ClearSpawnedVisuals()
    {
        for (int i = spawnedVisuals.Count - 1; i >= 0; i--)
        {
            if (spawnedVisuals[i] != null)
                Destroy(spawnedVisuals[i]);
        }

        spawnedVisuals.Clear();
    }

    void SpawnGobbo()
    {
        if (gobboCampPrefab == null)
        {
            Debug.LogWarning("CampVisualSpawner has no Gobbo Camp Prefab assigned.");
            return;
        }

        Vector3 pos = gobboSpawnPoint != null ? gobboSpawnPoint.position : transform.position;
        pos.z = 0f;

        GameObject gobbo = Instantiate(gobboCampPrefab, pos, Quaternion.identity);
        gobbo.name = "Camp Gobbo";
        spawnedVisuals.Add(gobbo);

        GobboController controller = gobbo.GetComponent<GobboController>();

        if (controller != null)
        {
            GameState.Instance.ApplyToPlayer(controller);
            controller.enabled = false;
        }

        DisableRunMovement(gobbo);
        ForceVisible(gobbo, 10);
    }

    void SpawnActiveSquad()
    {
        if (activeBuddyPrefab == null)
        {
            Debug.LogWarning("CampVisualSpawner has no Active Buddy Prefab assigned.");
            return;
        }

        List<BuddyData> active = GameState.Instance.GetActiveSquad();

        for (int i = 0; i < active.Count; i++)
        {
            Vector3 pos = GetSpot(activeBuddySpots, i, activeCircleRadius, i, active.Count);
            SpawnBuddyVisual(activeBuddyPrefab, active[i], pos, true, 8);
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
            Vector3 pos = GetSpot(reserveBuddySpots, i, reserveCircleRadius, i, reserve.Count);
            SpawnBuddyVisual(prefab, reserve[i], pos, false, 6);
        }
    }

    void SpawnBuddyVisual(GameObject prefab, BuddyData data, Vector3 position, bool activeSquad, int sortingOrder)
    {
        if (prefab == null || data == null)
            return;

        position.z = 0f;

        GameObject buddyObject = Instantiate(prefab, position, Quaternion.identity);
        buddyObject.name = activeSquad ? data.buddyName : data.buddyName + " Camp Dot";
        spawnedVisuals.Add(buddyObject);

        BuddyUnit unit = buddyObject.GetComponent<BuddyUnit>();

        if (unit != null)
            unit.Initialize(data.Clone());

        if (!activeSquad)
            buddyObject.transform.localScale *= 0.65f;

        DisableRunBehavior(buddyObject);
        ForceVisible(buddyObject, sortingOrder);
    }

    void DisableRunBehavior(GameObject obj)
    {
        BuddyBrain brain = obj.GetComponent<BuddyBrain>();
        if (brain != null) brain.enabled = false;

        BuddyFollow follow = obj.GetComponent<BuddyFollow>();
        if (follow != null) follow.enabled = false;

        BuddyCombat combat = obj.GetComponent<BuddyCombat>();
        if (combat != null) combat.enabled = false;

        BuddyScavenger scavenger = obj.GetComponent<BuddyScavenger>();
        if (scavenger != null) scavenger.enabled = false;

        DisableRunMovement(obj);
    }

    void DisableRunMovement(GameObject obj)
    {
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }
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

    Vector3 GetSpot(Transform[] spots, int index, float fallbackRadius, int circleIndex, int circleCount)
    {
        if (spots != null && index < spots.Length && spots[index] != null)
            return spots[index].position;

        float angle = circleCount <= 0 ? 0f : (circleIndex / (float)circleCount) * Mathf.PI * 2f;
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * fallbackRadius;
        return transform.position + offset;
    }
}
