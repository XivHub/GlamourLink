using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using GlamourLink.Apply;
using XivHubPluginKit.UI;

namespace GlamourLink.Windows;

/// <summary>
/// The per-slot table shared by the main window's fetched plan and the
/// library window's selected outfit. The two windows never draw it in the
/// same frame-scope, so one fixed ImGui id is safe.
/// </summary>
internal static class PlanTable
{
    public static void Draw(GlamourPlan plan)
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
}
