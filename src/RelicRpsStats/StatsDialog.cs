using Godot;

namespace RelicRpsStats;

internal sealed class StatsDialog
{
    private readonly AcceptDialog dialog;
    private readonly StatsBook book;
    private readonly OptionButton profiles;
    private readonly LineEdit search;
    private readonly Label totals;
    private readonly Label pagination;
    private readonly VBoxContainer rows;
    private readonly Button previous, next;
    private readonly Profile[] owners;
    private Opponent[] filtered = [];
    private int page;

    internal StatsDialog(Control parent, StatsBook book, string? preferredOwner, string? error)
    {
        this.book = book;
        var focus = parent.GetViewport().GuiGetFocusOwner();
        dialog = new AcceptDialog { Name = "RelicRpsStatsDialog", Title = "Rock Paper Scissors — lifetime stats",
            Exclusive = true, Transient = true, DialogText = "", MinSize = new Vector2I(640, 420), WrapControls = false };
        parent.AddChild(dialog);
        var panel = StatsPanel.CreatePanel("StatsContents");
        dialog.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.OffsetLeft = 12; panel.OffsetTop = 12; panel.OffsetRight = -12; panel.OffsetBottom = -60;
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 10); panel.AddChild(stack);
        stack.AddChild(new Label { Text = "ROCK • PAPER • SCISSORS", HorizontalAlignment = HorizontalAlignment.Center });
        profiles = new OptionButton(); stack.AddChild(profiles);
        owners = book.Profiles.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray();
        for (int i = 0; i < owners.Length; i++) profiles.AddItem($"{owners[i].Name} ({owners[i].Id})", i);
        if (owners.Length == 0) profiles.AddItem("No recorded multiplayer contests yet");
        int preferred = Array.FindIndex(owners, p => p.Id == preferredOwner);
        profiles.Select(Math.Max(0, preferred));
        totals = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; stack.AddChild(totals);
        var note = new Label
        {
            Text = "Relic win %: wins / contested relics. Throw win % excludes ties.\n" +
                "Teammate rows count shared contests; Other = a third player won. Three different moves tie.\n" +
                "Recorded on this computer since installation. Uncontested and consolation relics are excluded.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        note.AddThemeFontSizeOverride("font_size", 17); stack.AddChild(note);
        if (error != null) stack.AddChild(new Label { Text = error, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        search = new LineEdit { PlaceholderText = "Search all teammates…", MaxLength = 100 }; stack.AddChild(search);
        // The plain Control prevents transient wrapped-label minimums from expanding the dialog.
        var scrollBounds = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 100) };
        stack.AddChild(scrollBounds);
        var scroll = new ScrollContainer { Name = "TeammateScroll", SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 100), VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scrollBounds.AddChild(scroll);
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(rows);
        var footer = new HBoxContainer(); stack.AddChild(footer);
        previous = new Button { Text = "Previous" }; next = new Button { Text = "Next" };
        pagination = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center };
        footer.AddChild(previous); footer.AddChild(pagination); footer.AddChild(next);
        previous.Pressed += () => { page--; RenderPage(); scroll.ScrollVertical = 0; };
        next.Pressed += () => { page++; RenderPage(); scroll.ScrollVertical = 0; };
        profiles.ItemSelected += _ => Filter(); search.TextChanged += _ => Filter();
        void Close()
        {
            Mod.RevealingStarted -= Close;
            dialog.Hide();
            dialog.QueueFree();
            if (GodotObject.IsInstanceValid(focus) && focus!.IsInsideTree() && focus.IsVisibleInTree()) focus.GrabFocus();
        }
        dialog.Confirmed += Close; dialog.Canceled += Close;
        Mod.RevealingStarted += Close;
        dialog.TreeExiting += () => Mod.RevealingStarted -= Close;
        Filter();
        dialog.PopupCenteredClamped(new Vector2I(1080, 740), 0.9f);
        void Fit()
        {
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            panel.OffsetLeft = 12; panel.OffsetTop = 12; panel.OffsetRight = -12; panel.OffsetBottom = -60;
        }
        dialog.SizeChanged += Fit;
        // Wrapped labels initially measure at zero width. Godot grows Control offsets to that
        // transient minimum and does not shrink them again; fit after two layout passes.
        var tree = dialog.GetTree();
        int passes = 2;
        void Settle() { Fit(); if (--passes == 0) tree.ProcessFrame -= Settle; }
        tree.ProcessFrame += Settle;
        dialog.TreeExiting += () => tree.ProcessFrame -= Settle;
    }

    private void Filter()
    {
        var profile = owners.ElementAtOrDefault(profiles.Selected);
        var t = profile?.Totals ?? new Totals();
        totals.Text = $"Contested relics: {t.RelicWins} W / {t.RelicLosses} L  •  Win rate {t.RelicRate}\n" +
            $"Throws: {t.ThrowWins} W / {t.ThrowLosses} L / {t.ThrowTies} ties  •  Decisive win rate {t.ThrowRate}";
        filtered = profile?.Opponents.Values.Where(o => o.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase) ||
            o.Id.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Id, StringComparer.Ordinal).ToArray() ?? [];
        page = 0; RenderPage();
    }

    private void RenderPage()
    {
        foreach (var child in rows.GetChildren()) { rows.RemoveChild(child); child.Free(); }
        int pages = Math.Max(1, (filtered.Length + StatsBook.PageSize - 1) / StatsBook.PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        foreach (var opponent in filtered.Skip(page * StatsBook.PageSize).Take(StatsBook.PageSize))
        {
            var t = opponent.Totals;
            var label = new Label { Name = "OpponentRow_" + rows.GetChildCount(), Text = $"{opponent.Name}  •  {opponent.Id}\n" +
                $"Relics: {t.RelicWins} W / {t.RelicLosses} L / {t.OtherWinner} Other ({t.RelicRate})     " +
                $"Throws: {t.ThrowWins} W / {t.ThrowLosses} L / {t.ThrowTies} T ({t.ThrowRate})",
                AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 65) };
            rows.AddChild(label);
        }
        if (filtered.Length == 0) rows.AddChild(new Label { Text = "No matching teammates. Stats begin with your first contested relic." });
        pagination.Text = $"Page {page + 1} / {pages}  •  {filtered.Length} teammates";
        previous.Disabled = page == 0; next.Disabled = page + 1 == pages;
    }
}
