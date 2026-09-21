using System;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using GlamourLink.Eorzea;
using Lumina.Excel.Exceptions;
using Lumina.Excel.Sheets;

namespace GlamourLink.Game;

public enum DyeLookup
{
    Found,
    Unknown,
    OutOfRange,
}

/// <summary>
/// Resolves an EC dye slug against the game's English <see cref="Stain"/>
/// sheet. No fuzzy fallback: slugs are machine-generated, and the only two
/// unresolvable ones measured in the sample (<c>gold</c>, <c>purple</c>)
/// were both facewear dyes, which are out of scope.
/// </summary>
public sealed class StainResolver
{
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _log;

    private readonly object _lock = new();
    private Dictionary<string, uint>? _stainsByName;

    public StainResolver(IDataManager dataManager, IPluginLog log)
    {
        _dataManager = dataManager;
        _log = log;
    }

    public DyeLookup Resolve(string slug, out byte stainId)
    {
        EnsureIndex();

        stainId = 0;

        var name = NameKey.Normalize(DyeSlugs.ToSheetName(slug));
        if (!_stainsByName!.TryGetValue(name, out var rowId))
        {
            return DyeLookup.Unknown;
        }

        if (rowId > byte.MaxValue)
        {
            return DyeLookup.OutOfRange;
        }

        stainId = (byte)rowId;
        return DyeLookup.Found;
    }

    private void EnsureIndex()
    {
        if (_stainsByName is not null)
        {
            return;
        }

        lock (_lock)
        {
            if (_stainsByName is not null)
            {
                return;
            }

            var byName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var sheet = _dataManager.GetExcelSheet<Stain>(ClientLanguage.English);
                foreach (var row in sheet)
                {
                    if (row.Name.ByteLength == 0)
                    {
                        continue;
                    }

                    byName[NameKey.Normalize(row.Name.ExtractText())] = row.RowId;
                }
            }
            catch (SheetNotFoundException ex)
            {
                _log.Error(ex, "GlamourLink could not load the Stain sheet; dye resolution will be unavailable.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "GlamourLink failed to build the stain index; dye resolution will be unavailable.");
            }

            _stainsByName = byName;
        }
    }
}
