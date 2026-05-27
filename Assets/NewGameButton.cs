using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Optional direct New Game button helper. MainMenuController slot flow is preferred.
public class NewGameButton : MonoBehaviour
{
    public string defaultPlayerName = "Gobbo";
    public string firstSceneName = "SampleScene";
    public TMP_InputField playerNameInput;
    public Button newGameButton;
    public bool autoHookButton = true;

    void Start() { if (autoHookButton) HookButton(); }
    void OnEnable() { if (autoHookButton) HookButton(); }

    void HookButton()
    {
        if (newGameButton == null) newGameButton = GetComponent<Button>();
        if (newGameButton == null) return;
        newGameButton.onClick.RemoveListener(StartNewGame);
        newGameButton.onClick.AddListener(StartNewGame);
        newGameButton.interactable = true;
    }

    public void StartNewGame()
    {
        string playerName = defaultPlayerName;
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
            playerName = playerNameInput.text.Trim();

        SporeSaveSlotData data = SporeSaveManager.CreateNewGame(playerName, firstSceneName);
        if (data == null)
        {
            Debug.LogWarning("[NewGameButton] New game blocked. All 3 save slots are full.");
            HookButton();
            return;
        }

        Debug.Log("[NewGameButton] New game in slot " + data.slotIndex + " for " + playerName + ". Loading " + firstSceneName);
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstSceneName);
    }
}
