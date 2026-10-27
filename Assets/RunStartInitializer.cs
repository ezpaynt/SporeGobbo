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
        bool introMode = SampleSceneModeController.IsIntroMode;
        if (!introMode) RunReturnService.ResetForNewRun();
        GameState state = EnsureGameState();

        MapGenerator map = MapGenerator.Instance;
        if (map == null)
            map = Object.FindAnyObjectByType<MapGenerator>();

        IWorldRegion region=RegionQueries.Current;
        yield return null;
        float wait=0;
        while((region!=null?!region.LogicalReady:map!=null&&map.Data==null)&&wait<5){wait+=Time.unscaledDeltaTime;yield return null;}
        if(region==null&&(map==null||map.Data==null)||region!=null&&!region.LogicalReady){Debug.LogError("Run terrain missing or not ready");yield break;}

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

        if(region!=null){region.BindActor(player.gameObject);var interaction=Object.FindAnyObjectByType<CampInteractionDetector>();if(interaction==null)interaction=new GameObject("Existing world interaction service").AddComponent<CampInteractionDetector>();interaction.SetPlayer(player.transform); }
        BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>();
        if (applySavedRosterToSceneRoster && roster != null && state.ownedGobbos != null && state.ownedGobbos.Count > 0)
            state.ApplyToRoster(roster);

        // Always capture the actual run-start roster here. This keeps post-run
        // roll-call lists from confusing newly recruited buddies with the squad
        // that left camp, even if the camp portal forgot to take the snapshot.
        if (!introMode) state.BeginRunSnapshot();

        CameraFollow cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
        if (cameraFollow != null)
            cameraFollow.target = player.transform;

        if (introMode)
        {
            PlayerDeathWatcher deathWatcher = player.GetComponent<PlayerDeathWatcher>();
            if (deathWatcher != null) deathWatcher.enabled = false;
            yield break;
        }

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

        if(RegionQueries.Current is IWorldRegion region) spawnPos=region.Arrival;
        else if (spawnAtMapStartCell)
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
