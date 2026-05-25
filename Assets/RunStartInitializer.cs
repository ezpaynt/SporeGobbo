using System.Collections;
using UnityEngine;

public class RunStartInitializer : MonoBehaviour
{
    public bool applySavedPlayerStats = true;
    public bool applySavedRoster = false;
    public bool beginRunSnapshotOnStart = false;
    public bool ensureRunSquadSpawner = true;
    public GameObject buddyPrefabOverride;

    IEnumerator Start()
    {
        GameState state = EnsureGameState();

        // Wait one frame so MapGenerator has time to spawn Gobbo/BuddyRoster.
        yield return null;

        GobboController player = Object.FindAnyObjectByType<GobboController>();
        BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>();

        if (applySavedPlayerStats && player != null)
            state.ApplyToPlayer(player);

        // Default is OFF now. Camp squad select edits GameState directly, and applying an
        // empty scene roster here can wipe the selected active squad.
        if (applySavedRoster && roster != null && state.ownedBuddies != null && state.ownedBuddies.Count > 0)
            state.ApplyToRoster(roster);

        if (ensureRunSquadSpawner)
            EnsureRunSquadSpawner(player);

        if (beginRunSnapshotOnStart)
            state.BeginRunSnapshot();
    }

    void EnsureRunSquadSpawner(GobboController player)
    {
        RunSquadSpawner spawner = Object.FindAnyObjectByType<RunSquadSpawner>(FindObjectsInactive.Include);
        if (spawner == null)
        {
            GameObject obj = new GameObject("RunSquadSpawner_AUTO");
            spawner = obj.AddComponent<RunSquadSpawner>();
        }

        if (spawner.buddyPrefab == null)
        {
            if (buddyPrefabOverride != null)
                spawner.buddyPrefab = buddyPrefabOverride;
            else if (player != null)
                spawner.buddyPrefab = player.buddyPrefab;
        }

        spawner.spawnOnStart = false;
        spawner.SpawnActiveSquad();
    }

    GameState EnsureGameState()
    {
        if (GameState.Instance != null)
            return GameState.Instance;

        GameObject obj = new GameObject("GameState");
        return obj.AddComponent<GameState>();
    }
}
