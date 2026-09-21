using System;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace GlamourLink;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public const string DefaultApiBaseUrl = "https://ffxiv.eorzeacollection.com/api/glamour";
    public const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    public int Version { get; set; } = 1;

    public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;
    public string UserAgent { get; set; } = DefaultUserAgent;

    public bool ClearEmptyGearSlots { get; set; } = true;
    public bool ClearEmptyWeaponSlots { get; set; } = false;
    public bool SaveAsDesignByDefault { get; set; } = false;

    /// <summary>
    /// Dev-only live logging to a devlog server on the LAN. Off, and pointing nowhere,
    /// on every normal install; the telemetry object is not even constructed until both
    /// of these are set, so a user who never touches them pays nothing for the feature.
    /// </summary>
    public bool DevLog { get; set; } = false;

    public string DevLogUrl { get; set; } = "";

    [NonSerialized]
    private IDalamudPluginInterface? pi;

    public void Initialize(IDalamudPluginInterface p) => pi = p;

    public void Save() => pi!.SavePluginConfig(this);
}
