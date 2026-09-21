using Glamourer.Api.Enums;
using GlamourLink.Eorzea;
using Lumina.Excel.Sheets;

namespace GlamourLink.Game;

/// <summary>
/// The EC slot key to Glamourer slot mapping measured in
/// GlamourLink-ec-api-notes.md, plus the reverse test against an
/// <see cref="Item"/> row's <see cref="Item.EquipSlotCategory"/>.
/// </summary>
public static class SlotMap
{
    public static ApiEquipSlot? ToEquipSlot(EcSlotKey key) => key switch
    {
        EcSlotKey.Head => ApiEquipSlot.Head,
        EcSlotKey.Body => ApiEquipSlot.Body,
        EcSlotKey.Hands => ApiEquipSlot.Hands,
        EcSlotKey.Legs => ApiEquipSlot.Legs,
        EcSlotKey.Feet => ApiEquipSlot.Feet,
        EcSlotKey.Weapon => ApiEquipSlot.MainHand,
        EcSlotKey.Offhand => ApiEquipSlot.OffHand,
        EcSlotKey.Earrings => ApiEquipSlot.Ears,
        EcSlotKey.Necklace => ApiEquipSlot.Neck,
        EcSlotKey.Bracelets => ApiEquipSlot.Wrists,
        EcSlotKey.LeftRing => ApiEquipSlot.LFinger,
        EcSlotKey.RightRing => ApiEquipSlot.RFinger,
        EcSlotKey.Fashion => null,
        EcSlotKey.Facewear => null,
        _ => null,
    };

    public static bool IsBonus(EcSlotKey key) => key == EcSlotKey.Facewear;

    public static bool IsWeapon(EcSlotKey key) => key is EcSlotKey.Weapon or EcSlotKey.Offhand;

    public static string Label(EcSlotKey key) => key switch
    {
        EcSlotKey.Head => "Head",
        EcSlotKey.Body => "Body",
        EcSlotKey.Hands => "Hands",
        EcSlotKey.Legs => "Legs",
        EcSlotKey.Feet => "Feet",
        EcSlotKey.Weapon => "Weapon",
        EcSlotKey.Offhand => "Off-hand",
        EcSlotKey.Earrings => "Earrings",
        EcSlotKey.Necklace => "Necklace",
        EcSlotKey.Bracelets => "Bracelets",
        EcSlotKey.LeftRing => "Left ring",
        EcSlotKey.RightRing => "Right ring",
        EcSlotKey.Fashion => "Fashion accessory",
        EcSlotKey.Facewear => "Facewear",
        _ => key.ToString(),
    };

    /// <summary>
    /// Whether an <see cref="Item"/> row's equip slot category admits
    /// <paramref name="slot"/>. A category column value of <c>1</c> means
    /// equippable there; both finger slots read from either
    /// <see cref="EquipSlotCategory.FingerL"/> or
    /// <see cref="EquipSlotCategory.FingerR"/> because most rings equip to
    /// either hand.
    /// </summary>
    public static bool FitsSlot(in Item row, ApiEquipSlot slot)
    {
        var category = row.EquipSlotCategory.Value;
        return slot switch
        {
            ApiEquipSlot.MainHand => category.MainHand == 1,
            ApiEquipSlot.OffHand => category.OffHand == 1,
            ApiEquipSlot.Head => category.Head == 1,
            ApiEquipSlot.Body => category.Body == 1,
            ApiEquipSlot.Hands => category.Gloves == 1,
            ApiEquipSlot.Legs => category.Legs == 1,
            ApiEquipSlot.Feet => category.Feet == 1,
            ApiEquipSlot.Ears => category.Ears == 1,
            ApiEquipSlot.Neck => category.Neck == 1,
            ApiEquipSlot.Wrists => category.Wrists == 1,
            ApiEquipSlot.LFinger or ApiEquipSlot.RFinger => category.FingerL == 1 || category.FingerR == 1,
            _ => false,
        };
    }
}
