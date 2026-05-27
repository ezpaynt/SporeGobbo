using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Optional direct New Game button helper.
/// MainMenuController is preferred for the 3-slot UI, but this supports your current NewGameSystem object too.
/// It creates the first empty slot and refuses if all 3 are full.
/// </summary>
public class NewGameButton : MonoBehaviour
{
    [Header("New Game")]
    public string defaultPlayerName = "Gobbo";
    public string firstSceneName = "SampleScene";
    public TMP_InputField playerNameInput;
    public Button newGameButton;

    void Start() => HookButton();
    void OnEnable() => HookButton();

    void HookButton()
    {
        if (newGameButton == null) newGameButton = GetComponent<Button>();
        if (newGameButton == null) return;
        newGameButton.onClick.RemoveListener(StartNewGame);
        newGameButton.onClick.AddListener(StartNewGame);
        newGameButton.interactable = SporeSaveManager.HasOpenSlot();
    }

    public void StartNewGame()
    {
        string playerName = defaultPlayerName;
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text)) playerName = playerNameInput.text.Trim();

        SporeSaveSlotData save = SporeSaveManager.CreateNewGameInFirstEmptySlot(firstSceneName, playerName);
        if (save == null)
        {
            Debug.LogWarning("[NewGameButton] All three save slots are full. New game refused.");
            HookButton();
            return;
        }

        GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
        bridge.newGameSceneName = firstSceneName;
        bridge.SetCurrentSlotWithoutSaving(save.slotIndex, save.playerName, save.saveName, save.markedSuccessorId);

        Debug.Log("[NewGameButton] New game in slot " + save.slotIndex + " for " + playerName + ". Loading " + firstSceneName);
        SceneManager.LoadScene(firstSceneName);
    }
}
