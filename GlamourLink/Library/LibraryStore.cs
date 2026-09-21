using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Plugin.Services;
using GlamourLink.Apply;

namespace GlamourLink.Library;

/// <summary>The on-disk shape of <c>library.json</c>: a version tag plus the outfits.</summary>
public sealed class OutfitLibrary
{
    public int Version { get; set; } = 1;
    public List<SavedOutfit> Outfits { get; set; } = new();
}

/// <summary>
/// Loads and saves the saved-outfit library. The constructor reads the file once;
/// after that the store is touched only from the draw loop (a button press calling
/// a mutator), so it holds no lock.
/// </summary>
public sealed class LibraryStore
{
    /// <summary>Outfit cap; a constant rather than a setting, so the load-time read stays bounded.</summary>
    public const int MaxOutfits = 500;

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IPluginLog _log;
    private OutfitLibrary _library = new();

    public LibraryStore(string pluginConfigDirectory, IPluginLog log)
    {
        _log = log;
        Path = System.IO.Path.Combine(pluginConfigDirectory, "library.json");
        Load();
    }

    public string Path { get; }

    public IReadOnlyList<SavedOutfit> Outfits => _library.Outfits;

    public int Count => _library.Outfits.Count;

    public bool IsFull => Count >= MaxOutfits;

    private void Load()
    {
        if (!File.Exists(Path))
        {
            return;
        }

        OutfitLibrary? loaded;
        try
        {
            var text = File.ReadAllText(Path);
            loaded = JsonSerializer.Deserialize<OutfitLibrary>(text, _json);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Could not parse {Path}; loading an empty library.");
            BackupUnreadable();
            return;
        }

        if (loaded is null)
        {
            _log.Error($"{Path} deserialised to nothing; loading an empty library.");
            BackupUnreadable();
            return;
        }

        if (loaded.Version > 1)
        {
            _log.Warning($"{Path} was written by a newer GlamourLink (version {loaded.Version}); loading it anyway.");
        }

        _library = loaded;
    }

    /// <summary>
    /// Moves an unreadable library file aside so the user can recover it, rather
    /// than overwriting or deleting it. If the move itself fails, the file is left
    /// untouched and the library simply loads empty for this session.
    /// </summary>
    private void BackupUnreadable()
    {
        var backup = $"{Path}.corrupt-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.json";
        try
        {
            File.Move(Path, backup);
            _log.Error($"Moved the unreadable library to {backup}.");
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Could not move the unreadable library file at {Path}; leaving it in place.");
        }
    }

    public void Save()
    {
        try
        {
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_library, _json));
            File.Move(tmp, Path, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Could not save the outfit library to {Path}.");
        }
    }

    public SavedOutfit? Find(int ecId)
    {
        foreach (var outfit in _library.Outfits)
        {
            if (outfit.EcId == ecId)
            {
                return outfit;
            }
        }

        return null;
    }

    /// <summary>
    /// Merges a fetched plan into the library by EC id. An existing record keeps its
    /// name, tags, favourite, note and saved/last-applied timestamps; only the gear
    /// and the character/server it was fetched under are replaced. This is the only
    /// path a refetch takes. The insert branch — a brand-new record — is reached
    /// only from the save button.
    /// </summary>
    public SavedOutfit? AddOrUpdateFromPlan(GlamourPlan plan)
    {
        var fresh = SavedOutfit.FromPlan(plan);
        var existing = Find(plan.Id);
        if (existing is not null)
        {
            existing.Character = fresh.Character;
            existing.Server = fresh.Server;
            existing.Slots = fresh.Slots;
            Save();
            return existing;
        }

        if (IsFull)
        {
            return null;
        }

        _library.Outfits.Insert(0, fresh);
        Save();
        return fresh;
    }

    public void Remove(SavedOutfit outfit)
    {
        _library.Outfits.Remove(outfit);
        Save();
    }

    public void MarkApplied(SavedOutfit outfit)
    {
        outfit.LastAppliedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
    }
}
