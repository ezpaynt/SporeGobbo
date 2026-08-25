using System.Collections.Generic;
using SporeGobbo.CampLifecycle;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeathWatcher : MonoBehaviour
{
    [Header("Death Flow")]
    public string campSceneName = "CampScene";
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
        if (logDebug) Debug.Log("[PlayerDeathWatcher] Awake on " + gameObject.name + " scene=" + SceneManager.GetActiveScene().name);
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
        ClearSceneChangeSuppression();
    }

    private void Update()
    {
        TryHandleDeath("Update", false);
    }

    private void OnDisable()
    {
        TryHandleDeath("OnDisable", true);
    }

    public void HandleDeathNow(string source = "Direct")
    {
        TryHandleDeath(source, true, true);
    }

    private void TryHandleDeath(string source, bool allowWhileSuppressedIfActuallyDead, bool force = false)
    {
        if (handledDeath || applicationQuitting) return;
        bool actuallyDead = force || LooksDead();
        if (!actuallyDead) return;
        if (suppressDeathHandling && !allowWhileSuppressedIfActuallyDead)
        {
            if (logDebug) Debug.Log("[PlayerDeathWatcher] Skipped death from " + source + " because scene-change suppression is active.");
            return;
        }
        handledDeath = true;
        HandlePlayerDeath(source);
    }

    private bool LooksDead()
    {
        if (player == null) player = GetComponent<GobboController>();
        if (player == null) return false;
        return player.health <= 0;
    }

    private void HandlePlayerDeath(string source)
    {
        GobboUnitSaveData deadLeader = null;
        if (GameState.Instance != null)
        {
            if (saveRunBeforeLeaving && player != null) GameState.Instance.SavePlayer(player);
            RunSnackLootService.FinalizeDeath(GameState.Instance);
            if (GameState.Instance.lastRun != null)
            {
                GameState.Instance.lastRun.survived = false;
                GobboUnitSaveData leader = GameState.Instance.GetLeader();
                GameState.Instance.lastRun.playerLevelEnd = leader != null ? Mathf.Max(1, leader.level) : 1;
            }

            deadLeader = GameState.Instance.GetLeader()?.CloneUnit();
            GobboUnitSaveData liveLeader = GameState.Instance.GetLeader();
            if (liveLeader != null)
            {
                liveLeader.health = 0;
                liveLeader.isDead = true;
                liveLeader.causeOfDeath = deathCause;
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
        if (GameState.Instance != null && deadLeader != null)
            GameState.Instance.AddDeathHistoryRecord(GameState.Instance.BuildDeathRecord(deadLeader, runNumber, deathCause, true));
        Debug.Log("[PlayerDeathWatcher] handled death from " + source + ". Successor candidates: " + candidateIds.Count + ", locked/preferred: " + (string.IsNullOrWhiteSpace(store.lockedSuccessorId) ? "none" : store.lockedSuccessorId));
        SporePauseService.ResetAll();
        CampArrivalContext.Clear();

        if (CampLifecyclePolicy.DecideDeathDestination(candidateIds.Count) == DeathDestination.GameOver)
        {
            if (GameState.Instance != null)
            {
                GameState.Instance.lineageEnded = true;
                SporeSaveManager.SaveCurrentSlotFromGameState();
            }
            store.ClearPendingDeath();
            LineageGameOverScreen.Show();
            return;
        }

        GameState.Instance.lineageEnded = false;
        CampArrivalContext.SetPending(CampArrivalMode.PostDeathSuccession);
        SuppressDeathHandlingForSceneChange();
        SceneManager.LoadScene(campSceneName);
    }

    private List<GobboUnitSaveData> BuildSuccessorSnapshots(List<string> ids)
    {
        List<GobboUnitSaveData> snapshots = new List<GobboUnitSaveData>();
        if (GameState.Instance != null)
        {
            foreach (GobboUnitSaveData gobbo in GameState.Instance.GetAllGobbos(includeLeader: false, includeDead: false))
                AddCandidateSnapshot(gobbo, ids, snapshots);
        }

        return snapshots;
    }

    private void AddCandidateSnapshot(GobboUnitSaveData gobbo, List<string> ids, List<GobboUnitSaveData> snapshots)
    {
        if (gobbo == null) return;
        gobbo.EnsureRuntimeDefaults();
        if (!CampLifecyclePolicy.IsValidSurvivor(gobbo.uniqueId, gobbo.isDead, gobbo.health, gobbo.isLeader)) return;
        if (ids.Contains(gobbo.uniqueId)) return;
        ids.Add(gobbo.uniqueId);
        snapshots.Add(gobbo.CloneUnit());
    }
}
