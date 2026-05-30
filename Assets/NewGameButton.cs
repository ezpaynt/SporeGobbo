using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// New Game entry point. If a name prompt is assigned, clicking New Game opens it first.
/// If no prompt UI is assigned, it falls back to defaultPlayerName.
/// </summary>
public class NewGameButton : MonoBehaviour
{
    [Header("Defaults")]
    public string defaultPlayerName = "Gobbo";
    public string firstSceneName = "SampleScene";

    [Header("Optional Naming Prompt")]
    public GameObject namePromptPanel;
    public TMP_InputField playerNameInput;
    public Button confirmNameButton;
    public Button cancelNameButton;

    [Header("Buttons")]
    public Button newGameButton;
    public bool autoHookButton = true;

    [Header("Full Saves Popup")]
    public GameObject savesFullPanel;
    public TMP_Text savesFullText;
    public string savesFullMessage = "All 3 save slots are full. Delete a save before starting a new camp.";

    void Start()
    {
        if (namePromptPanel != null) namePromptPanel.SetActive(false);
        if (savesFullPanel != null) savesFullPanel.SetActive(false);
        if (autoHookButton) HookButtons();
    }

    void OnEnable()
    {
        if (autoHookButton) HookButtons();
    }

    void HookButtons()
    {
        if (newGameButton == null) newGameButton = GetComponent<Button>();
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(OnNewGameClicked);
            newGameButton.onClick.AddListener(OnNewGameClicked);
            newGameButton.interactable = true;
        }

        if (confirmNameButton != null)
        {
            confirmNameButton.onClick.RemoveListener(ConfirmNamedNewGame);
            confirmNameButton.onClick.AddListener(ConfirmNamedNewGame);
        }

        if (cancelNameButton != null)
        {
            cancelNameButton.onClick.RemoveListener(CancelNamePrompt);
            cancelNameButton.onClick.AddListener(CancelNamePrompt);
        }
    }

    public void OnNewGameClicked()
    {
        if (!SporeSaveManager.CanCreateNewGame())
        {
            ShowSavesFull();
            return;
        }

        if (namePromptPanel != null && playerNameInput != null)
        {
            playerNameInput.text = defaultPlayerName;
            namePromptPanel.SetActive(true);
            playerNameInput.Select();
            playerNameInput.ActivateInputField();
            return;
        }

        StartNewGameWithName(defaultPlayerName);
    }

    public void ConfirmNamedNewGame()
    {
        string name = defaultPlayerName;
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text)) name = playerNameInput.text.Trim();
        if (namePromptPanel != null) namePromptPanel.SetActive(false);
        StartNewGameWithName(name);
    }

    public void CancelNamePrompt()
    {
        if (namePromptPanel != null) namePromptPanel.SetActive(false);
    }

    // Kept for existing button hookups.
    public void StartNewGame()
    {
        OnNewGameClicked();
    }

    void StartNewGameWithName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) playerName = defaultPlayerName;
        SporeSaveSlotData data = SporeSaveManager.CreateNewGame(playerName.Trim(), firstSceneName);
        if (data == null)
        {
            ShowSavesFull();
            HookButtons();
            return;
        }

        Debug.Log("[NewGameButton] New game in slot " + data.slotIndex + " for " + playerName + ". Loading " + firstSceneName);
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstSceneName);
    }

    void ShowSavesFull()
    {
        if (savesFullPanel != null)
        {
            savesFullPanel.SetActive(true);
            if (savesFullText != null) savesFullText.text = savesFullMessage;
        }
        else
        {
            Debug.LogWarning("[NewGameButton] " + savesFullMessage);
        }
    }
}
