using TMPro;
using UnityEngine;

public class CampMessageUI : MonoBehaviour
{
    public static CampMessageUI Instance { get; private set; }

    [Header("UI")]
    public GameObject panel;
    public TMP_Text messageText;

    [Header("Timing")]
    public bool hideAutomatically = true;
    public float visibleSeconds = 3f;

    private float hideTimer = 0f;

    void Awake()
    {
        Instance = this;
        Hide();
    }

    void Update()
    {
        if (!hideAutomatically || hideTimer <= 0f)
            return;

        hideTimer -= Time.deltaTime;
        if (hideTimer <= 0f)
            Hide();
    }

    public static void Show(string message)
    {
        if (Instance != null)
        {
            Instance.ShowMessage(message);
            return;
        }

        Debug.Log("Camp message: " + message);
    }

    public void ShowMessage(string message)
    {
        if (messageText != null)
            messageText.text = message;

        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        hideTimer = visibleSeconds;
    }

    public void Hide()
    {
        hideTimer = 0f;

        if (panel != null)
            panel.SetActive(false);
    }
}
