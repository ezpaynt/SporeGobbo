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

        List<BuddyData> candidateSnapshots = BuildSuccessorCandidates();
        List<string> candidateIds = new List<string>();

        foreach (BuddyData buddy in candidateSnapshots)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            if (!candidateIds.Contains(buddy.uniqueId))
                candidateIds.Add(buddy.uniqueId);
        }

        int runNumber = GameState.Instance != null ? Mathf.Max(1, GameState.Instance.currentRunNumber) : 1;
        string leaderName = "Gobbo";
        int leaderLevel = 1;

        if (GameState.Instance != null && GameState.Instance.gobbo != null)
        {
            leaderLevel = Mathf.Max(1, GameState.Instance.gobbo.level);
            leaderName = GameState.Instance.gobbo.gobboType.ToString();
        }

        PlayerDeathRunStore store = PlayerDeathRunStore.GetOrCreate();
        string preferredSuccessorId = store.lockedSuccessorId;

        if (string.IsNullOrWhiteSpace(preferredSuccessorId))
        {
            CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.Instance;
            if (pref != null)
                preferredSuccessorId = pref.GetMarkedSuccessorId();
        }

        store.BeginPlayerDeath(
            leaderName,
            "Gobbo",
            leaderLevel,
            runNumber,
            deathCause,
            candidateIds,
            candidateSnapshots,
            preferredSuccessorId);

        Debug.Log("[PlayerDeathWatcher] handled death from " + source +
                  ". Successor candidates: " + candidateSnapshots.Count +
                  ", locked/preferred: " + (string.IsNullOrWhiteSpace(preferredSuccessorId) ? "none" : preferredSuccessorId));

        Time.timeScale = 1f;

        if (loadGameOverSceneIfNoSuccessors && candidateSnapshots.Count == 0 && !string.IsNullOrWhiteSpace(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
        else
            SceneManager.LoadScene(campSceneName);
    }

    List<BuddyData> BuildSuccessorCandidates()
    {
        List<BuddyData> result = new List<BuddyData>();
        HashSet<string> seen = new HashSet<string>();

        if (GameState.Instance != null && GameState.Instance.ownedBuddies != null)
        {
            foreach (BuddyData buddy in GameState.Instance.ownedBuddies)
                AddCandidate(result, seen, buddy);
        }

        BuddyUnit[] units = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit unit in units)
        {
            if (unit == null)
                continue;

            AddCandidate(result, seen, unit.data);
        }

        return result;
    }

    void AddCandidate(List<BuddyData> result, HashSet<string> seen, BuddyData buddy)
    {
        if (buddy == null)
            return;

        buddy.EnsureId();
        buddy.EnsureRuntimeDefaults();

        if (buddy.health <= 0)
            return;

        if (seen.Contains(buddy.uniqueId))
            return;

        seen.Add(buddy.uniqueId);
        result.Add(buddy.Clone());
    }
}
