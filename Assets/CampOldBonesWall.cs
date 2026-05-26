using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampOldBonesWall : MonoBehaviour, ICampInteractable
{
    [Header("Interaction")]
    public string interactPrompt = "Read old bones";

    [Header("UI")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Button closeButton;

    [Header("Text")]
    public string title = "Old Bones Wall";
    public string emptyMessage = "No little bones remembered yet.";

    [Header("Visibility")]
    [Tooltip("If true, the wall object hides itself until the death-history store has at least one record.")]
    public bool hideWhenNoDeaths = true;

    private bool isOpen = false;

    void Awake()
    {
        HookButtons();
        CloseMenu();
    }

    void Start()
    {
        HookButtons();
        RefreshWallVisibility();
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
        if (isOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    public void OpenMenu()
    {
        RefreshWallVisibility();

        if (panel == null)
        {
            Debug.LogWarning("CampOldBonesWall has no Panel assigned.", this);
            CampMessageUI.Show("Old Bones wall menu is not wired yet.");
            return;
        }

        isOpen = true;

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        if (titleText != null)
            titleText.text = title;

        RefreshText();
    }

    public void CloseMenu()
    {
        isOpen = false;

        if (panel != null)
            panel.SetActive(false);
    }

    public void RefreshWallVisibility()
    {
        if (!hideWhenNoDeaths)
            return;

        CampDeathHistoryStore store = CampDeathHistoryStore.GetOrCreate();
        bool shouldShow = store != null && store.HasAnyDeaths();

        // Do not hide if this component is on a child interact spot.
        // Assign the script to the visible wall object for best results.
        if (gameObject.activeSelf != shouldShow)
            gameObject.SetActive(shouldShow);
    }

    public void ForceShowWall()
    {
        gameObject.SetActive(true);
    }

    void HookButtons()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(CloseMenu);
    }

    void RefreshText()
    {
        if (bodyText == null)
        {
            Debug.LogWarning("CampOldBonesWall has no Body Text assigned.", this);
            return;
        }

        CampDeathHistoryStore store = CampDeathHistoryStore.GetOrCreate();

        if (store == null || store.deadBuddyHistory == null || store.deadBuddyHistory.Count == 0)
        {
            bodyText.text = emptyMessage;
            return;
        }

        string text = "";

        for (int i = store.deadBuddyHistory.Count - 1; i >= 0; i--)
        {
            DeadBuddyRecord record = store.deadBuddyHistory[i];
            if (record == null)
                continue;

            text += record.GetDisplayLine();

            if (i > 0)
                text += "\n\n";
        }

        if (string.IsNullOrWhiteSpace(text))
            text = emptyMessage;

        bodyText.text = text;
    }
}
