using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Camp portal as a clean camp interactable.
/// Distance checks, prompt display, and E input are handled by CampInteractionDetector.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CampRunPortal : MonoBehaviour, ICampInteractable
{
    [Header("Run Scene")]
    public string runSceneName = "SampleScene";

    [Header("Interaction")]
    public string interactPrompt = "Enter tunnel";

    [Header("Confirmation")]
    public bool useConfirmationPanel = true;
    public GameObject promptPanel;
    public TMPro.TMP_Text promptText;
    public UnityEngine.UI.Button goButton;
    public UnityEngine.UI.Button cancelButton;
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

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public void Interact(GobboController player)
    {
        currentPlayer = player;

        if (useConfirmationPanel)
        {
            if (promptOpen)
                HidePrompt();
            else
                ShowPrompt();

            return;
        }

        StartNextRun();
    }

    void HookButtons()
    {
        if (goButton != null)
        {
            goButton.onClick.RemoveAllListeners();
            goButton.onClick.AddListener(StartNextRun);

            TMPro.TMP_Text text = goButton.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (text != null)
                text.text = goButtonText;
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(HidePrompt);

            TMPro.TMP_Text text = cancelButton.GetComponentInChildren<TMPro.TMP_Text>(true);
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
        }
        else
        {
            Debug.Log("Camp portal confirmation missing. Starting next run directly.");
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

                BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>(FindObjectsInactive.Include);
                if (roster != null)
                    GameState.Instance.SaveRoster(roster);
            }

            if (beginRunSnapshotBeforeLeaving)
                GameState.Instance.BeginRunSnapshot();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(runSceneName);
    }
}
