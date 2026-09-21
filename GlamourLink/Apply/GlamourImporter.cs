using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GlamourLink.Eorzea;
using GlamourLink.Game;
using GlamourLink.Glamourer;

namespace GlamourLink.Apply;

/// <summary>
/// Owns the whole import-and-apply pipeline for the main window: the Eorzea
/// Collection client, the two sheet resolvers, the Glamourer IPC surface and
/// the applier. Fetch and apply share one busy flag, so the window never has
/// to reason about the two racing each other.
///
/// Background work publishes by assigning <see cref="Plan"/> once, at the
/// end of the operation; the draw loop only ever reads it.
/// </summary>
public sealed class GlamourImporter : IDisposable
{
    /// <summary>Recent-imports cap; a constant rather than a setting.</summary>
    private const int RecentCap = 15;
    private static readonly TimeSpan MinFetchGap = TimeSpan.FromSeconds(2);

    private readonly Configuration _cfg;
    private readonly IPluginLog _log;
    private readonly EorzeaCollectionClient _client;
    private readonly ItemResolver _items;
    private readonly StainResolver _stains;
    private readonly GlamourApplier _applier;

    private int _busy;
    private DateTime _lastFetchStartUtc = DateTime.MinValue;
    private CancellationTokenSource? _cts;

    public GlamourImporter(Configuration cfg, IDataManager dataManager, IPluginLog log,
        IDalamudPluginInterface pluginInterface, IFramework framework, IObjectTable objectTable)
    {
        _cfg = cfg;
        _log = log;
        _client = new EorzeaCollectionClient(cfg, log);
        _items = new ItemResolver(dataManager, log);
        _stains = new StainResolver(dataManager, log);
        GlamourerIpc = new GlamourerIpc(pluginInterface, log);
        _applier = new GlamourApplier(GlamourerIpc, framework, objectTable);
    }

    public GlamourerIpc GlamourerIpc { get; }

    public GlamourPlan? Plan { get; private set; }
    public string? Error { get; private set; }
    public string? ApplyMessage { get; private set; }

    public bool Busy => Volatile.Read(ref _busy) != 0;
    public string BusyLabel { get; private set; } = "";

    public void StartImport(string input)
    {
        if (Busy)
        {
            return;
        }

        if (DateTime.UtcNow - _lastFetchStartUtc < MinFetchGap)
        {
            Error = "Give Eorzea Collection a moment.";
            return;
        }

        if (!EorzeaUrl.TryParseId(input, out var id, out var parseError))
        {
            Error = parseError;
            return;
        }

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return;
        }

        _lastFetchStartUtc = DateTime.UtcNow;
        Error = null;
        ApplyMessage = null;
        BusyLabel = "Fetching...";

        var cts = ReplaceCts();
        _ = Task.Run(() => RunImportAsync(id, cts.Token));
    }

    public void StartApply(bool saveDesign, string designName)
    {
        if (Busy || Plan is not { } plan)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return;
        }

        ApplyMessage = null;
        BusyLabel = "Applying...";

        var cts = ReplaceCts();
        _ = Task.Run(() => RunApplyAsync(plan, saveDesign, designName, cts.Token));
    }

    /// <summary>
    /// Removes any existing entry with the same id, inserts the new one at
    /// index 0 and truncates to <see cref="RecentCap"/>, so the list reads
    /// newest first with no separate sort step.
    /// </summary>
    public void RememberRecent(GlamourPlan plan)
    {
        var recents = _cfg.Recents;
        recents.RemoveAll(r => r.Id == plan.Id);
        recents.Insert(0, new Configuration.RecentImport
        {
            Id = plan.Id,
            Name = plan.Name,
            Character = plan.Character,
            LastUsedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });

        if (recents.Count > RecentCap)
        {
            recents.RemoveRange(RecentCap, recents.Count - RecentCap);
        }

        _cfg.Save();
    }

    private CancellationTokenSource ReplaceCts()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        var cts = new CancellationTokenSource();
        _cts = cts;
        return cts;
    }

    private async Task RunImportAsync(int id, CancellationToken ct)
    {
        try
        {
            var result = await _client.FetchAsync(id, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (result.Status != FetchStatus.Ok || result.Glamour is null)
            {
                if (result.Status != FetchStatus.Cancelled)
                {
                    Error = result.Message;
                }

                return;
            }

            var plan = GlamourPlan.Build(result.Glamour, _items, _stains, _cfg);
            Error = null;
            Plan = plan;
            RememberRecent(plan);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "GlamourLink import failed.");
            Error = "Something went wrong importing that glamour.";
        }
        finally
        {
            BusyLabel = "";
            Volatile.Write(ref _busy, 0);
        }
    }

    private async Task RunApplyAsync(GlamourPlan plan, bool saveDesign, string designName, CancellationToken ct)
    {
        try
        {
            var outcome = await _applier
                .ApplyAsync(plan, saveDesign, designName, ct, status => BusyLabel = status)
                .ConfigureAwait(false);

            if (!ct.IsCancellationRequested)
            {
                ApplyMessage = outcome.Message;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "GlamourLink apply failed.");
            ApplyMessage = "Something went wrong applying that glamour.";
        }
        finally
        {
            BusyLabel = "";
            Volatile.Write(ref _busy, 0);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _client.Dispose();
    }
}
