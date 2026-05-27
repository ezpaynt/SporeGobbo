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

    private static bool suppressDeathHandling;
    private static bool applicationQuitting;

    public static void SuppressDeathHandlingForSceneChange()
    {
        suppressDeathHandling = true;
        Debug.Log("[PlayerDeathWatcher] Death handling suppressed for normal scene change.");
    }

    public static void ClearSceneChangeSuppression()
    {
        suppressDeathHandling = false;
    }

    void Awake()
    {
        player = GetComponent<GobboController>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Once the new scene exists, normal death detection may run again.
        ClearSceneChangeSuppression();
    }

    void Update()
    {
        TryHandleDeath("Update");
    }

    void OnDisable()
    {
        // GobboController.Die() disables the player object. Scene unload also disables it,
        // so this must ignore clean scene transitions.
        TryHandleDeath("OnDisable");
    }

    void TryHandleDeath(string source)
    {
        if (handledDeath) return;
        if (applicationQuitting) return;
        if (suppressDeathHandling) return;
        if (!LooksDead()) return;

        handledDeath = true;
        HandlePlayerDeath(source);
    }

    bool LooksDead()
    {
        if (player == null) player = GetComponent<GobboController>();
        if (player == null) return false;

        // Health <= 0 is the actual death condition.
        if (player.health <= 0) return true;

        // Do NOT treat any disabled player as dead. Normal scene loads disable objects too.
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

        List<string> candidateIds = new List<string>();
        List<BuddyData> snapshots = BuildSuccessorSnapshots(candidateIds);

        int runNumber = GameState.Instance != null ? Mathf.Max(1, GameState.Instance.currentRunNumber) : 1;
        string leaderName = "Gobbo";
        string leaderType = "Gobbo";
        int leaderLevel = 1;

        if (GameState.Instance != null && GameState.Instance.gobbo != null)
        {
            leaderLevel = Mathf.Max(1, GameState.Instance.gobbo.level);
            leaderType = GameState.Instance.gobbo.gobboType.ToString();
            leaderName = leaderType;
        }

        PlayerDeathRunStore store = PlayerDeathRunStore.GetOrCreate();
        store.BeginPlayerDeath(leaderName, leaderType, leaderLevel, runNumber, deathCause, candidateIds, snapshots);

        Debug.Log("[PlayerDeathWatcher] handled death from " + source + ". Successor candidates: " + candidateIds.Count +
                  ", locked/preferred: " + (string.IsNullOrWhiteSpace(store.lockedSuccessorId) ? "none" : store.lockedSuccessorId));

        Time.timeScale = 1f;

        SuppressDeathHandlingForSceneChange();
        if (loadGameOverSceneIfNoSuccessors && candidateIds.Count == 0 && !string.IsNullOrWhiteSpace(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
        else
            SceneManager.LoadScene(campSceneName);
    }

    List<BuddyData> BuildSuccessorSnapshots(List<string> ids)
    {
        List<BuddyData> snapshots = new List<BuddyData>();

        if (GameState.Instance != null && GameState.Instance.ownedBuddies != null)
        {
            foreach (BuddyData buddy in GameState.Instance.ownedBuddies)
            {
                AddCandidateSnapshot(buddy, ids, snapshots);
            }
        }

        // Fallback for old/direct SampleScene testing where GameState roster was not populated.
        if (ids.Count == 0)
        {
            BuddyUnit[] units = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
            foreach (BuddyUnit unit in units)
            {
                if (unit == null) continue;
                AddCandidateSnapshot(unit.data, ids, snapshots);
            }
        }

        return snapshots;
    }

    void AddCandidateSnapshot(BuddyData buddy, List<string> ids, List<BuddyData> snapshots)
    {
        if (buddy == null) return;

        buddy.EnsureId();
        buddy.EnsureRuntimeDefaults();
        if (buddy.health <= 0) return;
        if (ids.Contains(buddy.uniqueId)) return;

        ids.Add(buddy.uniqueId);
        snapshots.Add(buddy.Clone());
    }
}
