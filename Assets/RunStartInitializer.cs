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

    [Header("Scene-authored References")]
    public RunSquadSpawner runSquadSpawner;

    IEnumerator Start()
    {
        GameState state = EnsureGameState();
        yield return null;

        GobboController player = Object.FindAnyObjectByType<GobboController>();
        BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>();

        if (applySavedPlayerStats && player != null)
            state.ApplyToPlayer(player);

        if (applySavedRosterToSceneRoster && roster != null && state.ownedBuddies != null && state.ownedBuddies.Count > 0)
            state.ApplyToRoster(roster);

        if (beginRunSnapshotOnStart)
            state.BeginRunSnapshot();

        if (runSquadSpawner == null)
            runSquadSpawner = Object.FindAnyObjectByType<RunSquadSpawner>(FindObjectsInactive.Include);

        if (runSquadSpawner != null)
        {
            runSquadSpawner.SpawnActiveSquad();
        }
        else
        {
            Debug.LogWarning("RunStartInitializer: add one RunSquadSpawner scene object and assign its buddy prefab.", this);
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
