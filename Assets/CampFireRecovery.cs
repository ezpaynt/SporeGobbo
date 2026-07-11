using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampFireRecovery : MonoBehaviour, ICampInteractable
{
    [Header("Interaction")]
    public string prompt = "Open fire menu";
    public string recoveredPrompt = "Open fire menu";
    public string recoveryMessage = "The gobbos eat, warm up, and stop looking so busted.";
    public string alreadyRecoveredMessage = "Everyone already had their little meal, but you can still look at the fire menu.";

    [Header("Fire Menu UI")]
    public GameObject fireMenuPanel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Button eatAndRestButton;
    public Button closeButton;
    public Button upgradesButton;
    public string title = "Campfire";
    public string eatAndRestButtonText = "Eat and Rest";
    public string closeButtonText = "Back";
    public string upgradesButtonText = "Upgrades";
    [TextArea(2, 5)] public string bodyBeforeRecovery = "Warm up, eat, and patch up the camp.";
    [TextArea(2, 5)] public string bodyAfterRecovery = "Everyone is fed and patched up for now.";

    [Header("Future Upgrade Hook")]
    public GameObject upgradesPanel;
    public bool showUpgradesButton = true;

    [Header("Recovery")]
    public bool healPlayer = true;
    public bool healBuddies = true;
    public bool saveAfterRecovery = true;

    private bool recoveredThisCampVisit = false;
    private bool menuOpen = false;
    private GobboController currentPlayer;

    void Awake()
    {
        HookButtons();
        CloseMenu();
    }

    void Update()
    {
        if (menuOpen && Input.GetKeyDown(KeyCode.Escape)) CloseMenu();
    }

    void HookButtons()
    {
        if (eatAndRestButton != null)
        {
            eatAndRestButton.onClick.RemoveAllListeners();
            eatAndRestButton.onClick.AddListener(DoRecoveryFromMenu);
            SetButtonText(eatAndRestButton, eatAndRestButtonText);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseMenu);
            SetButtonText(closeButton, closeButtonText);
        }

        if (upgradesButton != null)
        {
            upgradesButton.onClick.RemoveAllListeners();
            upgradesButton.onClick.AddListener(ToggleUpgradePanel);
            SetButtonText(upgradesButton, upgradesButtonText);
        }
    }

    void SetButtonText(Button button, string text)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }

    public string GetInteractPrompt() => recoveredThisCampVisit ? recoveredPrompt : prompt;

    public void Interact(GobboController player)
    {
        currentPlayer = player;
        if (!menuOpen) OpenMenu();
    }

    public void OpenMenu()
    {
        menuOpen = true;
        CampMenuModal.Open(currentPlayer, this, CloseMenu);
        if (fireMenuPanel != null)
        {
            fireMenuPanel.SetActive(true);
            fireMenuPanel.transform.SetAsLastSibling();
        }

        if (titleText != null) titleText.text = title;
        RefreshMenuText();
    }

    public void CloseMenu()
    {
        menuOpen = false;
        if (fireMenuPanel != null) fireMenuPanel.SetActive(false);
        if (upgradesPanel != null) upgradesPanel.SetActive(false);
        CampMenuModal.Close(this);
    }

    void RefreshMenuText()
    {
        bool needsRecovery = HasMissingHealth();

        if (bodyText != null)
            bodyText.text = BuildBodyText(needsRecovery);

        if (eatAndRestButton != null)
        {
            eatAndRestButton.interactable = !recoveredThisCampVisit && needsRecovery;
            SetButtonText(eatAndRestButton, recoveredThisCampVisit || !needsRecovery ? "Already Rested" : eatAndRestButtonText);
        }

        if (upgradesButton != null) upgradesButton.gameObject.SetActive(showUpgradesButton);
    }

    string BuildBodyText(bool needsRecovery)
    {
        GameState state = GameState.Instance;
        GobboUnitSaveData leader = state != null ? state.GetLeader() : null;

        int leaderHealth = currentPlayer != null ? currentPlayer.health : leader != null ? leader.health : 0;
        int leaderMaxHealth = currentPlayer != null ? currentPlayer.maxHealth : leader != null ? leader.maxHealth : 0;
        int leaderMissing = Mathf.Max(0, leaderMaxHealth - leaderHealth);

        int injuredBuddies = 0;
        int buddyMissing = 0;
        if (state != null && state.ownedGobbos != null)
        {
            foreach (GobboUnitSaveData buddy in state.ownedGobbos)
            {
                if (buddy == null) continue;
                buddy.EnsureRuntimeDefaults();
                int missing = Mathf.Max(0, buddy.maxHealth - buddy.health);
                if (missing <= 0) continue;
                injuredBuddies++;
                buddyMissing += missing;
            }
        }

        string status = needsRecovery ? bodyBeforeRecovery : bodyAfterRecovery;
        string leaderLine = leaderMaxHealth > 0
            ? "Leader HP: " + leaderHealth + " / " + leaderMaxHealth + " (missing " + leaderMissing + ")"
            : "Leader HP: unknown";
        string buddyLine = injuredBuddies > 0
            ? "Buddies hurt: " + injuredBuddies + " (missing " + buddyMissing + " HP total)"
            : "Buddies hurt: none";
        string costLine = "Heal cost: Free";

        return status + "\n\n" + leaderLine + "\n" + buddyLine + "\n" + costLine;
    }

    bool HasMissingHealth()
    {
        GameState state = GameState.Instance;
        GobboUnitSaveData leader = state != null ? state.GetLeader() : null;

        if (healPlayer)
        {
            if (currentPlayer != null && currentPlayer.health < currentPlayer.maxHealth)
                return true;
            if (leader != null && leader.health < leader.maxHealth)
                return true;
        }

        if (healBuddies && state != null && state.ownedGobbos != null)
        {
            foreach (GobboUnitSaveData buddy in state.ownedGobbos)
            {
                if (buddy == null) continue;
                buddy.EnsureRuntimeDefaults();
                if (buddy.health < buddy.maxHealth)
                    return true;
            }
        }

        return false;
    }

    void DoRecoveryFromMenu()
    {
        Recover(currentPlayer);
        RefreshMenuText();
    }

    void Recover(GobboController player)
    {
        if (recoveredThisCampVisit)
        {
            CampMessageUI.Show(alreadyRecoveredMessage);
            return;
        }

        if (!HasMissingHealth())
        {
            CampMessageUI.Show("Everyone is already patched up.");
            return;
        }

        GameState state = GameState.Instance;
        if (state != null)
        {
            if (healPlayer)
            {
                GobboUnitSaveData leader = state.GetLeader();
                leader.health = leader.maxHealth;
                if (player != null) player.health = player.maxHealth;
            }

            if (healBuddies && state.ownedGobbos != null)
            {
                foreach (GobboUnitSaveData buddy in state.ownedGobbos)
                {
                    if (buddy == null) continue;
                    buddy.EnsureRuntimeDefaults();
                    buddy.health = buddy.maxHealth;
                    buddy.hasBeenHit = false;
                }

                BuddyUnit[] visibleBuddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
                foreach (BuddyUnit unit in visibleBuddies)
                {
                    if (unit != null && unit.unitData != null)
                    {
                        unit.unitData.health = unit.unitData.maxHealth;
                        unit.unitData.hasBeenHit = false;
                        unit.ApplyVisuals();
                    }
                }
            }

            if (saveAfterRecovery && player != null)
                state.SavePlayer(player);

            if (saveAfterRecovery)
                SporeSaveManager.SaveCurrentSlotFromGameState();
        }

        recoveredThisCampVisit = true;
        CampMessageUI.Show(recoveryMessage);
        if (CampStartRoutineManager.Instance != null) CampStartRoutineManager.Instance.NotifyFireRecovered();
        Debug.Log("Camp fire recovery complete.");
    }

    void ToggleUpgradePanel()
    {
        if (upgradesPanel == null)
        {
            CampMessageUI.Show("Upgrade menu placeholder. Put the future upgrade UI here.");
            return;
        }
        upgradesPanel.SetActive(!upgradesPanel.activeSelf);
    }
}
