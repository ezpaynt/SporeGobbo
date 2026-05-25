using System.Collections.Generic;
using UnityEngine;

public class RunSquadSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject buddyPrefab;

    [Header("Spawn")]
    public float spawnRadius = 1.25f;
    public float formationSpread = 1.2f;
    public bool spawnOnStart = true;
    public bool clearExistingRunBuddiesFirst = true;

    [Header("Behavior")]
    public bool enableBrain = true;
    public bool enableFollow = true;
    public bool enableCombat = true;
    public bool enableScavengerIfBuddyCollectsFood = true;

    private bool spawned;

    void Start()
    {
        if (spawnOnStart)
            SpawnActiveSquad();
    }

    public void SpawnActiveSquad()
    {
        if (spawned)
            return;

        if (GameState.Instance == null)
        {
            Debug.LogWarning("RunSquadSpawner: no GameState found, so no saved active squad can spawn.", this);
            return;
        }

        GameState.Instance.RepairRosterState();

        Transform player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("RunSquadSpawner: no Player found.", this);
            return;
        }

        ResolveBuddyPrefab(player);
        if (buddyPrefab == null)
        {
            Debug.LogWarning("RunSquadSpawner: no buddy prefab assigned and player has none.", this);
            return;
        }

        if (clearExistingRunBuddiesFirst)
            ClearExistingRunBuddies();

        List<BuddyData> active = GameState.Instance.GetActiveSquad();
        spawned = true;

        if (active.Count == 0)
        {
            Debug.Log("RunSquadSpawner: active squad is empty. No buddies spawned.");
            return;
        }

        for (int i = 0; i < active.Count; i++)
        {
            BuddyData buddy = active[i];
            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();
            SpawnBuddy(buddy, player, i, active.Count);
        }

        Debug.Log("RunSquadSpawner spawned active squad count: " + active.Count);
    }

    void ResolveBuddyPrefab(Transform player)
    {
        if (buddyPrefab != null || player == null)
            return;

        GobboController controller = player.GetComponent<GobboController>();
        if (controller != null)
            buddyPrefab = controller.buddyPrefab;
    }

    void SpawnBuddy(BuddyData buddy, Transform player, int index, int count)
    {
        float angle = count <= 0 ? 0f : (index / (float)count) * Mathf.PI * 2f;
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
        GameObject obj = Instantiate(buddyPrefab, (Vector2)player.position + offset, Quaternion.identity);
        obj.name = buddy.buddyName + " Run Buddy";
        obj.tag = "Untagged";

        int buddyLayer = LayerMask.NameToLayer("Buddy");
        if (buddyLayer >= 0)
            obj.layer = buddyLayer;

        BuddyUnit unit = obj.GetComponent<BuddyUnit>();
        if (unit != null)
            unit.Initialize(buddy);

        ConfigureRunBehavior(obj, buddy, player, index, count);
    }

    void ConfigureRunBehavior(GameObject obj, BuddyData buddy, Transform player, int index, int count)
    {
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        CampWander wander = obj.GetComponent<CampWander>();
        if (wander != null)
            wander.enabled = false;

        CampDirectedWalk directedWalk = obj.GetComponent<CampDirectedWalk>();
        if (directedWalk != null)
            directedWalk.enabled = false;

        BuddyBrain brain = obj.GetComponent<BuddyBrain>();
        if (brain != null)
            brain.enabled = enableBrain;

        BuddyFollow follow = obj.GetComponent<BuddyFollow>();
        if (follow != null)
        {
            follow.enabled = enableFollow;
            follow.SetPlayer(player);
            follow.followSpeed = buddy.moveSpeed;
            follow.brainAllowsMovement = true;

            Vector2 formation = GetFormationOffset(index, count);
            follow.SetFormationOffset(formation);
        }

        BuddyCombat combat = obj.GetComponent<BuddyCombat>();
        if (combat != null)
        {
            combat.enabled = enableCombat;
            combat.SetPlayer(player);
            combat.damage = buddy.damage;
            combat.attackCooldown = buddy.attackCooldown;
            combat.brainAllowsMovement = false;
        }

        BuddyScavenger scavenger = obj.GetComponent<BuddyScavenger>();
        if (scavenger != null)
        {
            scavenger.enabled = enableScavengerIfBuddyCollectsFood && buddy.collectsFood;
            scavenger.brainAllowsMovement = false;
        }
    }

    Vector2 GetFormationOffset(int index, int count)
    {
        if (count <= 0)
            return Vector2.zero;

        float angle = (index / (float)count) * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * formationSpread;
    }

    void ClearExistingRunBuddies()
    {
        BuddyUnit[] units = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit unit in units)
        {
            if (unit == null)
                continue;

            // Do not delete a player object that also has BuddyUnit by mistake.
            if (unit.CompareTag("Player"))
                continue;

            Destroy(unit.gameObject);
        }
    }

    Transform FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            return playerObject.transform;

        GobboController controller = Object.FindAnyObjectByType<GobboController>();
        return controller != null ? controller.transform : null;
    }
}
