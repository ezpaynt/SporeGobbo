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

    private void Awake()
    {
        AutoFindMissingReferences();
        HookButtons();
        HideAllPanels();
    }

    private void OnEnable()
    {
        AutoFindMissingReferences();
        HookButtons();
    }

    private void Start()
    {
        AutoFindMissingReferences();
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
        AutoFindMissingReferences();
        HookButtons();
        RefreshCandidates();

        if (eligibleSuccessors.Count <= 0)
        {
            Log("No eligible gobbos found. Opening game over panel.");
            OpenGameOverPanel();
            return;
        }

        OpenSuccessionPanel();
    }

    private void AutoFindMissingReferences()
    {
        if (successionPanel != null)
        {
            Button[] buttons = successionPanel.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button == null) continue;
                string n = button.name.ToLowerInvariant();
                if (acceptMarkedSuccessorButton == null && (n.Contains("accept") || n.Contains("marked") || n.Contains("successor")))
                    acceptMarkedSuccessorButton = button;
                else if (letCampChooseButton == null && (n.Contains("choose") || n.Contains("camp") || n.Contains("let")))
                    letCampChooseButton = button;
            }

            if (buttons.Length >= 1 && acceptMarkedSuccessorButton == null) acceptMarkedSuccessorButton = buttons[0];
            if (buttons.Length >= 2 && letCampChooseButton == null) letCampChooseButton = buttons[1];
        }

        if (gameOverPanel != null && returnToMainMenuButton == null)
        {
            Button[] buttons = gameOverPanel.GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0) returnToMainMenuButton = buttons[0];
        }
    }

    private void HookButtons()
    {
        if (acceptMarkedSuccessorButton != null)
        {
            PrepareButton(acceptMarkedSuccessorButton, "AcceptMarkedSuccessorButton", "Accept\nMarked\nSuccessor");
            acceptMarkedSuccessorButton.onClick.RemoveAllListeners();
            acceptMarkedSuccessorButton.onClick.AddListener(AcceptMarkedSuccessor);
            Log("Hooked accept button: " + GetPath(acceptMarkedSuccessorButton.transform));
        }
        else Log("WARNING: acceptMarkedSuccessorButton is not assigned.");

        if (letCampChooseButton != null)
        {
            PrepareButton(letCampChooseButton, "LetCampChooseButton", "Let Camp\nChoose");
            letCampChooseButton.onClick.RemoveAllListeners();
            letCampChooseButton.onClick.AddListener(LetCampChoose);
            Log("Hooked choose button: " + GetPath(letCampChooseButton.transform));
        }
        else Log("WARNING: letCampChooseButton is not assigned.");

        if (returnToMainMenuButton != null)
        {
            PrepareButton(returnToMainMenuButton, "ReturnToMainMenuButton", "Return To\nMain Menu");
            returnToMainMenuButton.onClick.RemoveAllListeners();
            returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
            Log("Hooked return button: " + GetPath(returnToMainMenuButton.transform));
        }
    }

    private void PrepareButton(Button button, string objectName, string label)
    {
        if (button == null) return;
        button.name = objectName;
        button.gameObject.SetActive(true);
        button.interactable = true;
        button.transform.SetAsLastSibling();

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            button.targetGraphic = image;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
            text.raycastTarget = false;
        }

        Text oldText = button.GetComponentInChildren<Text>(true);
        if (oldText != null)
        {
            oldText.text = label;
            oldText.raycastTarget = false;
        }
    }

    private void RefreshCandidates()
    {
        eligibleSuccessors.Clear();
        markedSuccessor = null;

        PlayerDeathRunStore store = PlayerDeathRunStore.Instance;
        if (store == null) return;

        string preferredId = store.lockedSuccessorId;

        if (store.survivorSnapshots != null && store.survivorSnapshots.Count > 0)
        {
            foreach (GobboUnitSaveData snapshot in store.survivorSnapshots)
            {
                if (snapshot == null) continue;
                GobboUnitSaveData copy = snapshot.CloneUnit();
                copy.isLeader = false;
                copy.isDead = false;
                copy.EnsureRuntimeDefaults();
                if (copy.health <= 0) copy.health = Mathf.Max(1, copy.maxHealth);
                eligibleSuccessors.Add(copy);
            }
        }
        else if (GameState.Instance != null)
        {
            foreach (GobboUnitSaveData unit in GameState.Instance.GetAllGobbos(false, false))
            {
                if (unit == null || unit.isLeader || unit.isDead) continue;
                GobboUnitSaveData copy = unit.CloneUnit();
                copy.EnsureRuntimeDefaults();
                if (copy.health <= 0) copy.health = Mathf.Max(1, copy.maxHealth);
                eligibleSuccessors.Add(copy);
            }
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

        Log("Opened succession panel. Locked/preferred successor id: " +
            (string.IsNullOrWhiteSpace(preferredId) ? "none" : preferredId) +
            ", marked successor: " + (markedSuccessor != null ? markedSuccessor.displayName : "none") +
            ", eligible count: " + eligibleSuccessors.Count);
    }

    private void OpenSuccessionPanel()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (successionPanel != null)
        {
            successionPanel.SetActive(true);
            successionPanel.transform.SetAsLastSibling();
            EnsurePanelInteractive(successionPanel);
        }

        if (titleText != null) titleText.text = title;
        if (bodyText != null)
            bodyText.text = markedSuccessor != null
                ? string.Format(markedSuccessorBody, markedSuccessor.displayName)
                : noMarkedSuccessorBody;

        if (acceptMarkedSuccessorButton != null) acceptMarkedSuccessorButton.gameObject.SetActive(markedSuccessor != null);
        if (letCampChooseButton != null) letCampChooseButton.gameObject.SetActive(true);

        HookButtons();
    }

    private void OpenGameOverPanel()
    {
        if (successionPanel != null) successionPanel.SetActive(false);
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            EnsurePanelInteractive(gameOverPanel);
        }

        if (gameOverText != null) gameOverText.text = gameOverMessage;
        HookButtons();
    }

    private void EnsurePanelInteractive(GameObject panel)
    {
        if (panel == null) return;

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        Graphic[] graphics = panel.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic == null) continue;
            if (graphic.GetComponent<Button>() == null && graphic.GetComponentInParent<Button>() != null)
                graphic.raycastTarget = false;
        }
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

    private GobboUnitSaveData PickStrongestSuccessor()
    {
        GobboUnitSaveData best = null;
        foreach (GobboUnitSaveData unit in eligibleSuccessors)
        {
            if (unit == null) continue;
            if (best == null || GetSuccessorScore(unit) > GetSuccessorScore(best)) best = unit;
        }
        return best;
    }

    private int GetSuccessorScore(GobboUnitSaveData unit)
    {
        if (unit == null) return -999999;
        return unit.level * 10000 + unit.maxHealth * 100 + unit.attack * 25 + unit.defense * 10;
    }

    private void PromoteSuccessor(GobboUnitSaveData successor)
    {
        if (successor == null || GameState.Instance == null) return;

        successor.EnsureRuntimeDefaults();
        bool promoted = GameState.Instance.PromoteBuddyToLeader(successor.uniqueId);
        if (!promoted)
        {
            Log("Failed to promote successor id: " + successor.uniqueId + ". Trying camp choice fallback.");
            OpenGameOverPanel();
            return;
        }

        GameState.Instance.markedSuccessorId = "";

        CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.Instance;
        if (pref != null) pref.ClearSuccessor();

        PlayerDeathRunStore store = PlayerDeathRunStore.Instance;
        if (store != null) store.ClearPendingDeath();

        SporeSaveManager.SaveCurrentSlotFromGameState();

        Log("Promoted successor: " + successor.displayName + " / " + successor.uniqueId);
        HideAllPanels();

        Time.timeScale = 1f;
        CampSceneController controller = Object.FindAnyObjectByType<CampSceneController>(FindObjectsInactive.Include);
        if (controller != null) controller.RevealCampVisuals();
    }

    public void ReturnToMainMenu()
    {
        Log("Return to main menu clicked. Scene: " + mainMenuSceneName);
        Time.timeScale = 1f;
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName)) SceneManager.LoadScene(mainMenuSceneName);
    }

    private void HideAllPanels()
    {
        if (successionPanel != null) successionPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private string GetPath(Transform t)
    {
        if (t == null) return "null";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[CampSuccessionUI] " + message);
    }
}
