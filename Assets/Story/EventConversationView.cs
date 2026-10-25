using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class EventConversationView : MonoBehaviour
{
    public Button ContinueButton { get; private set; }
    public CanvasGroup Group { get; private set; }
    private Action continued;

    public void Build(string speaker, string line, Action onContinue)
    {
        continued = onContinue;
        Group = gameObject.AddComponent<CanvasGroup>();
        RectTransform root = GetComponent<RectTransform>();
        if (root == null) throw new InvalidOperationException("EventConversationView requires a RectTransform.");
        root.anchorMin = new Vector2(0.2f, 0.08f);
        root.anchorMax = new Vector2(0.8f, 0.42f);
        root.offsetMin = root.offsetMax = Vector2.zero;
        Image background = gameObject.AddComponent<Image>();
        background.color = new Color(0.04f, 0.025f, 0.02f, 0.94f);

        Text speakerText = CreateText("Speaker", transform, 30, TextAnchor.MiddleLeft);
        SetRect(speakerText.rectTransform, new Vector2(0.06f, 0.7f), new Vector2(0.94f, 0.94f));
        speakerText.text = speaker;
        speakerText.fontStyle = FontStyle.Bold;

        Text lineText = CreateText("Line", transform, 27, TextAnchor.MiddleLeft);
        SetRect(lineText.rectTransform, new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.72f));
        lineText.text = "\u201c" + line + "\u201d";

        GameObject buttonObject = new GameObject("Continue", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        ContinueButton = buttonObject.GetComponent<Button>();
        buttonObject.GetComponent<Image>().color = new Color(0.72f, 0.61f, 0.35f, 1f);
        SetRect((RectTransform)buttonObject.transform, new Vector2(0.34f, 0.07f), new Vector2(0.66f, 0.3f));
        Text buttonText = CreateText("Label", buttonObject.transform, 24, TextAnchor.MiddleCenter);
        SetRect(buttonText.rectTransform, Vector2.zero, Vector2.one);
        buttonText.text = "CONTINUE";
        ContinueButton.onClick.AddListener(HandleContinue);
    }

    void HandleContinue()
    {
        ContinueButton.interactable = false;
        continued?.Invoke();
    }

    static Text CreateText(string name, Transform parent, int size, TextAnchor anchor)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Text));
        child.transform.SetParent(parent, false);
        Text text = child.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
