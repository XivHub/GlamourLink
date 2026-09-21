using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;

namespace GlamourLink.Glamourer;

public enum GlamourerAvailability
{
    Ready,
    NotInstalled,
    WrongMajor,
}

/// <summary>
/// The only place in GlamourLink that names a <c>Glamourer.Api</c> type.
/// Every call is wrapped: subscriber construction succeeds whether or not
/// Glamourer is loaded, but invocation throws
/// <see cref="IpcNotReadyError"/> when the provider is absent.
/// </summary>
public sealed class GlamourerIpc
{
    private readonly IPluginLog _log;

    private readonly ApiVersion _apiVersion;
    private readonly SetItem _setItem;
    private readonly SetBonusItem _setBonusItem;
    private readonly GetStateBase64 _getStateBase64;
    private readonly AddDesign _addDesign;

    private readonly object _versionLock = new();
    private (int Major, int Minor)? _loggedVersion;

    public GlamourerIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;
        _apiVersion = new ApiVersion(pluginInterface);
        _setItem = new SetItem(pluginInterface);
        _setBonusItem = new SetBonusItem(pluginInterface);
        _getStateBase64 = new GetStateBase64(pluginInterface);
        _addDesign = new AddDesign(pluginInterface);
    }

    private const int CheckCacheMs = 1000;

    private long _checkedAt = -CheckCacheMs;
    private GlamourerAvailability _cachedAvailability;
    private string _cachedMessage = "";
    private int _cachedMajor;
    private int _cachedMinor;

    /// <summary>
    /// Asks Glamourer for its API version, reusing the answer for a second.
    /// Callers include the draw loop, which asks once per control per frame;
    /// Glamourer being loaded or unloaded is a thing a person does, so a
    /// second-old answer is always current enough and an unload still lands
    /// well before anyone can press a button. Framework thread only.
    /// </summary>
    public GlamourerAvailability Check(out string message, out int major, out int minor)
    {
        var now = Environment.TickCount64;
        if (now - _checkedAt < CheckCacheMs)
        {
            message = _cachedMessage;
            major = _cachedMajor;
            minor = _cachedMinor;
            return _cachedAvailability;
        }

        var result = Query(out message, out major, out minor);

        _checkedAt = now;
        _cachedMessage = message;
        _cachedMajor = major;
        _cachedMinor = minor;
        _cachedAvailability = result;
        return result;
    }

    /// <summary> Drops the cached answer so the next <see cref="Check"/> asks Glamourer again. </summary>
    public void InvalidateCheck() => _checkedAt = 0;

    private GlamourerAvailability Query(out string message, out int major, out int minor)
    {
        major = 0;
        minor = 0;

        try
        {
            (major, minor) = _apiVersion.Invoke();
        }
        catch (IpcError)
        {
            message = "Glamourer is not installed or not loaded.";
            return GlamourerAvailability.NotInstalled;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "GlamourLink could not read Glamourer's API version.");
            message = "Glamourer is not installed or not loaded.";
            return GlamourerAvailability.NotInstalled;
        }

        LogVersion(major, minor);

        if (major != 1)
        {
            message = $"GlamourLink speaks Glamourer API 1.x; this Glamourer reports {major}.{minor}.";
            return GlamourerAvailability.WrongMajor;
        }

        message = "";
        return GlamourerAvailability.Ready;
    }

    public bool TrySetItem(int objectIndex, ApiEquipSlot slot, ulong itemId, byte stain1, byte stain2, out GlamourerApiEc ec,
        out string error)
    {
        ec = GlamourerApiEc.UnknownError;
        try
        {
            ec = _setItem.Invoke(objectIndex, slot, itemId, new byte[] { stain1, stain2 }, key: 0, flags: ApplyFlag.Once);
            error = "";
            return true;
        }
        catch (IpcNotReadyError)
        {
            error = $"This Glamourer version does not provide {SetItem.Label}.";
            return false;
        }
        catch (IpcError ex)
        {
            error = Generic(SetItem.Label, ex);
            return false;
        }
        catch (Exception ex)
        {
            error = Generic(SetItem.Label, ex);
            return false;
        }
    }

    public bool TrySetBonusItem(int objectIndex, ulong glassesId, out GlamourerApiEc ec, out string error)
    {
        ec = GlamourerApiEc.UnknownError;
        try
        {
            ec = _setBonusItem.Invoke(objectIndex, ApiBonusSlot.Glasses, glassesId, key: 0, flags: ApplyFlag.Once);
            error = "";
            return true;
        }
        catch (IpcNotReadyError)
        {
            error = $"This Glamourer version does not provide {SetBonusItem.Label}.";
            return false;
        }
        catch (IpcError ex)
        {
            error = Generic(SetBonusItem.Label, ex);
            return false;
        }
        catch (Exception ex)
        {
            error = Generic(SetBonusItem.Label, ex);
            return false;
        }
    }

    public bool TryGetStateBase64(int objectIndex, out string? state, out string error)
    {
        state = null;
        try
        {
            var (ec, data) = _getStateBase64.Invoke(objectIndex, key: 0);
            if (ec != GlamourerApiEc.Success || data is null)
            {
                error = $"Glamourer could not read your current appearance ({ec}).";
                return false;
            }

            state = data;
            error = "";
            return true;
        }
        catch (IpcNotReadyError)
        {
            error = $"This Glamourer version does not provide {GetStateBase64.Label}.";
            return false;
        }
        catch (IpcError ex)
        {
            error = Generic(GetStateBase64.Label, ex);
            return false;
        }
        catch (Exception ex)
        {
            error = Generic(GetStateBase64.Label, ex);
            return false;
        }
    }

    public bool TryAddDesign(string state, string name, out Guid guid, out string error)
    {
        guid = Guid.Empty;
        try
        {
            var ec = _addDesign.Invoke(state, name, out guid);
            if (ec != GlamourerApiEc.Success)
            {
                error = $"Glamourer did not save the design ({ec}).";
                return false;
            }

            error = "";
            return true;
        }
        catch (IpcNotReadyError)
        {
            error = $"This Glamourer version does not provide {AddDesign.Label}.";
            return false;
        }
        catch (IpcError ex)
        {
            error = Generic(AddDesign.Label, ex);
            return false;
        }
        catch (Exception ex)
        {
            error = Generic(AddDesign.Label, ex);
            return false;
        }
    }

    private string Generic(string label, Exception ex)
    {
        _log.Warning(ex, $"GlamourLink's {label} call failed.");
        return $"Glamourer's {label} call failed.";
    }

    private void LogVersion(int major, int minor)
    {
        lock (_versionLock)
        {
            if (_loggedVersion == (major, minor))
            {
                return;
            }

            _loggedVersion = (major, minor);
            _log.Information($"GlamourLink sees Glamourer API {major}.{minor}.");
        }
    }
}
