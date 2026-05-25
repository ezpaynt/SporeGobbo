using System.Collections;
using UnityEngine;

public class RunStartInitializer : MonoBehaviour
{
    [Header("Apply Saved State")]
    public bool applySavedPlayerStats = true;
    [Tooltip("Normally false. GameState owns the roster; SampleScene should read it, not overwrite it from a scene BuddyRoster.")]
    public bool applySavedRosterToSceneRoster = false;

    [Header("Run Snapshot")]
    [Tooltip("CampRunPortal normally starts the snapshot before loading the run. This only starts one for direct SampleScene testing if none exists.")]
    public bool beginSnapshotIfMissing = true;

    [Header("Squad")]
    public RunSquadSpawner runSquadSpawner;
    public bool spawnSquadAfterPlayerLoad = true;

    IEnumerator Start()
    {
        GameState state = EnsureGameState();

        // Wait one frame so scene objects or MapGenerator-spawned objects exist.
        yield return null;

        GobboController player = Object.FindAnyObjectByType<GobboController>();
        if (applySavedPlayerStats && player != null)
            state.ApplyToPlayer(player);

        if (applySavedRosterToSceneRoster)
        {
            BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>();
            if (roster != null && state.ownedBuddies.Count > 0)
                state.ApplyToRoster(roster);
        }

        state.RepairRosterState();

        if (beginSnapshotIfMissing && !state.HasRunSnapshot())
            state.BeginRunSnapshot();

        if (spawnSquadAfterPlayerLoad)
        {
            if (runSquadSpawner == null)
                runSquadSpawner = Object.FindAnyObjectByType<RunSquadSpawner>(FindObjectsInactive.Include);

            if (runSquadSpawner != null)
                runSquadSpawner.SpawnActiveSquad();
            else
                Debug.LogWarning("RunStartInitializer: no RunSquadSpawner assigned/found. Add one scene object to SampleScene.", this);
        }
    }

    GameState EnsureGameState()
    {
        if (GameState.Instance != null)
            return GameState.Instance;

        GameObject obj = new GameObject("GameState");
        return obj.AddComponent<GameState>();
    }
}
