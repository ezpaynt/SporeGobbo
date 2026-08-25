using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Terminal presentation for a lineage with no valid successor.</summary>
public sealed class LineageGameOverScreen : MonoBehaviour
{
    const string ObjectName = "LineageGameOverScreen";
    Button returnButton;

    public static void Show()
    {
        LineageGameOverScreen existing = Object.FindAnyObjectByType<LineageGameOverScreen>();
        if (existing != null)
        {
            existing.Open();
            return;
        }

        GameObject root = new GameObject(ObjectName, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LineageGameOverScreen));
        root.GetComponent<LineageGameOverScreen>().Build();
    }

    void Build()
    {
        EnsureEventSystem();

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image backdrop = CreateImage("Backdrop", transform, new Color(0.025f, 0.02f, 0.03f, 0.97f));
        Stretch(backdrop.rectTransform);

        Text title = CreateText("Title", transform, "GAME OVER", 72, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(900f, 120f));

        Text message = CreateText("Message", transform,
            "No gobbos are left to continue this lineage.", 34, TextAnchor.MiddleCenter);
        SetRect(message.rectTransform, new Vector2(0.5f, 0.49f), new Vector2(1100f, 100f));

        Image buttonImage = CreateImage("ReturnToMainMenuButton", transform, new Color(0.25f, 0.18f, 0.28f, 1f));
        SetRect(buttonImage.rectTransform, new Vector2(0.5f, 0.34f), new Vector2(420f, 90f));
        returnButton = buttonImage.gameObject.AddComponent<Button>();
        returnButton.targetGraphic = buttonImage;
        returnButton.onClick.AddListener(ReturnToMainMenu);
        Text label = CreateText("Label", buttonImage.transform, "Return to Main Menu", 30, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);

        Open();
    }

    void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        SporeUiCoordinator.Instance.PushModal(this, null, true, returnButton);
    }

    public void ReturnToMainMenu()
    {
        CampArrivalContext.Clear();
        PlayerDeathRunStore.Instance?.ClearPendingDeath();
        if (SporeUiCoordinator.Instance != null) SporeUiCoordinator.Instance.PopModal(this, false);
        SporePauseService.ResetAll();
        SceneManager.LoadScene("MainMenu");
    }

    void OnDestroy()
    {
        if (SporeUiCoordinator.Instance != null) SporeUiCoordinator.Instance.PopModal(this, false);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EventSystem.current = events.GetComponent<EventSystem>();
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }
}
