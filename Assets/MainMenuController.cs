using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu controller for the real 3-slot save system.
/// New Game only uses empty slots. Continue/Load always restore GameState and open CampScene.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private enum SlotMode { NewGame, LoadGame }

    [Header("Scenes")]
    public string firstSceneName = "SampleScene";
    public string campSceneName = "CampScene";
    public string fallbackContinueSceneName = "CampScene"; // compatibility; Continue ignores old save scene data.

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject saveSlotPanel;
    public GameObject settingsPanel;
    public GameObject placeholderPanel;

    [Header("Main Buttons")]
    public Button newGameButton;
    public Button continueButton;
    public Button loadGameButton;
    public Button collectionBookButton;
    public Button gobboDeedsButton;
    public Button shopButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Save Slot Buttons")]
    public Button slotButton1;
    public Button slotButton2;
    public Button slotButton3;
    public Button backButton;

    [Header("Optional Text")]
    public TMP_Text titleText;
    public TMP_Text placeholderText;

    [Header("Options")]
    public bool autoFindReferences = true;
    public bool disableFutureButtons = false;
    public string defaultPlayerName = "Gobbo";

    private SlotMode currentSlotMode = SlotMode.LoadGame;

    void Awake()
    {
        if (autoFindReferences) AutoFindReferences();
        HookButtons();
    }

    void Start()
    {
        Time.timeScale = 1f;
        ShowMainMenu();
        RefreshContinueButton();
    }

    void AutoFindReferences()
    {
        if (mainMenuPanel == null) mainMenuPanel = GameObject.Find("MainMenuPanel");
        if (saveSlotPanel == null) saveSlotPanel = GameObject.Find("SaveSlotPanel");
        if (settingsPanel == null) settingsPanel = GameObject.Find("SettingsPanel");
        if (placeholderPanel == null) placeholderPanel = GameObject.Find("PlaceholderPanel");

        if (newGameButton == null) newGameButton = FindButton("NewGameButton");
        if (continueButton == null) continueButton = FindButton("ContinueButton");
        if (loadGameButton == null) loadGameButton = FindButton("LoadGameButton");
        if (collectionBookButton == null) collectionBookButton = FindButton("CollectionBookButton");
        if (gobboDeedsButton == null) gobboDeedsButton = FindButton("GobboDeedsButton");
        if (shopButton == null) shopButton = FindButton("ShopButton");
        if (settingsButton == null) settingsButton = FindButton("SettingsButton");
        if (quitButton == null) quitButton = FindButton("QuitButton");

        if (slotButton1 == null) slotButton1 = FindButton("SlotButton1");
        if (slotButton2 == null) slotButton2 = FindButton("SlotButton2");
        if (slotButton3 == null) slotButton3 = FindButton("SlotButton3");
        if (backButton == null) backButton = FindButton("BackButton");

        if (titleText == null)
        {
            GameObject foundTitle = GameObject.Find("TitleText");
            if (foundTitle != null) titleText = foundTitle.GetComponent<TMP_Text>();
        }

        if (placeholderText == null && placeholderPanel != null)
            placeholderText = placeholderPanel.GetComponentInChildren<TMP_Text>(true);
    }

    Button FindButton(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    void HookButtons()
    {
        Hook(newGameButton, OpenNewGameSlots);
        Hook(continueButton, ContinueLastGame);
        Hook(loadGameButton, OpenLoadGameSlots);
        Hook(collectionBookButton, () => ShowPlaceholder("Collection Book coming soon."));
        Hook(gobboDeedsButton, () => ShowPlaceholder("Gobbo Deeds coming soon."));
        Hook(shopButton, () => ShowPlaceholder("Shop coming soon."));
        Hook(settingsButton, ShowSettings);
        Hook(quitButton, QuitGame);
        Hook(slotButton1, () => ChooseSlot(1));
        Hook(slotButton2, () => ChooseSlot(2));
        Hook(slotButton3, () => ChooseSlot(3));
        Hook(backButton, ShowMainMenu);
    }

    void Hook(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    void RefreshContinueButton()
    {
        if (continueButton != null) continueButton.interactable = SporeSaveManager.LoadLastPlayedSlot() != null;
        if (newGameButton != null) newGameButton.interactable = SporeSaveManager.CanCreateNewGame();

        if (disableFutureButtons)
        {
            if (collectionBookButton != null) collectionBookButton.interactable = false;
            if (gobboDeedsButton != null) gobboDeedsButton.interactable = false;
            if (shopButton != null) shopButton.interactable = false;
        }
    }

    public void ShowMainMenu()
    {
        SetPanel(mainMenuPanel, true);
        SetPanel(saveSlotPanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(placeholderPanel, false);
        RefreshContinueButton();
    }

    public void OpenNewGameSlots()
    {
        if (!SporeSaveManager.CanCreateNewGame())
        {
            ShowPlaceholder("All 3 save slots are full. Delete a save before starting another gobbo camp.");
            return;
        }

        currentSlotMode = SlotMode.NewGame;
        OpenSlotPanel();
    }

    public void OpenLoadGameSlots()
    {
        currentSlotMode = SlotMode.LoadGame;
        OpenSlotPanel();
    }

    void OpenSlotPanel()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(saveSlotPanel, true);
        SetPanel(settingsPanel, false);
        SetPanel(placeholderPanel, false);
        RefreshSlotButtons();
    }

    void RefreshSlotButtons()
    {
        RefreshSlotButton(slotButton1, 1);
        RefreshSlotButton(slotButton2, 2);
        RefreshSlotButton(slotButton3, 3);
    }

    void RefreshSlotButton(Button button, int slotIndex)
    {
        if (button == null) return;

        SporeSaveSlotData data = SporeSaveManager.LoadSlot(slotIndex);
        bool hasSave = data != null && data.hasSave;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            if (currentSlotMode == SlotMode.NewGame)
                label.text = hasSave ? data.GetButtonLabel() + "\nFull" : "Slot " + slotIndex + " — New Game";
            else
                label.text = hasSave ? data.GetButtonLabel() : "Slot " + slotIndex + " — Empty";
        }

        button.interactable = currentSlotMode == SlotMode.NewGame ? !hasSave : hasSave;
    }

    void ChooseSlot(int slotIndex)
    {
        if (currentSlotMode == SlotMode.NewGame)
        {
            StartNewGame(slotIndex);
            return;
        }

        LoadGame(slotIndex);
    }

    public void StartNewGame(int slotIndex)
    {
        if (SporeSaveManager.HasSave(slotIndex))
        {
            ShowPlaceholder("That slot already has a camp. New games only use empty slots for now.");
            return;
        }

        SporeSaveSlotData data = SporeSaveManager.CreateNewGame(slotIndex, defaultPlayerName, firstSceneName, false);
        if (data == null)
        {
            ShowPlaceholder("No empty save slots are available.");
            return;
        }

        Debug.Log("[MainMenuController] New game in slot " + slotIndex + " for " + data.playerName + ". Loading " + firstSceneName);
        SceneManager.LoadScene(firstSceneName);
    }

    public void ContinueLastGame()
    {
        SporeSaveSlotData data = SporeSaveManager.LoadLastPlayedSlot();
        if (data == null || !data.hasSave)
        {
            ShowPlaceholder("No save found yet.");
            return;
        }

        LoadSlotToCamp(data.slotIndex);
    }

    public void LoadGame(int slotIndex)
    {
        SporeSaveSlotData data = SporeSaveManager.LoadSlot(slotIndex);
        if (data == null || !data.hasSave)
        {
            ShowPlaceholder("That slot is empty.");
            return;
        }

        LoadSlotToCamp(slotIndex);
    }

    void LoadSlotToCamp(int slotIndex)
    {
        if (!SporeSaveManager.LoadSlotIntoGameState(slotIndex))
        {
            ShowPlaceholder("Could not load that save.");
            return;
        }

        Debug.Log("[MainMenuController] Loaded slot " + slotIndex + ". Loading camp.");
        Time.timeScale = 1f;
        SceneManager.LoadScene(campSceneName);
    }

    // Compatibility for old button hookups. Continue/Load never use saved nextSceneName now.
    void LoadSceneFromSave(SporeSaveSlotData data, string fallbackScene)
    {
        if (data == null || !data.hasSave) return;
        LoadSlotToCamp(data.slotIndex);
    }

    public void ShowSettings()
    {
        if (settingsPanel != null)
        {
            SetPanel(mainMenuPanel, false);
            SetPanel(saveSlotPanel, false);
            SetPanel(settingsPanel, true);
            SetPanel(placeholderPanel, false);
            return;
        }

        ShowPlaceholder("Settings coming soon.");
    }

    public void ShowPlaceholder(string message)
    {
        SetPanel(mainMenuPanel, true);
        SetPanel(saveSlotPanel, false);
        SetPanel(settingsPanel, false);

        if (placeholderPanel != null)
        {
            placeholderPanel.SetActive(true);
            placeholderPanel.transform.SetAsLastSibling();
        }

        if (placeholderText != null) placeholderText.text = message;
        Debug.Log(message);
    }

    public void QuitGame()
    {
        Debug.Log("Quit game requested.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetPanel(GameObject panel, bool visible)
    {
        if (panel != null) panel.SetActive(visible);
    }
}
