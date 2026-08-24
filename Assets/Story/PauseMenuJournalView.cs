using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PauseMenuJournalView : MonoBehaviour
{
    static readonly Color Panel = new Color(0.055f, 0.16f, 0.09f, 0.98f);
    static readonly Color Accent = new Color(0.09f, 0.23f, 0.13f, 1f);
    static readonly Color Selected = new Color(0.15f, 0.34f, 0.19f, 1f);
    static readonly Color Bone = new Color(0.93f, 0.9f, 0.78f, 1f);
    static readonly Color Muted = new Color(0.72f, 0.73f, 0.62f, 1f);

    RectTransform threadList;
    TMP_Text pageTitle;
    TMP_Text pageBody;
    TMP_FontAsset font;
    JournalSnapshot snapshot;
    string selectedThreadId = "";
    readonly List<Button> threadButtons = new List<Button>();
    public Selectable DefaultSelectable => threadButtons.Count > 0 ? threadButtons[0] : null;

    public void Build(RectTransform root, TMP_FontAsset sharedFont)
    {
        if (root == null || threadList != null) return;
        font = sharedFont != null ? sharedFont : TMP_Settings.defaultFontAsset;

        RectTransform left = PanelRect("JournalThreads", root, new Vector2(0.02f, 0.03f), new Vector2(0.30f, 0.97f));
        Heading(left, "STORY THREADS");
        RectTransform viewport = Rect("Viewport", left, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.85f));
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect threadScroll = left.gameObject.AddComponent<ScrollRect>();
        threadScroll.viewport = viewport;
        threadScroll.horizontal = false;
        threadScroll.vertical = true;
        threadScroll.movementType = ScrollRect.MovementType.Clamped;
        threadList = Rect("ThreadButtons", viewport, new Vector2(0f, 1f), Vector2.one);
        threadList.pivot = new Vector2(0.5f, 1f);
        VerticalLayoutGroup listLayout = threadList.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.childControlHeight = true;
        listLayout.childControlWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childForceExpandWidth = true;
        ContentSizeFitter listFit = threadList.gameObject.AddComponent<ContentSizeFitter>();
        listFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        threadScroll.content = threadList;

        RectTransform right = PanelRect("JournalPage", root, new Vector2(0.32f, 0.03f), new Vector2(0.98f, 0.97f));
        pageTitle = Text("ThreadTitle", right, 28f, TextAlignmentOptions.Center);
        pageTitle.fontStyle = FontStyles.Bold;
        SetRect(pageTitle.rectTransform, new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero);

        RectTransform pageViewport = Rect("PageViewport", right, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.83f));
        pageViewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect pageScroll = right.gameObject.AddComponent<ScrollRect>();
        pageScroll.viewport = pageViewport;
        pageScroll.horizontal = false;
        pageScroll.vertical = true;
        pageScroll.movementType = ScrollRect.MovementType.Clamped;
        pageBody = Text("GrowingPage", pageViewport, 22f, TextAlignmentOptions.TopLeft);
        pageBody.rectTransform.anchorMin = new Vector2(0f, 1f);
        pageBody.rectTransform.anchorMax = Vector2.one;
        pageBody.rectTransform.pivot = new Vector2(0.5f, 1f);
        pageBody.rectTransform.offsetMin = new Vector2(12f, 0f);
        pageBody.rectTransform.offsetMax = new Vector2(-12f, 0f);
        ContentSizeFitter pageFit = pageBody.gameObject.AddComponent<ContentSizeFitter>();
        pageFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        pageScroll.content = pageBody.rectTransform;
    }

    public void Refresh(JournalSnapshot value)
    {
        snapshot = value ?? new JournalSnapshot();
        if (FindThread(selectedThreadId) == null)
            selectedThreadId = snapshot.threads.Count > 0 ? snapshot.threads[0].threadId : "";
        RebuildThreadButtons();
        ShowSelectedThread();
    }

    void RebuildThreadButtons()
    {
        if (threadList == null) return;
        for (int i = threadList.childCount - 1; i >= 0; i--) Destroy(threadList.GetChild(i).gameObject);
        threadButtons.Clear();

        if (snapshot == null || snapshot.threads.Count == 0)
        {
            TMP_Text empty = Text("NoThreads", threadList, 20f, TextAlignmentOptions.Center);
            empty.text = "No journal entries yet.";
            empty.color = Muted;
            Preferred(empty.gameObject, 54f);
            return;
        }

        foreach (JournalThreadSnapshot thread in snapshot.threads)
        {
            string threadId = thread.threadId;
            Button button = NewButton("Thread_" + threadId, threadList, thread.displayName, () => SelectThread(threadId));
            button.GetComponent<Image>().color = threadId == selectedThreadId ? Selected : Accent;
            Preferred(button.gameObject, 54f);
            threadButtons.Add(button);
        }
    }

    void SelectThread(string threadId)
    {
        selectedThreadId = threadId;
        RebuildThreadButtons();
        ShowSelectedThread();
    }

    void ShowSelectedThread()
    {
        JournalThreadSnapshot thread = FindThread(selectedThreadId);
        if (thread == null)
        {
            pageTitle.text = "JOURNAL";
            pageBody.text = "Nothing has been written yet.";
            pageBody.color = Muted;
            return;
        }

        pageTitle.text = thread.displayName;
        pageBody.color = Bone;
        StringBuilder page = new StringBuilder();
        foreach (JournalEntrySnapshot entry in thread.entries)
        {
            if (page.Length > 0) page.Append("\n\n");
            page.Append(entry.bodyText);
        }
        pageBody.text = page.ToString();
    }

    JournalThreadSnapshot FindThread(string threadId)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(threadId)) return null;
        foreach (JournalThreadSnapshot thread in snapshot.threads)
            if (thread.threadId == threadId) return thread;
        return null;
    }

    void Heading(RectTransform panel, string heading)
    {
        TMP_Text text = Text("Heading", panel, 24f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.text = heading;
        SetRect(text.rectTransform, new Vector2(0f, 0.86f), Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, -4f));
    }

    RectTransform PanelRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = Rect(name, parent, min, max);
        rect.gameObject.AddComponent<Image>().color = Panel;
        return rect;
    }

    RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetRect(rect, min, max, Vector2.zero, Vector2.zero);
        return rect;
    }

    TMP_Text Text(string name, Transform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = Bone;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    Button NewButton(string name, Transform parent, string label, Action clicked)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = Accent;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => clicked?.Invoke());
        TMP_Text text = Text("ButtonText", go.transform, 20f, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        text.text = label;
        return button;
    }

    static void Preferred(GameObject target, float height)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null) element = target.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
    }

    static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
