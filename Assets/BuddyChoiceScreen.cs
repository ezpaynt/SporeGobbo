using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        if (nameInput != null)
        {
            nameInput.gameObject.SetActive(true);
            nameInput.text = GetRandomBuddyName();
            nameInput.Select();
            nameInput.ActivateInputField();
        }

        if (continueButton != null)
            continueButton.interactable = true;

        if (pauseGameWhileNaming)
            Time.timeScale = 0f;
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

        if (pauseGameWhileNaming)
            Time.timeScale = 1f;
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
