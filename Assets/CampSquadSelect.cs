using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampSquadSelect : MonoBehaviour, ICampInteractable
{
    [Header("Interaction")]
    public string interactPrompt = "Choose who comes";
    [Tooltip("Extra distance beyond this object's collider where interaction is still accepted. Keep this small so the station matches the visible art.")]
    public float interactBuffer = 0.15f;

    [Header("Required Scene UI")]
    [Tooltip("Scene-authored panel. This script no longer creates panels/canvases for you.")]
    public GameObject panel;
    public TMP_Text titleText;
    public Transform activeListParent;
    public Transform reserveListParent;
    public Button closeButton;

    [Header("Generated Rows Only")]
    public float rowHeight = 44f;
    public int rowFontSize = 16;
    public Color activeRowColor = new Color(0.24f, 0.42f, 0.25f, 0.95f);
    public Color reserveRowColor = new Color(0.30f, 0.28f, 0.20f, 0.95f);
    public Color headerColor = new Color(1f, 0.92f, 0.72f, 1f);

    [Header("Optional Refresh")]
    [Tooltip("Leave off unless you want the visible camp buddies to respawn immediately after squad changes.")]
    public bool refreshCampVisualsAfterChange = false;
    public CampPlayableSpawner campPlayableSpawner;

    private Collider2D stationCollider;
    private bool isOpen;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    void Awake()
    {
        stationCollider = GetComponent<Collider2D>();

        if (campPlayableSpawner == null)
            campPlayableSpawner = Object.FindAnyObjectByType<CampPlayableSpawner>(FindObjectsInactive.Include);
    }

    void Start()
    {
        HookCloseButton();
        CloseMenu();
        ValidateSetup();
    }

    void Update()
    {
        if (!isOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseMenu();
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public void Interact(GobboController player)
    {
        if (isOpen)
        {
            CloseMenu();
            return;
        }

        if (!IsPlayerCloseEnough(player))
            return;

        OpenMenu();
    }

    public void OpenMenu()
    {
        if (!HasRequiredUi())
        {
            Debug.LogWarning("CampSquadSelect needs a scene-authored Panel, Title Text, Active List Parent, Reserve List Parent, and Close Button assigned in the inspector.", this);
            CampMessageUI.Show("Squad select UI is not assigned yet.");
            return;
        }

        isOpen = true;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        RefreshMenu();
    }

    public void CloseMenu()
    {
        isOpen = false;

        if (panel != null)
            panel.SetActive(false);
    }

    bool IsPlayerCloseEnough(GobboController player)
    {
        if (player == null)
            return false;

        if (stationCollider == null)
            return true;

        Vector2 closest = stationCollider.ClosestPoint(player.transform.position);
        float distance = Vector2.Distance(closest, player.transform.position);
        return distance <= Mathf.Max(0.01f, interactBuffer);
    }

    bool HasRequiredUi()
    {
        return panel != null && titleText != null && activeListParent != null && reserveListParent != null && closeButton != null;
    }

    void ValidateSetup()
    {
        if (!HasRequiredUi())
            Debug.LogWarning("CampSquadSelect is scene-authored now. Assign its Panel, Title Text, Active List Parent, Reserve List Parent, and Close Button. It will not auto-create them.", this);
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
            titleText.text = "Choose Who Comes\nNo GameState found.";
            return;
        }

        GameState.Instance.RepairRosterState();

        List<BuddyData> active = GameState.Instance.GetActiveSquad();
        List<BuddyData> reserve = GameState.Instance.GetReserveBuddies();

        titleText.text = "Choose Who Comes  (" + active.Count + " / " + GameState.Instance.maxActiveSquad + ")";

        AddHeader(activeListParent, "ACTIVE SQUAD", "Click to leave at camp.");
        if (active.Count == 0)
            AddInfoRow(activeListParent, "Nobody selected.");
        foreach (BuddyData buddy in active)
            AddBuddyRow(activeListParent, buddy, true);

        AddHeader(reserveListParent, "CAMP RESERVE", "Click to bring next run.");
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
        rt.sizeDelta = new Vector2(0f, rowHeight);

        Image bg = row.GetComponent<Image>();
        bg.color = currentlyActive ? activeRowColor : reserveRowColor;

        Button btn = row.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => ToggleBuddy(buddy.uniqueId, currentlyActive));

        TMP_Text txt = CreateText(row.transform, "Label", TextAlignmentOptions.Left, rowFontSize, FontStyles.Normal);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
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

        bool changed;
        if (currentlyActive)
        {
            changed = GameState.Instance.MoveBuddyToReserve(buddyId);
        }
        else
        {
            changed = GameState.Instance.MoveBuddyToActiveSquad(buddyId);
            if (!changed)
                CampMessageUI.Show("Active squad is full.");
        }

        if (changed)
            GameState.Instance.RepairRosterState();

        RefreshMenu();

        if (changed && refreshCampVisualsAfterChange && campPlayableSpawner != null)
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
        rt.sizeDelta = new Vector2(0f, 60f);

        TMP_Text text = CreateText(box.transform, "Text", TextAlignmentOptions.Center, 18, FontStyles.Bold);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.color = headerColor;
        text.text = title + "\n<size=13><font-weight=400>" + subtitle + "</font-weight></size>";
    }

    void AddInfoRow(Transform parent, string message)
    {
        if (parent == null)
            return;

        GameObject row = new GameObject("Info", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        spawnedRows.Add(row);

        RectTransform rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 36f);

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

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size + Vector3.one * interactBuffer * 2f);
        else
            Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
