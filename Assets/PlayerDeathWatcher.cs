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
        // GobboController.Die() disables the player object. This catches that exact case.
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

        // If disabled by death before health was visible to this component, treat disabled player as dead.
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

        List<string> candidates = BuildSuccessorCandidateIds();
        int runNumber = GameState.Instance != null ? Mathf.Max(1, GameState.Instance.currentRunNumber) : 1;
        string leaderName = "Gobbo";
        int leaderLevel = 1;

        if (GameState.Instance != null && GameState.Instance.gobbo != null)
        {
            leaderLevel = Mathf.Max(1, GameState.Instance.gobbo.level);
            leaderName = GameState.Instance.gobbo.gobboType.ToString();
        }

        PlayerDeathRunStore store = PlayerDeathRunStore.GetOrCreate();
        store.BeginPlayerDeath(leaderName, leaderLevel, runNumber, deathCause, candidates);

        Debug.Log("[PlayerDeathWatcher] handled death from " + source + ". Successor candidates: " + candidates.Count);

        Time.timeScale = 1f;

        if (loadGameOverSceneIfNoSuccessors && candidates.Count == 0 && !string.IsNullOrWhiteSpace(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
        else
            SceneManager.LoadScene(campSceneName);
    }

    List<string> BuildSuccessorCandidateIds()
    {
        List<string> ids = new List<string>();

        if (GameState.Instance != null && GameState.Instance.ownedBuddies != null)
        {
            foreach (BuddyData buddy in GameState.Instance.ownedBuddies)
            {
                if (buddy == null)
                    continue;

                buddy.EnsureId();
                buddy.EnsureRuntimeDefaults();

                // For succession, any living roster gobbo counts, active or reserve.
                if (buddy.health > 0 && !ids.Contains(buddy.uniqueId))
                    ids.Add(buddy.uniqueId);
            }
        }

        // Fallback: scene BuddyUnits, for old test scenes where GameState was not populated.
        if (ids.Count == 0)
        {
            BuddyUnit[] units = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
            foreach (BuddyUnit unit in units)
            {
                if (unit == null || unit.data == null)
                    continue;

                unit.data.EnsureId();
                unit.data.EnsureRuntimeDefaults();

                if (unit.data.health > 0 && !ids.Contains(unit.data.uniqueId))
                    ids.Add(unit.data.uniqueId);
            }
        }

        return ids;
    }
}
