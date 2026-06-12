using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class CampBuddyGrowthChoicePresenter
{
    public static void Show(
        GameObject panel,
        TMP_Text titleText,
        Button[] choiceButtons,
        TMP_Text[] choiceTexts,
        GobboUnitSaveData buddy,
        IReadOnlyList<BuddyTypeSetup> choices,
        Action<int> onChoiceSelected)
    {
        if (panel == null)
        {
            Debug.LogError("No CampBuddyEvolutionPanel assigned/found. Add it under Canvas and assign it on CampSceneController.");
            return;
        }

        if (buddy != null) buddy.EnsureRuntimeDefaults();
        if (titleText != null)
        {
            string displayName = buddy != null && !string.IsNullOrWhiteSpace(buddy.displayName) ? buddy.displayName : "that gobbo";
            titleText.text = "Choose what " + displayName + " grows into";
        }

        FillChoiceButtons(choiceButtons, choiceTexts, choices, onChoiceSelected);
        ShowPanel(panel);
    }

    public static void Hide(GameObject panel, Button[] choiceButtons, TMP_Text[] choiceTexts)
    {
        ClearChoiceButtons(choiceButtons, choiceTexts);
        if (panel != null) panel.SetActive(false);
    }

    static void FillChoiceButtons(Button[] choiceButtons, TMP_Text[] choiceTexts, IReadOnlyList<BuddyTypeSetup> choices, Action<int> onChoiceSelected)
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

            BuddyTypeSetup setup = choices[i];
            button.gameObject.SetActive(true);
            button.interactable = true;
            int index = i;
            button.onClick.AddListener(() => onChoiceSelected?.Invoke(index));

            Image image = button.GetComponent<Image>();
            if (image != null) image.raycastTarget = true;

            SetChoiceText(choiceTexts, i, FormatChoiceLabel(setup));
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

    static string FormatChoiceLabel(BuddyTypeSetup setup)
    {
        if (setup == null) return "";
        return setup.displayName + "\nHP: " + setup.maxHealth + "\nDMG: " + setup.damage + "\nSPD: " + setup.moveSpeed.ToString("0.0");
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
