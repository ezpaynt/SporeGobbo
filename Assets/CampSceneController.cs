using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

/// <summary>
/// Owns only the camp arrival/report flow.
/// It does not spawn fallback objects, build UI, or repair scene setup.
/// Required scene references should be assigned in the Inspector.
/// </summary>
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
    [Tooltip("Camp-only panel. Do not use the old BuddyChoiceScreen for camp evolution.")]
    public GameObject campBuddyEvolutionPanel;
    public TMP_Text campBuddyEvolutionTitle;
    public Button[] campBuddyEvolutionButtons = new Button[3];
    public TMP_Text[] campBuddyEvolutionTexts = new TMP_Text[3];

    [Header("Optional Camp Management Screen Later")]
    public GameObject campMenuPanel;

    [Header("Required Camp Flow References")]
    public CampPlayableSpawner campPlayableSpawner;
    public CampStartRoutineManager campStartRoutineManager;
    public CampSuccessionUI campSuccessionUI;

    [Header("Camp Arrival Options")]
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

    [Header("Debug")]
    public bool logMissingReferences = true;

    private readonly List<BuddyTypeSetup> currentEvolutionChoices = new List<BuddyTypeSetup>();
    private string currentlyEvolvingBuddyId = "";

    void Awake()
    {
        ValidateRequiredReferences();
        HideAllCampFlowPanels();
    }

    void Start()
    {
        HookButtons();

        if (TryStartDeathSuccessionFlow())
            return;

        if (skipReportsAndOpenCampImmediatelyForTesting)
            RevealCampVisuals();
        else
            ShowRunStatsScreen();
    }

    void Update()
    {
        if (Time.timeScale == 0f && pauseMenu != null && !pauseMenu.activeSelf)
            Time.timeScale = 1f;

        if (!Input.GetKeyDown(pauseKey))
            return;

        if (IsReportOrEvolutionPanelOpen())
            return;

        if (pauseMenu == null)
            return;

        bool open = !pauseMenu.activeSelf;
        pauseMenu.SetActive(open);
        Time.timeScale = open ? 0f : 1f;
    }

    public void ResumeCampFromPause()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        Time.timeScale = 1f;
    }

    bool IsReportOrEvolutionPanelOpen()
    {
        return (runStatsPanel != null && runStatsPanel.activeSelf) ||
               (survivorsPanel != null && survivorsPanel.activeSelf) ||
               (campBuddyEvolutionPanel != null && campBuddyEvolutionPanel.activeSelf);
    }

    bool TryStartDeathSuccessionFlow()
    {
        PlayerDeathRunStore pendingDeath = PlayerDeathRunStore.Instance;
        if (pendingDeath == null || !pendingDeath.playerDiedThisRun)
            return false;

        HideAllCampFlowPanels();

        if (campSuccessionUI != null)
        {
            campSuccessionUI.TryOpenDeathFlow();
        }
        else
        {
            Debug.LogWarning("Player died, but CampSceneController has no CampSuccessionUI assigned.");
        }

        return true;
    }

    void ValidateRequiredReferences()
    {
        if (!logMissingReferences)
            return;

        if (continueToSurvivorsButton == null) Debug.LogWarning("CampSceneController missing Continue To Survivors Button.");
        if (continueToCampButton == null) Debug.LogWarning("CampSceneController missing Continue To Camp Button.");
        if (campPlayableSpawner == null) Debug.LogWarning("CampSceneController missing CampPlayableSpawner. Camp will not spawn.");
        if (campStartRoutineManager == null) Debug.LogWarning("CampSceneController missing CampStartRoutineManager. Camp arrival routine will be skipped.");
        if (campSuccessionUI == null) Debug.LogWarning("CampSceneController missing CampSuccessionUI. Player death succession flow will not open.");
        if (campBuddyEvolutionPanel != null)
        {
            if (campBuddyEvolutionButtons == null || campBuddyEvolutionButtons.Length == 0)
                Debug.LogWarning("CampSceneController has CampBuddyEvolutionPanel but no evolution buttons assigned.");
        }
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
            continueToCampButton.onClick.AddListener(TryRevealCampVisuals);
        }
    }

    void HideAllCampFlowPanels()
    {
        if (runStatsPanel != null) runStatsPanel.SetActive(false);
        if (survivorsPanel != null) survivorsPanel.SetActive(false);
        if (campMenuPanel != null) campMenuPanel.SetActive(false);
        if (campBuddyEvolutionPanel != null) campBuddyEvolutionPanel.SetActive(false);
    }

    void ShowRunStatsScreen()
    {
        HideAllCampFlowPanels();

        if (GameState.Instance == null)
        {
            Debug.LogWarning("CampSceneController could not show run stats because no GameState exists.");
            RevealCampVisuals();
            return;
        }

        if (runStatsPanel != null)
            runStatsPanel.SetActive(true);

        FillRunStatsText();
    }

    public void ShowSurvivorsScreen()
    {
        HideAllCampFlowPanels();

        if (survivorsPanel != null)
            survivorsPanel.SetActive(true);

        RefreshSurvivorsScreen();
    }

    public void RefreshSurvivorsScreen()
    {
        FillSurvivorsText();
        RefreshContinueButtonState();
    }

    void TryRevealCampVisuals()
    {
        List<GobboUnitSaveData> pending = GetPendingBuddyEvolutions();
        if (pending.Count > 0)
        {
            OpenBuddyGrowthChoice(pending[0].uniqueId);
            return;
        }

        RevealCampVisuals();
    }

    public void RevealCampVisuals()
    {
        if (healAutomaticallyWhenCampOpens)
        {
            HealPlayerForCamp();
            HealAllBuddiesForCamp();
        }

        HideAllCampFlowPanels();

        if (campPlayableSpawner == null)
        {
            Debug.LogWarning("CampSceneController cannot spawn camp: CampPlayableSpawner is not assigned.");
            return;
        }

        campPlayableSpawner.SpawnPlayableCamp();
        BeginCampStartRoutineIfPresent();
    }

    void BeginCampStartRoutineIfPresent()
    {
        if (campStartRoutineManager != null)
            campStartRoutineManager.BeginCampVisit();
    }

    void HealAllBuddiesForCamp()
    {
        GameState state = GameState.Instance;
        if (state == null || state.ownedGobbos == null) return;

        foreach (GobboUnitSaveData buddy in state.ownedGobbos)
        {
            if (buddy == null) continue;
            buddy.EnsureRuntimeDefaults();
            buddy.health = buddy.maxHealth;
            buddy.hasBeenHit = false;
        }
    }

    void HealPlayerForCamp()
    {
        if (GameState.Instance == null) return;
        GobboUnitSaveData leader = GameState.Instance.GetLeader();
        if (leader == null) return;
        leader.health = leader.maxHealth;
    }

    void FillRunStatsText()
    {
        if (runStatsText == null || GameState.Instance == null) return;

        RunSummaryData run = GameState.Instance.lastRun;
        GobboUnitSaveData leader = GameState.Instance.GetLeader();
        if (run == null || leader == null)
        {
            runStatsText.text = "Welcome back to camp.";
            return;
        }

        runStatsText.text = "You made it back to camp!" +
            "\n\nRun: " + run.runNumber +
            "\nLevel: " + run.playerLevelStart + " → " + run.playerLevelEnd +
            "\nXP gained: " + run.xpGained +
            "\nHealth: " + leader.health + " / " + leader.maxHealth +
            "\nAttack: " + leader.attack +
            "\nDefense: " + leader.defense +
            "\nDig Power: " + leader.digPower +
            "\nDig Radius: " + leader.digRadius.ToString("0.00") +
            "\n\nFood for the horde: " + run.foodValueGained +
            "\nSpores gained: " + run.sporesGained + " Total: " + leader.spores +
            "\nMushrooms gained: " + run.mushroomsGained + " Total: " + leader.mushrooms +
            "\nShinies gained: " + run.shiniesGained + " Total: " + leader.shinies +
            "\nEnemies killed: " + run.enemiesKilled;
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
        string text = "Roll call";

        text += "\n\nNew buddies: " + run.buddiesFound;
        if (run.newBuddyNames != null && run.newBuddyNames.Count > 0)
        {
            foreach (string name in run.newBuddyNames)
                text += "\n+ " + name;
        }
        else text += "\n- Nobody new joined.";

        text += "\n\nBuddies lost: " + run.buddiesLost;
        if (run.deadBuddyNames != null && run.deadBuddyNames.Count > 0)
        {
            foreach (string name in run.deadBuddyNames)
                text += "\n- " + name;
        }
        else text += "\n- Nobody died.";

        text += "\n\nLeveled up:";
        if (run.leveledBuddyNames != null && run.leveledBuddyNames.Count > 0)
        {
            foreach (string name in run.leveledBuddyNames)
                text += "\n↑ " + name;
        }
        else text += "\n- Nobody leveled this time.";

        text += "\n\nReady to grow: " + pending.Count;
        if (pending.Count > 0)
            text += "\nPress the Grow Ready Buddy button before camp opens.";

        int total = GameState.Instance.ownedGobbos != null ? GameState.Instance.ownedGobbos.Count : 0;
        text += "\n\nTotal little guys: " + total;
        return text;
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
        if (pendingEvolutionListParent == null) return;

        ClearListParent(pendingEvolutionListParent);
        AddListText(pendingEvolutionListParent, "READY TO GROW", true);

        if (pending == null || pending.Count == 0)
        {
            AddListText(pendingEvolutionListParent, "No growth choices waiting.", false);
            return;
        }

        foreach (GobboUnitSaveData buddy in pending)
        {
            if (buddy == null) continue;
            buddy.EnsureRuntimeDefaults();
            AddListText(pendingEvolutionListParent, buddy.displayName + " needs a growth choice", false);
        }
    }

    string FormatRunBuddyReport(BuddyRunReport report)
    {
        if (report == null) return "Missing buddy report";

        string line = report.displayName;
        line += "\nLv " + report.levelStart + " → " + report.levelEnd;
        line += " | XP +" + report.xpGained;
        line += " | Kills +" + report.killsGained;
        line += "\nNights +" + report.nightsGained + " | Happy " + report.happinessEnd;
        if (!string.IsNullOrWhiteSpace(report.traitLabel) && report.traitLabel != "None")
            line += " | " + report.traitLabel;
        if (report.readyToGrow) line += "\nREADY TO GROW";
        if (report.died) line += "\nDIED";
        return line;
    }

    string FormatCampBuddyReport(BuddyRunReport report)
    {
        if (report == null) return "Missing buddy report";

        string line = report.displayName;
        line += "\nLv " + report.levelStart + " → " + report.levelEnd;
        line += " | Camp XP +" + report.xpGained;
        line += "\nNights " + report.nightsEnd + " | Happy " + report.happinessEnd;
        if (!string.IsNullOrWhiteSpace(report.traitLabel) && report.traitLabel != "None")
            line += " | " + report.traitLabel;
        if (report.readyToGrow) line += "\nREADY TO GROW";
        return line;
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
        OpenCampBuddyEvolutionPanel(buddy);
    }

    void OpenCampBuddyEvolutionPanel(GobboUnitSaveData buddy)
    {
        if (campBuddyEvolutionPanel == null)
        {
            Debug.LogError("No CampBuddyEvolutionPanel assigned. Add it under Canvas and assign it on CampSceneController.");
            return;
        }

        BuildCampEvolutionChoices(buddy);
        HideAllCampFlowPanels();

        if (campBuddyEvolutionTitle != null)
            campBuddyEvolutionTitle.text = "Choose what " + buddy.displayName + " grows into";

        for (int i = 0; i < campBuddyEvolutionButtons.Length; i++)
        {
            Button button = campBuddyEvolutionButtons[i];
            if (button == null) continue;

            if (i >= currentEvolutionChoices.Count)
            {
                button.gameObject.SetActive(false);
                continue;
            }

            BuddyTypeSetup setup = currentEvolutionChoices[i];
            button.gameObject.SetActive(true);
            button.interactable = true;
            button.onClick.RemoveAllListeners();

            int index = i;
            button.onClick.AddListener(() => ChooseCampEvolution(index));

            Image image = button.GetComponent<Image>();
            if (image != null) image.raycastTarget = true;

            if (i < campBuddyEvolutionTexts.Length && campBuddyEvolutionTexts[i] != null)
            {
                campBuddyEvolutionTexts[i].text = setup.displayName +
                    "\nHP: " + setup.maxHealth +
                    "\nDMG: " + setup.damage +
                    "\nSPD: " + setup.moveSpeed.ToString("0.0");
                campBuddyEvolutionTexts[i].raycastTarget = false;
            }
        }

        campBuddyEvolutionPanel.SetActive(true);
        campBuddyEvolutionPanel.transform.SetAsLastSibling();

        CanvasGroup group = campBuddyEvolutionPanel.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    void BuildCampEvolutionChoices(GobboUnitSaveData buddy)
    {
        currentEvolutionChoices.Clear();

        // Camp growth no longer depends on the run-scene BuddyRoster.
        // Replace GetFallbackEvolutionChoices with a shared BuddyTypeDatabase later.
        List<BuddyTypeSetup> choices = GetFallbackEvolutionChoices(3);

        foreach (BuddyTypeSetup setup in choices)
        {
            if (setup != null && setup.buddyType != BuddyType.Baby)
                currentEvolutionChoices.Add(setup);
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
        BuddyProgression.ApplyEvolutionChoice(buddy, choice.buddyType, null);
        ApplySetupDirectlyIfNeeded(buddy, choice);

        SporeSaveManager.SaveCurrentSlotFromGameState();

        CloseCampBuddyEvolutionPanel();
        RefreshSurvivorsScreen();
    }

    void ApplySetupDirectlyIfNeeded(GobboUnitSaveData buddy, BuddyTypeSetup setup)
    {
        if (buddy == null || setup == null) return;

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

        if (campBuddyEvolutionPanel != null)
            campBuddyEvolutionPanel.SetActive(false);

        if (survivorsPanel != null)
            survivorsPanel.SetActive(true);
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

        bool hasPending = HasPendingBuddyEvolutions();
        continueToCampButton.interactable = true;

        TMP_Text label = continueToCampButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = hasPending ? "Grow Ready Buddy" : "Continue";
    }

    bool HasPendingBuddyEvolutions() => GetPendingBuddyEvolutions().Count > 0;

    List<GobboUnitSaveData> GetPendingBuddyEvolutions()
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (GameState.Instance == null || GameState.Instance.ownedGobbos == null) return result;

        foreach (GobboUnitSaveData buddy in GameState.Instance.ownedGobbos)
        {
            if (buddy == null) continue;
            buddy.EnsureRuntimeDefaults();
            if (buddy.pendingEvolution) result.Add(buddy);
        }

        return result;
    }
}
