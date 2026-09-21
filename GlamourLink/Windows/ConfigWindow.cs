using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using XivHubPluginKit.UI;

namespace GlamourLink.Windows;

public sealed class ConfigWindow : Window
{
    public ConfigWindow() : base("GlamourLink Configuration###glamourlink-config")
    {
        Size = new System.Numerics.Vector2(420, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.Text("Appearance");
        ImGui.TextColored(HubStyle.Faint, "Shared with every XIV Hub plugin.");
        ImGui.Spacing();
        HubThemeEditor.Draw(Plugin.ThemeConfig);
    }
}
