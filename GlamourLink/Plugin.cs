using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using GlamourLink.Windows;
using XivHubPluginKit.UI;

namespace GlamourLink;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/glink";

    public static Configuration Configuration { get; private set; } = null!;

    /// <summary>Shared across every XIV Hub plugin; see XivHubPluginKit/UI/THEME.md.</summary>
    public static HubThemeConfigService ThemeConfig { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("GlamourLink");

    private readonly MainWindow _mainWindow;
    private readonly ConfigWindow _configWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        ThemeConfig = new HubThemeConfigService(
            PluginInterface.GetPluginConfigDirectory(),
            (msg, ex) => Log.Warning(ex, msg));
        HubStyle.Init(ThemeConfig);

        _mainWindow = new MainWindow();
        _configWindow = new ConfigWindow();
        WindowSystem.AddWindow(_mainWindow);
        WindowSystem.AddWindow(_configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open GlamourLink; \"/glink <url or id>\" imports directly, \"/glink config\" opens settings",
        });

        PluginInterface.UiBuilder.Draw += DrawThemed;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);

        PluginInterface.UiBuilder.Draw -= DrawThemed;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
    }

    /// <summary>
    /// One wrap point for the whole plugin: no window class knows the theme
    /// exists, and the pop is guaranteed even if a window throws mid-draw —
    /// ImGui's style stack is global, so an unbalanced push corrupts every
    /// plugin drawing after this one.
    /// </summary>
    private void DrawThemed()
    {
        HubStyle.Push();
        try { WindowSystem.Draw(); }
        finally { HubStyle.Pop(); }
    }

    private void OnCommand(string command, string args)
    {
        var arg = args.Trim();
        if (arg.Length == 0)
        {
            ToggleMainUi();
            return;
        }
        if (arg.Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            ToggleConfigUi();
            return;
        }
        ImportFromCommand(arg);
    }

    private void ImportFromCommand(string arg)
    {
        ChatGui.PrintError("[GlamourLink] Glamour import is added in a later update.");
    }

    public void ToggleMainUi() => _mainWindow.Toggle();
    public void ToggleConfigUi() => _configWindow.Toggle();
}
