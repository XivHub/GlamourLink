using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using XivHubPluginKit.UI;

namespace GlamourLink.Windows;

public sealed class MainWindow : Window
{
    private string _input = "";

    public MainWindow() : base("GlamourLink###glamourlink-main")
    {
        Size = new System.Numerics.Vector2(480, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.InputText("Glamour URL or id", ref _input, 256);
        ImGui.Button("Fetch");
        ImGui.TextColored(HubStyle.Faint, "Paste a glamour URL or id and press Fetch.");
    }
}
