using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class CampPendingGrowthListPresenter
{
    public static void Show(
        Transform parent,
        IReadOnlyList<GobboUnitSaveData> pendingBuddies,
        Action<string> onResolveSelected)
    {
        if (parent == null) return;

        Clear(parent);
        AddText(parent, "READY TO GROW", true);

        if (pendingBuddies == null || pendingBuddies.Count == 0)
        {
            AddText(parent, "No growth choices waiting.", false);
            return;
        }

        foreach (GobboUnitSaveData buddy in pendingBuddies)
        {
            if (buddy == null) continue;
            buddy.EnsureRuntimeDefaults();
            AddBuddyRow(parent, buddy, onResolveSelected);
        }
    }

    static void AddBuddyRow(Transform parent, GobboUnitSaveData buddy, Action<string> onResolveSelected)
    {
        GameObject row = new GameObject("PendingGrowthRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, 48f);

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.minHeight = 48f;
        rowLayout.preferredHeight = 48f;

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 10f;
        layout.padding = new RectOffset(0, 0, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        string label = buddy.displayName + " - " + FormatGrowthType(BuddyGrowthService.GetPendingGrowthChoiceType(buddy));
        TMP_Text text = AddText(row.transform, label, false);
        LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
        textLayout.minWidth = 160f;
        textLayout.preferredWidth = 240f;
        textLayout.flexibleWidth = 1f;
        textLayout.minHeight = 40f;

        Button button = AddButton(row.transform, "Evolve");
        string buddyId = buddy.uniqueId;
        button.onClick.AddListener(() => onResolveSelected?.Invoke(buddyId));
    }

    static TMP_Text AddText(Transform parent, string text, bool header)
    {
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
        rect.sizeDelta = new Vector2(0f, header ? 34f : 40f);
        return label;
    }

    static Button AddButton(Transform parent, string text)
    {
        GameObject item = new GameObject("EvolveButton", typeof(RectTransform));
        item.transform.SetParent(parent, false);

        RectTransform rect = item.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 36f);

        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.minWidth = 120f;
        layout.preferredWidth = 120f;
        layout.flexibleWidth = 0f;
        layout.minHeight = 36f;
        layout.preferredHeight = 36f;

        Image image = item.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;

        Button button = item.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(item.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 18;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.196f, 0.196f, 0.196f, 1f);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        return button;
    }

    static string FormatGrowthType(BuddyGrowthChoiceType type)
    {
        switch (type)
        {
            case BuddyGrowthChoiceType.Evolution: return "Evolution";
            case BuddyGrowthChoiceType.StatCard: return "Stat Card";
            case BuddyGrowthChoiceType.Trait: return "Trait";
            case BuddyGrowthChoiceType.Mutation: return "Mutation";
            default: return "Growth";
        }
    }

    static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
    }
}
