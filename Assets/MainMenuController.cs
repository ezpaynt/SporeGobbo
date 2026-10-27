using TMPro;

using UnityEngine;

using UnityEngine.SceneManagement;

using UnityEngine.UI;



/// Main menu controller for the real 3-slot save system.

/// New Game only uses empty slots. Continue/Load always restore GameState and open CampScene.

public class MainMenuController : MonoBehaviour

{

    private enum SlotMode { NewGame, LoadGame }



    [Header("Scenes")]

    public string firstSceneName = "SampleScene";

    public string campSceneName = "CampScene";

    public string fallbackContinueSceneName = "CampScene";



    [Header("Panels")]

    public GameObject mainMenuPanel;

    public GameObject saveSlotPanel;

    public GameObject settingsPanel;

    public GameObject placeholderPanel;



    [Header("Optional Delete Confirm Panel")]

    public GameObject deleteConfirmPanel;

    public TMP_Text deleteConfirmText;

    public Button confirmDeleteButton;

    public Button cancelDeleteButton;



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



    [Header("Optional Delete Slot Buttons")]

    public Button deleteSlotButton1;

    public Button deleteSlotButton2;

    public Button deleteSlotButton3;



    [Header("Optional Text")]

    public TMP_Text titleText;

    public TMP_Text placeholderText;



    [Header("Options")]

    public bool autoFindReferences = true;

    public bool disableFutureButtons = false;

    public string defaultPlayerName = "Gobbo";



    private SlotMode currentSlotMode = SlotMode.LoadGame;

    private int pendingDeleteSlot = 0;



    void Awake()

    {

        CampArrivalContext.Clear();

        if (autoFindReferences) AutoFindReferences();

        HookButtons();

    }



    void Start()

    {

        SporePauseService.ResetAll();

        ShowMainMenu();

        RefreshContinueButton();

    }



    void AutoFindReferences()

    {

        if (mainMenuPanel == null) mainMenuPanel = GameObject.Find("MainMenuPanel");

        if (saveSlotPanel == null) saveSlotPanel = GameObject.Find("SaveSlotPanel");

        if (settingsPanel == null) settingsPanel = GameObject.Find("SettingsPanel");

        if (placeholderPanel == null) placeholderPanel = GameObject.Find("PlaceholderPanel");

        if (deleteConfirmPanel == null) deleteConfirmPanel = GameObject.Find("DeleteConfirmPanel");



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

        if (deleteSlotButton1 == null) deleteSlotButton1 = FindButton("DeleteSlotButton1");

        if (deleteSlotButton2 == null) deleteSlotButton2 = FindButton("DeleteSlotButton2");

        if (deleteSlotButton3 == null) deleteSlotButton3 = FindButton("DeleteSlotButton3");

        if (confirmDeleteButton == null) confirmDeleteButton = FindButton("ConfirmDeleteButton");

        if (cancelDeleteButton == null) cancelDeleteButton = FindButton("CancelDeleteButton");



        if (placeholderText == null && placeholderPanel != null)

            placeholderText = placeholderPanel.GetComponentInChildren<TMP_Text>(true);

        if (deleteConfirmText == null && deleteConfirmPanel != null)

            deleteConfirmText = deleteConfirmPanel.GetComponentInChildren<TMP_Text>(true);

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

        Hook(deleteSlotButton1, () => AskDeleteSlot(1));

        Hook(deleteSlotButton2, () => AskDeleteSlot(2));

        Hook(deleteSlotButton3, () => AskDeleteSlot(3));

        Hook(confirmDeleteButton, ConfirmDeleteSlot);

        Hook(cancelDeleteButton, CancelDeleteSlot);

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

        if (newGameButton != null) newGameButton.interactable = true; // clicking can show the full-slots message

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

        SetPanel(deleteConfirmPanel, false);

        RefreshContinueButton();

        UiFocusUtility.Select(UiFocusUtility.IsValid(continueButton) ? continueButton : newGameButton);

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

        SetPanel(deleteConfirmPanel, false);

        RefreshSlotButtons();

        UiFocusUtility.Select(UiFocusUtility.FindFirst(saveSlotPanel, slotButton1));

    }



    void RefreshSlotButtons()

    {

        RefreshSlotButton(slotButton1, deleteSlotButton1, 1);

        RefreshSlotButton(slotButton2, deleteSlotButton2, 2);

        RefreshSlotButton(slotButton3, deleteSlotButton3, 3);

    }



    void RefreshSlotButton(Button slotButton, Button deleteButton, int slotIndex)

    {

        SporeSaveSlotData data = SporeSaveManager.LoadSlot(slotIndex);

        bool hasSave = data != null && data.hasSave;

        var status = SporeSaveManager.InspectSlot(slotIndex);

        bool occupied = status != SporeSaveManager.SlotStatus.Missing;



        if (slotButton != null)

        {

            TMP_Text label = slotButton.GetComponentInChildren<TMP_Text>(true);

            if (label != null)

            {

                if (currentSlotMode == SlotMode.NewGame)

                    label.text = occupied ? (hasSave ? data.GetButtonLabel() : "Slot " + slotIndex + " — " + status) + "\nFull" : "Slot " + slotIndex + " — New Game";

                else

                    label.text = hasSave ? data.GetButtonLabel() : "Slot " + slotIndex + " — " + (status == SporeSaveManager.SlotStatus.RecoveryAvailable ? "Recover backup" : occupied ? status.ToString() : "Empty");

                label.text = (V1RuntimeProfile.DevelopmentStorage ? "DEVELOPMENT " + (char)('A' + slotIndex - 1) : "V1") + "\n" + label.text;

            }

            slotButton.interactable = currentSlotMode == SlotMode.NewGame ? !occupied : hasSave || status == SporeSaveManager.SlotStatus.RecoveryAvailable;

        }



        if (deleteButton != null)

        {

            deleteButton.gameObject.SetActive(currentSlotMode == SlotMode.LoadGame && hasSave);

            deleteButton.interactable = currentSlotMode == SlotMode.LoadGame && hasSave;

            TMP_Text deleteLabel = deleteButton.GetComponentInChildren<TMP_Text>(true);

            if (deleteLabel != null) deleteLabel.text = "Delete";

        }

    }



    void ChooseSlot(int slotIndex)

    {

        if (currentSlotMode == SlotMode.NewGame) StartNewGame(slotIndex);

        else LoadGame(slotIndex);

    }



    public void StartNewGame(int slotIndex)

    {

        if (SporeSaveManager.InspectSlot(slotIndex) != SporeSaveManager.SlotStatus.Missing)

        {

            ShowPlaceholder("That slot contains preserved campaign data. Choose an empty V1 slot.");

            return;

        }



        NewGameButton namingFlow = FindAnyObjectByType<NewGameButton>(FindObjectsInactive.Include);

        if (namingFlow != null && namingFlow.HasNamingPrompt)

        {

            namingFlow.BeginNamedNewGame(slotIndex);

            return;

        }



        SporeSaveSlotData data = SporeSaveManager.CreateNewGame(slotIndex, defaultPlayerName, firstSceneName, false);

        if (data == null)

        {

            ShowPlaceholder("No empty save slots are available.");

            return;

        }



        Debug.Log("[MainMenuController] New game in slot " + slotIndex + " for " + data.playerName + ". Loading " + firstSceneName);

        CampArrivalContext.Clear();

        SampleSceneModeContext.SetPending(SampleSceneMode.Intro);

        SporePauseService.ResetAll();

        SceneManager.LoadScene(GameSceneCatalog.ResolveIntro(firstSceneName));

    }



    public void ContinueLastGame()

    {

        SporeSaveSlotData data = SporeSaveManager.LoadLastPlayedSlot();

        if (data == null || !data.hasSave)

        {

            ShowPlaceholder("No readable V1 campaign was found. Use Load Game to inspect slots or recover an available backup. Historical campaigns remain separate.");

            return;

        }

        LoadSlotToCamp(data.slotIndex);

    }



    public void LoadGame(int slotIndex)

    {

        if (SporeSaveManager.InspectSlot(slotIndex) == SporeSaveManager.SlotStatus.RecoveryAvailable)
        {
            try { SporeSaveManager.RecoverSlot(slotIndex); }
            catch (System.Exception error) { Debug.LogWarning("[Campaign recovery] " + error.Message); ShowPlaceholder("Backup recovery failed. Existing files are preserved."); return; }
        }

        SporeSaveSlotData data = SporeSaveManager.LoadSlot(slotIndex);

        if (data == null || !data.hasSave)

        {

            ShowPlaceholder("Slot " + slotIndex + " — " + SporeSaveManager.InspectSlot(slotIndex) + ". Existing files are preserved.");

            return;

        }

        LoadSlotToCamp(slotIndex);

    }



    void LoadSlotToCamp(int slotIndex)

    {

        SporeSaveSlotData candidate = SporeSaveManager.LoadSlot(slotIndex);

        if (candidate != null && candidate.hasSave && candidate.lineageEnded)

        {

            CampArrivalContext.Clear();

            ShowPlaceholder("That lineage has ended. Start a New Game in an empty slot.");

            return;

        }

        if (!SporeSaveManager.LoadSlotIntoGameState(slotIndex))

        {

            ShowPlaceholder("Could not load that save.");

            return;

        }

        SporeSaveSlotData loaded = SporeSaveManager.LoadSlot(slotIndex);

        bool loadIntro = SporeSaveManager.ShouldLoadIntro(loaded);

        string destination = loadIntro ? GameSceneCatalog.ResolveIntro(firstSceneName) : campSceneName;

        if (loadIntro)

        {

            CampArrivalContext.Clear();

            SampleSceneModeContext.SetPending(SampleSceneMode.Intro);

        }

        else if (!CampArrivalContext.HasPending) CampArrivalContext.SetPending(CampArrivalMode.LoadedSave);

        Debug.Log("[MainMenuController] Loaded slot " + slotIndex + ". Loading " + destination + ".");

        SporePauseService.ResetAll();

        SceneManager.LoadScene(GameSceneCatalog.Resolve(destination));

    }



    public void AskDeleteSlot(int slotIndex)

    {

        SporeSaveSlotData data = SporeSaveManager.LoadSlot(slotIndex);

        if (data == null || !data.hasSave) return;

        pendingDeleteSlot = slotIndex;

        if (deleteConfirmText != null)

            deleteConfirmText.text = "Delete " + data.saveName + "?\nThis cannot be undone.";

        if (deleteConfirmPanel != null)

        {

            deleteConfirmPanel.SetActive(true);

            deleteConfirmPanel.transform.SetAsLastSibling();

            SporeUiCoordinator.Instance.PushModal(this, CancelDeleteSlot, false, cancelDeleteButton);

        }

        else

        {

            ConfirmDeleteSlot();

        }

    }



    public void ConfirmDeleteSlot()

    {

        if (pendingDeleteSlot <= 0) return;

        SporeSaveManager.DeleteSlot(pendingDeleteSlot);

        pendingDeleteSlot = 0;

        SetPanel(deleteConfirmPanel, false);

        SporeUiCoordinator.Instance.PopModal(this);

        RefreshSlotButtons();

        RefreshContinueButton();

    }



    public void CancelDeleteSlot()

    {

        pendingDeleteSlot = 0;

        SetPanel(deleteConfirmPanel, false);

        SporeUiCoordinator.Instance.PopModal(this);

    }



    // Compatibility for old button hookups. Continue/Load never use saved nextSceneName now.

    void LoadSceneFromSave(SporeSaveSlotData data, string fallbackScene)

    {

        if (data == null || !data.hasSave) return;

        LoadSlotToCamp(data.slotIndex);

    }



    public void ShowSettings()

    {

        SetPanel(mainMenuPanel, false);

        SetPanel(saveSlotPanel, false);

        SetPanel(settingsPanel, false);

        SetPanel(placeholderPanel, false);

        SetPanel(deleteConfirmPanel, false);

        SporeControlsScreen.Open(ReturnFromSettings);

    }



    void ReturnFromSettings()

    {

        ShowMainMenu();

        UiFocusUtility.Select(settingsButton);

    }



    public void ShowPlaceholder(string message)

    {

        SetPanel(mainMenuPanel, true);

        SetPanel(saveSlotPanel, false);

        SetPanel(settingsPanel, false);

        SetPanel(deleteConfirmPanel, false);

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

