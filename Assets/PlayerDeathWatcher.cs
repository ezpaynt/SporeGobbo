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

    [Header("Debug")]
    public bool logDebug = true;

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

    private void Awake()
    {
        player = GetComponent<GobboController>();
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (logDebug)
            Debug.Log("[PlayerDeathWatcher] Awake on " + gameObject.name + " scene=" + SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Once the new scene exists, normal death detection may run again.
        ClearSceneChangeSuppression();
    }

    private void Update()
    {
        TryHandleDeath("Update", false);
    }

    private void OnDisable()
    {
        // GobboController.Die() disables the player object.
        // Scene unload also disables it, so only a true health <= 0 state may trigger death here.
        TryHandleDeath("OnDisable", true);
    }

    /// <summary>
    /// Explicit death entry point. GobboController.Die() can call this before disabling itself.
    /// This also makes testing safer because actual death is no longer confused with scene unload.
    /// </summary>
    public void HandleDeathNow(string source = "Direct")
    {
        TryHandleDeath(source, true, true);
    }

    private void TryHandleDeath(string source, bool allowWhileSuppressedIfActuallyDead, bool force = false)
    {
        if (handledDeath) return;
        if (applicationQuitting) return;

        bool actuallyDead = force || LooksDead();
        if (!actuallyDead) return;

        // Normal scene changes suppress disable-based false positives, but should never suppress
        // a real health <= 0 player death.
        if (suppressDeathHandling && !allowWhileSuppressedIfActuallyDead)
        {
            if (logDebug)
                Debug.Log("[PlayerDeathWatcher] Skipped death from " + source + " because scene-change suppression is active.");
            return;
        }

        handledDeath = true;
        HandlePlayerDeath(source);
    }

    private bool LooksDead()
    {
        if (player == null) player = GetComponent<GobboController>();
        if (player == null) return false;

        // Health <= 0 is the actual death condition.
        // Do NOT treat a disabled player as dead; scene unload disables objects too.
        return player.health <= 0;
    }

    private void HandlePlayerDeath(string source)
    {
        if (GameState.Instance != null)
        {
            if (saveRunBeforeLeaving && player != null)
                GameState.Instance.SavePlayer(player);

            if (GameState.Instance.lastRun != null)
            {
                GameState.Instance.lastRun.survived = false;

                GobboUnitSaveData leader = GameState.Instance.GetLeader();
                GameState.Instance.lastRun.playerLevelEnd = leader != null ? Mathf.Max(1, leader.level) : 1;
            }
        }

        List<string> candidateIds = new List<string>();
        List<GobboUnitSaveData> snapshots = BuildSuccessorSnapshots(candidateIds);

        int runNumber = GameState.Instance != null ? Mathf.Max(1, GameState.Instance.currentRunNumber) : 1;
        string leaderName = "Gobbo";
        string leaderType = "Gobbo";
        int leaderLevel = 1;

        if (GameState.Instance != null)
        {
            GobboUnitSaveData leader = GameState.Instance.GetLeader();
            if (leader != null)
            {
                leader.EnsureRuntimeDefaults();
                leaderName = string.IsNullOrWhiteSpace(leader.displayName) ? "Gobbo" : leader.displayName;
                leaderType = leader.gobboType.ToString();
                leaderLevel = Mathf.Max(1, leader.level);
            }
        }

        PlayerDeathRunStore store = PlayerDeathRunStore.GetOrCreate();
        store.BeginPlayerDeath(leaderName, leaderType, leaderLevel, runNumber, deathCause, candidateIds, snapshots);

        Debug.Log("[PlayerDeathWatcher] handled death from " + source +
                  ". Successor candidates: " + candidateIds.Count +
                  ", locked/preferred: " + (string.IsNullOrWhiteSpace(store.lockedSuccessorId) ? "none" : store.lockedSuccessorId));

        Time.timeScale = 1f;

        // Prevent the scene unload caused by this LoadScene from triggering a second death.
        SuppressDeathHandlingForSceneChange();

        if (loadGameOverSceneIfNoSuccessors && candidateIds.Count == 0 && !string.IsNullOrWhiteSpace(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
        else
            SceneManager.LoadScene(campSceneName);
    }

    private List<GobboUnitSaveData> BuildSuccessorSnapshots(List<string> ids)
    {
        List<GobboUnitSaveData> snapshots = new List<GobboUnitSaveData>();

        if (GameState.Instance != null)
        {
            foreach (GobboUnitSaveData gobbo in GameState.Instance.GetAllGobbos(includeLeader: false, includeDead: false))
            {
                AddCandidateSnapshot(gobbo, ids, snapshots);
            }
        }

        // Fallback for old/direct SampleScene testing where GameState roster was not populated.
        if (ids.Count == 0)
        {
            BuddyUnit[] units = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
            foreach (BuddyUnit unit in units)
            {
                if (unit == null || unit.data == null) continue;
                AddCandidateSnapshot(unit.data, ids, snapshots);
            }
        }

        return snapshots;
    }

    private void AddCandidateSnapshot(GobboUnitSaveData gobbo, List<string> ids, List<GobboUnitSaveData> snapshots)
    {
        if (gobbo == null) return;

        gobbo.EnsureRuntimeDefaults();
        if (gobbo.health <= 0) return;
        if (string.IsNullOrWhiteSpace(gobbo.uniqueId)) return;
        if (ids.Contains(gobbo.uniqueId)) return;

        ids.Add(gobbo.uniqueId);
        snapshots.Add(gobbo.CloneUnit());
    }
}
