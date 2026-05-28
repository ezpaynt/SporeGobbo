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
        return player.health <= 0;
    }

    void HandlePlayerDeath(string source)
    {
        GameState gs = GameState.Instance;

        if (gs != null)
        {
            if (saveRunBeforeLeaving && player != null)
                gs.SavePlayer(player);

            if (gs.lastRun != null)
            {
                GobboUnitSaveData leaderForSummary = gs.GetLeader();
                gs.lastRun.survived = false;
                gs.lastRun.playerLevelEnd = leaderForSummary != null ? Mathf.Max(1, leaderForSummary.level) : 1;
            }
        }

        List<string> candidateIds = new List<string>();
        List<GobboUnitSaveData> snapshots = BuildSuccessorSnapshots(candidateIds);

        int runNumber = gs != null ? Mathf.Max(1, gs.currentRunNumber) : 1;
        string leaderName = "Gobbo";
        string leaderType = "Gobbo";
        int leaderLevel = 1;

        if (gs != null)
        {
            GobboUnitSaveData leader = gs.GetLeader();
            if (leader != null)
            {
                leader.EnsureRuntimeDefaults();
                leaderName = string.IsNullOrWhiteSpace(leader.displayName) ? "Gobbo" : leader.displayName;
                leaderType = leader.gobboType.ToString();
                leaderLevel = Mathf.Max(1, leader.level);
            }
        }

        PlayerDeathRunStore store = PlayerDeathRunStore.GetOrCreate();

        // If the run did not explicitly lock a successor, keep the camp-marked successor as preference.
        if (string.IsNullOrWhiteSpace(store.lockedSuccessorId) && gs != null)
            store.LockSuccessorForRun(gs.GetMarkedSuccessorId());

        store.BeginPlayerDeath(leaderName, leaderType, leaderLevel, runNumber, deathCause, candidateIds, snapshots);

        Debug.Log("[PlayerDeathWatcher] handled death from " + source
            + ". Successor candidates: " + candidateIds.Count
            + ", locked/preferred: " + (string.IsNullOrWhiteSpace(store.lockedSuccessorId) ? "none" : store.lockedSuccessorId));

        Time.timeScale = 1f;
        SuppressDeathHandlingForSceneChange();

        if (loadGameOverSceneIfNoSuccessors && candidateIds.Count == 0 && !string.IsNullOrWhiteSpace(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
        else
            SceneManager.LoadScene(campSceneName);
    }

    List<GobboUnitSaveData> BuildSuccessorSnapshots(List<string> ids)
    {
        List<GobboUnitSaveData> snapshots = new List<GobboUnitSaveData>();
        GameState gs = GameState.Instance;

        if (gs != null)
        {
            gs.RepairRosterState();
            List<GobboUnitSaveData> gobbos = gs.GetAllGobbos(includeLeader: false, includeDead: false);
            foreach (GobboUnitSaveData unit in gobbos)
                AddCandidateSnapshot(unit, ids, snapshots);
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

    void AddCandidateSnapshot(GobboUnitSaveData unit, List<string> ids, List<GobboUnitSaveData> snapshots)
    {
        if (unit == null) return;
        unit.EnsureRuntimeDefaults();
        if (unit.isLeader) return;
        if (unit.isDead) return;
        if (unit.health <= 0) return;
        if (string.IsNullOrWhiteSpace(unit.uniqueId)) unit.EnsureId();
        if (ids.Contains(unit.uniqueId)) return;

        ids.Add(unit.uniqueId);
        GobboUnitSaveData copy = unit.CloneUnit();
        copy.isLeader = false;
        copy.isDead = false;
        snapshots.Add(copy);
    }
}
