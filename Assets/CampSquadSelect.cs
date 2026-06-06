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

    [Header("Successor UI")]
    public bool showSuccessorColumn = true;
    public string markSuccessorText = "Mark";
    public string markedSuccessorText = "Marked";
    public string clearSuccessorText = "Clear";
    public float successorButtonWidth = 92f;
    public bool allowClickMarkedButtonToClear = true;

    [Header("Optional Refresh")]
    public CampPlayableSpawner campPlayableSpawner;
    public bool refreshCampVisualsAfterChange = false;

    [Header("Auto UI Style")]
    public bool buildReadableUiIfMissing = true;
    public Vector2 panelSize = new Vector2(860f, 560f);
    public int panelSortingOrder = 500;

    private Transform player;
    private bool playerInsideTrigger;
    private bool isOpen;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

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
        if (player == null) return;

        if (isOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(interactKey))
                CloseMenu();
            return;
        }

        bool closeEnough = Vector2.Distance(transform.position, player.position) <= interactRange;
        if ((playerInsideTrigger || closeEnough) && Input.GetKeyDown(interactKey))
            OpenMenu();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
        {
            playerInsideTrigger = true;
            player = other.transform;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
            playerInsideTrigger = false;
    }

    public string GetInteractPrompt() => interactPrompt;

    public void Interact(GobboController playerController)
    {
        OpenMenu();
    }

    public void OpenMenu()
    {
        if (panel == null) BuildReadableUi();

        isOpen = true;
        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        CanvasGroup group = panel != null ? panel.GetComponent<CanvasGroup>() : null;
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        RefreshMenu();
    }

    public void CloseMenu()
    {
        isOpen = false;
        if (panel != null) panel.SetActive(false);
    }

    void HookCloseButton()
    {
        if (closeButton == null) return;
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(CloseMenu);
    }

    void RefreshMenu()
    {
        ClearRows();

        if (GameState.Instance == null)
        {
            if (titleText != null) titleText.text = "Choose Who Comes\nNo GameState found.";
            return;
        }

        RepairMarkedSuccessorIfMissing();

        List<GobboUnitSaveData> active = GameState.Instance.GetActiveSquadUnits();
        List<GobboUnitSaveData> reserve = GameState.Instance.GetReserveGobboUnits();
        GobboUnitSaveData marked = GameState.Instance.GetMarkedSuccessorUnit();
        string markedName = marked != null ? DisplayName(marked) : "none";

        if (titleText != null)
        {
            titleText.text = "Choose Who Comes (" + active.Count + " / " + GameState.Instance.maxActiveSquad + ")" +
                             (showSuccessorColumn ? "\nSuccessor: " + markedName : "");
        }

        EnsureListParentLayout(activeListParent);
        EnsureListParentLayout(reserveListParent);

        AddHeader(activeListParent, "ACTIVE SQUAD", showSuccessorColumn ? "Main button moves gobbo.\nRight button marks successor." : "Click a gobbo to leave them at camp.");
        if (active.Count == 0) AddInfoRow(activeListParent, "Nobody selected.");
        foreach (GobboUnitSaveData gobbo in active) AddGobboRow(activeListParent, gobbo, true);

        AddHeader(reserveListParent, "CAMP RESERVE", showSuccessorColumn ? "Main button moves gobbo.\nRight button marks successor." : "Click a gobbo to bring them next run.");
        if (reserve.Count == 0) AddInfoRow(reserveListParent, "Nobody waiting.");
        foreach (GobboUnitSaveData gobbo in reserve) AddGobboRow(reserveListParent, gobbo, false);
    }

    void AddGobboRow(Transform parent, GobboUnitSaveData gobbo, bool currentlyActive)
    {
        if (parent == null || gobbo == null) return;

        gobbo.EnsureRuntimeDefaults();

        GameObject row = new GameObject((currentlyActive ? "Active_" : "Reserve_") + DisplayName(gobbo), typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        spawnedRows.Add(row);

        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.sizeDelta = new Vector2(0f, 52f);
        rowRt.offsetMin = new Vector2(0f, rowRt.offsetMin.y);
        rowRt.offsetMax = new Vector2(0f, rowRt.offsetMax.y);

        LayoutElement rowElement = row.AddComponent<LayoutElement>();
        rowElement.preferredHeight = 52f;
        rowElement.minHeight = 52f;
        rowElement.flexibleWidth = 1f;

        Image rowBg = row.GetComponent<Image>();
        rowBg.color = currentlyActive ? new Color(0.18f, 0.28f, 0.18f, 0.65f) : new Color(0.22f, 0.20f, 0.14f, 0.65f);
        rowBg.raycastTarget = false;

        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.spacing = 7f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        Button moveButton = CreateRowButton(row.transform, "MoveButton", GobboLabel(gobbo, currentlyActive), currentlyActive ? new Color(0.24f, 0.42f, 0.25f, 0.95f) : new Color(0.30f, 0.28f, 0.20f, 0.95f));
        LayoutElement moveLayout = moveButton.gameObject.AddComponent<LayoutElement>();
        moveLayout.flexibleWidth = 1f;
        moveLayout.minHeight = 44f;
        moveButton.onClick.AddListener(() => ToggleGobbo(gobbo.uniqueId, currentlyActive));

        if (showSuccessorColumn)
        {
            bool isMarked = IsMarkedSuccessor(gobbo.uniqueId);
            Button markButton = CreateRowButton(row.transform, "SuccessorButton", isMarked ? markedSuccessorText : markSuccessorText, isMarked ? new Color(0.74f, 0.52f, 0.18f, 1f) : new Color(0.36f, 0.28f, 0.16f, 1f));
            LayoutElement markLayout = markButton.gameObject.AddComponent<LayoutElement>();
            markLayout.preferredWidth = successorButtonWidth;
            markLayout.minWidth = successorButtonWidth;
            markLayout.minHeight = 44f;
            markButton.onClick.AddListener(() => ToggleSuccessorMark(gobbo.uniqueId));
        }
    }

    Button CreateRowButton(Transform parent, string name, string label, Color bgColor)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);

        Image image = obj.GetComponent<Image>();
        image.color = bgColor;

        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text txt = CreateText(obj.transform, "Text", TextAlignmentOptions.Left, 15, FontStyles.Normal);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = new Vector2(10f, 3f);
        txt.rectTransform.offsetMax = new Vector2(-10f, -3f);
        txt.color = Color.white;
        txt.text = label;
        txt.raycastTarget = false;

        return button;
    }

    string GobboLabel(GobboUnitSaveData gobbo, bool active)
    {
        string action = active ? " → Send to reserve" : " → Bring along";
        string successor = IsMarkedSuccessor(gobbo.uniqueId) ? " [SUCCESSOR]" : "";
        return DisplayName(gobbo) + " " + gobbo.gobboType + " / " + gobbo.ageStage + " Lv " + gobbo.level + " HP " + gobbo.health + "/" + gobbo.maxHealth + successor + action;
    }

    string DisplayName(GobboUnitSaveData gobbo)
    {
        if (gobbo == null) return "Gobbo";
        return string.IsNullOrWhiteSpace(gobbo.displayName) ? "Gobbo" : gobbo.displayName;
    }

    void ToggleGobbo(string gobboId, bool currentlyActive)
    {
        if (GameState.Instance == null || string.IsNullOrWhiteSpace(gobboId)) return;

        if (currentlyActive)
        {
            GameState.Instance.MoveBuddyToReserve(gobboId);
        }
        else
        {
            bool moved = GameState.Instance.MoveBuddyToActiveSquad(gobboId);
            if (!moved) CampMessageUI.Show("Active squad is full.");
        }

        GameState.Instance.RepairRosterState();
        SporeSaveManager.SaveCurrentSlotFromGameState();
        RefreshMenu();

        if (refreshCampVisualsAfterChange && campPlayableSpawner != null)
            campPlayableSpawner.SpawnPlayableCamp();
    }

    void ToggleSuccessorMark(string gobboId)
    {
        if (string.IsNullOrWhiteSpace(gobboId) || GameState.Instance == null) return;

        bool isMarked = IsMarkedSuccessor(gobboId);
        if (isMarked && allowClickMarkedButtonToClear)
        {
            GameState.Instance.SetMarkedSuccessorId("");
            CampSuccessorPreferenceStore.GetOrCreate().SyncFromGameState();
            CampMessageUI.Show("No successor marked.");
        }
        else
        {
            GameState.Instance.SetMarkedSuccessorId(gobboId);
            CampSuccessorPreferenceStore.GetOrCreate().SyncFromGameState();
            GobboUnitSaveData gobbo = GameState.Instance.FindGobboById(gobboId);
            CampMessageUI.Show((gobbo != null ? DisplayName(gobbo) : "That gobbo") + " is marked as successor.");
        }

        SporeSaveManager.SaveCurrentSlotFromGameState();
        RefreshMenu();
    }

    bool IsMarkedSuccessor(string gobboId)
    {
        return GameState.Instance != null && !string.IsNullOrWhiteSpace(gobboId) && GameState.Instance.GetMarkedSuccessorId() == gobboId;
    }

    void RepairMarkedSuccessorIfMissing()
    {
        if (GameState.Instance == null) return;

        string markedId = GameState.Instance.GetMarkedSuccessorId();
        if (string.IsNullOrWhiteSpace(markedId)) return;

        if (GameState.Instance.GetMarkedSuccessor() == null)
            GameState.Instance.SetMarkedSuccessorId("");

        CampSuccessorPreferenceStore.GetOrCreate().SyncFromGameState();
    }

    void EnsureListParentLayout(Transform parent)
    {
        if (parent == null) return;

        RectTransform rt = parent as RectTransform;
        if (rt != null && rt.rect.width < 80f)
            rt.sizeDelta = new Vector2(360f, Mathf.Max(260f, rt.sizeDelta.y));

        VerticalLayoutGroup layout = parent.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 7f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = parent.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = parent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void AddHeader(Transform parent, string title, string subtitle)
    {
        if (parent == null) return;

        GameObject box = new GameObject(title + " Header", typeof(RectTransform));
        box.transform.SetParent(parent, false);
        spawnedRows.Add(box);
        box.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 64f);

        TMP_Text text = CreateText(box.transform, "Text", TextAlignmentOptions.Center, 18, FontStyles.Bold);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.color = new Color(1f, 0.92f, 0.72f, 1f);
        text.text = title + "\n" + subtitle;
    }

    void AddInfoRow(Transform parent, string message)
    {
        if (parent == null) return;

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

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

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
        if (player != null) return;
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null) player = found.transform;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
