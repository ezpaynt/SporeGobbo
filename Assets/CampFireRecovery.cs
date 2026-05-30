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
    public string eatAndRestButtonText = "Eat / Rest";
    public string closeButtonText = "Close";
    public string upgradesButtonText = "Upgrades";
    [TextArea(2, 5)] public string bodyBeforeRecovery = "Eat with the little guys and patch everyone up.";
    [TextArea(2, 5)] public string bodyAfterRecovery = "Everyone is fed for now. Later this can show upgrades.";

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
        if (bodyText != null) bodyText.text = recoveredThisCampVisit ? bodyAfterRecovery : bodyBeforeRecovery;
        if (eatAndRestButton != null) eatAndRestButton.interactable = !recoveredThisCampVisit;
        if (upgradesButton != null) upgradesButton.gameObject.SetActive(showUpgradesButton);
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
