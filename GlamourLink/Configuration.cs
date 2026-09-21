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

    [NonSerialized]
    private IDalamudPluginInterface? pi;

    public void Initialize(IDalamudPluginInterface p) => pi = p;

    public void Save() => pi!.SavePluginConfig(this);
}
