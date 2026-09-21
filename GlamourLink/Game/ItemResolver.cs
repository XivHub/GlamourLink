using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using Lumina.Excel.Exceptions;
using Lumina.Excel.Sheets;

namespace GlamourLink.Game;

/// <summary>
/// Resolves Eorzea Collection item and glasses names against the game's own
/// English <see cref="Item"/> and <see cref="Glasses"/> sheets. Both caches
/// are built lazily, once, under a lock, and a sheet that throws leaves its
/// index permanently empty instead of taking the import down.
/// </summary>
public sealed class ItemResolver
{
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _log;

    private readonly object _itemLock = new();
    private Dictionary<string, List<uint>>? _itemsByName;
    private Dictionary<ApiEquipSlot, List<(uint Id, string Name)>>? _itemsBySlot;

    private readonly object _glassesLock = new();
    private Dictionary<string, List<(uint Id, string Name)>>? _glassesByName;
    private List<(uint Id, string Name)>? _glassesCandidates;

    public ItemResolver(IDataManager dataManager, IPluginLog log)
    {
        _dataManager = dataManager;
        _log = log;
    }

    public bool TryResolveEquipment(ApiEquipSlot slot, string requestedName, out uint itemId, out string matchedName, out bool guessed)
    {
        EnsureItemIndex();

        itemId = 0;
        matchedName = "";
        guessed = false;

        var query = NameKey.Normalize(requestedName);
        List<(uint Id, string Name)> slotCandidates = _itemsBySlot!.TryGetValue(slot, out var forSlot) ? forSlot : [];

        if (_itemsByName!.TryGetValue(query, out var exactIds))
        {
            uint? bestId = null;
            foreach (var candidateId in exactIds)
            {
                foreach (var (id, name) in slotCandidates)
                {
                    if (id != candidateId)
                    {
                        continue;
                    }

                    if (bestId is null || id < bestId)
                    {
                        bestId = id;
                        matchedName = name;
                    }
                }
            }

            if (bestId is not null)
            {
                itemId = bestId.Value;
                guessed = false;
                return true;
            }
        }

        if (FuzzyName.TryBestMatch(slotCandidates, query, maxDistance: 2, out var fuzzyId, out var fuzzyName))
        {
            itemId = fuzzyId;
            matchedName = fuzzyName;
            guessed = true;
            return true;
        }

        return false;
    }

    public bool TryResolveGlasses(string requestedName, out ushort glassesId, out string matchedName, out bool guessed)
    {
        EnsureGlassesIndex();

        glassesId = 0;
        matchedName = "";
        guessed = false;

        var query = NameKey.Normalize(requestedName);

        if (_glassesByName!.TryGetValue(query, out var exact) && exact.Count > 0)
        {
            var (id, name) = exact[0];
            glassesId = (ushort)id;
            matchedName = name;
            guessed = false;
            return true;
        }

        if (FuzzyName.TryBestMatch(_glassesCandidates!, query, maxDistance: 2, out var fuzzyId, out var fuzzyName))
        {
            glassesId = (ushort)fuzzyId;
            matchedName = fuzzyName;
            guessed = true;
            return true;
        }

        return false;
    }

    private void EnsureItemIndex()
    {
        if (_itemsByName is not null)
        {
            return;
        }

        lock (_itemLock)
        {
            if (_itemsByName is not null)
            {
                return;
            }

            var byName = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
            var bySlot = new Dictionary<ApiEquipSlot, List<(uint Id, string Name)>>();

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var sheet = _dataManager.GetExcelSheet<Item>(ClientLanguage.English);
                var rowCount = 0;

                foreach (var row in sheet)
                {
                    if (row.Name.ByteLength == 0 || row.EquipSlotCategory.RowId == 0)
                    {
                        continue;
                    }

                    var name = NameKey.Normalize(row.Name.ExtractText());
                    if (!byName.TryGetValue(name, out var ids))
                    {
                        ids = [];
                        byName[name] = ids;
                    }

                    ids.Add(row.RowId);

                    foreach (var slot in EquipSlots)
                    {
                        if (SlotMap.FitsSlot(row, slot))
                        {
                            if (!bySlot.TryGetValue(slot, out var list))
                            {
                                list = [];
                                bySlot[slot] = list;
                            }

                            list.Add((row.RowId, name));
                        }
                    }

                    rowCount++;
                }

                _log.Information($"GlamourLink built the item index: {rowCount} rows in {stopwatch.ElapsedMilliseconds} ms.");
                Plugin.Dev($"item index: {rowCount} rows in {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (SheetNotFoundException ex)
            {
                _log.Error(ex, "GlamourLink could not load the Item sheet; item resolution will be unavailable.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "GlamourLink failed to build the item index; item resolution will be unavailable.");
            }

            _itemsBySlot = bySlot;
            _itemsByName = byName;
        }
    }

    private void EnsureGlassesIndex()
    {
        if (_glassesByName is not null)
        {
            return;
        }

        lock (_glassesLock)
        {
            if (_glassesByName is not null)
            {
                return;
            }

            var byName = new Dictionary<string, List<(uint Id, string Name)>>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<(uint Id, string Name)>();

            try
            {
                var sheet = _dataManager.GetExcelSheet<Glasses>(ClientLanguage.English);

                foreach (var row in sheet)
                {
                    if (row.Name.ByteLength == 0)
                    {
                        continue;
                    }

                    var name = NameKey.Normalize(row.Name.ExtractText());
                    if (!byName.TryGetValue(name, out var list))
                    {
                        list = [];
                        byName[name] = list;
                    }

                    list.Add((row.RowId, name));
                    candidates.Add((row.RowId, name));
                }
            }
            catch (SheetNotFoundException ex)
            {
                _log.Error(ex, "GlamourLink could not load the Glasses sheet; facewear resolution will be unavailable.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "GlamourLink failed to build the glasses index; facewear resolution will be unavailable.");
            }

            _glassesCandidates = candidates;
            _glassesByName = byName;
        }
    }

    private static readonly ApiEquipSlot[] EquipSlots =
    [
        ApiEquipSlot.MainHand,
        ApiEquipSlot.OffHand,
        ApiEquipSlot.Head,
        ApiEquipSlot.Body,
        ApiEquipSlot.Hands,
        ApiEquipSlot.Legs,
        ApiEquipSlot.Feet,
        ApiEquipSlot.Ears,
        ApiEquipSlot.Neck,
        ApiEquipSlot.Wrists,
        ApiEquipSlot.LFinger,
        ApiEquipSlot.RFinger,
    ];
}
