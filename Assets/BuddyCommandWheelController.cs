using System.Collections.Generic;
using SporeGobbo.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuddyCommandWheelController : MonoBehaviour
{
    private const float DeadZone = 0.28f;
    private const float FirstSliceAngle = 90f;

    private readonly BuddyCommand[] commands =
    {
        BuddyCommand.Follow,
        BuddyCommand.Stay,
        BuddyCommand.Aggressive,
        BuddyCommand.Passive
    };

    private readonly CommandWheelStateModel state = new();
    private readonly List<Image> sliceBackgrounds = new();
    private readonly List<TMP_Text> sliceLabels = new();
    private GobboController player;
    private SporeInputReader inputReader;
    private Canvas canvas;
    private RectTransform wheelRoot;

    public bool IsOpen => state.IsOpen;
    public static BuddyCommandWheelController Active { get; private set; }

    public void Configure(GobboController owner)
    {
        player = owner;
        inputReader = SporeInputReader.Instance;
        EnsureUi();
    }

    void Update()
    {
        if (player == null || player.IsDead)
        {
            CancelWithoutCommand();
            return;
        }

        if (inputReader == null) inputReader = SporeInputReader.Instance;
        if (inputReader == null) return;

        if (state.AwaitingOpenRelease && !inputReader.GameplayButtonsSuppressed && !inputReader.CommandWheel.IsHeld)
            state.NotifyOpenReleased();

        if (!state.IsOpen)
        {
            if (inputReader.Context == SporeInputContext.Gameplay && inputReader.CommandWheel.StartedThisFrame)
                Open();
            return;
        }

        if (inputReader.Context != SporeInputContext.Wheel)
        {
            CancelWithoutCommand(false);
            return;
        }

        UpdateSelection();

        if (inputReader.WheelCancel.StartedThisFrame)
        {
            CancelWithoutCommand();
            return;
        }

        if (inputReader.CommandWheel.ReleasedThisFrame)
        {
            state.ReleaseWithoutConfirm();
            CloseUiAndReturnToGameplay();
            return;
        }

        if (inputReader.WheelConfirm.StartedThisFrame)
            Confirm();
    }

    public void CancelWithoutCommand() => CancelWithoutCommand(true);

    public void CancelWithoutCommand(bool returnToGameplay)
    {
        if (!state.IsOpen) return;
        state.Cancel();
        if (Active == this) Active = null;
        SetVisible(false);
        if (returnToGameplay && inputReader != null && inputReader.Context == SporeInputContext.Wheel)
            inputReader.SetContext(SporeInputContext.Gameplay);
    }

    void Open()
    {
        if (!state.TryOpen()) return;
        EnsureUi();
        inputReader.Buffer.Clear();
        inputReader.SetContext(SporeInputContext.Wheel);
        Active = this;
        SetVisible(true);
        RefreshVisuals();
    }

    void UpdateSelection()
    {
        Vector2 selection;
        if (inputReader.ActiveControlScheme == SporeControlScheme.Gamepad)
            selection = inputReader.WheelSelectStick;
        else
            selection = inputReader.WheelSelectPointer - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        int selected = RadialSelectionMath.GetSlice(selection, DeadZoneFor(selection), commands.Length, FirstSliceAngle);
        if (selected >= 0) state.Select(selected);
        RefreshVisuals();
    }

    float DeadZoneFor(Vector2 selection)
    {
        return inputReader.ActiveControlScheme == SporeControlScheme.Gamepad
            ? DeadZone
            : 65f;
    }

    void Confirm()
    {
        int selected = state.Confirm();
        if (selected < 0 || selected >= commands.Length) return;
        player.IssueBuddyCommand(commands[selected]);
        CloseUiAndReturnToGameplay();
    }

    void CloseUiAndReturnToGameplay()
    {
        SetVisible(false);
        if (Active == this) Active = null;
        if (inputReader != null && inputReader.Context == SporeInputContext.Wheel)
            inputReader.SetContext(SporeInputContext.Gameplay);
    }

    void EnsureUi()
    {
        if (canvas != null) return;

        GameObject canvasObject = new("BuddyCommandWheelCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObject = new("Wheel", typeof(RectTransform), typeof(Image));
        wheelRoot = rootObject.GetComponent<RectTransform>();
        wheelRoot.SetParent(canvas.transform, false);
        wheelRoot.anchorMin = wheelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        wheelRoot.sizeDelta = new Vector2(520f, 520f);
        rootObject.GetComponent<Image>().color = new Color(0.02f, 0.08f, 0.04f, 0.82f);

        float step = 360f / commands.Length;
        for (int i = 0; i < commands.Length; i++)
        {
            float angle = (FirstSliceAngle + step * i) * Mathf.Deg2Rad;
            Vector2 position = new(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject slice = new("Slice_" + commands[i], typeof(RectTransform), typeof(Image));
            RectTransform rect = slice.GetComponent<RectTransform>();
            rect.SetParent(wheelRoot, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position * 165f;
            rect.sizeDelta = new Vector2(175f, 92f);
            Image background = slice.GetComponent<Image>();
            sliceBackgrounds.Add(background);

            GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 25f;
            label.color = new Color(0.94f, 0.91f, 0.79f);
            label.text = Label(commands[i]);
            sliceLabels.Add(label);
        }

        GameObject center = new("DeadZone", typeof(RectTransform), typeof(Image));
        RectTransform centerRect = center.GetComponent<RectTransform>();
        centerRect.SetParent(wheelRoot, false);
        centerRect.anchorMin = centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.sizeDelta = new Vector2(115f, 115f);
        center.GetComponent<Image>().color = new Color(0.04f, 0.12f, 0.06f, 1f);
        SetVisible(false);
    }

    void RefreshVisuals()
    {
        for (int i = 0; i < commands.Length; i++)
        {
            bool highlighted = state.SelectedIndex == i;
            bool active = player != null && player.IsBuddyCommandActive(commands[i]);
            sliceBackgrounds[i].color = highlighted
                ? new Color(0.78f, 0.58f, 0.12f, 0.98f)
                : active ? new Color(0.12f, 0.46f, 0.2f, 0.95f)
                : new Color(0.08f, 0.2f, 0.11f, 0.94f);
            sliceLabels[i].text = Label(commands[i]) + (active ? "\nACTIVE" : "");
        }
    }

    void SetVisible(bool visible)
    {
        if (canvas != null) canvas.gameObject.SetActive(visible);
    }

    static string Label(BuddyCommand command)
    {
        return command == BuddyCommand.Aggressive ? "BITE" : command.ToString().ToUpperInvariant();
    }

    void OnDisable() => CancelWithoutCommand();
    void OnDestroy() => CancelWithoutCommand();
}
