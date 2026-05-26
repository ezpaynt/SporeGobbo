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

    void Awake()
    {
        player = GetComponent<GobboController>();
    }

    void Update()
    {
        if (handled || player == null)
            return;

        if (player.health <= 0)
            HandlePlayerDeath();
    }

    void HandlePlayerDeath()
    {
        handled = true;

        GameState state = EnsureGameState();
        if (saveRunBeforeLeaving && state != null)
            SaveCurrentRunState(state);

        List<string> successorIds = GetLivingSuccessorIds(state);

        int level = state != null && state.gobbo != null ? Mathf.Max(1, state.gobbo.level) : 1;
        int runNumber = state != null ? Mathf.Max(1, state.currentRunNumber) : 1;

        PlayerDeathRunStore.GetOrCreate().BeginPlayerDeath("Player Gobbo", level, runNumber, deathCause, successorIds);

        Time.timeScale = 1f;

        if (successorIds.Count == 0 && loadGameOverSceneIfNoSuccessor)
            SceneManager.LoadScene(gameOverSceneName);
        else
            SceneManager.LoadScene(campSceneName);
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
            if (!ids.Contains(unit.data.uniqueId))
                ids.Add(unit.data.uniqueId);
        }

        // Fallback: if the scene list is empty, let camp reserves keep the tribe alive.
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
