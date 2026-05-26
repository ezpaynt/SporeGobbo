using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampRunPortal : MonoBehaviour, ICampInteractable
{
    [Header("Run Scene")]
    public string runSceneName = "SampleScene";

    [Header("Interaction")]
    public string interactPrompt = "Enter tunnel";

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

    void Update()
    {
        if (promptOpen && Input.GetKeyDown(KeyCode.Escape))
            HidePrompt();
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public void Interact(GobboController player)
    {
        currentPlayer = player;

        if (!useConfirmationPanel)
        {
            StartNextRun();
            return;
        }

        if (!promptOpen)
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
        CampMenuModal.Open(currentPlayer, this, HidePrompt);

        if (promptText != null)
            promptText.text = promptMessage;

        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
            promptPanel.transform.SetAsLastSibling();
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

        CampMenuModal.Close(this);
    }

    public void StartNextRun()
    {
        CampMenuModal.Close(this);

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
