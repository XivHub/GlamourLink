using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GlamourLink.Apply;
using GlamourLink.Glamourer;
using GlamourLink.Library;
using XivHubPluginKit.UI;

namespace GlamourLink.Windows;

public sealed class LibraryWindow : Window
{
    private readonly GlamourImporter _importer;

    private string _search = "";
    private string _character = "";
    private string _server = "";
    private bool _favouritesOnly;
    private bool _hasWeaponOnly;

    private readonly List<SavedOutfit> _shown = new();
    private readonly List<string> _characters = new();
    private readonly List<string> _servers = new();
    private readonly HashSet<string> _distinct = new(StringComparer.Ordinal);

    private SavedOutfit? _selected;
    private SavedOutfit? _bound;
    private GlamourPlan? _selectedPlan;
    private string _nameEdit = "";
    private string _noteEdit = "";
    private string _tagInput = "";
    private string _detailMessage = "";
    private bool _saveDesign;
    private string _designName = "";

    public LibraryWindow(GlamourImporter importer) : base("GlamourLink Library###glamourlink-library")
    {
        _importer = importer;
        Size = new Vector2(820, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 380),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        HandleRefetchOutcome();
        Rebuild();
        DrawFilterBar();
        DrawList();
        DrawDetail();
    }

    /// <summary>
    /// Picks up at most one refetch result per frame. An outfit deleted while
    /// its refetch was in flight is looked up by id and, when it is gone, the
    /// outcome is dropped outright: merging it back in would re-insert the
    /// outfit with fresh gear and none of the user's tags, favourite, name or
    /// note, silently undoing the delete. Delete is not gated on
    /// <see cref="GlamourImporter.Busy"/>, so this race is reachable by hand.
    /// </summary>
    private void HandleRefetchOutcome()
    {
        var outcome = _importer.TakeRefetch();
        if (outcome is null)
        {
            return;
        }

        if (Plugin.Library.Find(outcome.EcId) is null)
        {
            return;
        }

        if (outcome.Plan is not null)
        {
            Plugin.Library.AddOrUpdateFromPlan(outcome.Plan);
            _bound = null;
        }
        else
        {
            _detailMessage = outcome.Message;
        }
    }

    private void DrawFilterBar()
    {
        ImGui.SetNextItemWidth(220);
        ImGui.InputTextWithHint("##search", "Search names and tags", ref _search, 64);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        using (var combo = ImRaii.Combo("##character", _character.Length == 0 ? "Any character" : _character))
        {
            if (combo)
            {
                if (ImGui.Selectable("Any character", _character.Length == 0))
                {
                    _character = "";
                }

                foreach (var character in _characters)
                {
                    if (ImGui.Selectable(character, _character == character))
                    {
                        _character = character;
                    }
                }
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        using (var combo = ImRaii.Combo("##server", _server.Length == 0 ? "Any server" : _server))
        {
            if (combo)
            {
                if (ImGui.Selectable("Any server", _server.Length == 0))
                {
                    _server = "";
                }

                foreach (var server in _servers)
                {
                    if (ImGui.Selectable(server, _server == server))
                    {
                        _server = server;
                    }
                }
            }
        }

        ImGui.SameLine();
        ImGui.Checkbox("Favourites", ref _favouritesOnly);

        ImGui.SameLine();
        ImGui.Checkbox("Has a weapon", ref _hasWeaponOnly);

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            _search = "";
            _character = "";
            _server = "";
            _favouritesOnly = false;
            _hasWeaponOnly = false;
        }

        ImGui.TextColored(HubStyle.Faint, $"{_shown.Count} of {Plugin.Library.Count} outfits");
    }

    /// <summary>
    /// Filters and sorts the library into <see cref="_shown"/> every frame this
    /// window is open. O(outfits) over at most 500 entries is far below the cost
    /// of the table draw below it, this only runs while the window is open, and
    /// caching it would add an invalidation path with no measurable gain.
    /// </summary>
    private void Rebuild()
    {
        _shown.Clear();
        _characters.Clear();
        _servers.Clear();
        _distinct.Clear();

        foreach (var outfit in Plugin.Library.Outfits)
        {
            var character = outfit.Character.Length == 0 ? "Unknown" : outfit.Character;
            if (_distinct.Add($"c:{character}"))
            {
                _characters.Add(character);
            }

            var server = outfit.Server.Length == 0 ? "Unknown" : outfit.Server;
            if (_distinct.Add($"s:{server}"))
            {
                _servers.Add(server);
            }
        }

        var term = _search.Trim();

        foreach (var outfit in Plugin.Library.Outfits)
        {
            if (term.Length > 0 && !Matches(outfit, term))
            {
                continue;
            }

            var character = outfit.Character.Length == 0 ? "Unknown" : outfit.Character;
            if (_character.Length > 0 && !character.Equals(_character, StringComparison.Ordinal))
            {
                continue;
            }

            var server = outfit.Server.Length == 0 ? "Unknown" : outfit.Server;
            if (_server.Length > 0 && !server.Equals(_server, StringComparison.Ordinal))
            {
                continue;
            }

            if (_favouritesOnly && !outfit.Favourite)
            {
                continue;
            }

            if (_hasWeaponOnly && !outfit.HasWeapon)
            {
                continue;
            }

            _shown.Add(outfit);
        }

        _shown.Sort((a, b) =>
        {
            var favourite = b.Favourite.CompareTo(a.Favourite);
            if (favourite != 0)
            {
                return favourite;
            }

            var aStamp = Math.Max(a.LastAppliedUnix, a.SavedAtUnix);
            var bStamp = Math.Max(b.LastAppliedUnix, b.SavedAtUnix);
            return bStamp.CompareTo(aStamp);
        });
    }

    private static bool Matches(SavedOutfit outfit, string term)
    {
        if (outfit.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var tag in outfit.Tags)
        {
            if (tag.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawList()
    {
        using (var child = ImRaii.Child("library-list", new Vector2(260, 0), true))
        {
            if (child)
            {
                if (Plugin.Library.Count == 0)
                {
                    ImGui.TextColored(HubStyle.Faint, "Fetch a glamour and press Save to library to put it here.");
                }
                else if (_shown.Count == 0)
                {
                    ImGui.TextColored(HubStyle.Faint, "No outfit matches those filters.");
                }
                else
                {
                    foreach (var outfit in _shown)
                    {
                        DrawRow(outfit);
                    }
                }
            }
        }

        ImGui.SameLine();
    }

    private void DrawRow(SavedOutfit outfit)
    {
        using var id = ImRaii.PushId(outfit.EcId);

        using (ImRaii.PushColor(ImGuiCol.Text, outfit.Favourite ? HubStyle.Accent : HubStyle.Faint))
        {
            if (ImGui.SmallButton("*"))
            {
                outfit.Favourite = !outfit.Favourite;
                Plugin.Library.Save();
            }
        }

        ImGui.SameLine();
        if (ImGui.Selectable(outfit.Name, ReferenceEquals(outfit, _selected)))
        {
            _selected = outfit;
        }

        var character = outfit.Character.Length == 0 ? "Unknown" : outfit.Character;
        var server = outfit.Server.Length == 0 ? "Unknown" : outfit.Server;
        ImGui.TextColored(HubStyle.Faint, $"{character} · {server}");

        for (var i = 0; i < outfit.Tags.Count; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
            }

            using (ImRaii.PushColor(ImGuiCol.Text, HubStyle.Info))
            {
                if (ImGui.SmallButton(outfit.Tags[i]))
                {
                    _search = outfit.Tags[i];
                }
            }
        }
    }

    /// <summary>
    /// Resets the per-outfit editor state exactly once per selection, keyed
    /// by reference. Materialising <see cref="_selectedPlan"/> once per
    /// selection rather than per frame is what lets the slot table keep
    /// showing each row's <see cref="ApplyState"/> after an apply.
    /// </summary>
    private void BindSelection()
    {
        if (ReferenceEquals(_bound, _selected))
        {
            return;
        }

        _bound = _selected;
        _selectedPlan = _selected?.ToPlan();
        _nameEdit = _selected?.Name ?? "";
        _noteEdit = _selected?.Note ?? "";
        _tagInput = "";
        _detailMessage = "";
        _saveDesign = Plugin.Configuration.SaveAsDesignByDefault;
        _designName = _selected?.Name ?? "";
    }

    private void DrawDetail()
    {
        BindSelection();

        using var child = ImRaii.Child("library-detail", new Vector2(0, 0), false);
        if (!child)
        {
            return;
        }

        if (_selected is null)
        {
            ImGui.TextColored(HubStyle.Faint, "Pick an outfit on the left.");
            return;
        }

        DrawDetailHeader();
        ImGui.Spacing();
        DrawNoteAndTags();
        ImGui.Spacing();
        PlanTable.Draw(_selectedPlan!);
        ImGui.Spacing();
        DrawApplyAndRefetch();
        ImGui.Spacing();
        DrawDeleteControls();
    }

    private void DrawDetailHeader()
    {
        var selected = _selected!;

        ImGui.InputText("##name", ref _nameEdit, 64);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            selected.Name = _nameEdit;
            Plugin.Library.Save();
        }

        ImGui.SameLine();
        ImGui.TextColored(HubStyle.Info, $"#{selected.EcId}");

        var savedDate = DateTimeOffset.FromUnixTimeSeconds(selected.SavedAtUnix).LocalDateTime;
        var line = $"{selected.Character} · {selected.Server} · saved {savedDate:yyyy-MM-dd}";
        if (selected.LastAppliedUnix > 0)
        {
            var appliedDate = DateTimeOffset.FromUnixTimeSeconds(selected.LastAppliedUnix).LocalDateTime;
            line += $" · last applied {appliedDate:yyyy-MM-dd}";
        }

        ImGui.TextColored(HubStyle.Faint, line);
    }

    private void DrawNoteAndTags()
    {
        var selected = _selected!;

        ImGui.InputTextMultiline("##note", ref _noteEdit, 500, new Vector2(-1, 56));
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            selected.Note = _noteEdit;
            Plugin.Library.Save();
        }

        for (var i = 0; i < selected.Tags.Count; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
            }

            if (ImGui.SmallButton($"{selected.Tags[i]} x"))
            {
                selected.Tags.RemoveAt(i);
                Plugin.Library.Save();
                break;
            }
        }

        if (selected.Tags.Count > 0)
        {
            ImGui.SameLine();
        }

        ImGui.SetNextItemWidth(160);
        var submitted =
            ImGui.InputTextWithHint("##tag", "add a tag", ref _tagInput, 24, ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();
        var addClicked = ImGui.Button("Add");

        if (submitted || addClicked)
        {
            AddTag(selected);
        }
    }

    private void AddTag(SavedOutfit outfit)
    {
        var tag = Regex.Replace(_tagInput.Trim(), @"\s+", " ");
        _tagInput = "";

        if (tag.Length == 0)
        {
            return;
        }

        if (outfit.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (outfit.Tags.Count >= 12)
        {
            _detailMessage = "Twelve tags is the limit.";
            return;
        }

        outfit.Tags.Add(tag);
        Plugin.Library.Save();
    }

    private void DrawApplyAndRefetch()
    {
        var selected = _selected!;
        var plan = _selectedPlan!;

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
            ? _importer.BusyLabel
            : availability != GlamourerAvailability.Ready
                ? glamourerMessage
                : nothingToSend
                    ? "Nothing in this outfit would be sent."
                    : null;

        bool applyClicked;
        using (ImRaii.Disabled(disabledReason is not null))
        {
            using (HubStyle.Primary())
            {
                applyClicked = ImGui.Button("Apply");
            }
        }

        if (disabledReason is not null && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(disabledReason);
        }

        ImGui.SameLine();
        bool refetchClicked;
        using (ImRaii.Disabled(_importer.Busy))
        {
            refetchClicked = ImGui.Button("Refetch");
        }

        if (refetchClicked)
        {
            var declineMessage = _importer.StartRefetch(selected.EcId);
            if (declineMessage is not null)
            {
                _detailMessage = declineMessage;
            }
        }

        if (applyClicked)
        {
            // Stamps the press, not the outcome: the outcome arrives on a
            // background continuation and the store is draw-loop-only.
            Plugin.Library.MarkApplied(selected);
            _importer.StartApply(plan, _saveDesign, _designName);
        }

        if (!string.IsNullOrEmpty(_importer.ApplyMessage) && ReferenceEquals(_importer.LastAppliedPlan, plan))
        {
            ImGui.TextColored(plan.Failed == 0 ? HubStyle.Good : HubStyle.Bad, _importer.ApplyMessage);
        }

        ImGui.TextColored(HubStyle.Faint,
            "Refetch updates the character, server and gear. Your name, tags, favourite and note are kept.");

        if (_detailMessage.Length > 0)
        {
            ImGui.TextColored(HubStyle.Bad, _detailMessage);
        }
    }

    private void DrawDeleteControls()
    {
        var selected = _selected!;

        if (ImGui.Button("Delete"))
        {
            ImGui.OpenPopup("delete-outfit");
        }

        using var popup = ImRaii.PopupModal("delete-outfit");
        if (popup)
        {
            ImGui.TextUnformatted($"Delete \"{selected.Name}\"? This cannot be undone.");

            if (ImGui.Button("Delete"))
            {
                Plugin.Library.Remove(selected);
                _selected = null;
                _bound = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }
        }
    }
}
