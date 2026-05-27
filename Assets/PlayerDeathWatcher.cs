using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeathWatcher : MonoBehaviour
{
    [Header("Death Flow")]
    public string campSceneName = "CampScene";
    public string gameOverSceneName = "GameOverScene";
    public bool loadGameOverSceneIfNoSuccessors = false;
    public bool saveRunBeforeLeaving = true;
    public string deathCause = "The leader got chewed up in the dirt.";

    private GobboController player;
    private bool handledDeath;

    void Awake()
    {
        player = GetComponent<GobboController>();
    }

    void Update()
    {
        TryHandleDeath("Update");
    }

    void OnDisable()
    {
        // GobboController.Die() disables the player object.
        TryHandleDeath("OnDisable");
    }

    void TryHandleDeath(string source)
    {
        if (handledDeath)
            return;

        if (!LooksDead())
            return;

        handledDeath = true;
        HandlePlayerDeath(source);
    }

    bool LooksDead()
    {
        if (player == null)
            player = GetComponent<GobboController>();

        if (player != null && player.health <= 0)
            return true;

        if (player != null && !gameObject.activeInHierarchy)
            return true;

        return false;
    }

    void HandlePlayerDeath(string source)
    {
        if (GameState.Instance != null)
        {
            if (saveRunBeforeLeaving && player != null)
                GameState.Instance.SavePlayer(player);

            if (GameState.Instance.lastRun != null)
            {
                GameState.Instance.lastRun.survived = false;
                GameState.Instance.lastRun.playerLevelEnd = GameState.Instance.gobbo != null ? GameState.Instance.gobbo.level : 1;
            }
        }

        List<string> candidateIds;
        List<BuddyData> snapshots;
        BuildSuccessorCandidates(out candidateIds, out snapshots);

        int runNumber = GameState.Instance != null ? Mathf.Max(1, GameState.Instance.currentRunNumber) : 1;
        string leaderName = "Gobbo";
        int leaderLevel = 1;

        if (GameState.Instance != null && GameState.Instance.gobbo != null)
        {
            leaderLevel = Mathf.Max(1, GameState.Instance.gobbo.level);
            leaderName = GameState.Instance.gobbo.gobboType.ToString();
        }

        PlayerDeathRunStore store = PlayerDeathRunStore.GetOrCreate();
        store.BeginPlayerDeath(leaderName, "Gobbo", leaderLevel, runNumber, deathCause, candidateIds, snapshots);

        Debug.Log("[PlayerDeathWatcher] handled death from " + source + ". Successor candidates: " + candidateIds.Count + ", locked/preferred: " + (string.IsNullOrWhiteSpace(store.lockedSuccessorId) ? "none" : store.lockedSuccessorId));

        Time.timeScale = 1f;

        if (loadGameOverSceneIfNoSuccessors && candidateIds.Count == 0 && !string.IsNullOrWhiteSpace(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
        else
            SceneManager.LoadScene(campSceneName);
    }

    void BuildSuccessorCandidates(out List<string> ids, out List<BuddyData> snapshots)
    {
        ids = new List<string>();
        snapshots = new List<BuddyData>();

        if (GameState.Instance == null || GameState.Instance.ownedBuddies == null)
            return;

        foreach (BuddyData buddy in GameState.Instance.ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();

            if (buddy.health <= 0)
                continue;

            if (!ids.Contains(buddy.uniqueId))
            {
                ids.Add(buddy.uniqueId);
                snapshots.Add(buddy.Clone());
            }
        }
    }
}
