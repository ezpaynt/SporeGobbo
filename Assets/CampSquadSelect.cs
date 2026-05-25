using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampSquadSelect : MonoBehaviour, ICampInteractable
{
    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    public string interactPrompt = "Choose who comes";
    [Tooltip("Extra distance from this object's collider where E still works. Keep small so the spot matches the visible station.")]
    public float interactBuffer = 0.25f;

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

    [Header("Auto UI Style")]
    public bool buildReadableUiIfMissing = true;
    public Vector2 panelSize = new Vector2(860f, 560f);
    public int panelSortingOrder = 500;

    private Transform player;
    private Collider2D stationCollider;
    private bool isOpen;
    private float openedAtTime = -99f;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    void Awake()
    {
        stationCollider = GetComponent<Collider2D>();
        if (stationCollider != null)
            stationCollider.isTrigger = true;

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

        if (isOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                CloseMenu();
            return;
        }

        if (player != null && IsPlayerCloseEnough() && Input.GetKeyDown(interactKey))
            OpenMenu();
    }

    bool IsPlayerCloseEnough()
    {
        if (player == null)
            return false;

        if (stationCollider == null)
            return Vector2.Distance(transform.position, player.position) <= 1.25f;

        Vector2 closest = stationCollider.ClosestPoint(player.position);
        float distance = Vector2.Distance(closest, player.position);
        return distance <= Mathf.Max(0.02f, interactBuffer);
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public void Interact(GobboController playerController)
    {
        // GobboController may also call this on the same E press. Open is idempotent.
        OpenMenu();
    }

    public void OpenMenu()
    {
        if (Time.unscaledTime - openedAtTime < 0.1f)
            return;

        if (panel == null)
            BuildReadableUi();

        isOpen = true;
        openedAtTime = Time.unscaledTime;

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

        GameState.Instance.RepairRosterState();
        List<BuddyData> active = GameState.Instance.GetActiveSquad();
        List<BuddyData> reserve = GameState.Instance.GetReserveBuddies();

        if (titleText != null)
            titleText.text = "Choose Who Comes  (" + active.Count + " / " + GameState.Instance.maxActiveSquad + ")";

        AddHeader(activeListParent, "ACTIVE SQUAD", "Click a buddy to leave them at camp.");
        if (active.Count == 0)
            AddInfoRow(activeListParent, "Nobody selected.");
        foreach (BuddyData buddy in active)
            AddBuddyRow(activeListParent, buddy, true);

        AddHeader(reserveListParent, "CAMP RESERVE", "Click a buddy to bring them next run.");
        if (reserve.Count == 0)
            AddInfoRow(reserveListParent, "Nobody waiting.");
        foreach (BuddyData buddy in reserve)
            AddBuddyRow(reserveListParent, buddy, false);
    }

    void AddBuddyRow(Transform parent, BuddyData buddy, bool currentlyActive)
    {
        if (parent == null || buddy == null)
            return;

        buddy.EnsureRuntimeDefaults();

        GameObject row = new GameObject((currentlyActive ? "Active_" : "Reserve_") + buddy.buddyName, typeof(RectTransform), typeof(Image), typeof(Button));
        row.transform.SetParent(parent, false);
        spawnedRows.Add(row);

        RectTransform rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 46f);

        Image bg = row.GetComponent<Image>();
        bg.color = currentlyActive ? new Color(0.24f, 0.42f, 0.25f, 0.95f) : new Color(0.30f, 0.28f, 0.20f, 0.95f);

        Button btn = row.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => ToggleBuddy(buddy.uniqueId, currentlyActive));

        TMP_Text txt = CreateText(row.transform, "Label", TextAlignmentOptions.Left, 16, FontStyles.Normal);
        txt.rectTransform.anchorMin = new Vector2(0f, 0f);
        txt.rectTransform.anchorMax = new Vector2(1f, 1f);
        txt.rectTransform.offsetMin = new Vector2(12f, 3f);
        txt.rectTransform.offsetMax = new Vector2(-12f, -3f);
        txt.color = Color.white;
        txt.text = BuddyLabel(buddy, currentlyActive);
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
                CampMessageUI.Show("Active squad is full.");
        }

        GameState.Instance.RepairRosterState();
        RefreshMenu();

        if (refreshCampVisualsAfterChange && campPlayableSpawner != null)
            campPlayableSpawner.SpawnPlayableCamp();
    }

    void AddHeader(Transform parent, string title, string subtitle)
    {
        if (parent == null)
            return;

        GameObject box = new GameObject(title + " Header", typeof(RectTransform));
        box.transform.SetParent(parent, false);
        spawnedRows.Add(box);
        RectTransform rt = box.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 62f);

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
        panel.GetComponent<Image>().color = new Color(0.055f, 0.045f, 0.035f, 0.96f);

        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(panel.transform, false);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 27;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(20f, -76f);
        titleRt.offsetMax = new Vector2(-20f, -16f);

        activeListParent = CreateColumn(panel.transform, "ActiveList", new Vector2(0.04f, 0.16f), new Vector2(0.48f, 0.82f));
        reserveListParent = CreateColumn(panel.transform, "ReserveList", new Vector2(0.52f, 0.16f), new Vector2(0.96f, 0.82f));

        closeButton = CreateButton(panel.transform, "CloseButton", "Close", new Vector2(0.5f, 0.055f), new Vector2(170f, 44f));
        HookCloseButton();
    }

    Transform CreateColumn(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject col = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        col.transform.SetParent(parent, false);
        RectTransform rt = col.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image image = col.GetComponent<Image>();
        image.color = new Color(0.13f, 0.11f, 0.085f, 0.98f);

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
        if (stationCollider != null)
            Gizmos.DrawWireCube(stationCollider.bounds.center, stationCollider.bounds.size);
        else
            Gizmos.DrawWireSphere(transform.position, 1.25f);
    }
}
