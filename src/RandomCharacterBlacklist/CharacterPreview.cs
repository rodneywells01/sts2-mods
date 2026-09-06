using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace RandomCharacterBlacklist;

// A single scene-owned cosmetic slot. No combat creature, timers, or queued callbacks.
internal sealed class CharacterPreview
{
    private readonly NCharacterSelectScreen screen;
    private readonly Control panel;
    private readonly Dictionary<string, NCreatureVisuals> visuals = new();
    private readonly HashSet<string> unavailable = new();
    private NCreatureVisuals? active;
    private double elapsed;
    private double duration;

    internal CharacterPreview(NCharacterSelectScreen screen, Control panel)
    {
        this.screen = screen;
        this.panel = panel;
        var tree = screen.GetTree();
        tree.ProcessFrame += Tick;
        screen.VisibilityChanged += () => { if (!screen.IsVisibleInTree()) Cancel(); };
        screen.TreeExiting += () => { Cancel(); tree.ProcessFrame -= Tick; };
    }

    internal void Warm(IEnumerable<CharacterModel> characters)
    {
        foreach (var character in characters) GetVisual(character);
    }

    private NCreatureVisuals? GetVisual(CharacterModel character)
    {
        string id = character.Id.ToString();
        if (visuals.TryGetValue(id, out var cached)) return cached;
        if (unavailable.Contains(id)) return null;
        NCreatureVisuals? node = null;
        try
        {
            // Load from the game's resources even if the combat preload cache is cold.
            string path = (string)AccessTools.Property(typeof(CharacterModel), "VisualsPath").GetValue(character)!;
            node = ResourceLoader.Load<PackedScene>(path).Instantiate<NCreatureVisuals>();
            node.Name = "RandomPreview_" + character.Id.Entry;
            node.Visible = false;
            node.ZIndex = 15;
            screen.AddChild(node);
            IgnoreInput(node);
            node.ProcessMode = Node.ProcessModeEnum.Disabled;
            visuals.Add(id, node);
            return node;
        }
        catch (Exception error)
        {
            node?.QueueFree();
            unavailable.Add(id);
            GD.PrintErr($"[{Mod.Id}] Preview unavailable for {id}: {error.Message}");
            return null;
        }
    }

    private static void IgnoreInput(Node node)
    {
        if (node is Control control) { control.MouseFilter = Control.MouseFilterEnum.Ignore; control.FocusMode = Control.FocusModeEnum.None; }
        foreach (var child in node.GetChildren()) IgnoreInput(child);
    }

    internal void Play(CharacterModel character, bool included)
    {
        Cancel(); // Hide first, including when the replacement asset fails to load.
        if (!screen.IsVisibleInTree()) return;
        var node = GetVisual(character);
        if (node == null) return;
        try
        {
            string clip = "die";
            if (included)
            {
                var states = (List<(AnimState, string)>)AccessTools.Property(typeof(CharacterModel), "AnimationStates").GetValue(character)!;
                clip = states.First(s => s.Item2 == "PowerUp").Item1.Id;
            }
            var animation = node.SpineBody?.TryGetAnimationState();
            if (animation == null) return;
            animation.SetAnimation(clip, loop: false);
            duration = Math.Clamp(animation.GetCurrentAnimationDuration() ?? 1f, 0.35, 5);
            elapsed = 0;
            active = node;
            node.ProcessMode = Node.ProcessModeEnum.Inherit;
            node.Modulate = new Color(1, 1, 1, 0);
            node.Visible = true;
            Position(node);
            GD.Print($"[{Mod.Id}] Preview {character.Id.Entry} {clip}");
        }
        catch (Exception error)
        {
            Cancel();
            GD.PrintErr($"[{Mod.Id}] Preview skipped: {error.Message}");
        }
    }

    private void Position(NCreatureVisuals node)
    {
        var bounds = node.Bounds;
        float scale = Math.Min(220f / Math.Max(bounds.Size.X, 1), 250f / Math.Max(bounds.Size.Y, 1));
        scale = Math.Min(scale, 1.3f);
        node.Scale = Vector2.One * scale;
        // Both nodes share the character screen's coordinates. Track the dropdown
        // itself so the preview stays beside it as layout or resolution changes.
        node.Position = panel.Position + new Vector2(-24f, panel.Size.Y - 16f)
            - (bounds.Position + bounds.Size) * scale;
    }

    private void Tick()
    {
        if (active == null) return;
        if (!screen.IsVisibleInTree()) { Cancel(); return; }
        elapsed += screen.GetProcessDeltaTime();
        // Finish the native clip, then fade its final pose; entry doesn't delay playback.
        if (elapsed >= duration + 0.18) { Cancel(); return; }
        float alpha = (float)Math.Min(Math.Min(elapsed / 0.12, 1), (duration + 0.18 - elapsed) / 0.18);
        active.Modulate = new Color(1, 1, 1, alpha);
        Position(active);
    }

    internal void Cancel()
    {
        if (active != null && GodotObject.IsInstanceValid(active))
        {
            active.Hide();
            active.ProcessMode = Node.ProcessModeEnum.Disabled;
        }
        active = null;
    }
}

