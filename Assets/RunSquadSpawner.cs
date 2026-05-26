using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunSquadSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject buddyPrefab;

    [Header("Spawn")]
    public bool spawnOnStart = true;
    public float startDelay = 0.15f;
    public float waitForPlayerSeconds = 3f;
    public float spawnRadius = 1.25f;
    public float formationSpread = 1.2f;

    [Header("Behavior")]
    public bool enableBrain = true;
    public bool enableFollow = true;
    public bool enableCombat = true;
    public bool enableScavengerIfBuddyCollectsFood = true;

    private bool spawned;

    void Start()
    {
        if (spawnOnStart)
            StartCoroutine(SpawnRoutine());
    }

    public void SpawnActiveSquad()
    {
        if (!gameObject.activeInHierarchy)
            return;

        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        if (spawned)
            yield break;

        spawned = true;

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        GameState state = EnsureGameState();
        if (state == null)
        {
            Debug.LogWarning("RunSquadSpawner: no GameState found.");
            yield break;
        }

        Transform player = null;
        float timer = 0f;
        while (timer < waitForPlayerSeconds && player == null)
        {
            player = FindPlayer();
            if (player != null)
                break;

            timer += Time.deltaTime;
            yield return null;
        }

        if (player == null)
        {
            Debug.LogWarning("RunSquadSpawner: no Player found, so active squad could not spawn.", this);
            yield break;
        }

        if (buddyPrefab == null)
        {
            GobboController controller = player.GetComponent<GobboController>();
            if (controller != null)
                buddyPrefab = controller.buddyPrefab;
        }

        if (buddyPrefab == null)
        {
            Debug.LogWarning("RunSquadSpawner: assign Buddy Prefab, or assign buddyPrefab on the player GobboController.", this);
            yield break;
        }

        state.RepairRosterState();
        List<BuddyData> active = state.GetActiveSquad();

        if (active.Count == 0)
        {
            Debug.Log("RunSquadSpawner: active squad is empty. No buddies spawned.");
            yield break;
        }

        for (int i = 0; i < active.Count; i++)
        {
            SpawnBuddy(active[i], i, active.Count, player);
        }

        Debug.Log("RunSquadSpawner spawned active squad count: " + active.Count);
    }

    void SpawnBuddy(BuddyData buddy, int index, int count, Transform player)
    {
        if (buddy == null || player == null || buddyPrefab == null)
            return;

        buddy.EnsureId();
        buddy.EnsureRuntimeDefaults();

        float angle = count <= 0 ? 0f : (index / (float)count) * Mathf.PI * 2f;
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
        Vector3 spawnPos = (Vector2)player.position + offset;
        spawnPos.z = 0f;

        GameObject obj = Instantiate(buddyPrefab, spawnPos, Quaternion.identity);
        obj.name = buddy.buddyName + " Run Buddy";

        int buddyLayer = LayerMask.NameToLayer("Buddy");
        if (buddyLayer >= 0)
            obj.layer = buddyLayer;

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        BuddyUnit unit = obj.GetComponent<BuddyUnit>();
        if (unit != null)
            unit.Initialize(buddy);
        else
            Debug.LogWarning("Run buddy prefab is missing BuddyUnit.", obj);

        BuddyBrain brain = obj.GetComponent<BuddyBrain>();
        if (brain != null)
        {
            brain.enabled = enableBrain;
            brain.allowCombat = true;
            brain.allowFollowing = true;
            brain.allowScavenging = buddy.collectsFood;
        }

        BuddyFollow follow = obj.GetComponent<BuddyFollow>();
        if (follow != null)
        {
            follow.enabled = enableFollow;
            follow.SetPlayer(player);
            Vector2 formation = Random.insideUnitCircle;
            if (formation.sqrMagnitude < 0.001f)
                formation = Vector2.right;
            follow.SetFormationOffset(formation.normalized * formationSpread);
            follow.brainAllowsMovement = !enableBrain;
        }

        BuddyCombat combat = obj.GetComponent<BuddyCombat>();
        if (combat != null)
        {
            combat.enabled = enableCombat;
            combat.SetPlayer(player);
            combat.brainAllowsMovement = false;
        }

        BuddyScavenger scavenger = obj.GetComponent<BuddyScavenger>();
        if (scavenger != null)
        {
            scavenger.enabled = enableScavengerIfBuddyCollectsFood && buddy.collectsFood;
            scavenger.brainAllowsMovement = false;
        }

        CampWander wander = obj.GetComponent<CampWander>();
        if (wander != null)
            wander.enabled = false;

        CampDirectedWalk directedWalk = obj.GetComponent<CampDirectedWalk>();
        if (directedWalk != null)
            directedWalk.enabled = false;
    }

    Transform FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            return playerObject.transform;

        GobboController controller = Object.FindAnyObjectByType<GobboController>();
        return controller != null ? controller.transform : null;
    }

    GameState EnsureGameState()
    {
        if (GameState.Instance != null)
            return GameState.Instance;

        GameObject obj = new GameObject("GameState");
        return obj.AddComponent<GameState>();
    }
}
