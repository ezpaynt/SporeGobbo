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

    int pendingSlotIndex;

    public bool HasNamingPrompt => namePromptPanel != null && playerNameInput != null;

    [Header("Full Saves Popup")]
    public GameObject savesFullPanel;
    public TMP_Text savesFullText;
    public string savesFullMessage = "All 3 save slots are full. Delete a save before starting a new camp.";

    void Start()
    {
        if (namePromptPanel != null) namePromptPanel.SetActive(false);
        if (savesFullPanel != null) savesFullPanel.SetActive(false);
        HookButtons();
    }

    void OnEnable() => HookButtons();

    void HookButtons()
    {
        if (newGameButton == null) newGameButton = GetComponent<Button>();
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(OnNewGameClicked);
            if (autoHookButton) newGameButton.onClick.AddListener(OnNewGameClicked);
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
        BeginNamedNewGame(0);
    }

    public void BeginNamedNewGame(int slotIndex)
    {
        if (!SporeSaveManager.CanCreateNewGame())
        {
            ShowSavesFull();
            return;
        }

        pendingSlotIndex = slotIndex;
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
        pendingSlotIndex = 0;
    }

    // Kept for existing button hookups.
    public void StartNewGame()
    {
        OnNewGameClicked();
    }

    GameState EnsureGameStateForNewGame()
    {
        if (GameState.Instance != null)
            return GameState.Instance;

        GameObject stateObject = new GameObject("GameState");
        return stateObject.AddComponent<GameState>();
    }

    void StartNewGameWithName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) playerName = defaultPlayerName;
        SporeSaveSlotData data = pendingSlotIndex > 0
            ? SporeSaveManager.CreateNewGame(pendingSlotIndex, playerName.Trim(), firstSceneName, false)
            : SporeSaveManager.CreateNewGame(playerName.Trim(), firstSceneName);
        pendingSlotIndex = 0;
        if (data == null)
        {
            ShowSavesFull();
            HookButtons();
            return;
        }

        GameState state = EnsureGameStateForNewGame();
        state.SetLeaderName(playerName.Trim());

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
