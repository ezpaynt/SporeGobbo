using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CampSuccessionUI : MonoBehaviour
{
    [Header("Succession Panel")]
    public GameObject successionPanel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Button acceptMarkedSuccessorButton;
    public Button letCampChooseButton;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public Button returnToMainMenuButton;
    public string mainMenuSceneName = "MainMenu";

    [Header("Text")]
    public string title = "The leader is bones now.";
    [TextArea(2, 5)] public string markedSuccessorBody = "{0} was marked to take over.";
    [TextArea(2, 5)] public string noMarkedSuccessorBody = "No gobbo was marked as successor. The camp has to choose.";
    [TextArea(2, 5)] public string gameOverMessage = "GAME OVER\n\nNo gobbos are left to remember the camp.";

    [Header("Debug")]
    public bool logDebugMessages = true;

    private GobboUnitSaveData markedSuccessor;
    private readonly List<GobboUnitSaveData> eligibleSuccessors = new List<GobboUnitSaveData>();

    void Awake()
    {
        HookButtons();
        HideAllPanels();
    }

    void OnEnable()
    {
        HookButtons();
    }

    void Start()
    {
        HookButtons();
    }

    public bool TryOpenDeathFlow()
    {
        PlayerDeathRunStore store = PlayerDeathRunStore.Instance;
        if (store == null || !store.playerDiedThisRun)
        {
            Log("No pending player death. Succession UI will stay closed.");
            HideAllPanels();
            return false;
        }

        OpenDeathFlow();
        return true;
    }

    public void OpenDeathFlow()
    {
        RefreshCandidates();

        if (eligibleSuccessors.Count <= 0)
        {
            Log("No eligible gobbos found. Opening game over panel.");
            OpenGameOverPanel();
            return;
        }

        OpenSuccessionPanel();
    }

    void HookButtons()
    {
        if (acceptMarkedSuccessorButton != null)
        {
            acceptMarkedSuccessorButton.name = "AcceptMarkedSuccessorButton";
            acceptMarkedSuccessorButton.onClick.RemoveAllListeners();
            acceptMarkedSuccessorButton.onClick.AddListener(AcceptMarkedSuccessor);
        }

        if (letCampChooseButton != null)
        {
            letCampChooseButton.name = "LetCampChooseButton";
            letCampChooseButton.onClick.RemoveAllListeners();
            letCampChooseButton.onClick.AddListener(LetCampChoose);
        }

        if (returnToMainMenuButton != null)
        {
            returnToMainMenuButton.name = "ReturnToMainMenuButton";
            returnToMainMenuButton.onClick.RemoveAllListeners();
            returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    void RefreshCandidates()
    {
        eligibleSuccessors.Clear();
        markedSuccessor = null;

        PlayerDeathRunStore store = PlayerDeathRunStore.Instance;
        string preferredId = store != null ? store.lockedSuccessorId : "";
        if (string.IsNullOrWhiteSpace(preferredId) && GameState.Instance != null)
            preferredId = GameState.Instance.GetMarkedSuccessorId();

        if (store != null && store.survivorSnapshots != null && store.survivorSnapshots.Count > 0)
        {
            foreach (GobboUnitSaveData snapshot in store.survivorSnapshots)
                AddEligibleCopy(snapshot);
        }
        else if (GameState.Instance != null)
        {
            foreach (GobboUnitSaveData unit in GameState.Instance.GetAllGobbos(includeLeader: false, includeDead: false))
                AddEligibleCopy(unit);
        }

        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            foreach (GobboUnitSaveData unit in eligibleSuccessors)
            {
                if (unit != null && unit.uniqueId == preferredId)
                {
                    markedSuccessor = unit;
                    break;
                }
            }
        }

        Log("Opened succession panel. Locked/preferred successor id: "
            + (string.IsNullOrWhiteSpace(preferredId) ? "none" : preferredId)
            + ", marked successor: " + (markedSuccessor != null ? GetDisplayName(markedSuccessor) : "none")
            + ", eligible count: " + eligibleSuccessors.Count);
    }

    void AddEligibleCopy(GobboUnitSaveData source)
    {
        if (source == null) return;
        source.EnsureRuntimeDefaults();
        if (source.isLeader || source.isDead) return;
        if (source.health <= 0) source.health = Mathf.Max(1, source.maxHealth);

        GobboUnitSaveData copy = source.CloneUnit();
        copy.isLeader = false;
        copy.isDead = false;
        copy.EnsureRuntimeDefaults();

        foreach (GobboUnitSaveData existing in eligibleSuccessors)
            if (existing != null && existing.uniqueId == copy.uniqueId)
                return;

        eligibleSuccessors.Add(copy);
    }

    void OpenSuccessionPanel()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (successionPanel != null)
        {
            successionPanel.SetActive(true);
            successionPanel.transform.SetAsLastSibling();
            EnsureCanvasGroupInteractive(successionPanel);
        }

        if (titleText != null) titleText.text = title;
        if (bodyText != null)
            bodyText.text = markedSuccessor != null
                ? string.Format(markedSuccessorBody, GetDisplayName(markedSuccessor))
                : noMarkedSuccessorBody;

        if (acceptMarkedSuccessorButton != null) acceptMarkedSuccessorButton.gameObject.SetActive(markedSuccessor != null);
        if (letCampChooseButton != null) letCampChooseButton.gameObject.SetActive(true);

        HookButtons();
    }

    void OpenGameOverPanel()
    {
        if (successionPanel != null) successionPanel.SetActive(false);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            EnsureCanvasGroupInteractive(gameOverPanel);
        }

        if (gameOverText != null) gameOverText.text = gameOverMessage;
        HookButtons();
    }

    void EnsureCanvasGroupInteractive(GameObject panel)
    {
        if (panel == null) return;
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    public void AcceptMarkedSuccessor()
    {
        Log("Accept marked successor clicked.");
        if (markedSuccessor == null)
        {
            LetCampChoose();
            return;
        }

        PromoteSuccessor(markedSuccessor);
    }

    public void LetCampChoose()
    {
        Log("Let Camp Choose clicked.");
        RefreshCandidates();

        GobboUnitSaveData chosen = PickStrongestSuccessor();
        if (chosen == null)
        {
            OpenGameOverPanel();
            return;
        }

        PromoteSuccessor(chosen);
    }

    GobboUnitSaveData PickStrongestSuccessor()
    {
        GobboUnitSaveData best = null;
        foreach (GobboUnitSaveData unit in eligibleSuccessors)
        {
            if (unit == null) continue;
            if (best == null || GetSuccessorScore(unit) > GetSuccessorScore(best)) best = unit;
        }
        return best;
    }

    int GetSuccessorScore(GobboUnitSaveData unit)
    {
        if (unit == null) return -999999;
        int power = Mathf.Max(unit.attack, unit.damage);
        return unit.level * 10000 + unit.maxHealth * 100 + power * 25 + unit.loyalty;
    }

    void PromoteSuccessor(GobboUnitSaveData successor)
    {
        if (successor == null || GameState.Instance == null) return;

        successor.EnsureRuntimeDefaults();
        GameState state = GameState.Instance;
        PlayerDeathRunStore store = PlayerDeathRunStore.Instance;

        // Memorialize the old leader before changing GameState leader.
        CampDeathHistoryStore history = CampDeathHistoryStore.GetOrCreate();
        if (history != null && store != null && !store.memorialAddedToHistory)
            history.AddDeadLeaderFromPendingStore(store);

        // Ensure GameState has all survivor snapshots. This protects death flows that cross scenes.
        RebuildRosterFromEligibleSnapshots(state);

        bool promoted = state.PromoteBuddyToLeader(successor.uniqueId);
        if (!promoted)
        {
            Log("Promotion failed for " + successor.uniqueId + ". Opening game over panel.");
            OpenGameOverPanel();
            return;
        }

        GobboUnitSaveData leader = state.GetLeader();
        if (leader != null)
        {
            leader.health = Mathf.Max(1, leader.maxHealth);
            leader.isLeader = true;
            leader.isDead = false;
            leader.EnsureRuntimeDefaults();
            state.SetLeader(leader);
        }

        state.SetMarkedSuccessorId("");
        state.RepairRosterState();

        CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.Instance;
        if (pref != null) pref.ClearSuccessor(false);

        if (store != null) store.ClearPendingDeath();

        SporeSaveManager.SaveCurrentSlotFromGameState();

        Log("Promoted successor: " + GetDisplayName(successor) + " / " + successor.uniqueId);
        HideAllPanels();

        CampSceneController controller = Object.FindAnyObjectByType<CampSceneController>(FindObjectsInactive.Include);
        if (controller != null) controller.RevealCampVisuals();
    }

    void RebuildRosterFromEligibleSnapshots(GameState state)
    {
        if (state == null) return;
        state.RepairRosterState();

        foreach (GobboUnitSaveData unit in eligibleSuccessors)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            if (state.FindGobboById(unit.uniqueId) != null) continue;

            BuddyData buddy = BuddyData.FromUnit(unit);
            if (buddy == null) continue;
            buddy.isLeader = false;
            buddy.isDead = false;
            state.AddBuddy(buddy, preferActiveSquad: false);
        }

        state.RepairRosterState();
    }

    public void ReturnToMainMenu()
    {
        Log("Return to main menu clicked. Scene: " + mainMenuSceneName);
        Time.timeScale = 1f;
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    void HideAllPanels()
    {
        if (successionPanel != null) successionPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    string GetDisplayName(GobboUnitSaveData unit)
    {
        if (unit == null) return "Gobbo";
        if (!string.IsNullOrWhiteSpace(unit.displayName)) return unit.displayName;
        BuddyData buddy = unit as BuddyData;
        if (buddy != null && !string.IsNullOrWhiteSpace(buddy.buddyName)) return buddy.buddyName;
        return "Gobbo";
    }

    void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[CampSuccessionUI] " + message);
    }
}
