
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

        spawned = true;

        if (GameState.Instance == null)
        {
            Debug.LogWarning("RunSquadSpawner: no GameState found, so no saved active squad can spawn.");
            return;
        }

        Transform player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("RunSquadSpawner: no Player found.");
            return;
        }

        if (buddyPrefab == null)
        {
            GobboController controller = player.GetComponent<GobboController>();
            if (controller != null)
                buddyPrefab = controller.buddyPrefab;
        }

        if (buddyPrefab == null)
        {
            Debug.LogWarning("RunSquadSpawner: no buddy prefab assigned and player has none.");
            return;
        }

        List<BuddyData> active = GameState.Instance.GetActiveSquad();

        for (int i = 0; i < active.Count; i++)
        {
            BuddyData buddy = active[i];

            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();

            float angle = active.Count <= 0 ? 0f : (i / (float)active.Count) * Mathf.PI * 2f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
            GameObject obj = Instantiate(buddyPrefab, (Vector2)player.position + offset, Quaternion.identity);
            obj.name = buddy.buddyName + " Run Buddy";
            obj.layer = LayerMask.NameToLayer("Buddy");

            BuddyUnit unit = obj.GetComponent<BuddyUnit>();
            if (unit != null)
                unit.Initialize(buddy);

            BuddyBrain brain = obj.GetComponent<BuddyBrain>();
            if (brain != null)
                brain.enabled = enableBrain;

            BuddyFollow follow = obj.GetComponent<BuddyFollow>();
            if (follow != null)
            {
                follow.enabled = enableFollow;
                follow.SetPlayer(player);
                Vector2 formation = Random.insideUnitCircle;
                if (formation.sqrMagnitude < 0.001f)
                    formation = Vector2.right;
                follow.SetFormationOffset(formation.normalized * formationSpread);
                follow.brainAllowsMovement = true;
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

            Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
            }
        }

        Debug.Log("RunSquadSpawner spawned active squad count: " + active.Count);
    }

    Transform FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            return playerObject.transform;

        GobboController controller = Object.FindAnyObjectByType<GobboController>();

        if (controller != null)
            return controller.transform;

        return null;
    }
}
