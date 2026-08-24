using System;
using System.Collections.Generic;
using SporeGobbo.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SporeControlsScreen : MonoBehaviour
{
    readonly struct RowSpec
    {
        public RowSpec(string label, string action, string part = null, bool keyboardEditable = true, bool gamepadEditable = true)
        { Label = label; Action = action; Part = part; KeyboardEditable = keyboardEditable; GamepadEditable = gamepadEditable; }
        public string Label { get; } public string Action { get; } public string Part { get; }
        public bool KeyboardEditable { get; } public bool GamepadEditable { get; }
    }

    static readonly RowSpec[] Rows = {
        new("Move (Stick)", "Move", null, false, false),
        new("Move Up", "Move", "up", true, false), new("Move Down", "Move", "down", true, false),
        new("Move Left", "Move", "left", true, false), new("Move Right", "Move", "right", true, false),
        new("Primary Attack", "PrimaryAttack"), new("Secondary Ability", "SecondaryAbility"),
        new("Ultimate", "Ultimate"), new("Dig", "Dig"), new("Dash", "Dash"), new("Interact", "Interact"),
        new("Buddy Commands", "CommandWheel"), new("Plant Spore", "PlantSpore"),
        new("Aim", "AimPointer", null, false, false), new("Pause", "Pause", null, false, false)
    };
    static readonly Color Backdrop = new(0f, 0f, 0f, .92f), Panel = new(.055f, .16f, .09f, 1f);
    static readonly Color ButtonColor = new(.09f, .23f, .13f, 1f), TextColor = new(.93f, .9f, .78f, 1f);
    readonly List<(TMP_Text text, RowSpec row, BindingScheme scheme)> displays = new();
    SporeBindingService service; Action closed; Canvas canvas; Button firstButton; GameObject dialog;
    TMP_Text dialogText; Button dialogSafeButton;
    int rebindCanceledFrame = -1;

    public static SporeControlsScreen Open(Action closed)
    {
        SporeControlsScreen existing = FindAnyObjectByType<SporeControlsScreen>();
        if (existing != null) return existing;
        var host = new GameObject("Shared Controls Screen", typeof(RectTransform));
        SporeControlsScreen screen = host.AddComponent<SporeControlsScreen>(); screen.closed = closed; screen.Build(); return screen;
    }

    void Build()
    {
        service = SporeInputReader.Instance?.Bindings;
        if (service == null) { Debug.LogError("Controls requires SporeInputReader binding service."); Destroy(gameObject); return; }
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 950;
        gameObject.AddComponent<GraphicRaycaster>(); CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        Image backdrop = gameObject.AddComponent<Image>(); backdrop.color = Backdrop;
        RectTransform root = transform as RectTransform; Stretch(root);
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        TMP_Text title = Text("Title", root, font, 34, TextAlignmentOptions.Center); title.text = "CONTROLS";
        SetRect(title.rectTransform, new Vector2(.1f, .91f), new Vector2(.9f, .98f));
        TMP_Text headers = Text("Headers", root, font, 22, TextAlignmentOptions.Center);
        headers.text = "ACTION                         KEYBOARD / MOUSE                         GAMEPAD";
        SetRect(headers.rectTransform, new Vector2(.12f, .855f), new Vector2(.88f, .91f));

        GameObject scrollObject = new("ControlsScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(root, false); SetRect((RectTransform)scrollObject.transform, new Vector2(.11f, .17f), new Vector2(.89f, .85f));
        scrollObject.GetComponent<Image>().color = Panel; ScrollRect scroll = scrollObject.GetComponent<ScrollRect>(); scroll.horizontal = false;
        GameObject viewportObject = new("Viewport", typeof(RectTransform), typeof(RectMask2D)); viewportObject.transform.SetParent(scrollObject.transform, false); Stretch((RectTransform)viewportObject.transform);
        GameObject contentObject = new("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform content = (RectTransform)contentObject.transform; content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(16,16,10,10); layout.spacing = 7; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = (RectTransform)viewportObject.transform; scroll.content = content;

        foreach (RowSpec row in Rows) BuildRow(content, font, row);
        Button reset = Button("ResetDefaults", root, font, "Reset All Controls", ConfirmReset); SetRect((RectTransform)reset.transform, new Vector2(.21f,.055f), new Vector2(.47f,.135f));
        Button back = Button("Back", root, font, "Back", RequestClose); SetRect((RectTransform)back.transform, new Vector2(.53f,.055f), new Vector2(.79f,.135f));
        Refresh();
        service.BindingsChanged += Refresh; service.RebindStarted += HandleRebindStarted; service.RebindCanceled += HandleRebindCanceled; service.ConflictFound += ShowConflict;
        SporeUiCoordinator.Instance.PushModal(this, HandleCancel, false, firstButton ?? back);
    }

    void BuildRow(Transform parent, TMP_FontAsset font, RowSpec row)
    {
        GameObject go = new(row.Action + (row.Part ?? "") + "Row", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup)); go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 48; HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 10; layout.childControlWidth = false; layout.childControlHeight = true;
        TMP_Text label = Text("Label", go.transform, font, 20, TextAlignmentOptions.MidlineLeft); label.text = row.Label; Width(label.gameObject, 280);
        BuildBindingBox(go.transform, font, row, BindingScheme.KeyboardMouse, row.KeyboardEditable);
        BuildBindingBox(go.transform, font, row, BindingScheme.Gamepad, row.GamepadEditable && row.Action != "Move");
    }

    void BuildBindingBox(Transform parent, TMP_FontAsset font, RowSpec row, BindingScheme scheme, bool editable)
    {
        string displayAction = row.Action == "AimPointer" && scheme == BindingScheme.Gamepad ? "AimStick" : row.Action;
        TMP_Text text;
        if (editable)
        {
            Button button = Button(displayAction + scheme, parent, font, "", () => StartRebind(row, scheme)); text = button.GetComponentInChildren<TMP_Text>(); Width(button.gameObject, 330); if (firstButton == null) firstButton = button;
        }
        else
        {
            text = Text(displayAction + scheme, parent, font, 19, TextAlignmentOptions.Center); Width(text.gameObject, 330);
        }
        displays.Add((text, new RowSpec(row.Label, displayAction, row.Part, row.KeyboardEditable, row.GamepadEditable), scheme));
    }

    void StartRebind(RowSpec row, BindingScheme scheme)
    {
        Guid id = service.GetBindingId("Gameplay", row.Action, scheme, row.Part);
        if (id != Guid.Empty) service.BeginRebind("Gameplay", row.Action, id, scheme);
    }
    void HandleRebindStarted(UnityEngine.InputSystem.InputAction action, Guid id)
    { foreach (var item in displays) if (service.GetBindingId("Gameplay", item.row.Action, item.scheme, item.row.Part) == id) item.text.text = "Waiting for input..."; }
    void ShowConflict(BindingConflict conflict)
    {
        ShowDialog(SporeBindingRules.FriendlyDisplay(UnityEngine.InputSystem.InputControlPath.ToHumanReadableString(conflict.ControlPath)) +
            " is already bound to " + conflict.ConflictingAction + ". Replace it?", "Replace", () => { HideDialog(); service.ResolveConflict(true); }, () => { HideDialog(); service.ResolveConflict(false); });
    }
    void ConfirmReset() => ShowDialog("Restore every control to its default binding?", "Reset Defaults", () => { HideDialog(); service.ResetAll(); }, HideDialog);
    void HandleRebindCanceled() { rebindCanceledFrame = Time.frameCount; Refresh(); }
    void HandleCancel() { if (rebindCanceledFrame == Time.frameCount) return; if (service.IsRebinding || service.HasPendingConflict) service.CancelRebind(); else if (dialog != null && dialog.activeSelf) HideDialog(); else RequestClose(); }
    void RequestClose() { service.CancelRebind(); SporeUiCoordinator.Instance.PopModal(this); Action callback = closed; closed = null; callback?.Invoke(); Destroy(gameObject); }
    void OnDestroy() { if (service == null) return; service.BindingsChanged -= Refresh; service.RebindStarted -= HandleRebindStarted; service.RebindCanceled -= HandleRebindCanceled; service.ConflictFound -= ShowConflict; }
    void Refresh()
    {
        foreach (var item in displays)
        {
            if ((item.row.Label == "Move (Stick)" && item.scheme == BindingScheme.KeyboardMouse) ||
                (item.row.Part != null && item.row.Action == "Move" && item.scheme == BindingScheme.Gamepad)) item.text.text = "—";
            else item.text.text = service.GetDisplay("Gameplay", item.row.Action, item.scheme, item.row.Part);
        }
    }

    void ShowDialog(string message, string acceptLabel, Action accept, Action cancel)
    {
        if (dialog == null) BuildDialog(); dialog.SetActive(true); dialog.transform.SetAsLastSibling(); dialogText.text = message;
        Button[] buttons = dialog.GetComponentsInChildren<Button>(); buttons[0].onClick.RemoveAllListeners(); buttons[0].onClick.AddListener(() => accept()); buttons[0].GetComponentInChildren<TMP_Text>().text = acceptLabel;
        buttons[1].onClick.RemoveAllListeners(); buttons[1].onClick.AddListener(() => cancel()); dialogSafeButton = buttons[1]; UiFocusUtility.Select(dialogSafeButton);
    }
    void BuildDialog()
    {
        dialog = new GameObject("Confirmation", typeof(RectTransform), typeof(Image)); dialog.transform.SetParent(transform, false); SetRect((RectTransform)dialog.transform, new Vector2(.29f,.32f), new Vector2(.71f,.68f)); dialog.GetComponent<Image>().color = Panel;
        dialogText = Text("Message", dialog.transform, TMP_Settings.defaultFontAsset, 23, TextAlignmentOptions.Center); SetRect(dialogText.rectTransform, new Vector2(.08f,.35f), new Vector2(.92f,.86f));
        Button accept = Button("Accept", dialog.transform, TMP_Settings.defaultFontAsset, "Accept", null); SetRect((RectTransform)accept.transform, new Vector2(.08f,.08f), new Vector2(.46f,.27f));
        Button cancel = Button("Cancel", dialog.transform, TMP_Settings.defaultFontAsset, "Cancel", null); SetRect((RectTransform)cancel.transform, new Vector2(.54f,.08f), new Vector2(.92f,.27f)); dialog.SetActive(false);
    }
    void HideDialog() { if (dialog != null) dialog.SetActive(false); Refresh(); UiFocusUtility.Select(firstButton); }
    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment) { GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false); TMP_Text t=go.GetComponent<TMP_Text>(); t.font=font; t.fontSize=size; t.color=TextColor; t.alignment=alignment; return t; }
    static Button Button(string name, Transform parent, TMP_FontAsset font, string label, Action click) { GameObject go=new(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false); go.GetComponent<Image>().color=ButtonColor; Button b=go.GetComponent<Button>(); if(click!=null)b.onClick.AddListener(()=>click()); TMP_Text t=Text("Text",go.transform,font,20,TextAlignmentOptions.Center); t.text=label; Stretch(t.rectTransform); return b; }
    static void Width(GameObject go,float width){LayoutElement e=go.GetComponent<LayoutElement>()??go.AddComponent<LayoutElement>();e.preferredWidth=width;e.minWidth=width;}
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
    static void SetRect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
}
