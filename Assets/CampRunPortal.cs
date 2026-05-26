using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Clean camp portal interactable.
/// CampInteractionDetector handles range, prompt, and E input.
/// This script only opens/closes the portal confirmation UI or starts the run.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CampRunPortal : MonoBehaviour, ICampInteractable
{
    [Header("Run Scene")]
    public string runSceneName = "SampleScene";

    [Header("Interaction")]
    public string interactPrompt = "Enter tunnel";
    public string closePrompt = "Close tunnel menu";

    [Header("Confirmation UI")]
    public bool useConfirmationPanel = true;
    public GameObject promptPanel;
    public TMP_Text promptText;
    public Button goButton;
    public Button cancelButton;
    public string promptMessage = "To the next cave?";
    public string goButtonText = "Go";
    public string cancelButtonText = "Not yet";

    [Header("Save")]
    public bool savePlayerBeforeLeaving = true;
    public bool beginRunSnapshotBeforeLeaving = true;

    private GobboController currentPlayer;
    private bool promptOpen = false;

    void Awake()
    {
        HookButtons();
        HidePrompt();
    }

    void Start()
    {
        HookButtons();
        HidePrompt();
    }

    void Update()
    {
        if (promptOpen && Input.GetKeyDown(KeyCode.Escape))
            HidePrompt();
    }

    public string GetInteractPrompt()
    {
        return promptOpen ? closePrompt : interactPrompt;
    }

    public void Interact(GobboController player)
    {
        currentPlayer = player;

        if (!useConfirmationPanel)
        {
            StartNextRun();
            return;
        }

        if (promptOpen)
            HidePrompt();
        else
            ShowPrompt();
    }

    void HookButtons()
    {
        if (goButton != null)
        {
            goButton.onClick.RemoveAllListeners();
            goButton.onClick.AddListener(StartNextRun);

            TMP_Text text = goButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = goButtonText;
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(HidePrompt);

            TMP_Text text = cancelButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = cancelButtonText;
        }
    }

    void ShowPrompt()
    {
        promptOpen = true;

        if (promptText != null)
            promptText.text = promptMessage;

        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
            promptPanel.transform.SetAsLastSibling();
            Debug.Log("Opened portal menu.", this);
        }
        else
        {
            Debug.LogWarning("CampRunPortal has no confirmation Prompt Panel assigned. Starting next run directly.", this);
            StartNextRun();
        }
    }

    void HidePrompt()
    {
        promptOpen = false;

        if (promptPanel != null)
            promptPanel.SetActive(false);
    }

    public void StartNextRun()
    {
        if (GameState.Instance != null)
        {
            if (savePlayerBeforeLeaving)
            {
                GobboController playerController = currentPlayer != null
                    ? currentPlayer
                    : Object.FindAnyObjectByType<GobboController>();

                if (playerController != null)
                    GameState.Instance.SavePlayer(playerController);

                GameState.Instance.RepairRosterState();
            }

            if (beginRunSnapshotBeforeLeaving)
                GameState.Instance.BeginRunSnapshot();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(runSceneName);
    }
}
