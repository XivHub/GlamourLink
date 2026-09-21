using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GlamourLink.Apply;
using GlamourLink.Glamourer;
using GlamourLink.Library;
using XivHubPluginKit.UI;

namespace GlamourLink.Windows;

public sealed class MainWindow : Window
{
    private readonly GlamourImporter _importer;
    private readonly Action _openLibrary;

    private string _input = "";
    private bool _saveDesign;
    private string _designName = "";
    private string _saveMessage = "";
    private GlamourPlan? _boundPlan;

    public MainWindow(GlamourImporter importer, Action openLibrary) : base("GlamourLink###glamourlink-main")
    {
        _importer = importer;
        _openLibrary = openLibrary;
        _saveDesign = Plugin.Configuration.SaveAsDesignByDefault;
        Size = new Vector2(560, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        DrawImportSection();

        var plan = _importer.Plan;
        if (plan is not null)
        {
            BindPlan(plan);
            ImGui.Spacing();
            PlanTable.Draw(plan);
            ImGui.Spacing();
            DrawFooter(plan);
        }
    }

    /// <summary>
    /// Resets the per-plan editor state exactly once per fetched plan, keyed
    /// by reference so a second draw of the same plan does not clobber what
    /// the user typed into the design name field.
    /// </summary>
    private void BindPlan(GlamourPlan plan)
    {
        if (ReferenceEquals(_boundPlan, plan))
        {
            return;
        }

        _boundPlan = plan;
        _designName = plan.Name;
        _saveDesign = Plugin.Configuration.SaveAsDesignByDefault;
        _saveMessage = "";
    }

    private void DrawImportSection()
    {
        var busy = _importer.Busy;

        ImGui.SetNextItemWidth(-90);
        var submitted = ImGui.InputTextWithHint("##glamour-input", "Glamour URL or id", ref _input, 256,
            ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();
        bool clicked;
        using (ImRaii.Disabled(busy))
        {
            clicked = ImGui.Button("Fetch", new Vector2(80, 0));
        }

        if (clicked || (submitted && !busy))
        {
            _importer.StartImport(_input);
        }

        ImGui.SameLine();
        if (ImGui.Button("Library"))
        {
            _openLibrary();
        }

        if (busy)
        {
            ImGui.TextColored(HubStyle.Faint, _importer.BusyLabel);
        }

        if (!string.IsNullOrEmpty(_importer.Error))
        {
            ImGui.TextColored(HubStyle.Bad, _importer.Error);
        }

        var availability = _importer.GlamourerIpc.Check(out var glamourerMessage, out var major, out var minor);
        if (availability == GlamourerAvailability.Ready)
        {
            ImGui.TextColored(HubStyle.Good, $"Glamourer {major}.{minor} is ready.");
        }
        else
        {
            ImGui.TextColored(HubStyle.Bad, glamourerMessage);
        }
    }

    private void DrawFooter(GlamourPlan plan)
    {
        ImGui.TextColored(HubStyle.Faint,
            $"{plan.Resolved} resolved · {plan.Guessed} guessed · {plan.Cleared} cleared · {plan.Skipped} skipped · {plan.Unresolved} unresolved");

        ImGui.Checkbox("Save as a Glamourer design", ref _saveDesign);

        if (_saveDesign)
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##design-name", ref _designName, 128);
        }

        var busy = _importer.Busy;
        var availability = _importer.GlamourerIpc.Check(out var glamourerMessage, out _, out _);
        var nothingToSend = plan.Sendable == 0;

        string? disabledReason = busy
            ? (_importer.BusyLabel.Length > 0 ? _importer.BusyLabel : "Busy...")
            : availability != GlamourerAvailability.Ready
                ? glamourerMessage
                : nothingToSend
                    ? "Nothing here can be applied."
                    : null;

        bool clicked;
        using (ImRaii.Disabled(disabledReason is not null))
        {
            using (HubStyle.Primary())
            {
                clicked = ImGui.Button("Apply to me");
            }
        }

        if (disabledReason is not null && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(disabledReason);
        }

        if (clicked)
        {
            _importer.StartApply(_saveDesign, _designName);
        }

        ImGui.SameLine();

        var isNewToLibrary = Plugin.Library.Find(plan.Id) is null;
        var saveLabel = isNewToLibrary ? "Save to library" : "Update in library";
        var saveDisabled = isNewToLibrary && Plugin.Library.IsFull;

        bool saveClicked;
        using (ImRaii.Disabled(saveDisabled))
        {
            saveClicked = ImGui.Button(saveLabel);
        }

        if (saveDisabled && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Your library is full ({LibraryStore.MaxOutfits} outfits). Delete one to make room.");
        }

        if (saveClicked)
        {
            Plugin.Library.AddOrUpdateFromPlan(plan);
            _saveMessage = isNewToLibrary ? "Saved to your library." : "Updated in your library.";
        }

        if (!string.IsNullOrEmpty(_importer.ApplyMessage) && ReferenceEquals(_importer.LastAppliedPlan, plan))
        {
            ImGui.TextColored(plan.Failed == 0 ? HubStyle.Good : HubStyle.Bad, _importer.ApplyMessage);
        }

        if (_saveMessage.Length > 0)
        {
            ImGui.TextColored(HubStyle.Good, _saveMessage);
        }
    }
}
