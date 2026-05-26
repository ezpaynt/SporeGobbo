using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampSquadSelect : MonoBehaviour, ICampInteractable
{
    [Header("Interaction")]
    public string interactPrompt = "Choose who comes";
    [Tooltip("Extra distance outside this object's collider where E still works. Keep it small so the world object feels honest.")]
    public float interactBuffer = 0.25f;

    [Header("Required Scene UI")]
    [Tooltip("A real panel you created under Canvas. This script will not make one.")]
    public GameObject panel;
    public TMP_Text titleText;
    public Transform activeListParent;
    public Transform reserveListParent;
    public Button closeButton;

    [Header("Generated Rows Only")]
    [Tooltip("The script only generates buddy rows inside the assigned list parents. It does not generate panels, canvases, or world objects.")]
    public float rowHeight = 48f;
    public int rowFontSize = 16;
    public Color activeRowColor = new Color(0.24f, 0.42f, 0.25f, 0.95f);
    public Color reserveRowColor = new Color(0.30f, 0.28f, 0.20f, 0.95f);
    public Color headerColor = new Color(1f, 0.92f, 0.72f, 1f);

    [Header("Optional Refresh")]
    public bool refreshCampVisualsAfterChange = false;
    public CampPlayableSpawner campPlayableSpawner;

    private Transform player;
    private Collider2D stationCollider;
    private bool isOpen;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    void Awake()
    {
        stationCollider = GetComponent<Collider2D>();
        // Important: do NOT force collider trigger/solid here.
        // You control that on the scene object.
    }

    void Start()
    {
        HookCloseButton();
        CloseMenu();
        ValidateSceneSetup();
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

    public void Interact(GobboController playerController)
    {
        if (playerController != null)
            player = playerController.transform;

        if (!IsPlayerCloseEnough())
            return;

        if (isOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    public void OpenMenu()
    {
        if (!HasRequiredUI())
        {
            Debug.LogWarning("CampSquadSelect is missing scene UI. Assign Panel, Title Text, Active List Parent, Reserve List Parent, and Close Button.", this);
            CampMessageUI.Show("Squad board has no menu wired yet.");
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
        ClearRows();

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

        if (!HasRequiredUI())
            return;

        if (GameState.Instance == null)
        {
            titleText.text = "Choose Who Comes\nNo GameState found.";
            return;
        }

        GameState.Instance.RepairRosterState();

        List<BuddyData> active = GameState.Instance.GetActiveSquad();
        List<BuddyData> reserve = GameState.Instance.GetReserveBuddies();

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

        GameObject row = new GameObject((currentlyActive ? "Active_" : "Reserve_") + buddy.buddyName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        spawnedRows.Add(row);

        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.preferredHeight = rowHeight;
        layout.minHeight = rowHeight;

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

        GameObject box = new GameObject(title + " Header", typeof(RectTransform), typeof(LayoutElement));
        box.transform.SetParent(parent, false);
        spawnedRows.Add(box);

        LayoutElement layout = box.GetComponent<LayoutElement>();
        layout.preferredHeight = 62f;
        layout.minHeight = 62f;

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

        GameObject row = new GameObject("Info", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        spawnedRows.Add(row);

        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.preferredHeight = 36f;
        layout.minHeight = 36f;

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

    bool HasRequiredUI()
    {
        return panel != null && titleText != null && activeListParent != null && reserveListParent != null && closeButton != null;
    }

    void ValidateSceneSetup()
    {
        if (!HasRequiredUI())
            Debug.LogWarning("CampSquadSelect needs real scene UI assigned. It will not auto-create a panel anymore.", this);
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        else
            Gizmos.DrawWireSphere(transform.position, 1.25f);
    }
}
