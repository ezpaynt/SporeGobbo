using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class CampSceneController : MonoBehaviour
{
    [Header("Run Stats Screen")]
    [FormerlySerializedAs("summaryPanel")] public GameObject runStatsPanel;
    [FormerlySerializedAs("summaryText")] public TMP_Text runStatsText;
    [FormerlySerializedAs("continueToCampButton")] public Button continueToSurvivorsButton;

    [Header("Survivors Screen")]
    [FormerlySerializedAs("campMenuPanel")] public GameObject survivorsPanel;
    public TMP_Text survivorsText;
    public Button continueToCampButton;

    [Header("Camp Buddy Evolution Panel")]
    [Tooltip("New camp-only panel. Do not use the old BuddyChoiceScreen for camp evolution.")] public GameObject campBuddyEvolutionPanel;
    public TMP_Text campBuddyEvolutionTitle;
    public Button campBuddyEvolutionBackButton;
    public Button[] campBuddyEvolutionButtons = new Button[3];
    public TMP_Text[] campBuddyEvolutionTexts = new TMP_Text[3];

    [Header("Optional Camp Management Screen Later")]
    public GameObject campMenuPanel;

    [Header("Playable Camp Spawn")]
    public CampPlayableSpawner campPlayableSpawner;
    public CampStartRoutineManager campStartRoutineManager;
    [Tooltip("Unused. Returning to camp should not heal; camp healing currently happens only through CampFireRecovery.")]
    public bool healAutomaticallyWhenCampOpens = false;
    public bool skipReportsAndOpenCampImmediatelyForTesting = false;

    [Header("Optional Future UI")]
    public TMP_Text playerStatsText;
    [Tooltip("Left list on SurvivorsPanel: buddies who went on the run.")]
    public Transform runBuddyListParent;
    [Tooltip("Optional legacy/fallback list parent. If runBuddyListParent is empty, this is used for the run squad list.")]
    public Transform activeSquadListParent;
    [Tooltip("Right list on SurvivorsPanel: buddies who stayed home at camp.")]
    public Transform reserveListParent;
    [Tooltip("Optional list for growth-ready buddies. Continue button still opens growth choices one at a time.")]
    public Transform pendingEvolutionListParent;
    public Button buddyButtonPrefab;

    [Header("Camp Pause")]
    public GameObject pauseMenu;
    public KeyCode pauseKey = KeyCode.Escape;

    private readonly List<BuddyTypeSetup> currentEvolutionChoices = new List<BuddyTypeSetup>();
    private readonly List<GobboCard> currentStatCardChoices = new List<GobboCard>();
    private string currentlyEvolvingBuddyId = "";

    private void Awake()
    {
        ValidateRequiredReferences();
    }

    private void Start()
    {
        EnsureGameState();
        HookButtons();

        if (TryStartDeathSuccessionFlow()) return;

        if (skipReportsAndOpenCampImmediatelyForTesting)
            RevealCampVisuals();
        else
            ShowRunStatsScreen();
    }

    private void Update()
    {
        // Safety: if a Resume button hides PauseMenu but forgets to unpause time,
        // immediately unfreeze the camp. This prevents the "resume once, then stuck" bug.
        if (Time.timeScale == 0f && pauseMenu != null && !pauseMenu.activeSelf)
            Time.timeScale = 1f;

        if (!Input.GetKeyDown(pauseKey)) return;

        // Do not steal Escape while report/evolution panels are open.
        if ((runStatsPanel != null && runStatsPanel.activeSelf) ||
            (survivorsPanel != null && survivorsPanel.activeSelf) ||
            (campBuddyEvolutionPanel != null && campBuddyEvolutionPanel.activeSelf))
            return;

        FindPauseMenuIfMissing();
        if (pauseMenu == null) return;

        bool open = !pauseMenu.activeSelf;
        pauseMenu.SetActive(open);
        Time.timeScale = open ? 0f : 1f;
    }

    public void ResumeCampFromPause()
    {
        FindPauseMenuIfMissing();
        if (pauseMenu != null) pauseMenu.SetActive(false);
        Time.timeScale = 1f;
    }

    void FindPauseMenuIfMissing()
    {
        if (pauseMenu != null) return;

        GameObject activeFound = GameObject.Find("PauseMenu");
        if (activeFound != null)
        {
            pauseMenu = activeFound;
            return;
        }

        // GameObject.Find cannot see inactive objects, so search loaded objects too.
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj != null && obj.name == "PauseMenu" && obj.scene.IsValid())
            {
                pauseMenu = obj;
                return;
            }
        }
    }

    bool TryStartDeathSuccessionFlow()
    {
        PlayerDeathRunStore pendingDeath = PlayerDeathRunStore.Instance;
        if (pendingDeath == null || !pendingDeath.playerDiedThisRun) return false;

        if (runStatsPanel != null) runStatsPanel.SetActive(false);
        if (survivorsPanel != null) survivorsPanel.SetActive(false);
        if (campMenuPanel != null) campMenuPanel.SetActive(false);
        if (campBuddyEvolutionPanel != null) campBuddyEvolutionPanel.SetActive(false);

        CampSuccessionUI successionUI = Object.FindAnyObjectByType<CampSuccessionUI>(FindObjectsInactive.Include);
        if (successionUI != null) successionUI.TryOpenDeathFlow();
        else Debug.LogWarning("Player died, but no CampSuccessionUI was found in CampScene.");
        return true;
    }

    void AutoFillMissingReferences()
    {
        if (campPlayableSpawner == null) campPlayableSpawner = Object.FindAnyObjectByType<CampPlayableSpawner>(FindObjectsInactive.Include);
        if (campStartRoutineManager == null) campStartRoutineManager = Object.FindAnyObjectByType<CampStartRoutineManager>(FindObjectsInactive.Include);
        FindPauseMenuIfMissing();
        if (runStatsText == null && runStatsPanel != null) runStatsText = runStatsPanel.GetComponentInChildren<TMP_Text>(true);
        if (survivorsText == null && survivorsPanel != null) survivorsText = survivorsPanel.GetComponentInChildren<TMP_Text>(true);
        if (continueToSurvivorsButton == null && runStatsPanel != null) continueToSurvivorsButton = runStatsPanel.GetComponentInChildren<Button>(true);
        if (continueToCampButton == null && survivorsPanel != null) continueToCampButton = survivorsPanel.GetComponentInChildren<Button>(true);
        AutoFillSurvivorListReferences();
        AutoFillCampBuddyEvolutionPanel();
    }

    void AutoFillSurvivorListReferences()
    {
        if (survivorsPanel == null) return;

        if (runBuddyListParent == null)
            runBuddyListParent = FindChildTransform(survivorsPanel.transform, "RunBuddyList", "CurrentRunList", "ActiveRunList", "ActiveSquadList");

        if (activeSquadListParent == null)
            activeSquadListParent = FindChildTransform(survivorsPanel.transform, "ActiveSquadList", "RunBuddyList", "CurrentRunList", "ActiveRunList");

        if (runBuddyListParent == null)
            runBuddyListParent = activeSquadListParent;

        if (reserveListParent == null)
            reserveListParent = FindChildTransform(survivorsPanel.transform, "ReserveList", "CampReserveList", "HomeBuddyList");

        if (pendingEvolutionListParent == null)
            pendingEvolutionListParent = FindChildTransform(survivorsPanel.transform, "PendingEvolutionList", "GrowthList", "ReadyToGrowList");
    }

    Transform FindChildTransform(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;
        foreach (string name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            Transform direct = root.Find(name);
            if (direct != null) return direct;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child == null) continue;
            foreach (string name in names)
            {
                if (!string.IsNullOrWhiteSpace(name) && child.name == name)
                    return child;
            }
        }
        return null;
    }

    void AutoFillCampBuddyEvolutionPanel()
    {
        if (campBuddyEvolutionPanel == null)
        {
            GameObject found = GameObject.Find("CampBuddyEvolutionPanel");
            if (found != null) campBuddyEvolutionPanel = found;
        }
        if (campBuddyEvolutionPanel == null) return;

        if (campBuddyEvolutionTitle == null)
        {
            Transform title = campBuddyEvolutionPanel.transform.Find("TitleText");
            if (title != null) campBuddyEvolutionTitle = title.GetComponent<TMP_Text>();
        }

        if (campBuddyEvolutionBackButton == null)
        {
            Transform back = FindChildTransform(campBuddyEvolutionPanel.transform, "BackButton", "Back", "CancelButton", "CloseButton");
            if (back != null) campBuddyEvolutionBackButton = back.GetComponent<Button>();
        }

        Button[] buttons = campBuddyEvolutionPanel.GetComponentsInChildren<Button>(true);
        int choiceIndex = 0;
        foreach (Button button in buttons)
        {
            if (button == null || !IsCampBuddyEvolutionChoiceButton(button)) continue;
            if (choiceIndex >= campBuddyEvolutionButtons.Length) break;
            if (campBuddyEvolutionButtons[choiceIndex] == null) campBuddyEvolutionButtons[choiceIndex] = button;
            if (campBuddyEvolutionTexts[choiceIndex] == null)
                campBuddyEvolutionTexts[choiceIndex] = button.GetComponentInChildren<TMP_Text>(true);
            choiceIndex++;
        }

        campBuddyEvolutionPanel.SetActive(false);
    }

    bool IsCampBuddyEvolutionChoiceButton(Button button)
    {
        if (button == null) return false;
        if (campBuddyEvolutionBackButton != null && button == campBuddyEvolutionBackButton) return false;
        string name = button.gameObject.name;
        if (name == "BackButton" || name == "Back" || name == "CancelButton" || name == "CloseButton") return false;
        return true;
    }

    void HookButtons()
    {
        if (continueToSurvivorsButton != null)
        {
            continueToSurvivorsButton.onClick.RemoveAllListeners();
            continueToSurvivorsButton.onClick.AddListener(ShowSurvivorsScreen);
        }
        if (continueToCampButton != null)
        {
            continueToCampButton.onClick.RemoveAllListeners();
            continueToCampButton.onClick.AddListener(RevealCampVisuals);
        }
        if (campBuddyEvolutionBackButton != null)
        {
            campBuddyEvolutionBackButton.onClick.RemoveAllListeners();
            campBuddyEvolutionBackButton.onClick.AddListener(CloseCampBuddyEvolutionPanel);
        }
    }

    void ShowRunStatsScreen()
    {
        if (runStatsPanel != null) runStatsPanel.SetActive(true);
        if (survivorsPanel != null) survivorsPanel.SetActive(false);
        if (campMenuPanel != null) campMenuPanel.SetActive(false);
        if (campBuddyEvolutionPanel != null) campBuddyEvolutionPanel.SetActive(false);
        FillRunStatsText();
    }

    public void ShowSurvivorsScreen()
    {
        if (runStatsPanel != null) runStatsPanel.SetActive(false);
        if (survivorsPanel != null) survivorsPanel.SetActive(true);
        if (campMenuPanel != null) campMenuPanel.SetActive(false);
        if (campBuddyEvolutionPanel != null) campBuddyEvolutionPanel.SetActive(false);
        RefreshSurvivorsScreen();
    }

    public void RefreshSurvivorsScreen()
    {
        FillSurvivorsText();
        RefreshContinueButtonState();
    }

    void TryRevealCampVisuals()
    {
        RevealCampVisuals();
    }

    public void RevealCampVisuals()
    {
        if (runStatsPanel != null) runStatsPanel.SetActive(false);
        if (survivorsPanel != null) survivorsPanel.SetActive(false);
        if (campMenuPanel != null) campMenuPanel.SetActive(false);
        if (campBuddyEvolutionPanel != null) campBuddyEvolutionPanel.SetActive(false);

        if (campPlayableSpawner != null)
        {
            campPlayableSpawner.SpawnPlayableCamp();
            BeginCampStartRoutineIfPresent();
            return;
        }

        Debug.LogWarning("CampSceneController cannot open playable camp because CampPlayableSpawner is not assigned.");
    }

    void BeginCampStartRoutineIfPresent()
    {
        if (campStartRoutineManager != null) campStartRoutineManager.BeginCampVisit();
    }

    void FillRunStatsText()
    {
        if (runStatsText == null || GameState.Instance == null) return;
        RunSummaryData run = GameState.Instance.lastRun;
        GobboUnitSaveData leader = GameState.Instance.GetLeader();
        runStatsText.text = CampReportTextBuilder.BuildRunStatsText(run, leader);
    }

    void FillSurvivorsText()
    {
        if (GameState.Instance == null) return;
        RunSummaryData run = GameState.Instance.lastRun;
        if (run == null) return;

        List<GobboUnitSaveData> pending = GetPendingBuddyEvolutions();

        if (survivorsText != null)
            survivorsText.text = BuildMiddleSurvivorSummary(run, pending);

        FillRunBuddyList(run);
        FillCampReserveList(run);
        FillPendingEvolutionList(pending);
    }

    string BuildMiddleSurvivorSummary(RunSummaryData run, List<GobboUnitSaveData> pending)
    {
        int total = GameState.Instance.ownedGobbos != null ? GameState.Instance.ownedGobbos.Count : 0;
        return CampReportTextBuilder.BuildMiddleSurvivorSummary(run, pending, total);
    }

    void FillRunBuddyList(RunSummaryData run)
    {
        Transform parent = runBuddyListParent != null ? runBuddyListParent : activeSquadListParent;
        if (parent == null) return;

        ClearListParent(parent);
        AddListText(parent, "RUN SQUAD", true);

        if (run.activeBuddyReports == null || run.activeBuddyReports.Count == 0)
        {
            AddListText(parent, "Nobody came on this run.", false);
            return;
        }

        foreach (BuddyRunReport report in run.activeBuddyReports)
            AddListText(parent, FormatRunBuddyReport(report), false);
    }

    void FillCampReserveList(RunSummaryData run)
    {
        if (reserveListParent == null) return;

        ClearListParent(reserveListParent);
        AddListText(reserveListParent, "CAMP BUDDIES", true);

        if (run.reserveBuddyReports == null || run.reserveBuddyReports.Count == 0)
        {
            AddListText(reserveListParent, "Nobody stayed home.", false);
            return;
        }

        foreach (BuddyRunReport report in run.reserveBuddyReports)
            AddListText(reserveListParent, FormatCampBuddyReport(report), false);
    }

    void FillPendingEvolutionList(List<GobboUnitSaveData> pending)
    {
        CampPendingGrowthListPresenter.Show(pendingEvolutionListParent, pending, OpenBuddyGrowthChoice);
    }

    string FormatRunBuddyReport(BuddyRunReport report)
    {
        return CampReportTextBuilder.FormatRunBuddyReport(report);
    }

    string FormatCampBuddyReport(BuddyRunReport report)
    {
        return CampReportTextBuilder.FormatCampBuddyReport(report);
    }

    string GetBuddyLine(GobboUnitSaveData buddy)
    {
        return CampReportTextBuilder.FormatBuddyLine(buddy);
    }

    void ClearListParent(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    TMP_Text AddListText(Transform parent, string text, bool header)
    {
        if (parent == null) return null;

        GameObject item = new GameObject(header ? "ListHeader" : "ListItem", typeof(RectTransform));
        item.transform.SetParent(parent, false);
        TMP_Text label = item.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = header ? 24 : 18;
        label.fontStyle = header ? FontStyles.Bold : FontStyles.Normal;
        label.color = Color.white;
        label.enableWordWrapping = true;
        label.raycastTarget = false;

        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, header ? 34f : 72f);
        return label;
    }

    public void OpenBuddyGrowthChoice(string buddyId)
    {
        Debug.Log("OpenBuddyGrowthChoice called for id: " + buddyId);
        if (string.IsNullOrWhiteSpace(buddyId))
        {
            Debug.LogWarning("Tried to open buddy growth choice with no buddy id.");
            return;
        }

        GobboUnitSaveData buddy = GameState.Instance != null ? GameState.Instance.FindOwnedGobbo(buddyId) : null;
        if (buddy == null)
        {
            Debug.LogWarning("Could not find buddy for growth id: " + buddyId);
            RefreshSurvivorsScreen();
            return;
        }

        buddy.EnsureRuntimeDefaults();
        currentlyEvolvingBuddyId = buddy.uniqueId;

        BuddyGrowthChoiceType growthType = BuddyGrowthService.GetPendingGrowthChoiceType(buddy);
        switch (growthType)
        {
            case BuddyGrowthChoiceType.Evolution:
                OpenCampBuddyEvolutionPanel(buddy);
                return;
            case BuddyGrowthChoiceType.StatCard:
                OpenCampBuddyStatCardPanel(buddy);
                return;
            case BuddyGrowthChoiceType.Trait:
            case BuddyGrowthChoiceType.Mutation:
                Debug.LogWarning("No camp UI exists yet for buddy growth type: " + growthType);
                currentlyEvolvingBuddyId = "";
                RefreshSurvivorsScreen();
                return;
            default:
                Debug.LogWarning("No pending camp growth found for: " + buddy.displayName);
                currentlyEvolvingBuddyId = "";
                RefreshSurvivorsScreen();
                return;
        }
    }

    void OpenCampBuddyEvolutionPanel(GobboUnitSaveData buddy)
    {
        if (campBuddyEvolutionPanel == null)
        {
            Debug.LogError("No CampBuddyEvolutionPanel assigned/found. Add it under Canvas and assign it on CampSceneController.");
            return;
        }

        BuildCampEvolutionChoices(buddy);
        if (survivorsPanel != null) survivorsPanel.SetActive(false);
        if (runStatsPanel != null) runStatsPanel.SetActive(false);
        if (campMenuPanel != null) campMenuPanel.SetActive(false);

        CampBuddyGrowthChoicePresenter.Show(
            campBuddyEvolutionPanel,
            campBuddyEvolutionTitle,
            campBuddyEvolutionButtons,
            campBuddyEvolutionTexts,
            buddy,
            currentEvolutionChoices,
            ChooseCampEvolution);

        if (campBuddyEvolutionBackButton != null) campBuddyEvolutionBackButton.gameObject.SetActive(true);

        Debug.Log("Opened CampBuddyEvolutionPanel with " + currentEvolutionChoices.Count + " choices for " + buddy.displayName);
    }

    void OpenCampBuddyStatCardPanel(GobboUnitSaveData buddy)
    {
        if (campBuddyEvolutionPanel == null)
        {
            Debug.LogError("No CampBuddyEvolutionPanel assigned/found. Reuse it for buddy stat cards and assign it on CampSceneController.");
            return;
        }

        BuildCampStatCardChoices(buddy);
        if (currentStatCardChoices.Count == 0)
        {
            Debug.LogWarning("No buddy stat card choices available for " + buddy.displayName);
            currentlyEvolvingBuddyId = "";
            RefreshSurvivorsScreen();
            return;
        }

        if (survivorsPanel != null) survivorsPanel.SetActive(false);
        if (runStatsPanel != null) runStatsPanel.SetActive(false);
        if (campMenuPanel != null) campMenuPanel.SetActive(false);

        CampBuddyStatCardChoicePresenter.Show(
            campBuddyEvolutionPanel,
            campBuddyEvolutionTitle,
            campBuddyEvolutionButtons,
            campBuddyEvolutionTexts,
            buddy,
            currentStatCardChoices,
            ChooseCampStatCard);

        if (campBuddyEvolutionBackButton != null) campBuddyEvolutionBackButton.gameObject.SetActive(true);

        Debug.Log("Opened buddy stat card panel with " + currentStatCardChoices.Count + " choices for " + buddy.displayName);
    }

    void BuildCampEvolutionChoices(GobboUnitSaveData buddy)
    {
        currentEvolutionChoices.Clear();
        BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>(FindObjectsInactive.Include);
        List<BuddyTypeSetup> choices = roster != null ? roster.GetRandomBuddyChoices(3) : GetFallbackEvolutionChoices(3);
        foreach (BuddyTypeSetup setup in choices)
        {
            if (setup != null && setup.buddyType != BuddyType.Baby) currentEvolutionChoices.Add(setup);
        }
    }

    void BuildCampStatCardChoices(GobboUnitSaveData buddy)
    {
        currentStatCardChoices.Clear();
        List<GobboCard> choices = BuddyProgression.GetStatCardChoices(buddy, 3);
        foreach (GobboCard card in choices)
        {
            if (card != null) currentStatCardChoices.Add(card);
        }
    }

    void ChooseCampEvolution(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= currentEvolutionChoices.Count) return;
        GobboUnitSaveData buddy = GameState.Instance != null ? GameState.Instance.FindOwnedGobbo(currentlyEvolvingBuddyId) : null;
        if (buddy == null)
        {
            Debug.LogWarning("Could not find currently evolving buddy: " + currentlyEvolvingBuddyId);
            CloseCampBuddyEvolutionPanel();
            RefreshSurvivorsScreen();
            return;
        }

        BuddyTypeSetup choice = currentEvolutionChoices[choiceIndex];
        BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>(FindObjectsInactive.Include);
        Debug.Log("Camp evolving " + buddy.displayName + " into " + choice.buddyType);
        BuddyProgression.ApplyEvolutionChoice(buddy, choice.buddyType, roster);
        ApplySetupDirectlyIfNeeded(buddy, choice, roster);
        SporeSaveManager.SaveCurrentSlotFromGameState();
        CloseCampBuddyEvolutionPanel();
        RefreshSurvivorsScreen();
    }

    void ChooseCampStatCard(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= currentStatCardChoices.Count) return;
        GobboUnitSaveData buddy = GameState.Instance != null ? GameState.Instance.FindOwnedGobbo(currentlyEvolvingBuddyId) : null;
        if (buddy == null)
        {
            Debug.LogWarning("Could not find currently growing buddy: " + currentlyEvolvingBuddyId);
            CloseCampBuddyEvolutionPanel();
            RefreshSurvivorsScreen();
            return;
        }

        GobboCard choice = currentStatCardChoices[choiceIndex];
        Debug.Log("Camp applying stat card " + choice.cardName + " to " + buddy.displayName);
        if (BuddyProgression.ApplyStatCardChoice(buddy, choice))
            SporeSaveManager.SaveCurrentSlotFromGameState();

        CloseCampBuddyEvolutionPanel();
        RefreshSurvivorsScreen();
    }

    void ApplySetupDirectlyIfNeeded(GobboUnitSaveData buddy, BuddyTypeSetup setup, BuddyRoster roster)
    {
        if (buddy == null || setup == null || roster != null) return;
        buddy.maxHealth = setup.maxHealth;
        buddy.health = Mathf.Min(Mathf.Max(1, buddy.health), buddy.maxHealth);
        buddy.damage = setup.damage;
        buddy.attack = setup.damage;
        buddy.defense = setup.defense;
        buddy.moveSpeed = setup.moveSpeed;
        buddy.attackCooldown = setup.attackCooldown;
        buddy.onlyFightsAfterHit = setup.onlyFightsAfterHit;
        buddy.collectsFood = setup.collectsFood;
        buddy.bodyColor = setup.bodyColor;
        buddy.visualSetId = setup.buddyType.ToString().ToLowerInvariant() + "_young";
    }

    void CloseCampBuddyEvolutionPanel()
    {
        currentlyEvolvingBuddyId = "";
        currentEvolutionChoices.Clear();
        currentStatCardChoices.Clear();
        CampBuddyGrowthChoicePresenter.Hide(campBuddyEvolutionPanel, campBuddyEvolutionButtons, campBuddyEvolutionTexts);
        CampBuddyStatCardChoicePresenter.Hide(campBuddyEvolutionPanel, campBuddyEvolutionButtons, campBuddyEvolutionTexts);
        if (campBuddyEvolutionBackButton != null) campBuddyEvolutionBackButton.gameObject.SetActive(true);
        if (survivorsPanel != null) survivorsPanel.SetActive(true);
    }

    List<BuddyTypeSetup> GetFallbackEvolutionChoices(int amount)
    {
        List<BuddyTypeSetup> all = new List<BuddyTypeSetup>
        {
            MakeSetup(BuddyType.Fast, "Fast Gobbo", 5, 1, 0, 5.2f, 0.65f, false, false, new Color(0.35f, 0.9f, 0.9f)),
            MakeSetup(BuddyType.Fat, "Fat Gobbo", 10, 1, 1, 2.8f, 0.9f, false, false, new Color(0.45f, 0.9f, 0.35f)),
            MakeSetup(BuddyType.Scavenger, "Scavenger Gobbo", 6, 1, 0, 3.8f, 0.8f, true, true, new Color(0.9f, 0.75f, 0.25f)),
            MakeSetup(BuddyType.Tank, "Tank Gobbo", 14, 1, 2, 2.5f, 1.0f, false, false, new Color(0.35f, 0.75f, 0.25f)),
            MakeSetup(BuddyType.Thrower, "Thrower Gobbo", 7, 1, 0, 3.2f, 0.9f, false, false, new Color(0.75f, 0.85f, 0.25f)),
            MakeSetup(BuddyType.Strong, "Strong Gobbo", 9, 2, 0, 3.1f, 0.95f, false, false, new Color(0.65f, 0.9f, 0.3f)),
            MakeSetup(BuddyType.Fungal, "Fungal Gobbo", 8, 1, 0, 3.0f, 1.0f, false, false, new Color(0.55f, 1f, 0.45f)),
            MakeSetup(BuddyType.Explosive, "Explosive Gobbo", 7, 2, 0, 3.4f, 1.05f, false, false, new Color(1f, 0.65f, 0.25f))
        };

        List<BuddyTypeSetup> choices = new List<BuddyTypeSetup>();
        while (choices.Count < amount && all.Count > 0)
        {
            int index = Random.Range(0, all.Count);
            choices.Add(all[index]);
            all.RemoveAt(index);
        }
        return choices;
    }

    BuddyTypeSetup MakeSetup(BuddyType type, string displayName, int hp, int damage, int defense, float speed, float cooldown, bool onlyAfterHit, bool collectsFood, Color color)
    {
        return new BuddyTypeSetup
        {
            buddyType = type,
            displayName = displayName,
            maxHealth = hp,
            damage = damage,
            defense = defense,
            moveSpeed = speed,
            attackCooldown = cooldown,
            onlyFightsAfterHit = onlyAfterHit,
            collectsFood = collectsFood,
            bodyColor = color,
            defaultVisualSetId = type.ToString().ToLowerInvariant() + "_young"
        };
    }

    void RefreshContinueButtonState()
    {
        if (continueToCampButton == null) return;
        continueToCampButton.interactable = true;
        TMP_Text label = continueToCampButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Continue";
    }

    bool HasPendingBuddyEvolutions() => BuddyGrowthService.HasPendingGrowth(GameState.Instance);

    List<GobboUnitSaveData> GetPendingBuddyEvolutions()
    {
        return BuddyGrowthService.GetPendingGrowthBuddies(GameState.Instance);
    }

    void EnsureGameState()
    {
        if (GameState.Instance != null) return;
        Debug.LogWarning("CampSceneController did not find GameState. Add a GameState object to the scene or enter CampScene through the normal game flow.");
    }

    void ValidateRequiredReferences()
    {
        if (campPlayableSpawner == null)
            Debug.LogWarning("CampSceneController missing CampPlayableSpawner reference.");

        if (runStatsPanel == null)
            Debug.LogWarning("CampSceneController missing Run Stats Panel reference.");

        if (survivorsPanel == null)
            Debug.LogWarning("CampSceneController missing Survivors Panel reference.");

        if (campStartRoutineManager == null)
            Debug.LogWarning("CampSceneController missing CampStartRoutineManager reference. Camp will still open, but arrival routine will not run.");
    }
}
