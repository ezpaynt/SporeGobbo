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
    [Tooltip("New camp-only panel. Do not use the old BuddyChoiceScreen for camp evolution.")]
    public GameObject campBuddyEvolutionPanel;
    public TMP_Text campBuddyEvolutionTitle;
    public Button[] campBuddyEvolutionButtons = new Button[3];
    public TMP_Text[] campBuddyEvolutionTexts = new TMP_Text[3];

    [Header("Optional Camp Management Screen Later")]
    public GameObject campMenuPanel;

    [Header("Playable Camp Spawn")]
    public CampPlayableSpawner campPlayableSpawner;
    public CampStartRoutineManager campStartRoutineManager;
    public bool healAutomaticallyWhenCampOpens = false;
    public bool skipReportsAndOpenCampImmediatelyForTesting = false;

    [Header("Legacy Camp Visuals / Fallback")]
    public CampVisualSpawner campVisualSpawner;

    [Header("Optional Future UI")]
    public TMP_Text playerStatsText;
    public Transform activeSquadListParent;
    public Transform reserveListParent;
    public Button buddyButtonPrefab;

    private readonly List<BuddyTypeSetup> currentEvolutionChoices = new List<BuddyTypeSetup>();
    private string currentlyEvolvingBuddyId = "";

    private void Awake()
    {
        AutoFillMissingReferences();
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
        if (campVisualSpawner == null) campVisualSpawner = Object.FindAnyObjectByType<CampVisualSpawner>(FindObjectsInactive.Include);
        if (runStatsText == null && runStatsPanel != null) runStatsText = runStatsPanel.GetComponentInChildren<TMP_Text>(true);
        if (survivorsText == null && survivorsPanel != null) survivorsText = survivorsPanel.GetComponentInChildren<TMP_Text>(true);
        if (continueToSurvivorsButton == null && runStatsPanel != null) continueToSurvivorsButton = runStatsPanel.GetComponentInChildren<Button>(true);
        if (continueToCampButton == null && survivorsPanel != null) continueToCampButton = survivorsPanel.GetComponentInChildren<Button>(true);
        AutoFillCampBuddyEvolutionPanel();
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

        Button[] buttons = campBuddyEvolutionPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < campBuddyEvolutionButtons.Length && i < buttons.Length; i++)
        {
            if (campBuddyEvolutionButtons[i] == null) campBuddyEvolutionButtons[i] = buttons[i];
            if (campBuddyEvolutionTexts[i] == null && campBuddyEvolutionButtons[i] != null)
                campBuddyEvolutionTexts[i] = campBuddyEvolutionButtons[i].GetComponentInChildren<TMP_Text>(true);
        }

        campBuddyEvolutionPanel.SetActive(false);
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

        if (runStatsPanel != null) runStatsPanel.SetActive(false);
        if (survivorsPanel != null) survivorsPanel.SetActive(false);
        if (campMenuPanel != null) campMenuPanel.SetActive(false);
        if (campBuddyEvolutionPanel != null) campBuddyEvolutionPanel.SetActive(false);

        if (campPlayableSpawner == null) campPlayableSpawner = Object.FindAnyObjectByType<CampPlayableSpawner>(FindObjectsInactive.Include);
        if (campPlayableSpawner != null)
        {
            campPlayableSpawner.SpawnPlayableCamp();
            BeginCampStartRoutineIfPresent();
            return;
        }

        if (campVisualSpawner == null) campVisualSpawner = Object.FindAnyObjectByType<CampVisualSpawner>(FindObjectsInactive.Include);
        if (campVisualSpawner != null)
        {
            campVisualSpawner.SpawnCampVisuals();
            BeginCampStartRoutineIfPresent();
        }
        else
        {
            Debug.LogWarning("No CampPlayableSpawner or CampVisualSpawner assigned/found. Camp UI hid, but no camp objects could spawn.");
        }
    }

    void BeginCampStartRoutineIfPresent()
    {
        if (campStartRoutineManager == null) campStartRoutineManager = Object.FindAnyObjectByType<CampStartRoutineManager>(FindObjectsInactive.Include);
        if (campStartRoutineManager != null) campStartRoutineManager.BeginCampVisit();
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
        leader.health = leader.maxHealth;
    }

    void FillRunStatsText()
    {
        if (runStatsText == null || GameState.Instance == null) return;
        RunSummaryData run = GameState.Instance.lastRun;
        GobboUnitSaveData leader = GameState.Instance.GetLeader();
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
        if (survivorsText == null || GameState.Instance == null) return;
        RunSummaryData run = GameState.Instance.lastRun;
        List<GobboUnitSaveData> pending = GetPendingBuddyEvolutions();
        string text = "Look who made it! (or didn't)" + "\n\nNew buddies: " + run.buddiesFound;

        if (run.newBuddyNames != null && run.newBuddyNames.Count > 0)
        {
            foreach (string name in run.newBuddyNames) text += "\n+ " + name;
        }
        else text += "\n- Nobody new joined this time.";

        text += "\n\nBuddies lost: " + run.buddiesLost;
        if (run.deadBuddyNames != null && run.deadBuddyNames.Count > 0)
        {
            foreach (string name in run.deadBuddyNames) text += "\n- " + name;
        }
        else text += "\n- Nobody died.";

        if (pending.Count > 0)
        {
            text += "\n\nREADY TO GROW:";
            foreach (GobboUnitSaveData buddy in pending)
                text += "\n* " + GetBuddyLine(buddy) + " CLICK THEIR BUTTON";
        }
        else text += "\n\nNo buddies need growth choices right now.";

        text += "\n\nActive squad:";
        List<GobboUnitSaveData> active = GameState.Instance.GetActiveSquadUnits();
        if (active.Count == 0) text += "\n- Nobody in active squad.";
        else foreach (GobboUnitSaveData buddy in active) if (buddy != null) text += "\n- " + GetBuddyLine(buddy);

        List<GobboUnitSaveData> reserve = GameState.Instance.GetReserveGobboUnits();
        text += "\n\nCamp reserve:";
        if (reserve.Count == 0) text += "\n- Nobody waiting at camp.";
        else foreach (GobboUnitSaveData buddy in reserve) if (buddy != null) text += "\n- " + GetBuddyLine(buddy);

        text += "\n\nTotal little guys: " + (GameState.Instance.ownedGobbos != null ? GameState.Instance.ownedGobbos.Count : 0);
        if (pending.Count > 0) text += "\n\nPress Grow Ready Buddy to choose each path before camp opens.";
        survivorsText.text = text;
    }

    string GetBuddyLine(GobboUnitSaveData buddy)
    {
        if (buddy == null) return "Missing buddy";
        buddy.EnsureRuntimeDefaults();
        return buddy.displayName + " the " + buddy.gobboType + " " + buddy.ageStage +
            " Lv " + buddy.level +
            " XP " + buddy.xp + "/" + buddy.xpToNextLevel +
            " HP " + buddy.health + "/" + buddy.maxHealth +
            (buddy.pendingEvolution ? " READY TO GROW" : "") +
            (buddy.runsWaitingForEvolution >= 2 ? " ANGRY DOT" : "");
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
        OpenCampBuddyEvolutionPanel(buddy);
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
        if (campBuddyEvolutionTitle != null) campBuddyEvolutionTitle.text = "Choose what " + buddy.displayName + " grows into";

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
                campBuddyEvolutionTexts[i].text = setup.displayName + "\nHP: " + setup.maxHealth + "\nDMG: " + setup.damage + "\nSPD: " + setup.moveSpeed.ToString("0.0");
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

        Debug.Log("Opened CampBuddyEvolutionPanel with " + currentEvolutionChoices.Count + " choices for " + buddy.displayName);
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
        if (campBuddyEvolutionPanel != null) campBuddyEvolutionPanel.SetActive(false);
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
        bool hasPending = HasPendingBuddyEvolutions();
        continueToCampButton.interactable = true;
        TMP_Text label = continueToCampButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = hasPending ? "Grow Ready Buddy" : "Continue";
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

    void EnsureGameState()
    {
        if (GameState.Instance != null) return;
        GameObject stateObject = new GameObject("GameState");
        stateObject.AddComponent<GameState>();
    }
}
