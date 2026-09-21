using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using GlamourLink.Apply;
using GlamourLink.Eorzea;
using GlamourLink.Game;

namespace GlamourLink.Library;

/// <summary>
/// One slot of a <see cref="SavedOutfit"/>: the fields of a <see cref="PlanEntry"/>
/// that are not derivable from <see cref="Key"/> via <see cref="SlotMap"/> and are
/// not runtime apply state.
/// </summary>
public sealed class SavedSlot
{
    public EcSlotKey Key { get; set; }
    public EntryStatus Status { get; set; }
    public string? RequestedName { get; set; }
    public string ResolvedName { get; set; } = "";
    public ulong ItemId { get; set; }
    public byte Stain1 { get; set; }
    public byte Stain2 { get; set; }
    public string DyeText { get; set; } = "-";
    public string Note { get; set; } = "";
}

/// <summary>
/// A glamour's fully resolved gear, stored so it applies with no network call and
/// survives the Eorzea Collection endpoint changing. <see cref="FromPlan"/> and
/// <see cref="ToPlan"/> are pure field copies with no resolution logic, so the plan
/// the user previewed is the plan that is stored and the plan that is replayed.
/// </summary>
public sealed class SavedOutfit
{
    public int EcId { get; set; }
    public string Name { get; set; } = "";
    public string Character { get; set; } = "";
    public string Server { get; set; } = "";
    public List<SavedSlot> Slots { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public bool Favourite { get; set; }
    public long SavedAtUnix { get; set; }
    public long LastAppliedUnix { get; set; }
    public string Note { get; set; } = "";

    [JsonIgnore]
    public bool HasWeapon => Slots.Any(s => SlotMap.IsWeapon(s.Key) && s.ItemId != 0);

    /// <summary> The character and server as shown to the user; both are blank for a hand-edited file. </summary>
    [JsonIgnore]
    public string DisplayCharacter => Character.Length == 0 ? "Unknown" : Character;

    [JsonIgnore]
    public string DisplayServer => Server.Length == 0 ? "Unknown" : Server;

    public static SavedOutfit FromPlan(GlamourPlan plan)
    {
        var outfit = new SavedOutfit
        {
            EcId = plan.Id,
            Name = plan.Name,
            Character = plan.Character,
            Server = plan.Server,
            SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LastAppliedUnix = 0,
        };

        foreach (var entry in plan.Entries)
        {
            outfit.Slots.Add(new SavedSlot
            {
                Key = entry.Key,
                Status = entry.Status,
                RequestedName = entry.RequestedName,
                ResolvedName = entry.ResolvedName,
                ItemId = entry.ItemId,
                Stain1 = entry.Stain1,
                Stain2 = entry.Stain2,
                DyeText = entry.DyeText,
                Note = entry.Note,
            });
        }

        return outfit;
    }

    public GlamourPlan ToPlan()
    {
        var entries = new List<PlanEntry>(Slots.Count);
        foreach (var slot in Slots)
        {
            entries.Add(new PlanEntry
            {
                Key = slot.Key,
                Label = SlotMap.Label(slot.Key),
                EquipSlot = SlotMap.ToEquipSlot(slot.Key),
                IsBonus = SlotMap.IsBonus(slot.Key),
                RequestedName = slot.RequestedName,
                ResolvedName = slot.ResolvedName,
                ItemId = slot.ItemId,
                Stain1 = slot.Stain1,
                Stain2 = slot.Stain2,
                DyeText = slot.DyeText,
                Status = slot.Status,
                Note = slot.Note,
                Apply = ApplyState.Pending,
                ApplyNote = "",
            });
        }

        return new GlamourPlan
        {
            Id = EcId,
            Name = Name,
            Character = Character,
            Server = Server,
            Entries = entries,
        };
    }
}
