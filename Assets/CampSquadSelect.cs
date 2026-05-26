using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampSquadSelect : MonoBehaviour, ICampInteractable
{
    [Header("Interaction")]
    public float interactRange = 1.35f;
    public KeyCode interactKey = KeyCode.E;
    public string interactPrompt = "Choose who comes";

    [Header("Optional Assigned UI")]
    public Canvas targetCanvas;
    public GameObject panel;
    public TMP_Text titleText;
    public Transform activeListParent;
    public Transform reserveListParent;
    public Button closeButton;

    [Header("Optional Refresh")]
    public CampPlayableSpawner campPlayableSpawner;
    public bool refreshCampVisualsAfterChange = false;

    [Header("Successor Marking")]
    public bool allowMarkSuccessor = true;
    public string noMarkedSuccessorText = "No successor marked";
    public string markSuccessorButtonText = "Mark";
    public string markedSuccessorButtonText = "Marked";

    [Header("Auto UI Style")]
    public bool buildReadableUiIfMissing = true;
    public Vector2 panelSize = new Vector2(760f, 520f);
    public int panelSortingOrder = 500;

    private Transform player;
    private bool isOpen;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (campPlayableSpawner == null)
            campPlayableSpawner = Object.FindAnyObjectByType<CampPlayableSpawner>(FindObjectsInactive.Include);
    }

    void Start()
    {
        if (buildReadableUiIfMissing && (panel == null || activeListParent == null || reserveListParent == null || closeButton == null))
            BuildReadableUi();

        HookCloseButton();
        CloseMenu();
    }

    void Update()
    {
        FindPlayerIfMissing();

        if (player == null)
            return;

        if (isOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(interactKey))
                CloseMenu();
            return;
        }

        if (Vector2.Distance(transform.position, player.position) <= interactRange && Input.GetKeyDown(interactKey))
            OpenMenu();
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public void Interact(GobboController player)
    {
        OpenMenu();
    }

    public void OpenMenu()
    {
        if (panel == null)
            BuildReadableUi();

        isOpen = true;
        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        RefreshMenu();
    }

    public void CloseMenu()
    {
        isOpen = false;
        if (panel != null)
            panel.SetActive(false);
    }

    void HookCloseButton()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(CloseMenu);
    }

    void RefreshMenu()
    {
        ClearRows();

        if (GameState.Instance == null)
        {
            if (titleText != null)
                titleText.text = "Choose Who Comes\nNo GameState found.";
            return;
        }

        List<BuddyData> active = GameState.Instance.GetActiveSquad();
        List<BuddyData> reserve = GameState.Instance.GetReserveBuddies();

        if (titleText != null)
            titleText.text = "Choose Who Comes  (" + active.Count + " / " + GameState.Instance.maxActiveSquad + ")\n<size=16>" + GetSuccessorLine() + "</size>";

        AddHeader(activeListParent, "ACTIVE SQUAD", "Click a buddy to leave them at camp. Mark only sets future leader.");
        if (active.Count == 0)
            AddInfoRow(activeListParent, "Nobody selected.");
        foreach (BuddyData buddy in active)
            AddBuddyRow(activeListParent, buddy, true);

        AddHeader(reserveListParent, "CAMP RESERVE", "Click a buddy to bring them next run. Mark only sets future leader.");
        if (reserve.Count == 0)
            AddInfoRow(reserveListParent, "Nobody waiting.");
        foreach (BuddyData buddy in reserve)
            AddBuddyRow(reserveListParent, buddy, false);
    }

    string GetSuccessorLine()
    {
        if (!allowMarkSuccessor || GameState.Instance == null)
            return "";

        CampSuccessorPreferenceStore store = CampSuccessorPreferenceStore.GetOrCreate();
        BuddyData marked = store != null ? store.GetMarkedSuccessor() : null;

        if (marked == null)
        {
            if (store != null && !string.IsNullOrWhiteSpace(store.markedSuccessorId))
                store.ClearSuccessor();

            return noMarkedSuccessorText;
        }

        return "Successor: " + marked.buddyName;
    }

    void AddBuddyRow(Transform parent, BuddyData buddy, bool currentlyActive)
    {
        if (parent == null || buddy == null)
            return;

        buddy.EnsureRuntimeDefaults();

        GameObject row = new GameObject((currentlyActive ? "Active_" : "Reserve_") + buddy.buddyName, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        spawnedRows.Add(row);

        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(0f, 44f);

        Image rowBg = row.GetComponent<Image>();
        rowBg.color = currentlyActive ? new Color(0.24f, 0.42f, 0.25f, 0.95f) : new Color(0.30f, 0.28f, 0.20f, 0.95f);
        rowBg.raycastTarget = false;

        Button toggleButton = CreateRowButton(row.transform, "SquadToggleButton", currentlyActive, allowMarkSuccessor ? 126f : 0f);
        toggleButton.onClick.AddListener(() => ToggleBuddy(buddy.uniqueId, currentlyActive));

        TMP_Text txt = CreateText(toggleButton.transform, "Label", TextAlignmentOptions.Left, 16, FontStyles.Normal);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = new Vector2(12f, 3f);
        txt.rectTransform.offsetMax = new Vector2(-12f, -3f);
        txt.color = Color.white;
        txt.text = BuddyLabel(buddy, currentlyActive);
        txt.raycastTarget = false;

        if (allowMarkSuccessor)
            AddSuccessorButton(row.transform, buddy);
    }

    Button CreateRowButton(Transform parent, string name, bool currentlyActive, float reserveRightSpace)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = new Vector2(-reserveRightSpace, 0f);

        Image image = obj.GetComponent<Image>();
        image.color = currentlyActive ? new Color(0.24f, 0.42f, 0.25f, 0.01f) : new Color(0.30f, 0.28f, 0.20f, 0.01f);

        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    void AddSuccessorButton(Transform parent, BuddyData buddy)
    {
        if (parent == null || buddy == null)
            return;

        CampSuccessorPreferenceStore store = CampSuccessorPreferenceStore.GetOrCreate();
        bool isMarked = store != null && store.markedSuccessorId == buddy.uniqueId;

        GameObject obj = new GameObject("SuccessorButton", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(112f, 32f);
        rt.anchoredPosition = new Vector2(-8f, 0f);

        Image image = obj.GetComponent<Image>();
        image.color = isMarked ? new Color(0.75f, 0.55f, 0.20f, 1f) : new Color(0.35f, 0.30f, 0.20f, 1f);

        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => MarkSuccessor(buddy.uniqueId));

        TMP_Text label = CreateText(obj.transform, "Text", TextAlignmentOptions.Center, 14, FontStyles.Bold);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.color = Color.white;
        label.text = isMarked ? markedSuccessorButtonText : markSuccessorButtonText;
        label.raycastTarget = false;
    }

    string BuddyLabel(BuddyData buddy, bool active)
    {
        string action = active ? "  → Send to reserve" : "  → Bring along";
        return buddy.buddyName + "   " + buddy.buddyType + " / " + buddy.ageStage +
               "   Lv " + buddy.level + "   HP " + buddy.health + "/" + buddy.maxHealth + action;
    }

    void ToggleBuddy(string buddyId, bool currentlyActive)
    {
        if (GameState.Instance == null || string.IsNullOrWhiteSpace(buddyId))
            return;

        if (currentlyActive)
        {
            GameState.Instance.MoveBuddyToReserve(buddyId);
        }
        else
        {
            bool moved = GameState.Instance.MoveBuddyToActiveSquad(buddyId);
            if (!moved)
                Debug.Log("Active squad is full.");
        }

        RefreshMenu();

        if (refreshCampVisualsAfterChange && campPlayableSpawner != null)
            campPlayableSpawner.SpawnPlayableCamp();
    }

    void MarkSuccessor(string buddyId)
    {
        if (GameState.Instance == null || string.IsNullOrWhiteSpace(buddyId))
            return;

        BuddyData buddy = GameState.Instance.FindBuddy(buddyId);
        if (buddy == null)
            return;

        CampSuccessorPreferenceStore.GetOrCreate().MarkSuccessor(buddy.uniqueId);
        CampMessageUI.Show(buddy.buddyName + " is marked as successor.");
        RefreshMenu();
    }

    void AddHeader(Transform parent, string title, string subtitle)
    {
        if (parent == null)
            return;

        GameObject box = new GameObject(title + " Header", typeof(RectTransform));
        box.transform.SetParent(parent, false);
        spawnedRows.Add(box);
        RectTransform rt = box.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 60f);

        TMP_Text text = CreateText(box.transform, "Text", TextAlignmentOptions.Center, 18, FontStyles.Bold);
        text.rectTransform.anchorMin = new Vector2(0f, 0f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.color = new Color(1f, 0.92f, 0.72f, 1f);
        text.text = title + "\n<size=13><font-weight=400>" + subtitle + "</font-weight></size>";
    }

    void AddInfoRow(Transform parent, string message)
    {
        if (parent == null)
            return;

        GameObject row = new GameObject("Info", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        spawnedRows.Add(row);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 36f);

        TMP_Text text = CreateText(row.transform, "Text", TextAlignmentOptions.Center, 15, FontStyles.Italic);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        text.text = message;
    }

    void ClearRows()
    {
        for (int i = spawnedRows.Count - 1; i >= 0; i--)
        {
            if (spawnedRows[i] != null)
                Destroy(spawnedRows[i]);
        }
        spawnedRows.Clear();
    }

    void BuildReadableUi()
    {
        if (targetCanvas == null)
            targetCanvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("CampCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasObj.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        targetCanvas.sortingOrder = Mathf.Max(targetCanvas.sortingOrder, panelSortingOrder);

        panel = new GameObject("SquadSelectPanel_AUTO", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(targetCanvas.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = panelSize;
        panel.GetComponent<Image>().color = new Color(0.055f, 0.045f, 0.035f, 0.94f);

        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(panel.transform, false);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 26;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(20f, -86f);
        titleRt.offsetMax = new Vector2(-20f, -16f);

        activeListParent = CreateColumn(panel.transform, "ActiveList", new Vector2(0.04f, 0.16f), new Vector2(0.48f, 0.78f));
        reserveListParent = CreateColumn(panel.transform, "ReserveList", new Vector2(0.52f, 0.16f), new Vector2(0.96f, 0.78f));

        closeButton = CreateButton(panel.transform, "CloseButton", "Close", new Vector2(0.5f, 0.055f), new Vector2(160f, 42f));
        HookCloseButton();
    }

    Transform CreateColumn(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject col = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        col.transform.SetParent(parent, false);
        RectTransform rt = col.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image image = col.GetComponent<Image>();
        image.color = new Color(0.13f, 0.11f, 0.085f, 0.96f);

        VerticalLayoutGroup layout = col.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 7f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        return col.transform;
    }

    Button CreateButton(Transform parent, string name, string label, Vector2 normalizedPos, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = normalizedPos;
        rt.anchorMax = normalizedPos;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.62f, 0.48f, 0.25f, 1f);

        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text txt = CreateText(obj.transform, "Text", TextAlignmentOptions.Center, 18, FontStyles.Bold);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = Vector2.zero;
        txt.rectTransform.offsetMax = Vector2.zero;
        txt.color = Color.white;
        txt.text = label;
        txt.raycastTarget = false;

        return button;
    }

    TMP_Text CreateText(Transform parent, string name, TextAlignmentOptions alignment, int fontSize, FontStyles style)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        TMP_Text text = obj.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    void FindPlayerIfMissing()
    {
        if (player != null)
            return;

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
            player = found.transform;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
