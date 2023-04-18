using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.UI.Classes;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.ModsTab;
using ModFileSystemSelector = Penumbra.UI.ModsTab.ModFileSystemSelector;
using Penumbra.Collections.Manager;
using Penumbra.UI.CollectionTab;

namespace Penumbra.UI.Tabs;

public class ModsTab : ITab
{
    private readonly ModFileSystemSelector _selector;
    private readonly ModPanel              _panel;
    private readonly TutorialService       _tutorial;
    private readonly ModManager            _modManager;
    private readonly ActiveCollections     _activeCollections;
    private readonly RedrawService         _redrawService;
    private readonly Configuration         _config;
    private readonly CollectionCombo       _collectionCombo;

    public ModsTab(ModManager modManager, CollectionManager collectionManager, ModFileSystemSelector selector, ModPanel panel,
        TutorialService tutorial, RedrawService redrawService, Configuration config)
    {
        _modManager        = modManager;
        _activeCollections = collectionManager.Active;
        _selector          = selector;
        _panel             = panel;
        _tutorial          = tutorial;
        _redrawService     = redrawService;
        _config            = config;
        _collectionCombo   = new CollectionCombo(collectionManager, () => collectionManager.Storage.OrderBy(c => c.Name).ToList());
    }

    public bool IsVisible
        => _modManager.Valid;

    public ReadOnlySpan<byte> Label
        => "Mods"u8;

    public void DrawHeader()
        => _tutorial.OpenTutorial(BasicTutorialSteps.Mods);

    public Mod SelectMod
    {
        set => _selector.SelectByValue(value);
    }

    public void DrawContent()
    {
        try
        {
            _selector.Draw(GetModSelectorSize(_config));
            ImGui.SameLine();
            using var group = ImRaii.Group();
            DrawHeaderLine();

            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

            using (var child = ImRaii.Child("##ModsTabMod", new Vector2(-1, _config.HideRedrawBar ? 0 : -ImGui.GetFrameHeight()),
                       true, ImGuiWindowFlags.HorizontalScrollbar))
            {
                style.Pop();
                if (child)
                    _panel.Draw();

                style.Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            }

            style.Push(ImGuiStyleVar.FrameRounding, 0);
            DrawRedrawLine();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Exception thrown during ModPanel Render:\n{e}");
            Penumbra.Log.Error($"{_modManager.Count} Mods\n"
              + $"{_activeCollections.Current.AnonymizedName} Current Collection\n"
              + $"{_activeCollections.Current.Settings.Count} Settings\n"
              + $"{_selector.SortMode.Name} Sort Mode\n"
              + $"{_selector.SelectedLeaf?.Name ?? "NULL"} Selected Leaf\n"
              + $"{_selector.Selected?.Name ?? "NULL"} Selected Mod\n"
              + $"{string.Join(", ", _activeCollections.Current.DirectlyInheritsFrom.Select(c => c.AnonymizedName))} Inheritances\n"
              + $"{_selector.SelectedSettingCollection.AnonymizedName} Collection\n");
        }
    }

    /// <summary> Get the correct size for the mod selector based on current config. </summary>
    public static float GetModSelectorSize(Configuration config)
    {
        var absoluteSize = Math.Clamp(config.ModSelectorAbsoluteSize, Configuration.Constants.MinAbsoluteSize,
            Math.Min(Configuration.Constants.MaxAbsoluteSize, ImGui.GetContentRegionAvail().X - 100));
        var relativeSize = config.ScaleModSelector
            ? Math.Clamp(config.ModSelectorScaledSize, Configuration.Constants.MinScaledSize, Configuration.Constants.MaxScaledSize)
            : 0;
        return !config.ScaleModSelector
            ? absoluteSize
            : Math.Max(absoluteSize, relativeSize * ImGui.GetContentRegionAvail().X / 100);
    }

    private void DrawRedrawLine()
    {
        if (Penumbra.Config.HideRedrawBar)
        {
            _tutorial.SkipTutorial(BasicTutorialSteps.Redrawing);
            return;
        }

        var frameHeight = new Vector2(0, ImGui.GetFrameHeight());
        var frameColor  = ImGui.GetColorU32(ImGuiCol.FrameBg);
        using (var _ = ImRaii.Group())
        {
            using (var font = ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGuiUtil.DrawTextButton(FontAwesomeIcon.InfoCircle.ToIconString(), frameHeight, frameColor);
                ImGui.SameLine();
            }

            ImGuiUtil.DrawTextButton("Redraw:        ", frameHeight, frameColor);
        }

        var hovered = ImGui.IsItemHovered();
        _tutorial.OpenTutorial(BasicTutorialSteps.Redrawing);
        if (hovered)
            ImGui.SetTooltip($"The supported modifiers for '/penumbra redraw' are:\n{TutorialService.SupportedRedrawModifiers}");

        void DrawButton(Vector2 size, string label, string lower)
        {
            if (ImGui.Button(label, size))
            {
                if (lower.Length > 0)
                    _redrawService.RedrawObject(lower, RedrawType.Redraw);
                else
                    _redrawService.RedrawAll(RedrawType.Redraw);
            }

            ImGuiUtil.HoverTooltip(lower.Length > 0 ? $"Execute '/penumbra redraw {lower}'." : $"Execute '/penumbra redraw'.");
        }

        using var disabled = ImRaii.Disabled(DalamudServices.SClientState.LocalPlayer == null);
        ImGui.SameLine();
        var buttonWidth = frameHeight with { X = ImGui.GetContentRegionAvail().X / 4 };
        DrawButton(buttonWidth, "All", string.Empty);
        ImGui.SameLine();
        DrawButton(buttonWidth, "Self", "self");
        ImGui.SameLine();
        DrawButton(buttonWidth, "Target", "target");
        ImGui.SameLine();
        DrawButton(frameHeight with { X = ImGui.GetContentRegionAvail().X - 1 }, "Focus", "focus");
    }

    /// <summary> Draw the header line that can quick switch between collections. </summary>
    private void DrawHeaderLine()
    {
        using var style      = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0).Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        var       buttonSize = new Vector2(ImGui.GetContentRegionAvail().X / 8f, 0);

        using (var _ = ImRaii.Group())
        {
            DrawDefaultCollectionButton(3 * buttonSize);
            ImGui.SameLine();
            DrawInheritedCollectionButton(3 * buttonSize);
            ImGui.SameLine();
            _collectionCombo.Draw("##collectionSelector", 2 * buttonSize.X, CollectionType.Current);
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImGuiUtil.DrawTextButton("The currently selected collection is not used in any way.", -Vector2.UnitX, Colors.PressEnterWarningBg);
    }

    private void DrawDefaultCollectionButton(Vector2 width)
    {
        var name      = $"{TutorialService.DefaultCollection} ({_activeCollections.Default.Name})";
        var isCurrent = _activeCollections.Default == _activeCollections.Current;
        var isEmpty   = _activeCollections.Default == ModCollection.Empty;
        var tt = isCurrent ? $"The current collection is already the configured {TutorialService.DefaultCollection}."
            : isEmpty      ? $"The {TutorialService.DefaultCollection} is configured to be empty."
                             : $"Set the {TutorialService.SelectedCollection} to the configured {TutorialService.DefaultCollection}.";
        if (ImGuiUtil.DrawDisabledButton(name, width, tt, isCurrent || isEmpty))
            _activeCollections.SetCollection(_activeCollections.Default, CollectionType.Current);
    }

    private void DrawInheritedCollectionButton(Vector2 width)
    {
        var noModSelected = _selector.Selected == null;
        var collection    = _selector.SelectedSettingCollection;
        var modInherited  = collection != _activeCollections.Current;
        var (name, tt) = (noModSelected, modInherited) switch
        {
            (true, _) => ("Inherited Collection", "No mod selected."),
            (false, true) => ($"Inherited Collection ({collection.Name})",
                "Set the current collection to the collection the selected mod inherits its settings from."),
            (false, false) => ("Not Inherited", "The selected mod does not inherit its settings."),
        };
        if (ImGuiUtil.DrawDisabledButton(name, width, tt, noModSelected || !modInherited))
            _activeCollections.SetCollection(collection, CollectionType.Current);
    }
}
