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

    [Header("Player Spawn")]
    public GameObject playerPrefab;
    public Transform fallbackPlayerSpawnPoint;
    public bool spawnPlayerIfMissing = true;
    public bool spawnAtMapStartCell = true;

    [Header("Scene-authored References")]
    public RunSquadSpawner runSquadSpawner;

    IEnumerator Start()
    {
        GameState state = EnsureGameState();

        yield return null;

        MapGenerator map = MapGenerator.Instance;
        if (map == null)
            map = Object.FindAnyObjectByType<MapGenerator>();

        float wait = 0f;
        while (map != null && map.Data == null && wait < 2f)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        GobboController player = Object.FindAnyObjectByType<GobboController>();

        if (player == null && spawnPlayerIfMissing)
            player = SpawnPlayer(map);

        BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>();

        if (applySavedPlayerStats && player != null)
            state.ApplyToPlayer(player);

        if (applySavedRosterToSceneRoster && roster != null && state.ownedGobbos != null && state.ownedGobbos.Count > 0)
            state.ApplyToRoster(roster);

        if (beginRunSnapshotOnStart)
            state.BeginRunSnapshot();

        if (runSquadSpawner == null)
            runSquadSpawner = Object.FindAnyObjectByType<RunSquadSpawner>(FindObjectsInactive.Include);

        if (runSquadSpawner != null)
            runSquadSpawner.SpawnActiveSquad();
        else
            Debug.LogWarning("RunStartInitializer: add one real RunSquadSpawner scene object and assign its buddy prefab.", this);
    }

    GobboController SpawnPlayer(MapGenerator map)
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("RunStartInitializer: Player Prefab is missing, so no player could spawn.", this);
            return null;
        }

        Vector3 spawnPos = transform.position;

        if (spawnAtMapStartCell && map != null)
            spawnPos = map.CellToWorldCenter(map.map.spawnCenter);
        else if (fallbackPlayerSpawnPoint != null)
            spawnPos = fallbackPlayerSpawnPoint.position;

        spawnPos.z = 0f;

        GameObject obj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        obj.name = "Run Player Gobbo";
        obj.tag = "Player";

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        GobboController controller = obj.GetComponent<GobboController>();
        if (controller == null)
        {
            Debug.LogWarning("RunStartInitializer: Player Prefab is missing GobboController.", obj);
            return null;
        }

        CameraFollow cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
        if (cameraFollow != null)
            cameraFollow.target = obj.transform;

        return controller;
    }

    GameState EnsureGameState()
    {
        if (GameState.Instance != null) return GameState.Instance;
        GameObject obj = new GameObject("GameState");
        return obj.AddComponent<GameState>();
    }
}
