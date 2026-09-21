using System;
using System.Collections.Generic;
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

    public List<RecentImport> Recents { get; set; } = new();

    public sealed class RecentImport
    {
        public int Id;
        public string Name = "";
        public string Character = "";
        public long LastUsedUnix;
    }

    [NonSerialized]
    private IDalamudPluginInterface? pi;

    public void Initialize(IDalamudPluginInterface p) => pi = p;

    public void Save() => pi!.SavePluginConfig(this);
}
