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

    private BuddyData markedSuccessor;
    private bool buttonsHooked;

    void Awake()
    {
        HookButtons();
    }

    void OnEnable()
    {
        HookButtons();
    }

    void Start()
    {
        HookButtons();
        HideAll();
        TryOpenDeathFlow();
    }

    void HookButtons()
    {
        // Re-hooking is safe and helps when Unity UI references are assigned/changed in the scene.
        if (acceptMarkedSuccessorButton != null)
        {
            acceptMarkedSuccessorButton.onClick.RemoveAllListeners();
            acceptMarkedSuccessorButton.onClick.AddListener(AcceptMarkedSuccessor);
            ForceButtonClickable(acceptMarkedSuccessorButton);
        }

        if (letCampChooseButton != null)
        {
            letCampChooseButton.onClick.RemoveAllListeners();
            letCampChooseButton.onClick.AddListener(LetCampChoose);
            ForceButtonClickable(letCampChooseButton);
            SetButtonText(letCampChooseButton, "Let the Camp Choose");
        }

        if (returnToMainMenuButton != null)
        {
            returnToMainMenuButton.onClick.RemoveAllListeners();
            returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
            ForceButtonClickable(returnToMainMenuButton);
            SetButtonText(returnToMainMenuButton, "Return to Main Menu");
        }

        buttonsHooked = true;
    }

    void ForceButtonClickable(Button button)
    {
        if (button == null)
            return;

        button.interactable = true;

        Graphic graphic = button.targetGraphic;
        if (graphic == null)
            graphic = button.GetComponent<Graphic>();

        if (graphic != null)
        {
            graphic.raycastTarget = true;
            button.targetGraphic = graphic;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.raycastTarget = false;

        Text legacyText = button.GetComponentInChildren<Text>(true);
        if (legacyText != null)
            legacyText.raycastTarget = false;
    }

    void SetButtonText(Button button, string label)
    {
        if (button == null)
            return;

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null)
            legacy.text = label;
    }

    void HideAll()
    {
        if (successionPanel != null)
            successionPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void TryOpenDeathFlow()
    {
        HookButtons();

        PlayerDeathRunStore pending = PlayerDeathRunStore.Instance;

        if (pending == null || !pending.playerDiedThisRun)
        {
            DebugLog("No pending player death. Succession UI will stay closed.");
            return;
        }

        CampDeathHistoryStore.GetOrCreate().AddDeadLeaderFromPendingStore(pending);

        if (!HasAnyEligibleGobbo())
        {
            DebugLog("No eligible gobbos found. Opening game over panel.");
            OpenGameOver();
            return;
        }

        OpenSuccessionPanel();
    }

    void OpenSuccessionPanel()
    {
        markedSuccessor = CampSuccessorPreferenceStore.GetOrCreate().GetMarkedSuccessor();

        if (successionPanel != null)
        {
            successionPanel.SetActive(true);
            successionPanel.transform.SetAsLastSibling();
        }

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (titleText != null)
            titleText.text = title;

        bool hasMarked = markedSuccessor != null;

        if (bodyText != null)
            bodyText.text = hasMarked ? string.Format(markedSuccessorBody, markedSuccessor.buddyName) : noMarkedSuccessorBody;

        if (acceptMarkedSuccessorButton != null)
        {
            acceptMarkedSuccessorButton.gameObject.SetActive(hasMarked);
            acceptMarkedSuccessorButton.interactable = hasMarked;
            if (hasMarked)
                SetButtonText(acceptMarkedSuccessorButton, "Accept " + markedSuccessor.buddyName);
        }

        if (letCampChooseButton != null)
        {
            letCampChooseButton.gameObject.SetActive(true);
            letCampChooseButton.interactable = true;
            SetButtonText(letCampChooseButton, "Let the Camp Choose");
        }

        DebugLog("Opened succession panel. Marked successor: " + (hasMarked ? markedSuccessor.buddyName : "none") +
                 ", eligible count: " + GetEligibleCandidates().Count);
    }

    void OpenGameOver()
    {
        if (successionPanel != null)
            successionPanel.SetActive(false);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
        }

        if (gameOverText != null)
            gameOverText.text = gameOverMessage;

        DebugLog("Opened game over panel.");
    }

    public void AcceptMarkedSuccessor()
    {
        DebugLog("Accept marked successor clicked.");

        if (markedSuccessor == null)
        {
            OpenSuccessionPanel();
            return;
        }

        PromoteSuccessor(markedSuccessor);
    }

    public void LetCampChoose()
    {
        DebugLog("Let Camp Choose clicked.");

        BuddyData chosen = ChooseStrongestGobbo();

        if (chosen == null)
        {
            DebugLog("Camp could not choose a successor.");
            OpenGameOver();
            return;
        }

        DebugLog("Camp chose successor: " + chosen.buddyName);
        PromoteSuccessor(chosen);
    }

    BuddyData ChooseStrongestGobbo()
    {
        List<BuddyData> candidates = GetEligibleCandidates();

        BuddyData best = null;
        int bestScore = int.MinValue;

        foreach (BuddyData buddy in candidates)
        {
            if (buddy == null)
                continue;

            buddy.EnsureRuntimeDefaults();

            int score =
                buddy.level * 10000 +
                buddy.maxHealth * 100 +
                buddy.damage * 50 +
                buddy.defense * 25 +
                buddy.loyalty;

            if (best == null || score > bestScore)
            {
                best = buddy;
                bestScore = score;
            }
        }

        return best;
    }

    List<BuddyData> GetEligibleCandidates()
    {
        List<BuddyData> result = new List<BuddyData>();

        if (GameState.Instance == null || GameState.Instance.ownedBuddies == null)
            return result;

        PlayerDeathRunStore pending = PlayerDeathRunStore.Instance;

        // Prefer the living survivor IDs captured at the moment of player death.
        if (pending != null && pending.eligibleSuccessorIds != null && pending.eligibleSuccessorIds.Count > 0)
        {
            foreach (string id in pending.eligibleSuccessorIds)
            {
                BuddyData buddy = GameState.Instance.FindBuddy(id);
                if (buddy != null && !result.Contains(buddy))
                    result.Add(buddy);
            }

            if (result.Count > 0)
                return result;
        }

        // Fallback: allow the camp roster to keep the tribe alive.
        foreach (BuddyData buddy in GameState.Instance.ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureRuntimeDefaults();

            if (buddy.health > 0 && !result.Contains(buddy))
                result.Add(buddy);
        }

        return result;
    }

    bool HasAnyEligibleGobbo()
    {
        return GetEligibleCandidates().Count > 0;
    }

    void PromoteSuccessor(BuddyData successor)
    {
        if (successor == null)
        {
            DebugLog("Promote failed: successor was null.");
            return;
        }

        if (GameState.Instance == null)
        {
            DebugLog("Promote failed: no GameState.");
            return;
        }

        if (GameState.Instance.gobbo == null)
            GameState.Instance.gobbo = new GobboSaveData();

        successor.EnsureId();
        successor.EnsureRuntimeDefaults();

        GobboSaveData player = GameState.Instance.gobbo;

        // Preserve player-only resources/unlocks, but replace identity/stats with the chosen buddy.
        player.level = Mathf.Max(1, successor.level);
        player.xp = Mathf.Max(0, successor.xp);
        player.xpToNextLevel = Mathf.Max(1, successor.xpToNextLevel);
        player.gobboType = successor.buddyType;
        player.ageStage = successor.ageStage;
        player.visualSetId = string.IsNullOrWhiteSpace(successor.visualSetId)
            ? successor.buddyType.ToString().ToLowerInvariant()
            : successor.visualSetId;
        player.pendingEvolution = successor.pendingEvolution;
        player.evolutionLevelWaiting = successor.evolutionLevelWaiting;

        player.maxHealth = Mathf.Max(1, successor.maxHealth);
        player.health = player.maxHealth;
        player.attack = Mathf.Max(1, successor.damage);
        player.defense = Mathf.Max(0, successor.defense);
        player.moveSpeed = Mathf.Max(1f, successor.moveSpeed);
        player.attackCooldown = Mathf.Max(0.1f, successor.attackCooldown);

        if (successor.chosenCardIds != null)
            player.chosenCardIds = new List<string>(successor.chosenCardIds);

        string id = successor.uniqueId;

        GameState.Instance.ownedBuddies.RemoveAll(b => b == null || b.uniqueId == id);

        if (GameState.Instance.activeSquadIds != null)
            GameState.Instance.activeSquadIds.RemoveAll(activeId => activeId == id);

        GameState.Instance.RepairRosterState();

        if (GameState.Instance.lastRun != null)
            GameState.Instance.lastRun.survived = false;

        CampSuccessorPreferenceStore.GetOrCreate().ClearSuccessor();

        PlayerDeathRunStore pending = PlayerDeathRunStore.Instance;
        if (pending != null)
            pending.ClearPendingDeath();

        HideAll();

        CampSceneController camp = Object.FindAnyObjectByType<CampSceneController>(FindObjectsInactive.Include);
        if (camp != null)
        {
            DebugLog("Promotion complete. Revealing camp visuals as new player.");
            camp.RevealCampVisuals();
        }
        else
        {
            DebugLog("Promotion complete, but no CampSceneController was found.");
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void DebugLog(string message)
    {
        if (logDebugMessages)
            Debug.Log("[CampSuccessionUI] " + message);
    }
}
