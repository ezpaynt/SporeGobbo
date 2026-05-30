using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpScreen : MonoBehaviour
{
    [System.Serializable]
    public class UpgradeButton
    {
        public Button button;
        public TextMeshProUGUI text;
    }

    [Header("Panel")]
    public GameObject levelUpPanel;
    [Header("Buttons")]
    public UpgradeButton[] buttons;
    [Header("Reroll")]
    public Button rerollButton;
    public TextMeshProUGUI rerollText;
    public int startingRerollCost = 1;

    private GobboController currentGobbo;
    private List<GobboCard> currentChoices = new List<GobboCard>();
    private List<string> seenThisSelection = new List<string>();
    private int rerollCost = 1;
    private GobboCardContext currentContext = GobboCardContext.RunLevelUp;

    void Awake()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(TryReroll);
        }
        Hide();
    }

    public void ShowChoices(GobboController gobbo)
    {
        if (levelUpPanel == null)
        {
            Debug.LogError("LevelUpScreen needs Level Up Panel assigned.");
            return;
        }

        currentGobbo = gobbo;
        currentContext = gobbo != null && gobbo.NeedsEvolutionChoice() ? GobboCardContext.EvolutionChoice : GobboCardContext.RunLevelUp;
        rerollCost = startingRerollCost;
        seenThisSelection.Clear();
        levelUpPanel.SetActive(true);
        Time.timeScale = 0f;
        BuildChoices();
    }

    void BuildChoices()
    {
        currentChoices.Clear();
        int count = Mathf.Min(3, buttons.Length);
        if (GobboCardDatabase.Instance != null)
            currentChoices = GobboCardDatabase.Instance.GetChoicesForPlayer(currentGobbo, currentContext, count, seenThisSelection);
        if (currentChoices.Count == 0 && GobboCardDatabase.Instance != null)
            currentChoices = GobboCardDatabase.Instance.GetChoicesForPlayer(currentGobbo, currentContext, count, null);

        foreach (GobboCard card in currentChoices)
        {
            if (card != null && !seenThisSelection.Contains(card.cardId)) seenThisSelection.Add(card.cardId);
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || buttons[i].button == null) continue;
            if (i >= currentChoices.Count)
            {
                buttons[i].button.gameObject.SetActive(false);
                continue;
            }

            GobboCard card = currentChoices[i];
            buttons[i].button.gameObject.SetActive(true);
            if (buttons[i].text != null) buttons[i].text.text = card.cardName + "\n" + card.description;
            buttons[i].button.onClick.RemoveAllListeners();
            GobboCard captured = card;
            buttons[i].button.onClick.AddListener(() => SelectCard(captured));
        }
        RefreshRerollUI();
    }

    void SelectCard(GobboCard card)
    {
        if (currentGobbo == null || card == null) { Hide(); return; }
        Hide();
        card.ApplyToPlayer(currentGobbo);
    }

    void TryReroll()
    {
        if (currentGobbo == null || GameState.Instance == null) return;
        if (!GameState.Instance.TrySpendShinies(rerollCost))
        {
            Debug.Log("Not enough shinies to reroll.");
            RefreshRerollUI();
            return;
        }
        rerollCost++;
        BuildChoices();
    }

    void RefreshRerollUI()
    {
        int shinies = GameState.Instance != null ? GameState.Instance.GetLeader().shinies : 0;
        if (rerollText != null) rerollText.text = "Reroll (" + rerollCost + " shiny)";
        if (rerollButton != null) rerollButton.interactable = shinies >= rerollCost && currentChoices.Count > 0;
    }

    void Hide()
    {
        Time.timeScale = 1f;
        if (levelUpPanel != null) levelUpPanel.SetActive(false);
    }
}
