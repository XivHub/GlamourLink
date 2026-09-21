using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Glamourer.Api.Enums;
using GlamourLink.Eorzea;
using GlamourLink.Game;

namespace GlamourLink.Apply;

public enum EntryStatus
{
    Resolved,
    Guessed,
    Cleared,
    Skipped,
    Unresolved,
    Applied,
    Failed,
}

public sealed class PlanEntry
{
    public EcSlotKey Key { get; init; }
    public string Label { get; init; } = "";
    public ApiEquipSlot? EquipSlot { get; init; }
    public bool IsBonus { get; init; }
    public string? RequestedName { get; init; }
    public string ResolvedName { get; set; } = "";
    public ulong ItemId { get; set; }
    public byte Stain1 { get; set; }
    public byte Stain2 { get; set; }
    public string DyeText { get; set; } = "-";
    public EntryStatus Status { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>
/// One Eorzea Collection glamour resolved against the game's sheets: what
/// will be sent to Glamourer, what could not be matched, and why. Built off
/// the framework thread and shown to the user before anything is applied.
/// </summary>
public sealed class GlamourPlan
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Character { get; init; } = "";
    public string Server { get; init; } = "";
    public IReadOnlyList<PlanEntry> Entries { get; init; } = [];

    public int Resolved => Count(EntryStatus.Resolved);
    public int Guessed => Count(EntryStatus.Guessed);
    public int Cleared => Count(EntryStatus.Cleared);
    public int Skipped => Count(EntryStatus.Skipped);
    public int Unresolved => Count(EntryStatus.Unresolved);

    private int Count(EntryStatus status) => Entries.Count(e => e.Status == status);

    public static GlamourPlan Build(EcGlamour glamour, ItemResolver items, StainResolver stains, Configuration cfg)
    {
        var gear = glamour.Gear ?? new EcGear();
        var entries = new List<PlanEntry>(14);

        foreach (var (key, item) in gear.Enumerate())
        {
            var entry = new PlanEntry
            {
                Key = key,
                Label = SlotMap.Label(key),
                EquipSlot = SlotMap.ToEquipSlot(key),
                IsBonus = SlotMap.IsBonus(key),
                RequestedName = item?.Name,
            };
            entries.Add(entry);

            if (key == EcSlotKey.Fashion)
            {
                entry.Status = EntryStatus.Skipped;
                entry.Note = "Fashion accessory; Glamourer has no slot for it.";
                continue;
            }

            var requested = item?.Name;
            if (string.IsNullOrWhiteSpace(requested))
            {
                FillEmpty(entry, key, cfg);
                continue;
            }

            if (entry.IsBonus)
            {
                ResolveFacewear(entry, items, requested, item?.Dyes);
                continue;
            }

            if (entry.EquipSlot is not { } slot)
            {
                entry.Status = EntryStatus.Skipped;
                entry.Note = "Glamourer has no slot for it.";
                continue;
            }

            ResolveEquipment(entry, items, stains, slot, requested, item?.Dyes);
        }

        return new GlamourPlan
        {
            Id = glamour.Id,
            Name = glamour.Name ?? "",
            Character = glamour.Character ?? "",
            Server = glamour.Server ?? "",
            Entries = entries,
        };
    }

    private static void FillEmpty(PlanEntry entry, EcSlotKey key, Configuration cfg)
    {
        if (SlotMap.IsWeapon(key))
        {
            if (cfg.ClearEmptyWeaponSlots)
            {
                Clear(entry);
            }
            else
            {
                entry.Status = EntryStatus.Skipped;
                entry.Note = "Left as-is so you keep a weapon.";
            }

            return;
        }

        if (cfg.ClearEmptyGearSlots)
        {
            Clear(entry);
        }
        else
        {
            entry.Status = EntryStatus.Skipped;
        }
    }

    /// <summary>
    /// Glamourer's <c>ResolveItem</c> replaces item id 0 with the slot's own
    /// Nothing item, for bonus slots as well as equipment, so clearing a slot
    /// needs no sentinel id.
    /// </summary>
    private static void Clear(PlanEntry entry)
    {
        entry.Status = EntryStatus.Cleared;
        entry.ItemId = 0;
        entry.ResolvedName = "Nothing";
    }

    private static void ResolveFacewear(PlanEntry entry, ItemResolver items, string requested, string? dyes)
    {
        if (items.TryResolveGlasses(requested, out var glassesId, out var matched, out var guessed))
        {
            entry.ItemId = glassesId;
            entry.ResolvedName = matched;
            entry.Status = guessed ? EntryStatus.Guessed : EntryStatus.Resolved;
            if (guessed)
            {
                Append(entry, $"Guessed from \"{requested}\".");
            }
        }
        else
        {
            entry.Status = EntryStatus.Unresolved;
            entry.Note = $"No item in the {entry.Label} slot is named that.";
        }

        // SetBonusItem takes a plain Glasses id with no stain component, and
        // glasses are dyed from a vocabulary that is not the Stain sheet, so a
        // facewear dye cannot be sent at all.
        var (first, second) = DyeSlugs.Parse(dyes);
        if (first is not null || second is not null)
        {
            Append(entry, "Facewear dye is not applicable.");
        }
    }

    private static void ResolveEquipment(PlanEntry entry, ItemResolver items, StainResolver stains, ApiEquipSlot slot,
        string requested, string? dyes)
    {
        if (items.TryResolveEquipment(slot, requested, out var itemId, out var matched, out var guessed))
        {
            entry.ItemId = itemId;
            entry.ResolvedName = matched;
            entry.Status = guessed ? EntryStatus.Guessed : EntryStatus.Resolved;
            if (guessed)
            {
                Append(entry, $"Guessed from \"{requested}\".");
            }

            if (SlotMap.IsWeapon(entry.Key))
            {
                Append(entry, "Weapons of another job's type only show in GPose.");
            }
        }
        else
        {
            entry.Status = EntryStatus.Unresolved;
            entry.Note = $"No item in the {entry.Label} slot is named that.";
        }

        ResolveDyes(entry, dyes, stains);
    }

    private static void ResolveDyes(PlanEntry entry, string? dyes, StainResolver stains)
    {
        var (first, second) = DyeSlugs.Parse(dyes);
        var names = new List<string>(2);

        entry.Stain1 = ResolveDye(entry, first, stains, names);
        entry.Stain2 = ResolveDye(entry, second, stains, names);
        entry.DyeText = names.Count > 0 ? string.Join(" + ", names) : "-";
    }

    private static byte ResolveDye(PlanEntry entry, string? slug, StainResolver stains, List<string> names)
    {
        if (slug is null)
        {
            return 0;
        }

        switch (stains.Resolve(slug, out var stainId))
        {
            case DyeLookup.Found:
                names.Add(StainDisplayName(slug));
                return stainId;
            case DyeLookup.Unknown:
                Append(entry, $"Unknown dye \"{slug}\".");
                return 0;
            case DyeLookup.OutOfRange:
                Append(entry, $"Dye \"{slug}\" is out of Glamourer's range.");
                return 0;
        }

        return 0;
    }

    /// <summary>
    /// An Eorzea Collection slug is the stain's own name lowercased and
    /// hyphenated, so title-casing the sheet name recovers it:
    /// <c>jet-black</c> is <c>Jet Black</c>.
    /// </summary>
    private static string StainDisplayName(string slug)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(DyeSlugs.ToSheetName(slug));

    private static void Append(PlanEntry entry, string note)
        => entry.Note = entry.Note.Length == 0 ? note : $"{entry.Note} {note}";
}
