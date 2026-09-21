using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using XivHubPluginKit.UI;

namespace GlamourLink.Windows;

public sealed class ConfigWindow : Window
{
    private string _apiBaseUrl;
    private string _userAgent;

    public ConfigWindow() : base("GlamourLink Configuration###glamourlink-config")
    {
        Size = new System.Numerics.Vector2(460, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
        _apiBaseUrl = Plugin.Configuration.ApiBaseUrl;
        _userAgent = Plugin.Configuration.UserAgent;
    }

    public override void OnOpen()
    {
        _apiBaseUrl = Plugin.Configuration.ApiBaseUrl;
        _userAgent = Plugin.Configuration.UserAgent;
    }

    public override void Draw()
    {
        DrawImportSection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawSourceSection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawRecentSection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawAppearanceSection();
    }

    private static void DrawImportSection()
    {
        ImGui.Text("Import");

        var cfg = Plugin.Configuration;

        var clearGear = cfg.ClearEmptyGearSlots;
        if (ImGui.Checkbox("Clear slots the glamour leaves empty", ref clearGear))
        {
            cfg.ClearEmptyGearSlots = clearGear;
            cfg.Save();
        }
        ImGui.TextColored(HubStyle.Faint, "Armour and accessories only.");

        var clearWeapon = cfg.ClearEmptyWeaponSlots;
        if (ImGui.Checkbox("Also clear an empty weapon slot", ref clearWeapon))
        {
            cfg.ClearEmptyWeaponSlots = clearWeapon;
            cfg.Save();
        }
        ImGui.TextColored(HubStyle.Faint, "Off by default so you are never unarmed.");

        var saveByDefault = cfg.SaveAsDesignByDefault;
        if (ImGui.Checkbox("Save as a Glamourer design by default", ref saveByDefault))
        {
            cfg.SaveAsDesignByDefault = saveByDefault;
            cfg.Save();
        }
    }

    private void DrawSourceSection()
    {
        ImGui.Text("Source");

        var cfg = Plugin.Configuration;

        ImGui.TextUnformatted("Base URL");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##api-base-url", ref _apiBaseUrl, 512))
        {
            cfg.ApiBaseUrl = _apiBaseUrl;
            cfg.Save();
        }

        ImGui.TextUnformatted("User-Agent");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##user-agent", ref _userAgent, 512))
        {
            cfg.UserAgent = _userAgent;
            cfg.Save();
        }

        if (ImGui.Button("Reset to defaults"))
        {
            _apiBaseUrl = Configuration.DefaultApiBaseUrl;
            _userAgent = Configuration.DefaultUserAgent;
            cfg.ApiBaseUrl = _apiBaseUrl;
            cfg.UserAgent = _userAgent;
            cfg.Save();
        }

        ImGui.TextColored(HubStyle.Faint,
            "Eorzea Collection does not document this endpoint. If imports start failing, it changed.");
    }

    private static void DrawRecentSection()
    {
        ImGui.Text("Recent");

        var cfg = Plugin.Configuration;
        using (ImRaii.Disabled(cfg.Recents.Count == 0))
        {
            if (ImGui.Button("Forget all"))
            {
                cfg.Recents.Clear();
                cfg.Save();
            }
        }
    }

    private static void DrawAppearanceSection()
    {
        ImGui.Text("Appearance");
        ImGui.TextColored(HubStyle.Faint, "Shared with every XIV Hub plugin.");
        ImGui.Spacing();
        HubThemeEditor.Draw(Plugin.ThemeConfig);
    }
}
