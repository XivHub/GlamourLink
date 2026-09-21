using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using GlamourLink.Glamourer;

namespace GlamourLink.Apply;

public sealed record ApplyOutcome(bool Ok, string Message, Guid DesignGuid);

/// <summary>
/// Sends a built <see cref="GlamourPlan"/> to Glamourer. Every Glamourer call
/// lives inside one of this class's two framework-thread hops: the apply pass,
/// and the optional design save two ticks later.
/// </summary>
public sealed class GlamourApplier
{
    private readonly GlamourerIpc _ipc;
    private readonly IFramework _framework;
    private readonly IObjectTable _objectTable;

    public GlamourApplier(GlamourerIpc ipc, IFramework framework, IObjectTable objectTable)
    {
        _ipc = ipc;
        _framework = framework;
        _objectTable = objectTable;
    }

    public async Task<ApplyOutcome> ApplyAsync(GlamourPlan plan, bool saveDesign, string designName, CancellationToken ct,
        Action<string>? onStatusChange = null)
    {
        ApplyStep step;
        try
        {
            step = await _framework.RunOnFrameworkThread(() => ApplyOnFramework(plan)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new ApplyOutcome(false, "", Guid.Empty);
        }

        if (!step.Ok || !saveDesign || step.Applied == 0)
        {
            return new ApplyOutcome(step.Ok, step.Message, Guid.Empty);
        }

        try
        {
            onStatusChange?.Invoke("Saving design...");

            // Glamourer settles the frame's equipment changes before the state
            // is readable, so the save waits two ticks rather than reading back
            // inside the apply hop.
            var save = await _framework
                .RunOnTick(() => SaveOnFramework(step.ObjectIndex, designName), delayTicks: 2, cancellationToken: ct)
                .ConfigureAwait(false);
            return new ApplyOutcome(save.Ok, $"{step.Message} {save.Message}", save.DesignGuid);
        }
        catch (OperationCanceledException)
        {
            return new ApplyOutcome(step.Ok, step.Message, Guid.Empty);
        }
    }

    private ApplyStep ApplyOnFramework(GlamourPlan plan)
    {
        // The draw loop's cached answer can be a second old. An apply happens once
        // per button press, so ask Glamourer directly rather than risk reporting a
        // failure slot by slot when it was simply unloaded.
        _ipc.InvalidateCheck();
        if (_ipc.Check(out var message, out _, out _) != GlamourerAvailability.Ready)
        {
            return new ApplyStep(false, message, 0, 0, 0);
        }

        var player = _objectTable.LocalPlayer;
        if (player is null)
        {
            return new ApplyStep(false, "Log in first.", 0, 0, 0);
        }

        var objectIndex = (int)player.ObjectIndex;
        var applied = 0;
        var failed = 0;

        foreach (var entry in plan.Entries)
        {
            entry.Apply = ApplyState.Pending;
            entry.ApplyNote = "";

            if (!entry.WillSend)
            {
                continue;
            }

            bool sent;
            GlamourerApiEc ec;
            string error;

            if (entry.IsBonus)
            {
                sent = _ipc.TrySetBonusItem(objectIndex, entry.ItemId, out ec, out error);
            }
            else if (entry.EquipSlot is { } slot)
            {
                sent = _ipc.TrySetItem(objectIndex, slot, entry.ItemId, entry.Stain1, entry.Stain2, out ec, out error);
            }
            else
            {
                entry.Apply = ApplyState.Failed;
                entry.ApplyNote = "Glamourer has no slot for it.";
                failed++;
                continue;
            }

            if (!sent)
            {
                entry.Apply = ApplyState.Failed;
                entry.ApplyNote = error;
                failed++;
                continue;
            }

            switch (ec)
            {
                case GlamourerApiEc.Success:
                    entry.Apply = ApplyState.Applied;
                    applied++;
                    break;
                case GlamourerApiEc.NothingDone:
                    entry.Apply = ApplyState.Applied;
                    entry.ApplyNote = "The slot already looked like this.";
                    applied++;
                    break;
                default:
                    entry.Apply = ApplyState.Failed;
                    entry.ApplyNote = $"Glamourer returned {ec}.";
                    failed++;
                    break;
            }
        }

        return new ApplyStep(applied > 0, Summarise(applied, failed), applied, failed, objectIndex);
    }

    private SaveStep SaveOnFramework(int objectIndex, string designName)
    {
        if (!_ipc.TryGetStateBase64(objectIndex, out var state, out var stateError) || state is null)
        {
            return new SaveStep(false, stateError, Guid.Empty);
        }

        var name = string.IsNullOrWhiteSpace(designName) ? "GlamourLink import" : designName.Trim();
        if (!_ipc.TryAddDesign(state, name, out var guid, out var addError))
        {
            return new SaveStep(false, addError, Guid.Empty);
        }

        return new SaveStep(true, $"Saved as design \"{name}\".", guid);
    }

    private static string Summarise(int applied, int failed)
    {
        if (applied == 0 && failed == 0)
        {
            return "Nothing in this plan could be applied.";
        }

        if (applied == 0)
        {
            return $"Nothing applied; {failed} {Slots(failed)} failed.";
        }

        if (failed == 0)
        {
            return $"Applied {applied} {Slots(applied)}.";
        }

        return $"Applied {applied} of {applied + failed} slots; {failed} failed.";
    }

    private static string Slots(int count) => count == 1 ? "slot" : "slots";

    private readonly record struct ApplyStep(bool Ok, string Message, int Applied, int Failed, int ObjectIndex);

    private readonly record struct SaveStep(bool Ok, string Message, Guid DesignGuid);
}
