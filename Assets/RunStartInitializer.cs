using System.Collections;
using UnityEngine;

public class RunStartInitializer : MonoBehaviour
{
    [Header("Run Setup")]
    public bool applySavedPlayerStats = true;
    [Tooltip("Leave this OFF unless you intentionally have a scene BuddyRoster you want to load from GameState. GameState is the real roster owner.")]
    public bool applySavedRosterToSceneRoster = false;
    [Tooltip("Usually OFF. CampRunPortal should begin the run snapshot before loading SampleScene.")]
    public bool beginRunSnapshotOnStart = false;

    [Header("Player Spawn - Required")]
    [Tooltip("This should be ON for RunScene/SampleScene. RunStartInitializer is responsible for creating the player.")]
    public bool spawnPlayerOnRunStart = true;
    public GameObject playerPrefab;
    [Tooltip("Optional only. If empty, the player spawns at MapGenerator.map.spawnCenter.")]
    public Transform fallbackPlayerSpawnPoint;
    public bool spawnAtMapStartCell = true;

    [Header("Scene-authored References")]
    public RunSquadSpawner runSquadSpawner;

    IEnumerator Start()
    {
        GameState state = EnsureGameState();

        MapGenerator map = MapGenerator.Instance;
        if (map == null)
            map = Object.FindAnyObjectByType<MapGenerator>();

        if (map == null)
        {
            Debug.LogError("RunStartInitializer: no MapGenerator found in the scene. Cannot start run.", this);
            yield break;
        }

        // Let MapGenerator.Start run first if it generates on start.
        yield return null;

        float wait = 0f;
        while (map.Data == null && wait < 3f)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        if (map.Data == null)
        {
            Debug.LogError("RunStartInitializer: MapGenerator exists but Data was never created. Turn Generate On Start on or call Generate() before spawning the player.", map);
            yield break;
        }

        GobboController player = Object.FindAnyObjectByType<GobboController>();

        if (player == null)
        {
            if (!spawnPlayerOnRunStart)
            {
                Debug.LogError("RunStartInitializer: no player exists, and Spawn Player On Run Start is OFF. Turn it ON or place a tagged Player with GobboController in the scene.", this);
                yield break;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("RunStartInitializer: Player Prefab is missing. Assign the Gobbo player prefab.", this);
                yield break;
            }

            player = SpawnPlayer(map);
        }

        if (player == null)
        {
            Debug.LogError("RunStartInitializer: failed to create/find player. Run cannot continue.", this);
            yield break;
        }

        if (!player.CompareTag("Player"))
        {
            Debug.LogWarning("RunStartInitializer: spawned/found player was not tagged Player. Setting tag to Player.", player);
            player.gameObject.tag = "Player";
        }

        if (applySavedPlayerStats)
            state.ApplyToPlayer(player);

        BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>();
        if (applySavedRosterToSceneRoster && roster != null && state.ownedGobbos != null && state.ownedGobbos.Count > 0)
            state.ApplyToRoster(roster);

        if (beginRunSnapshotOnStart)
            state.BeginRunSnapshot();

        CameraFollow cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
        if (cameraFollow != null)
            cameraFollow.target = player.transform;

        if (runSquadSpawner == null)
            runSquadSpawner = Object.FindAnyObjectByType<RunSquadSpawner>(FindObjectsInactive.Include);

        if (runSquadSpawner == null)
        {
            Debug.LogError("RunStartInitializer: no RunSquadSpawner found/assigned.", this);
            yield break;
        }

        runSquadSpawner.SpawnActiveSquad();
    }

    GobboController SpawnPlayer(MapGenerator map)
    {
        Vector3 spawnPos;

        if (spawnAtMapStartCell)
        {
            spawnPos = map.CellToWorldCenter(map.map.spawnCenter);
        }
        else if (fallbackPlayerSpawnPoint != null)
        {
            spawnPos = fallbackPlayerSpawnPoint.position;
        }
        else
        {
            Debug.LogError("RunStartInitializer: Spawn At Map Start Cell is OFF and no Fallback Player Spawn Point is assigned.", this);
            return null;
        }

        spawnPos.z = 0f;

        GameObject obj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        obj.name = "Run Player Gobbo";
        obj.tag = "Player";

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        GobboController controller = obj.GetComponent<GobboController>();
        if (controller == null)
        {
            Debug.LogError("RunStartInitializer: assigned Player Prefab does not have GobboController.", obj);
            return null;
        }

        return controller;
    }

    GameState EnsureGameState()
    {
        if (GameState.Instance != null) return GameState.Instance;

        GameObject obj = new GameObject("GameState");
        return obj.AddComponent<GameState>();
    }
}
