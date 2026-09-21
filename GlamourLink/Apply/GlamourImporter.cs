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
/// The result of a refetch started by <see cref="GlamourImporter.StartRefetch"/>. A
/// failed refetch carries a null <see cref="Plan"/> and the fetch failure's message.
/// </summary>
public sealed record RefetchOutcome(int EcId, GlamourPlan? Plan, string Message);

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
    private RefetchOutcome? _refetch;

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

    /// <summary>
    /// The plan an apply was last started for. Both windows compare this by
    /// reference against the plan they are drawing before showing
    /// <see cref="ApplyMessage"/>, so an apply started in one window does not
    /// print its result under the other window's button.
    /// </summary>
    public GlamourPlan? LastAppliedPlan { get; private set; }

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

    public void StartApply(GlamourPlan plan, bool saveDesign, string designName)
    {
        if (Busy)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return;
        }

        LastAppliedPlan = plan;
        ApplyMessage = null;
        BusyLabel = "Applying...";

        var cts = ReplaceCts();
        _ = Task.Run(() => RunApplyAsync(plan, saveDesign, designName, cts.Token));
    }

    public void StartApply(bool saveDesign, string designName)
    {
        if (Plan is not { } plan)
        {
            return;
        }

        StartApply(plan, saveDesign, designName);
    }

    /// <summary>
    /// Re-fetches an already-saved outfit's id and re-resolves it against the
    /// sheets, sharing <see cref="StartImport"/>'s busy flag and
    /// <see cref="MinFetchGap"/> floor so refetch cannot hammer the endpoint
    /// any faster than an ordinary fetch could. Returns a message when it
    /// declines to start, or <c>null</c> once the background task is running.
    /// Does not touch <see cref="Plan"/> or <see cref="Error"/>; the outcome
    /// is picked up by <see cref="TakeRefetch"/> instead.
    /// </summary>
    public string? StartRefetch(int ecId)
    {
        if (Busy)
        {
            return "Wait for the current fetch to finish.";
        }

        if (DateTime.UtcNow - _lastFetchStartUtc < MinFetchGap)
        {
            return "Give Eorzea Collection a moment.";
        }

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return "Wait for the current fetch to finish.";
        }

        _lastFetchStartUtc = DateTime.UtcNow;
        BusyLabel = "Refetching...";

        var cts = ReplaceCts();
        _ = Task.Run(() => RunRefetchAsync(ecId, cts.Token));
        return null;
    }

    /// <summary>
    /// Returns and clears the last refetch outcome, so exactly one draw-loop
    /// pass consumes it. Called from the library window only, and only while
    /// it is open; an outcome produced while it is closed is dropped and the
    /// outfit is left unchanged.
    /// </summary>
    public RefetchOutcome? TakeRefetch() => Interlocked.Exchange(ref _refetch, null);

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

    private async Task RunRefetchAsync(int ecId, CancellationToken ct)
    {
        try
        {
            var result = await _client.FetchAsync(ecId, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (result.Status != FetchStatus.Ok || result.Glamour is null)
            {
                if (result.Status != FetchStatus.Cancelled)
                {
                    Interlocked.Exchange(ref _refetch, new RefetchOutcome(ecId, null, result.Message));
                }

                return;
            }

            var plan = GlamourPlan.Build(result.Glamour, _items, _stains, _cfg);
            Interlocked.Exchange(ref _refetch, new RefetchOutcome(ecId, plan, result.Message));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "GlamourLink refetch failed.");
            Interlocked.Exchange(ref _refetch, new RefetchOutcome(ecId, null, "Something went wrong refetching that glamour."));
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
