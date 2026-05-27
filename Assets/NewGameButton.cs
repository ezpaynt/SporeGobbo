using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Put this on a Main Menu object or on the New Game button.
/// For now it can use a default name because you do not have name-entry UI yet.
/// Later, assign playerNameInput and it will use the typed name.
/// </summary>
public class NewGameButton : MonoBehaviour
{
    [Header("New Game")]
    public string defaultPlayerName = "Gobbo";
    public string firstSceneName = "SampleScene";
    public TMP_InputField playerNameInput;
    public Button newGameButton;

    void Start()
    {
        HookButton();
    }

    void OnEnable()
    {
        HookButton();
    }

    void HookButton()
    {
        if (newGameButton == null) newGameButton = GetComponent<Button>();
        if (newGameButton == null) return;
        newGameButton.onClick.RemoveListener(StartNewGame);
        newGameButton.onClick.AddListener(StartNewGame);
    }

    public void StartNewGame()
    {
        string playerName = defaultPlayerName;
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text)) playerName = playerNameInput.text.Trim();

        GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
        bridge.newGameSceneName = firstSceneName;
        bridge.CreateNewGame(playerName);

        Debug.Log("[NewGameButton] New game for " + playerName + ". Loading " + firstSceneName);
        SceneManager.LoadScene(firstSceneName);
    }
}
