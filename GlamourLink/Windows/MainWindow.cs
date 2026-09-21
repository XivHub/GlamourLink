using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GlamourLink.Apply;
using GlamourLink.Glamourer;
using XivHubPluginKit.UI;

namespace GlamourLink.Windows;

public sealed class MainWindow : Window
{
    private readonly GlamourImporter _importer;

    private string _input = "";
    private bool _saveDesign;
    private string _designName = "";
    private GlamourPlan? _boundPlan;

    public MainWindow(GlamourImporter importer) : base("GlamourLink###glamourlink-main")
    {
        _importer = importer;
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
            DrawPlanTable(plan);
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

    private void DrawPlanTable(GlamourPlan plan)
    {
        ImGui.TextUnformatted($"{plan.Name} — {plan.Character} ({plan.Server})");
        ImGui.SameLine();
        ImGui.TextColored(HubStyle.Info, $"#{plan.Id}");

        using var table = ImRaii.Table("glamourlink-plan", 4,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn("Slot");
        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Dye");
        ImGui.TableSetupColumn("Status");
        ImGui.TableHeadersRow();

        foreach (var entry in plan.Entries)
        {
            using var id = ImRaii.PushId((int)entry.Key);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextColored(HubStyle.Faint, entry.Label);
            DrawRowTooltip(entry);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(EntryItemText(entry));
            DrawRowTooltip(entry);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.DyeText);
            DrawRowTooltip(entry);

            ImGui.TableNextColumn();
            DrawStatusCell(entry);
            DrawRowTooltip(entry);
        }
    }

    private static string EntryItemText(PlanEntry entry) => entry.Status switch
    {
        EntryStatus.Unresolved => entry.RequestedName ?? "",
        EntryStatus.Skipped => entry.RequestedName ?? "-",
        _ => entry.ResolvedName,
    };

    private static void DrawStatusCell(PlanEntry entry)
    {
        if (entry.Apply == ApplyState.Pending)
        {
            var color = entry.Status switch
            {
                EntryStatus.Resolved => HubStyle.Good,
                EntryStatus.Guessed or EntryStatus.Cleared or EntryStatus.Skipped => HubStyle.Warn,
                EntryStatus.Unresolved => HubStyle.Bad,
                _ => HubStyle.Faint,
            };
            ImGui.TextColored(color, entry.Status.ToString());
        }
        else
        {
            var color = entry.Apply == ApplyState.Applied ? HubStyle.Good : HubStyle.Bad;
            ImGui.TextColored(color, entry.Apply.ToString());
        }
    }

    /// <summary>
    /// Note plus ApplyNote on a second line, so a slot that resolved by a
    /// guess still reads as a guess after it has been sent to Glamourer.
    /// </summary>
    private static void DrawRowTooltip(PlanEntry entry)
    {
        if (entry.Note.Length == 0 && entry.ApplyNote.Length == 0)
        {
            return;
        }

        if (!ImGui.IsItemHovered())
        {
            return;
        }

        if (entry.Note.Length > 0)
        {
            ImGui.SetTooltip(entry.ApplyNote.Length > 0 ? $"{entry.Note}\n{entry.ApplyNote}" : entry.Note);
        }
        else
        {
            ImGui.SetTooltip(entry.ApplyNote);
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
                    ? "Nothing in this plan would be sent."
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

        if (!string.IsNullOrEmpty(_importer.ApplyMessage))
        {
            ImGui.TextColored(plan.Failed == 0 ? HubStyle.Good : HubStyle.Bad, _importer.ApplyMessage);
        }
    }
}
