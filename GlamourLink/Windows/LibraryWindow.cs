using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GlamourLink.Apply;
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
        Rebuild();
        DrawFilterBar();
        DrawList();
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
}
