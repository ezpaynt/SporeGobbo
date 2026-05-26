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

    private bool isOpen = false;

    void Awake()
    {
        HookButtons();
        CloseMenu();
    }

    void Start()
    {
        HookButtons();
        CloseMenu();
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
            CloseMenu();
        else
            OpenMenu();
    }

    public void OpenMenu()
    {
        isOpen = true;

        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

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
            return;

        if (GameState.Instance == null)
        {
            bodyText.text = "No GameState found.";
            return;
        }

        string text = "";

        if (GameState.Instance.lastRun != null &&
            GameState.Instance.lastRun.deadBuddyNames != null &&
            GameState.Instance.lastRun.deadBuddyNames.Count > 0)
        {
            foreach (string name in GameState.Instance.lastRun.deadBuddyNames)
                text += "☠ " + name + "\n";
        }

        if (string.IsNullOrWhiteSpace(text))
            text = emptyMessage;

        bodyText.text = text.TrimEnd();
    }
}
