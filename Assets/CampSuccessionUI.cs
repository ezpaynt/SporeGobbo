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
    [TextArea(2, 5)] public string noMarkedSuccessorBody = "No gobbo was marked as successor.\nThe camp has to choose.";
    [TextArea(2, 5)] public string gameOverMessage = "GAME OVER\n\nNo gobbos are left to remember the camp.";

    [Header("Click Hardening")]
    public bool attachDirectClickRelays = false;
    public bool hideOtherCampPanelsWhenOpen = true;

    [Tooltip("Optional blockers to force off when death/game-over opens. If empty, common panel names are auto-hidden under the same Canvas.")]
    public List<GameObject> panelsToHideWhenOpen = new List<GameObject>();

    [Header("Debug")]
    public bool logDebugMessages = false;

    private GobboUnitSaveData markedSuccessor;
    private readonly List<GobboUnitSaveData> eligibleSuccessors = new List<GobboUnitSaveData>();
    private bool resolvingSuccession = false;
    private GobboUnitSaveData pendingPromotion; // UI retry identity; GameState remains the only roster authority.

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
            BindOwnedButton(acceptMarkedSuccessorButton,AcceptMarkedSuccessor,GameSceneCatalog.DevelopmentCandidate);
            AttachRelay(acceptMarkedSuccessorButton, DeathButtonClickRelay.DeathButtonAction.AcceptMarkedSuccessor);
            Log("Hooked accept button: " + GetPath(acceptMarkedSuccessorButton.transform));
        }
        else Log("WARNING: acceptMarkedSuccessorButton is not assigned.");

        if (letCampChooseButton != null)
        {
            PrepareButton(letCampChooseButton, "LetCampChooseButton", "Let Camp\nChoose");
            BindOwnedButton(letCampChooseButton,LetCampChoose,GameSceneCatalog.DevelopmentCandidate);
            AttachRelay(letCampChooseButton, DeathButtonClickRelay.DeathButtonAction.LetCampChoose);
            Log("Hooked choose button: " + GetPath(letCampChooseButton.transform));
        }
        else Log("WARNING: letCampChooseButton is not assigned.");

        if (returnToMainMenuButton != null)
        {
            PrepareButton(returnToMainMenuButton, "ReturnToMainMenuButton", "Return To\nMain Menu");
            BindOwnedButton(returnToMainMenuButton,ReturnToMainMenu,GameSceneCatalog.DevelopmentCandidate);
            AttachRelay(returnToMainMenuButton, DeathButtonClickRelay.DeathButtonAction.ReturnToMainMenu);
            Log("Hooked return button: " + GetPath(returnToMainMenuButton.transform));
        }
    }

    private void AttachRelay(Button button, DeathButtonClickRelay.DeathButtonAction action)
    {
        if (GameSceneCatalog.DevelopmentCandidate)
        {
            var existing = button != null ? button.GetComponent<DeathButtonClickRelay>() : null;
            if (existing != null) existing.Configure(this,action,false,false);
            return;
        }
        if (!attachDirectClickRelays || button == null) return;

        DeathButtonClickRelay relay = button.GetComponent<DeathButtonClickRelay>();
        if (relay == null) relay = button.gameObject.AddComponent<DeathButtonClickRelay>();
        relay.Configure(this, action);
        relay.logDebugMessages = logDebugMessages;
    }

    private void PrepareButton(Button button, string objectName, string label)
    {
        if (button == null) return;
        if (GameSceneCatalog.DevelopmentCandidate) button.enabled = true;

        button.name = objectName;
        // Do not force SetActive(true) here. OpenSuccessionPanel controls which buttons should be visible.
        button.interactable = true;
        button.transform.SetAsLastSibling();

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            button.targetGraphic = image;
        }

        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = label;
            tmpText.raycastTarget = false;
            if (GameSceneCatalog.DevelopmentCandidate)
            {
                tmpText.alignment=TextAlignmentOptions.Center;tmpText.margin=Vector4.zero;tmpText.enableAutoSizing=true;tmpText.fontSizeMin=22;tmpText.fontSizeMax=28;
                var textRect=tmpText.rectTransform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=new Vector2(12,8);textRect.offsetMax=new Vector2(-12,-8);
            }
        }

        Text oldText = button.GetComponentInChildren<Text>(true);
        if (oldText != null)
        {
            oldText.text = label;
            oldText.raycastTarget = false;
            if(GameSceneCatalog.DevelopmentCandidate){oldText.alignment=TextAnchor.MiddleCenter;oldText.resizeTextForBestFit=true;oldText.resizeTextMinSize=22;oldText.resizeTextMaxSize=28;}
        }
        if(GameSceneCatalog.DevelopmentCandidate)
        {
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(360,100);
            float x=markedSuccessor==null?0:button==acceptMarkedSuccessorButton?-230:button==letCampChooseButton?230:0;rect.anchoredPosition=new Vector2(x,130);
        }
    }

    // RemoveAllListeners leaves serialized listeners in place. The candidate owns
    // these three inspected buttons and binds exactly one command per activation.
    public static void BindOwnedButton(Button button,UnityEngine.Events.UnityAction command,bool replaceSerialized)
    {
        if (replaceSerialized) button.onClick = new Button.ButtonClickedEvent();
        else button.onClick.RemoveAllListeners();
        button.onClick.AddListener(command);
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
        resolvingSuccession = false;
        HideBlockingPanelsExcept(successionPanel);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (successionPanel != null)
        {
            successionPanel.SetActive(true);
            successionPanel.transform.SetAsLastSibling();
            EnsurePanelInteractive(successionPanel);
        }

        if (titleText != null) titleText.text = title;
        if (bodyText != null)
            bodyText.text = markedSuccessor != null ? string.Format(markedSuccessorBody, markedSuccessor.displayName) : noMarkedSuccessorBody;

        if (acceptMarkedSuccessorButton != null) acceptMarkedSuccessorButton.gameObject.SetActive(markedSuccessor != null);
        if (letCampChooseButton != null) letCampChooseButton.gameObject.SetActive(true);

        HookButtons();
        if(GameSceneCatalog.DevelopmentCandidate)
        {
            if(titleText!=null){var rect=titleText.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-65);rect.sizeDelta=new Vector2(1300,100);titleText.alignment=TextAlignmentOptions.Center;titleText.margin=Vector4.zero;titleText.enableAutoSizing=false;titleText.fontSize=42;}
            if(bodyText!=null){var rect=bodyText.rectTransform;rect.anchorMin=new Vector2(.08f,.30f);rect.anchorMax=new Vector2(.92f,.78f);rect.offsetMin=rect.offsetMax=Vector2.zero;bodyText.alignment=TextAlignmentOptions.TopLeft;bodyText.margin=Vector4.zero;bodyText.fontSize=32;bodyText.enableAutoSizing=false;}
        }
        SporeUiCoordinator.Instance.PushModal(this, null, false,
            letCampChooseButton != null ? letCampChooseButton : acceptMarkedSuccessorButton);
    }

    private void OpenGameOverPanel()
    {
        HideBlockingPanelsExcept(gameOverPanel);

        if (successionPanel != null) successionPanel.SetActive(false);
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            EnsurePanelInteractive(gameOverPanel);
        }

        if (gameOverText != null) gameOverText.text = gameOverMessage;

        HookButtons();
        SporeUiCoordinator.Instance.PushModal(this, null, false, returnToMainMenuButton);
    }

    private void HideBlockingPanelsExcept(GameObject panelToKeep)
    {
        if (!hideOtherCampPanelsWhenOpen) return;

        foreach (GameObject panel in panelsToHideWhenOpen)
        {
            if (panel == null || panel == panelToKeep) continue;
            panel.SetActive(false);
        }

        Canvas canvas = null;
        if (panelToKeep != null) canvas = panelToKeep.GetComponentInParent<Canvas>(true);
        if (canvas == null && successionPanel != null) canvas = successionPanel.GetComponentInParent<Canvas>(true);
        if (canvas == null) return;

        string[] commonBlockers =
        {
            "PausePanel", "PortalPanel", "CampFireMenuPanel", "CampMenuPanel",
            "SquadSelectPanel", "OldBonesPanel", "SurvivorsPanel", "RunStatsPanel",
            "CampBuddyEvolutionPanel", "GameOverPanel", "DeathPanel"
        };

        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child.gameObject == panelToKeep) continue;

            foreach (string blockerName in commonBlockers)
            {
                if (child.name == blockerName)
                {
                    child.gameObject.SetActive(false);
                    break;
                }
            }
        }
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
        if (pendingPromotion != null) { FinishDevelopmentPromotion(); return; }
        if (resolvingSuccession) { Log("Accept ignored because succession is already resolving."); return; }
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
        if (pendingPromotion != null) { FinishDevelopmentPromotion(); return; }
        if (resolvingSuccession) { Log("Let Camp Choose ignored because succession is already resolving."); return; }
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
        resolvingSuccession = true;

        successor.EnsureRuntimeDefaults();
        bool promoted = GameState.Instance.PromoteBuddyToLeader(successor.uniqueId);
        if (!promoted)
        {
            Log("Failed to promote successor id: " + successor.uniqueId + ". Trying game over fallback.");
            resolvingSuccession = false;
            OpenGameOverPanel();
            return;
        }

        GameState.Instance.markedSuccessorId = "";
        GameState.Instance.lineageEnded = false;

        if (GameSceneCatalog.DevelopmentCandidate)
        {
            pendingPromotion = successor;
            CampSuccessorPreferenceStore.Instance?.ClearSuccessor(false);
            FinishDevelopmentPromotion();
            return;
        }

        CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.Instance;
        if (pref != null) pref.ClearSuccessor();

        PlayerDeathRunStore store = PlayerDeathRunStore.Instance;
        if (store != null) store.ClearPendingDeath();

        SporeSaveManager.SaveCurrentSlotFromGameState();

        Log("Promoted successor: " + successor.displayName + " / " + successor.uniqueId);

        HideAllPanels();
        SporePauseService.ResetAll();

        CampSceneController controller = Object.FindAnyObjectByType<CampSceneController>(FindObjectsInactive.Include);
        if (controller != null) controller.RevealCampVisuals();
    }

    public void ReturnToMainMenu()
    {
        Log("Return to main menu clicked. Scene: " + mainMenuSceneName);
        CampArrivalContext.Clear();
        if (!GameSceneCatalog.DevelopmentCandidate) PlayerDeathRunStore.Instance?.ClearPendingDeath();
        SporePauseService.ResetAll();
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    void FinishDevelopmentPromotion()
    {
        if (pendingPromotion == null) return;
        resolvingSuccession = true;
        try { SporeSaveManager.CommitResolvedDevelopmentSuccession(); }
        catch (System.Exception error)
        {
            resolvingSuccession = false;
            if (bodyText != null) bodyText.text = "The successor was chosen, but saving failed.\nChoose again to retry saving. Quitting returns to the last committed Camp.\n" + error.Message;
            Debug.LogWarning("[Development succession] Save failed; last committed Camp preserved. Retry remains available: " + error.Message);
            return;
        }
        PlayerDeathRunStore.Instance?.ClearPendingDeath();
        pendingPromotion = null;
        HideAllPanels(); SporePauseService.ResetAll();
        Object.FindAnyObjectByType<CampSceneController>(FindObjectsInactive.Include)?.RevealCampVisuals();
    }

    private void HideAllPanels()
    {
        if (successionPanel != null) successionPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (SporeUiCoordinator.Instance != null) SporeUiCoordinator.Instance.PopModal(this);
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
