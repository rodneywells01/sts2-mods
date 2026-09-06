using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace RandomCharacterBlacklist;

internal sealed class BlacklistPanel
{
    private readonly NCharacterSelectScreen screen;
    private readonly VBoxContainer contents;
    private readonly Label status;
    private readonly Button toggle;
    private readonly PanelContainer root;
    private readonly Button reset;
    private readonly CheckButton enabled;
    private Tween? resultTween;
    private readonly CharacterPreview preview;
    private Control? previousFocus;
    private readonly List<(CheckButton Toggle, NCharacterSelectButton Character)> rows = new();
    internal bool IsOpen => contents.Visible;

    internal IEnumerable<NCharacterSelectButton> CharacterButtons()
        => screen.GetNode<Control>("CharSelectButtons/ButtonContainer").GetChildren().OfType<NCharacterSelectButton>();

    internal BlacklistPanel(NCharacterSelectScreen screen)
    {
        this.screen = screen;
        root = new PanelContainer { Name = "RandomCharacterBlacklistPanel", ZIndex = 20,
            Theme = new Theme { DefaultFontSize = 24 } };
        screen.AddChild(root);
        preview = new CharacterPreview(screen, root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        root.OffsetLeft = -124;
        root.OffsetRight = -28;
        root.OffsetTop = 26;
        root.GrowHorizontal = Control.GrowDirection.Begin;
        var style = new StyleBoxFlat
        {
            BgColor = new Color("172029"), BorderColor = new Color("8c7953"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12
        };
        root.AddThemeStyleboxOverride("panel", style);
        root.AddThemeFontSizeOverride("font_size", 20);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        root.AddChild(stack);
        using var dice = new Godot.Image();
        dice.LoadSvgFromString("""
            <svg xmlns="http://www.w3.org/2000/svg" width="58" height="40" viewBox="0 0 58 40">
            <g stroke="#d9bd77" stroke-width="2" fill="#25343c">
            <rect x="3" y="4" width="29" height="29" rx="5" transform="rotate(-9 17 18)"/>
            <rect x="26" y="8" width="28" height="28" rx="5" transform="rotate(10 40 22)"/>
            </g><g fill="#f4e5bd">
            <circle cx="10" cy="12" r="2.4"/><circle cx="22" cy="11" r="2.4"/>
            <circle cx="12" cy="25" r="2.4"/><circle cx="23" cy="24" r="2.4"/>
            <circle cx="34" cy="15" r="2.4"/><circle cx="40" cy="22" r="2.4"/><circle cx="46" cy="29" r="2.4"/>
            </g></svg>
            """);
        toggle = new Button { Name = "RandomOptionsDice", Icon = ImageTexture.CreateFromImage(dice),
            FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(64, 44),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd, TooltipText = "Random options (F8)" };
        stack.AddChild(toggle);
        contents = new VBoxContainer { Visible = false };
        contents.AddThemeConstantOverride("separation", 7);
        stack.AddChild(contents);
        toggle.Pressed += Toggle;
        enabled = new CheckButton { Text = "Custom Random", ButtonPressed = Mod.Preferences.Enabled, FocusMode = Control.FocusModeEnum.All };
        contents.AddChild(enabled);
        enabled.TooltipText = "Turn off to restore the game's normal seeded Random selection.";
        var hint = new Label { Text = "Include in Random", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        contents.AddChild(hint);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(350, 270), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        contents.AddChild(scroll);
        var choices = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(choices);
        foreach (var character in CharacterButtons().Where(b => !b.IsRandom))
        {
            string id = character.Character.Id.ToString();
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            choices.AddChild(row);
            var icon = new TextureRect
            {
                Texture = character.Character.IconTexture,
                CustomMinimumSize = new Vector2(40, 44),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            row.AddChild(icon);
            var choice = new CheckButton
            {
                Text = character.Character.Title.GetFormattedText(),
                ButtonPressed = !Mod.Preferences.ExcludedCharacters.Contains(id),
                FocusMode = Control.FocusModeEnum.All,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 44)
            };
            // Use the game's own name palette so modded characters can supply theirs too.
            foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
                choice.AddThemeColorOverride(color, character.Character.NameColor);
            row.AddChild(choice);
            rows.Add((choice, character));
            choice.Toggled += included =>
            {
                if (included) Mod.Preferences.ExcludedCharacters.Remove(id);
                else Mod.Preferences.ExcludedCharacters.Add(id);
                Save();
                preview.Play(character.Character, included);
            };
        }
        reset = new Button { Text = "Include all", FocusMode = Control.FocusModeEnum.All };
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        contents.AddChild(footer);
        reset.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(reset);
        const string help = "Characters switched on are included in Random.\nAll characters are included by default.\nChoices save automatically on this computer.\n\nWith Custom Random on, each click rolls again and\nreveals the pick immediately. The same character\ncan be picked again. Manual picks are always allowed.\n\nTurn Custom Random off for normal game behavior:\nRandom is revealed at embark using the run seed.\nYour choices never restrict other players.\n\nF8 opens or closes these options. Escape closes them.";
        const string creditedHelp = help + "\n\nBuilt by Rodney Wells (WatersEdge) with GPT-6 Astra.";
        var info = new Button
        {
            Text = "i", TooltipText = creditedHelp, CustomMinimumSize = new Vector2(40, 40),
            FocusMode = Control.FocusModeEnum.All
        };
        footer.AddChild(info);
        info.Pressed += () =>
        {
            var dialog = new AcceptDialog { Title = "About Random options", DialogText = creditedHelp };
            root.AddChild(dialog);
            dialog.Confirmed += dialog.QueueFree;
            dialog.Canceled += dialog.QueueFree;
            dialog.PopupCentered();
        };
        reset.Pressed += () =>
        {
            Mod.Preferences = new Preferences { Enabled = enabled.ButtonPressed };
            foreach (var row in rows) row.Toggle.SetPressedNoSignal(true);
            Mod.PreferenceError = null;
            Save();
        };
        status = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        contents.AddChild(status);
        enabled.Toggled += active =>
        {
            bool pendingVanillaRandom = screen.Lobby?.LocalPlayer.character is MegaCrit.Sts2.Core.Models.Characters.RandomCharacter;
            if (active && pendingVanillaRandom && (Mod.PreferenceError != null || EligibleCount() == 0))
            {
                enabled.SetPressedNoSignal(false);
                ShowMessage("Include an unlocked character before enabling Custom Random.");
                return;
            }
            Mod.Preferences.Enabled = active;
            toggle.TooltipText = active ? "Random options (F8)" : "Random options (F8) — Custom Random off";
            toggle.SelfModulate = new Color(1, 1, 1, active ? 1f : 0.55f);
            Save();
            if (active && pendingVanillaRandom) Mod.Roll(screen);
        };
        var randomButton = CharacterButtons().FirstOrDefault(button => button.IsRandom);
        if (randomButton != null) randomButton.Released += _ => Mod.Roll(screen);
        // Leave native character-row navigation alone. Only F8/mouse opens this panel.
        var focusOrder = new List<Control> { enabled };
        focusOrder.AddRange(rows.Select(row => (Control)row.Toggle));
        focusOrder.Add(reset);
        focusOrder.Add(info);
        for (int i = 0; i < focusOrder.Count; i++)
        {
            focusOrder[i].FocusNeighborTop = focusOrder[(i + focusOrder.Count - 1) % focusOrder.Count].GetPath();
            focusOrder[i].FocusNeighborBottom = focusOrder[(i + 1) % focusOrder.Count].GetPath();
        }
        Refresh();
        toggle.SelfModulate = new Color(1, 1, 1, Mod.Preferences.Enabled ? 1f : 0.55f);
        screen.Resized += FitPanel;
        Callable.From(FitPanel).CallDeferred();
    }

    private void FitPanel()
    {
        root.ResetSize();
        root.Position = new Vector2(screen.Size.X - 28 - root.Size.X, 26);
    }

    internal void Toggle()
    {
        if (!contents.Visible) previousFocus = screen.GetViewport().GuiGetFocusOwner();
        contents.Visible = !contents.Visible;
        if (contents.Visible) preview.Warm(CharacterButtons().Where(b => !b.IsRandom).Select(b => b.Character));
        else preview.Cancel();
        Callable.From(FitPanel).CallDeferred();
        Refresh();
        if (contents.Visible) enabled.GrabFocus();
        else if (previousFocus != null && GodotObject.IsInstanceValid(previousFocus) && previousFocus.IsVisibleInTree() && previousFocus.FocusMode != Control.FocusModeEnum.None) previousFocus.GrabFocus();
        else screen.GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
    }

    internal void ShowMessage(string message) { if (!contents.Visible) Toggle(); status.Text = message; status.Visible = true; }
    internal void ShowResult(string name)
    {
        toggle.TooltipText = $"Random: {name}\nRandom options (F8)";
        resultTween?.Kill();
        toggle.Modulate = new Color(1f, 1f, 1f, 0.45f);
        resultTween = toggle.CreateTween();
        resultTween.TweenProperty(toggle, "modulate:a", 1f, 0.3);
        Refresh();
    }

    internal bool HandleConfirm(InputEvent input)
    {
        // STS2 remaps its confirm action; a stock Godot Button's ui_accept is not
        // necessarily bound. Keep confirm local to a focused control in this panel.
        var focus = screen.GetViewport().GuiGetFocusOwner();
        if (focus is not BaseButton button || !root.IsAncestorOf(button)) return false;
        bool confirm = input is InputEventKey { Pressed: true, Echo: false } key &&
            (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Space);
        confirm |= input.IsActionPressed("ui_select") && !input.IsEcho();
        if (!confirm) return false;
        if (button.ToggleMode) button.ButtonPressed = !button.ButtonPressed;
        else button.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    private void Save()
    {
        // Invalid loaded data requires an explicit reset, so a stray toggle cannot overwrite it.
        if (Mod.PreferenceError != null) { ShowMessage(Mod.PreferenceError); return; }
        try { Mod.Preferences.Save(Mod.PreferencePath); Refresh(); }
        catch (Exception error)
        {
            status.Text = "Choices apply this session, but could not be saved.";
            status.Visible = true;
            GD.PrintErr($"[{Mod.Id}] Save failed: {error.Message}");
        }
    }

    private void Refresh()
    {
        foreach (var row in rows)
            row.Toggle.Text = row.Character.Character.Title.GetFormattedText() + (row.Character.IsLocked ? " (locked)" : "");
        int count = EligibleCount();
        status.Text = Mod.PreferenceError ?? (Mod.Preferences.Enabled && count == 0 ? "Include at least one character to use Random." : "");
        status.Visible = status.Text.Length > 0;
    }

    private int EligibleCount() => CharacterButtons().Count(b => !b.IsRandom && !b.IsLocked && b.Visible && !Mod.Preferences.ExcludedCharacters.Contains(b.Character.Id.ToString()));
}

