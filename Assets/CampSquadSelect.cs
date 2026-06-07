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

    [Header("Debug")]
    public bool logMissingReferences = true;

    [HideInInspector] public Vector2 panelSize = new Vector2(860f, 560f);
    [HideInInspector] public int panelSortingOrder = 500;

    private Transform player;
    private bool playerInsideTrigger;
    private bool isOpen;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

    }

    void Start()
    {
        ValidateReferences();
        HookCloseButton();
        CloseMenu();
    }

    void Update()
    {
        if (isOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(interactKey)))
            CloseMenu();
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
        if (panel == null)
        {
            Debug.LogWarning("CampSquadSelect missing Panel reference.");
            return;
        }

        isOpen = true;
        CampMenuModal.Open(null, this, CloseMenu);
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
        CampMenuModal.Close(this);
    }

    void ValidateReferences()
    {
        if (!logMissingReferences) return;
        if (panel == null) Debug.LogWarning("CampSquadSelect missing Panel reference.");
        if (activeListParent == null) Debug.LogWarning("CampSquadSelect missing Active List Parent reference.");
        if (reserveListParent == null) Debug.LogWarning("CampSquadSelect missing Reserve List Parent reference.");
        if (closeButton == null) Debug.LogWarning("CampSquadSelect missing Close Button reference.");
        if (campPlayableSpawner == null && refreshCampVisualsAfterChange)
            Debug.LogWarning("CampSquadSelect refreshes camp visuals after change but has no CampPlayableSpawner assigned.");
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
            SyncSuccessorPreferenceStore();
            CampMessageUI.Show("No successor marked.");
        }
        else
        {
            GameState.Instance.SetMarkedSuccessorId(gobboId);
            SyncSuccessorPreferenceStore();
            GobboUnitSaveData gobbo = GameState.Instance.FindGobboById(gobboId);
            CampMessageUI.Show((gobbo != null ? DisplayName(gobbo) : "That gobbo") + " is marked as successor.");
        }

        SporeSaveManager.SaveCurrentSlotFromGameState();
        RefreshMenu();
    }

    void SyncSuccessorPreferenceStore()
    {
        CampSuccessorPreferenceStore store = CampSuccessorPreferenceStore.Instance;
        if (store != null) store.SyncFromGameState();
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

        SyncSuccessorPreferenceStore();
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
