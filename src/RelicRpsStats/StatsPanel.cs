using Godot;

namespace RelicRpsStats;

internal sealed class StatsPanel
{
    private readonly Label summary;
    internal StatsPanel(Control parent)
    {
        var panel = CreatePanel("RelicRpsSummary");
        // Keep the updated record visible after native relic awards fade out, until leaving the room.
        var room = parent.GetParent() as Control ?? parent;
        room.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        panel.OffsetLeft = 30; panel.OffsetRight = 620;
        panel.OffsetTop = -180; panel.OffsetBottom = -35;
        var stack = new VBoxContainer(); panel.AddChild(stack);
        summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddChild(summary);
        var button = new Button { Text = "Rock Paper Scissors stats", FocusMode = Control.FocusModeEnum.None };
        stack.AddChild(button);
        button.Pressed += () => Mod.ShowStats(room);
        Refresh();
    }

    internal void Refresh()
    {
        Mod.Book.Profiles.TryGetValue(Mod.CurrentOwner() ?? "", out var profile);
        var t = profile?.Totals ?? new Totals();
        summary.Text = $"CONTESTED RELICS   {t.RelicWins} W / {t.RelicLosses} L   •   {t.RelicRate} wins\n" +
            $"THROWS   {t.ThrowWins} W / {t.ThrowLosses} L / {t.ThrowTies} T   •   {t.ThrowRate} decisive wins";
        if ((Mod.LoadError ?? Mod.SaveError) is { } error) summary.Text += "\n" + error;
    }

    internal static PanelContainer CreatePanel(string name)
    {
        var font = (Font)ThemeDB.FallbackFont.Duplicate();
        if (font is SystemFont system) system.MultichannelSignedDistanceField = true;
        var panel = new PanelContainer { Name = name, ZIndex = 50,
            Theme = new Theme { DefaultFont = font, DefaultFontSize = 21 } };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("172029ef"), BorderColor = new Color("b59b62"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12
        });
        return panel;
    }

    internal static void AddCompendiumButton(Control parent)
    {
        var button = new Button { Name = "RelicRpsStatsButton", Text = "Rock Paper Scissors stats",
            Theme = new Theme { DefaultFontSize = 26 }, FocusMode = Control.FocusModeEnum.All };
        parent.AddChild(button);
        button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        button.OffsetLeft = -460; button.OffsetRight = -45; button.OffsetTop = 115; button.OffsetBottom = 170;
        button.Pressed += () => Mod.ShowStats(parent);
    }
}
