using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampSquadSelect : MonoBehaviour, ICampInteractable
{
    [Header("Interaction")]
    public string interactPrompt = "Choose who comes";

    [Header("Assigned UI")]
    public GameObject panel;
    public TMP_Text titleText;
    public Transform activeListParent;
    public Transform reserveListParent;
    public Button closeButton;

    [Header("Optional Refresh")]
    public CampPlayableSpawner campPlayableSpawner;
    public bool refreshCampVisualsAfterChange = false;

    private bool isOpen;
    private GobboController currentPlayer;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    void Awake()
    {
        if (campPlayableSpawner == null)
            campPlayableSpawner = Object.FindAnyObjectByType<CampPlayableSpawner>(FindObjectsInactive.Include);
    }

    void Start()
    {
        HookCloseButton();
        CloseMenu();
    }

    void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseMenu();
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public void Interact(GobboController player)
    {
        currentPlayer = player;

        if (!isOpen)
            OpenMenu();
    }

    public void OpenMenu()
    {
        if (panel == null)
        {
            Debug.LogWarning("CampSquadSelect has no assigned Panel. Assign your handmade squad UI in the inspector.", this);
            CampMessageUI.Show("Squad menu is not wired yet.");
            return;
        }

        isOpen = true;
        CampMenuModal.Open(currentPlayer, this, CloseMenu);

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        RefreshMenu();
    }

    public void CloseMenu()
    {
        isOpen = false;

        if (panel != null)
            panel.SetActive(false);

        CampMenuModal.Close(this);
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
        rt.sizeDelta = new Vector2(0f, 44f);

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
                Debug.Log("Active squad is full.");
        }

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
}
