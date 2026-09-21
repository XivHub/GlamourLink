using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GlamourLink.Eorzea;

public enum EcSlotKey
{
    Head,
    Body,
    Hands,
    Legs,
    Feet,
    Weapon,
    Offhand,
    Earrings,
    Necklace,
    Bracelets,
    LeftRing,
    RightRing,
    Fashion,
    Facewear,
}

public sealed class EcItem
{
    public string? Name { get; set; }
    public string? Dyes { get; set; }
}

public sealed class EcGear
{
    public EcItem? Head { get; set; }
    public EcItem? Body { get; set; }
    public EcItem? Hands { get; set; }
    public EcItem? Legs { get; set; }
    public EcItem? Feet { get; set; }
    public EcItem? Weapon { get; set; }
    public EcItem? Offhand { get; set; }
    public EcItem? Earrings { get; set; }
    public EcItem? Necklace { get; set; }
    public EcItem? Bracelets { get; set; }

    [JsonPropertyName("left_ring")]
    public EcItem? LeftRing { get; set; }

    [JsonPropertyName("right_ring")]
    public EcItem? RightRing { get; set; }

    public EcItem? Fashion { get; set; }
    public EcItem? Facewear { get; set; }

    /// <summary>
    /// Every gear slot in the declared <see cref="EcSlotKey"/> order, so no
    /// other file spells the fourteen property accesses again.
    /// </summary>
    public IEnumerable<(EcSlotKey Key, EcItem? Item)> Enumerate()
    {
        yield return (EcSlotKey.Head, Head);
        yield return (EcSlotKey.Body, Body);
        yield return (EcSlotKey.Hands, Hands);
        yield return (EcSlotKey.Legs, Legs);
        yield return (EcSlotKey.Feet, Feet);
        yield return (EcSlotKey.Weapon, Weapon);
        yield return (EcSlotKey.Offhand, Offhand);
        yield return (EcSlotKey.Earrings, Earrings);
        yield return (EcSlotKey.Necklace, Necklace);
        yield return (EcSlotKey.Bracelets, Bracelets);
        yield return (EcSlotKey.LeftRing, LeftRing);
        yield return (EcSlotKey.RightRing, RightRing);
        yield return (EcSlotKey.Fashion, Fashion);
        yield return (EcSlotKey.Facewear, Facewear);
    }
}

public sealed class EcGlamour
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Character { get; set; }
    public string? Server { get; set; }
    public EcGear? Gear { get; set; }
}
