using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(GobboController))]
public class PlayerDeathWatcher : MonoBehaviour
{
    [Header("Death Flow")]
    public string campSceneName = "CampScene";
    public string gameOverSceneName = "GameOverScene";
    public bool loadGameOverSceneIfNoSuccessor = false;
    public bool saveRunBeforeLeaving = true;
    public string deathCause = "The leader got chewed up in the dirt.";

    private GobboController player;
    private bool handled;
    private bool applicationQuitting;

    void Awake()
    {
        player = GetComponent<GobboController>();
    }

    void Update()
    {
        CheckForDeath();
    }

    // Important: GobboController.Die() currently disables the player GameObject immediately.
    // When a GameObject is disabled, Update stops, so this watcher can miss the death.
    // OnDisable still fires during that same disable, so this catches the real run death.
    void OnDisable()
    {
        if (applicationQuitting || handled)
            return;

        CheckForDeath();
    }

    void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    void CheckForDeath()
    {
        if (handled)
            return;

        if (player == null)
            player = GetComponent<GobboController>();

        if (player == null)
            return;

        if (player.health <= 0)
            HandlePlayerDeath();
    }

    public void HandlePlayerDeath()
    {
        if (handled)
            return;

        handled = true;

        GameState state = EnsureGameState();

        if (saveRunBeforeLeaving && state != null)
            SaveCurrentRunState(state);

        List<string> successorIds = GetLivingSuccessorIds(state);

        MarkLastRunAsPlayerDeath(state, successorIds.Count);

        int level = state != null && state.gobbo != null
            ? Mathf.Max(1, state.gobbo.level)
            : Mathf.Max(1, player != null ? player.level : 1);

        int runNumber = state != null
            ? Mathf.Max(1, state.currentRunNumber)
            : 1;

        string deadName = "Player Gobbo";
        string deadType = player != null ? player.gobboType.ToString() : "Gobbo";

        PlayerDeathRunStore.GetOrCreate().BeginPlayerDeath(
            deadName,
            deadType,
            level,
            runNumber,
            deathCause,
            successorIds
        );

        Debug.Log("PlayerDeathWatcher handled death. Successor candidates: " + successorIds.Count);

        Time.timeScale = 1f;

        if (successorIds.Count == 0 && loadGameOverSceneIfNoSuccessor)
            SceneManager.LoadScene(gameOverSceneName);
        else
            SceneManager.LoadScene(campSceneName);
    }


    void MarkLastRunAsPlayerDeath(GameState state, int livingSuccessorCount)
    {
        if (state == null)
            return;

        state.lastRun.survived = false;
        state.lastRun.buddiesEnd = Mathf.Max(0, livingSuccessorCount);

        if (player != null)
        {
            state.lastRun.playerLevelEnd = Mathf.Max(1, player.level);
        }

        if (state.gobbo != null)
            state.gobbo.health = 0;
    }

    List<string> GetLivingSuccessorIds(GameState state)
    {
        List<string> ids = new List<string>();

        BuddyUnit[] livingSceneBuddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit unit in livingSceneBuddies)
        {
            if (unit == null || unit.data == null)
                continue;

            unit.data.EnsureId();

            if (unit.data.health > 0 && !ids.Contains(unit.data.uniqueId))
                ids.Add(unit.data.uniqueId);
        }

        // Fallback: if the scene list is empty, let camp reserve / saved roster keep the tribe alive.
        if (ids.Count == 0 && state != null && state.ownedBuddies != null)
        {
            foreach (BuddyData buddy in state.ownedBuddies)
            {
                if (buddy == null)
                    continue;

                buddy.EnsureId();

                if (buddy.health > 0 && !ids.Contains(buddy.uniqueId))
                    ids.Add(buddy.uniqueId);
            }
        }

        return ids;
    }

    void SaveCurrentRunState(GameState state)
    {
        if (state == null)
            return;

        // Save the actual dying player so run loot/resources stay current.
        // Death flow itself will replace the leader in camp.
        GobboController currentPlayer = Object.FindAnyObjectByType<GobboController>();
        if (currentPlayer != null)
            state.SavePlayer(currentPlayer);

        BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>(FindObjectsInactive.Include);
        if (roster != null)
            state.SaveRoster(roster);
    }

    GameState EnsureGameState()
    {
        if (GameState.Instance != null)
            return GameState.Instance;

        GameObject stateObject = new GameObject("GameState");
        return stateObject.AddComponent<GameState>();
    }
}
