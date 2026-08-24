using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SporeGobbo.Input;

public class BuddyChoiceScreen : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public TMP_InputField nameInput;
    public Button continueButton;

    [Header("Naming")]
    public string defaultPromptName = "Gobbo";
    public bool pauseGameWhileNaming = true;

    private SporeGrow currentSpore;

    void Awake()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(ConfirmName);
        }

        if (panel != null)
            panel.SetActive(false);
    }

    public void OpenForSpore(SporeGrow spore)
    {
        currentSpore = spore;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        bool gamepad = SporeInputReader.Instance != null &&
                       SporeInputReader.Instance.ActiveControlScheme == SporeGobbo.Input.SporeControlScheme.Gamepad;
        if (nameInput != null)
        {
            nameInput.gameObject.SetActive(true);
            nameInput.text = GetRandomBuddyName();
            if (!gamepad)
            {
                nameInput.Select();
                nameInput.ActivateInputField();
            }
        }

        if (continueButton != null)
            continueButton.interactable = true;

        SporeUiCoordinator.Instance.PushModal(this, ConfirmName, pauseGameWhileNaming,
            gamepad ? continueButton : nameInput);
    }

    void ConfirmName()
    {
        string chosenName = nameInput != null ? nameInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(chosenName))
            chosenName = GetRandomBuddyName();

        SporeGrow spore = currentSpore;
        currentSpore = null;

        Close();

        if (spore != null)
            spore.CompleteHatch(BuddyType.Baby, chosenName);
    }

    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);

        SporeUiCoordinator.Instance.PopModal(this);
    }

    string GetRandomBuddyName()
    {
        string[] names =
        {
            "Grub", "Pip", "Mug", "Bunk", "Snorp", "Wim",
            "Grot", "Bibble", "Nub", "Boil", "Lump", "Pickle",
            "Steven", "Maroo", "Bobby"
        };

        return names[Random.Range(0, names.Length)];
    }
}
