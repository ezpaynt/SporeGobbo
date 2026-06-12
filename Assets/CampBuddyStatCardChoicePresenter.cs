using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class CampBuddyStatCardChoicePresenter
{
    public static void Show(
        GameObject panel,
        TMP_Text titleText,
        Button[] choiceButtons,
        TMP_Text[] choiceTexts,
        GobboUnitSaveData buddy,
        IReadOnlyList<GobboCard> choices,
        Action<int> onChoiceSelected)
    {
        if (panel == null)
        {
            Debug.LogError("No camp buddy stat card panel assigned/found. Reuse the CampBuddyEvolutionPanel for now.");
            return;
        }

        if (buddy != null) buddy.EnsureRuntimeDefaults();
        if (titleText != null)
        {
            string displayName = buddy != null && !string.IsNullOrWhiteSpace(buddy.displayName) ? buddy.displayName : "that gobbo";
            titleText.text = "Choose how " + displayName + " grows";
        }

        FillChoiceButtons(choiceButtons, choiceTexts, choices, onChoiceSelected);
        ShowPanel(panel);
    }

    public static void Hide(GameObject panel, Button[] choiceButtons, TMP_Text[] choiceTexts)
    {
        ClearChoiceButtons(choiceButtons, choiceTexts);
        if (panel != null) panel.SetActive(false);
    }

    static void FillChoiceButtons(Button[] choiceButtons, TMP_Text[] choiceTexts, IReadOnlyList<GobboCard> choices, Action<int> onChoiceSelected)
    {
        if (choiceButtons == null) return;
        int choiceCount = choices != null ? choices.Count : 0;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null) continue;

            button.onClick.RemoveAllListeners();
            if (i >= choiceCount || choices[i] == null)
            {
                button.gameObject.SetActive(false);
                SetChoiceText(choiceTexts, i, "");
                continue;
            }

            GobboCard card = choices[i];
            button.gameObject.SetActive(true);
            button.interactable = true;
            int index = i;
            button.onClick.AddListener(() => onChoiceSelected?.Invoke(index));

            Image image = button.GetComponent<Image>();
            if (image != null) image.raycastTarget = true;

            SetChoiceText(choiceTexts, i, FormatChoiceLabel(card));
        }
    }

    static void ClearChoiceButtons(Button[] choiceButtons, TMP_Text[] choiceTexts)
    {
        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                Button button = choiceButtons[i];
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.interactable = true;
                button.gameObject.SetActive(false);
            }
        }

        if (choiceTexts != null)
        {
            for (int i = 0; i < choiceTexts.Length; i++)
            {
                if (choiceTexts[i] == null) continue;
                choiceTexts[i].text = "";
                choiceTexts[i].raycastTarget = false;
            }
        }
    }

    static void SetChoiceText(TMP_Text[] choiceTexts, int index, string text)
    {
        if (choiceTexts == null || index < 0 || index >= choiceTexts.Length || choiceTexts[index] == null) return;
        choiceTexts[index].text = text;
        choiceTexts[index].raycastTarget = false;
    }

    static string FormatChoiceLabel(GobboCard card)
    {
        if (card == null) return "";
        string effects = FormatEffects(card);
        if (string.IsNullOrWhiteSpace(effects))
            return card.cardName + "\n" + card.description;

        return card.cardName + "\n" + card.description + "\n" + effects;
    }

    static string FormatEffects(GobboCard card)
    {
        List<string> effects = new List<string>();
        if (card.maxHealthBonus != 0) effects.Add(Signed(card.maxHealthBonus) + " Max Health");
        if (card.healthBonus != 0) effects.Add(Signed(card.healthBonus) + " Health");
        if (card.attackBonus != 0) effects.Add(Signed(card.attackBonus) + " Attack");
        if (card.defenseBonus != 0) effects.Add(Signed(card.defenseBonus) + " Defense");
        if (card.moveSpeedBonus != 0f) effects.Add(Signed(card.moveSpeedBonus) + " Move Speed");
        if (card.attackCooldownBonus != 0f) effects.Add(Signed(card.attackCooldownBonus) + " Attack Cooldown");
        if (card.critChanceBonus != 0f) effects.Add(Signed(Mathf.RoundToInt(card.critChanceBonus * 100f)) + "% Crit Chance");

        return string.Join(", ", effects);
    }

    static string Signed(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
    }

    static string Signed(float value)
    {
        return value > 0f ? "+" + value.ToString("0.##") : value.ToString("0.##");
    }

    static void ShowPanel(GameObject panel)
    {
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) return;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }
}
