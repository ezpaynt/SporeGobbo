using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PauseMenuRunView : MonoBehaviour
{
    static readonly Color Overlay = new Color(0f, 0f, 0f, 0.78f);
    static readonly Color Panel = new Color(0.055f, 0.16f, 0.09f, 0.98f);
    static readonly Color Accent = new Color(0.09f, 0.23f, 0.13f, 1f);
    static readonly Color Bone = new Color(0.93f, 0.9f, 0.78f, 1f);
    static readonly Color Muted = new Color(0.72f, 0.73f, 0.62f, 1f);

    RectTransform mainPage, journalPage, optionsPage, confirmationPage, squadContent;
    TMP_Text playerText, modifiersText, runText, confirmationText;
    Button confirmButton, confirmationCancelButton, optionsBackButton, resumeDefaultButton, optionsOriginButton;
    PauseMenuJournalView journalView;
    TMP_FontAsset font;
    Action pendingConfirmation;
    bool built;

    public bool IsSubPageOpen =>
        (optionsPage != null && optionsPage.gameObject.activeSelf) ||
        (confirmationPage != null && confirmationPage.gameObject.activeSelf);

    public void Build(GameObject pausePanel, TMP_Text title, Button resume, Button menu, Button desktop,
        Action resumeAction, Action optionsAction, Action menuAction, Action desktopAction, bool journalOnly = false)
    {
        if (built || pausePanel == null) return;
        built = true; font = title != null ? title.font : TMP_Settings.defaultFontAsset;
        resumeDefaultButton = resume;
        RectTransform root = pausePanel.GetComponent<RectTransform>();
        SetRect(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image overlay = pausePanel.GetComponent<Image>(); if (overlay == null) overlay = pausePanel.AddComponent<Image>(); overlay.color = Overlay;
        if (title != null) title.gameObject.SetActive(false);

        RectTransform tabs = Rect("PauseTabs", root, new Vector2(.02f, .925f), new Vector2(.42f, .985f));
        HorizontalLayoutGroup tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 8f; tabLayout.childControlHeight = true; tabLayout.childControlWidth = true;
        tabLayout.childForceExpandHeight = true; tabLayout.childForceExpandWidth = true;
        NewButton("CurrentRunTab", tabs, journalOnly ? "Menu" : "Current Run", ShowMainPage);
        NewButton("JournalTab", tabs, "Journal", ShowJournal);

        mainPage = Rect("RunPausePage", root, Vector2.zero, new Vector2(1f, .91f));
        if (journalOnly)
        {
            BuildActions(PanelRect("ActionsPanel", mainPage, new Vector2(.30f, .16f), new Vector2(.70f, .90f)), resume, menu, desktop,
                resumeAction, optionsAction, menuAction, desktopAction);
        }
        else
        {
            playerText = PanelText(PanelRect("PlayerPanel", mainPage, new Vector2(.02f, .46f), new Vector2(.305f, .97f)), "PLAYER");
            modifiersText = PanelText(PanelRect("ModifiersPanel", mainPage, new Vector2(.315f, .46f), new Vector2(.60f, .97f)), "MODIFIERS");
            runText = PanelText(PanelRect("RunPanel", mainPage, new Vector2(.61f, .46f), new Vector2(.81f, .97f)), "RUN");
            BuildActions(PanelRect("ActionsPanel", mainPage, new Vector2(.82f, .46f), new Vector2(.98f, .97f)), resume, menu, desktop,
                resumeAction, optionsAction, menuAction, desktopAction);
            BuildSquad(PanelRect("ActiveSquadPanel", mainPage, new Vector2(.02f, .03f), new Vector2(.98f, .44f)));
        }

        journalPage = Rect("JournalPausePage", root, Vector2.zero, new Vector2(1f, .91f));
        journalView = journalPage.gameObject.AddComponent<PauseMenuJournalView>();
        journalView.Build(journalPage, font);

        BuildOptions(root); BuildConfirmation(root); ShowMainPage();
    }

    public void Refresh(PauseMenuStatusSnapshot snapshot, JournalSnapshot journal)
    {
        if (!built || snapshot == null) return;
        if (playerText != null) playerText.text = PlayerText(snapshot.player);
        if (modifiersText != null) modifiersText.text = ModifiersText(snapshot.player);
        if (runText != null) runText.text = RunText(snapshot.run);
        if (squadContent != null) RebuildSquad(snapshot.activeSquad);
        if (journalView != null) journalView.Refresh(journal);
    }

    public void ShowMainPage()
    {
        if (mainPage != null) mainPage.gameObject.SetActive(true);
        if (journalPage != null) journalPage.gameObject.SetActive(false);
        if (optionsPage != null) optionsPage.gameObject.SetActive(false);
        if (confirmationPage != null) confirmationPage.gameObject.SetActive(false);
        pendingConfirmation = null;
        UiFocusUtility.Select(resumeDefaultButton);
    }

    public void ReturnFromControls()
    {
        ShowMainPage();
        UiFocusUtility.Select(optionsOriginButton);
    }

    public void ShowJournal()
    {
        if (mainPage != null) mainPage.gameObject.SetActive(false);
        if (journalPage != null) journalPage.gameObject.SetActive(true);
        if (optionsPage != null) optionsPage.gameObject.SetActive(false);
        if (confirmationPage != null) confirmationPage.gameObject.SetActive(false);
        pendingConfirmation = null;
        UiFocusUtility.Select(journalView != null ? journalView.DefaultSelectable : null);
    }

    public void ShowOptions()
    {
        if (mainPage != null) mainPage.gameObject.SetActive(false);
        if (journalPage != null) journalPage.gameObject.SetActive(false);
        if (confirmationPage != null) confirmationPage.gameObject.SetActive(false);
        if (optionsPage != null) optionsPage.gameObject.SetActive(true);
        UiFocusUtility.Select(optionsBackButton);
    }

    public void ShowConfirmation(string message, string actionLabel, Action accepted)
    {
        pendingConfirmation = accepted; confirmationText.text = message; ButtonLabel(confirmButton, actionLabel);
        mainPage.gameObject.SetActive(false); journalPage.gameObject.SetActive(false); optionsPage.gameObject.SetActive(false); confirmationPage.gameObject.SetActive(true);
        UiFocusUtility.Select(confirmationCancelButton);
    }

    void BuildActions(RectTransform panel, Button resume, Button menu, Button desktop,
        Action resumeAction, Action optionsAction, Action menuAction, Action desktopAction)
    {
        Heading(panel, "ACTIONS"); RectTransform content = Rect("ActionButtons", panel, Vector2.zero, new Vector2(1f, .86f));
        SetRect(content, Vector2.zero, new Vector2(1f, .86f), new Vector2(12f, 12f), new Vector2(-12f, -4f));
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f; layout.childAlignment = TextAnchor.UpperCenter; layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        ExistingButton(resume, content, "Resume", resumeAction);
        Button options = NewButton("Options", content, "Options", optionsAction); optionsOriginButton = options; Preferred(options.gameObject, 44f);
        ExistingButton(menu, content, "Exit To Menu", menuAction); ExistingButton(desktop, content, "Exit To Desktop", desktopAction);
    }

    void BuildSquad(RectTransform panel)
    {
        Heading(panel, "ACTIVE SQUAD"); RectTransform viewport = Rect("Viewport", panel, Vector2.zero, new Vector2(1f, .82f));
        SetRect(viewport, Vector2.zero, new Vector2(1f, .82f), new Vector2(12f, 8f), new Vector2(-12f, -4f));
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .12f); viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.horizontal = false;
        scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        squadContent = Rect("BuddyEntries", viewport, new Vector2(0f, 1f), Vector2.one); squadContent.pivot = new Vector2(.5f, 1f);
        VerticalLayoutGroup layout = squadContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f; layout.padding = new RectOffset(6, 6, 6, 6); layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        ContentSizeFitter fit = squadContent.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = squadContent;
    }

    void BuildOptions(RectTransform root)
    {
        optionsPage = PanelRect("OptionsPage", root, new Vector2(.2f, .18f), new Vector2(.8f, .82f)); Heading(optionsPage, "OPTIONS");
        TMP_Text empty = Text("FutureSettings", optionsPage, 24f, TextAlignmentOptions.Center); empty.color = Muted;
        SetRect(empty.rectTransform, new Vector2(.08f, .25f), new Vector2(.92f, .72f), Vector2.zero, Vector2.zero);
        empty.text = "Settings will be added here in a future version.";
        optionsBackButton = NewButton("Back", optionsPage, "Back", ShowMainPage);
        SetRect((RectTransform)optionsBackButton.transform, new Vector2(.34f, .06f), new Vector2(.66f, .18f), Vector2.zero, Vector2.zero);
        optionsPage.gameObject.SetActive(false);
    }

    void BuildConfirmation(RectTransform root)
    {
        confirmationPage = PanelRect("ConfirmationPage", root, new Vector2(.28f, .3f), new Vector2(.72f, .7f)); Heading(confirmationPage, "CONFIRM");
        confirmationText = Text("ConfirmationText", confirmationPage, 25f, TextAlignmentOptions.Center);
        SetRect(confirmationText.rectTransform, new Vector2(.08f, .34f), new Vector2(.92f, .75f), Vector2.zero, Vector2.zero);
        confirmButton = NewButton("Confirm", confirmationPage, "Confirm", () => { Action action = pendingConfirmation; pendingConfirmation = null; action?.Invoke(); });
        SetRect((RectTransform)confirmButton.transform, new Vector2(.1f, .08f), new Vector2(.46f, .25f), Vector2.zero, Vector2.zero);
        confirmationCancelButton = NewButton("Cancel", confirmationPage, "Cancel", ShowMainPage);
        SetRect((RectTransform)confirmationCancelButton.transform, new Vector2(.54f, .08f), new Vector2(.9f, .25f), Vector2.zero, Vector2.zero);
        confirmationPage.gameObject.SetActive(false);
    }

    void RebuildSquad(List<BuddyPauseSnapshot> buddies)
    {
        for (int i = squadContent.childCount - 1; i >= 0; i--) Destroy(squadContent.GetChild(i).gameObject);
        if (buddies == null || buddies.Count == 0)
        {
            TMP_Text none = Text("NoActiveBuddies", squadContent, 20f, TextAlignmentOptions.Center); none.text = "No active buddies"; none.color = Muted; Preferred(none.gameObject, 48f); return;
        }
        foreach (BuddyPauseSnapshot buddy in buddies)
        {
            RectTransform entry = Rect("BuddyEntry", squadContent, Vector2.zero, Vector2.one); entry.gameObject.AddComponent<Image>().color = Accent; Preferred(entry.gameObject, 70f);
            TMP_Text text = Text("Stats", entry, 18f, TextAlignmentOptions.TopLeft);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 6f), new Vector2(-10f, -6f)); text.text = BuddyText(buddy);
        }
    }

    string PlayerText(PlayerPauseSnapshot p)
    {
        StringBuilder s = Header("PLAYER"); Line(s, "Name", p.name); Line(s, "Type", p.type); Line(s, "Stage", p.stage); Line(s, "Level", p.level);
        Line(s, "XP", p.xp + " / " + p.xpRequired); Line(s, "Health", p.health + " / " + p.maxHealth); Line(s, "Attack", p.attack);
        Line(s, "Defense", p.defense + " flat"); Line(s, "Move Speed", F(p.moveSpeed)); Line(s, "Attack Cooldown", Sec(p.attackCooldown));
        Line(s, "Attack Range", F(p.attackRange)); Line(s, "Attack Radius", F(p.attackRadius)); Line(s, "Dash Speed", F(p.dashSpeed));
        Line(s, "Dash Duration", Sec(p.dashDuration)); Line(s, "Dash Cooldown", Sec(p.dashCooldown)); Line(s, "Critical Chance", Percent(p.critChance));
        Line(s, "Critical Damage", F(p.critDamage) + "x"); Line(s, "Dig Power", p.digPower); Line(s, "Dig Radius", F(p.digRadius));
        Line(s, "Dig Range", F(p.digRange)); Line(s, "Dig Tick", Sec(p.digTickRate)); Line(s, "Followers", p.followers);
        if (p.hasSporeMend) { Line(s, "Spore Mend", "+" + p.sporeMendAmount + " HP"); Line(s, "Mend Cooldown", Sec(p.sporeMendCooldown)); }
        if (p.hasDashBite) { Line(s, "Dash Bite Range", F(p.dashBiteRange)); Line(s, "Bite Damage", F(p.dashBiteMultiplier) + "x"); Line(s, "Bite Cooldown", Sec(p.dashBiteCooldown)); }
        if (p.poisoned) Line(s, "Status", "Poisoned"); return s.ToString();
    }

    string ModifiersText(PlayerPauseSnapshot p)
    {
        StringBuilder s = Header("MODIFIERS"); s.AppendLine("<b>Permanent Snacks</b>"); Line(s, "Max Health", Sign(p.snackHealth));
        Line(s, "Attack", Sign(p.snackAttack)); Line(s, "Defense", Sign(p.snackDefense)); List(s, "Selected Cards", p.cards);
        List(s, "Unlocked Abilities", p.abilities); List(s, "Active Traits", p.traits); return s.ToString();
    }

    string RunText(RunPauseSnapshot r)
    {
        StringBuilder s = Header("RUN"); Line(s, "Run Number", r.number); Line(s, "Elapsed", TimeText(r.elapsed)); Line(s, "Enemies Defeated", r.enemies);
        Line(s, "Mushrooms", r.mushrooms); Line(s, "Shinies", r.shinies); Line(s, "Spores", r.spores); Line(s, "Snacks Found", r.snacks);
        Line(s, "Squad", r.squadSize + " / " + r.squadMax); Line(s, "New Buddies", r.newBuddies); List(s, "Upgrades Chosen", r.upgrades); return s.ToString();
    }

    string BuddyText(BuddyPauseSnapshot b)
    {
        string traits = b.traits != null && b.traits.Count > 0 ? string.Join(", ", b.traits) : "None";
        return "<b>" + b.name + "</b>  ?  " + b.type + " / " + b.stage + "  ?  Level " + b.level + "  XP " + b.xp + "/" + b.xpRequired +
            "  ?  HP " + b.health + "/" + b.maxHealth + "  ?  ATK " + b.attack + "  DEF " + b.defense + "  MOVE " + F(b.moveSpeed) + "  CD " + Sec(b.attackCooldown) + "\n" +
            "Snacks: HP " + Sign(b.snackHealth) + ", ATK " + Sign(b.snackAttack) + ", DEF " + Sign(b.snackDefense) + "  ?  Growth: " + b.growth +
            "  ?  Traits: " + traits + "  ?  " + (b.alive ? "Alive" : "Dead");
    }

    TMP_Text PanelText(RectTransform panel, string heading) { TMP_Text text = Text("Content", panel, 19f, TextAlignmentOptions.TopLeft); SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 12f), new Vector2(-14f, -12f)); text.text = "<size=25><b>" + heading + "</b></size>"; return text; }
    void Heading(RectTransform panel, string heading) { TMP_Text text = Text("Heading", panel, 25f, TextAlignmentOptions.Center); SetRect(text.rectTransform, new Vector2(0f, .84f), Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, -4f)); text.fontStyle = FontStyles.Bold; text.text = heading; }
    RectTransform PanelRect(string name, Transform parent, Vector2 min, Vector2 max) { RectTransform rect = Rect(name, parent, min, max); rect.gameObject.AddComponent<Image>().color = Panel; return rect; }
    RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max) { GameObject go = new GameObject(name, typeof(RectTransform)); RectTransform rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); SetRect(rect, min, max, Vector2.zero, Vector2.zero); return rect; }
    TMP_Text Text(string name, Transform parent, float size, TextAlignmentOptions alignment) { GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = Bone; text.alignment = alignment; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false; return text; }
    Button NewButton(string name, Transform parent, string label, Action clicked) { GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.color = Accent; Button button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => clicked?.Invoke()); TMP_Text text = Text("ButtonText", go.transform, 21f, TextAlignmentOptions.Center); SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f)); text.text = label; return button; }
    void ExistingButton(Button button, Transform parent, string label, Action clicked) { if (button == null) return; button.transform.SetParent(parent, false); SetRect((RectTransform)button.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => clicked?.Invoke()); Image image = button.GetComponent<Image>(); if (image != null) image.color = Accent; ButtonLabel(button, label); Preferred(button.gameObject, 44f); }
    static void ButtonLabel(Button button, string label) { TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null; if (text != null) text.text = label; }
    static void Preferred(GameObject target, float height) { LayoutElement element = target.GetComponent<LayoutElement>(); if (element == null) element = target.AddComponent<LayoutElement>(); element.preferredHeight = height; element.minHeight = height; }
    static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
    static StringBuilder Header(string heading) { return new StringBuilder("<size=25><b>" + heading + "</b></size>\n"); }
    static void Line(StringBuilder s, string label, object value) { s.Append("<b>").Append(label).Append(":</b> ").Append(value).Append('\n'); }
    static void List(StringBuilder s, string heading, List<string> values) { s.Append("\n<b>").Append(heading).Append("</b>\n"); if (values == null || values.Count == 0) { s.Append("None\n"); return; } foreach (string value in values) s.Append("? ").Append(value).Append('\n'); }
    static string Sign(int value) { return value >= 0 ? "+" + value : value.ToString(); }
    static string F(float value) { return value.ToString("0.##"); }
    static string Sec(float value) { return F(value) + "s"; }
    static string Percent(float value) { return (value * 100f).ToString("0.#") + "%"; }
    static string TimeText(float seconds) { int total = Mathf.Max(0, Mathf.FloorToInt(seconds)); return (total / 60).ToString("00") + ":" + (total % 60).ToString("00"); }
}
