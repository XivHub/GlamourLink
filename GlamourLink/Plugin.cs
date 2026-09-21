using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using GlamourLink.Apply;
using GlamourLink.Eorzea;
using GlamourLink.Library;
using GlamourLink.Windows;
using XivHubPluginKit;
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

    public static LibraryStore Library { get; private set; } = null!;

    /// <summary>Shared across every XIV Hub plugin; see XivHubPluginKit/UI/THEME.md.</summary>
    public static HubThemeConfigService ThemeConfig { get; private set; } = null!;

    private static DevTelemetry? _telemetry;

    /// <summary>
    /// Queue a dev-log line. A no-op on every normal install: the telemetry object
    /// only exists while the toggle is on and a URL is set, so a shipped plugin runs
    /// no timer and holds no HttpClient for this.
    /// </summary>
    public static void Dev(string line) => _telemetry?.Log(line);

    /// <summary>
    /// Creates or tears down the telemetry object to match the configuration. Called at
    /// start and whenever the dev settings change, so turning the toggle off actually
    /// stops the flush timer rather than leaving it spinning on an inactive instance.
    /// </summary>
    public static void RefreshTelemetry()
    {
        var wanted = Configuration.DevLog && !string.IsNullOrWhiteSpace(Configuration.DevLogUrl);

        if (wanted && _telemetry is null)
        {
            _telemetry = new DevTelemetry(
                "GlamourLink",
                () => Configuration.DevLog,
                () => Configuration.DevLogUrl,
                err => Log.Debug($"Dev log post failed: {err}"));
        }
        else if (!wanted && _telemetry is not null)
        {
            _telemetry.Dispose();
            _telemetry = null;
        }
    }

    public readonly WindowSystem WindowSystem = new("GlamourLink");

    private readonly MainWindow _mainWindow;
    private readonly ConfigWindow _configWindow;
    private readonly LibraryWindow _libraryWindow;
    private readonly EorzeaCollectionClient _eorzeaClient;
    private readonly GlamourImporter _importer;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);
        Library = new LibraryStore(PluginInterface.GetPluginConfigDirectory(), Log);

        _eorzeaClient = new EorzeaCollectionClient(Configuration, Log);
        _importer = new GlamourImporter(Configuration, DataManager, Log, PluginInterface, Framework, ObjectTable);

        ThemeConfig = new HubThemeConfigService(
            PluginInterface.GetPluginConfigDirectory(),
            (msg, ex) => Log.Warning(ex, msg));
        HubStyle.Init(ThemeConfig);

        RefreshTelemetry();
        Dev($"plugin loaded, {Library.Count} saved outfits");

        _mainWindow = new MainWindow(_importer, ToggleLibraryUi);
        _configWindow = new ConfigWindow();
        _libraryWindow = new LibraryWindow(_importer);
        WindowSystem.AddWindow(_mainWindow);
        WindowSystem.AddWindow(_configWindow);
        WindowSystem.AddWindow(_libraryWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open GlamourLink; \"/glink <url or id>\" imports, \"/glink library\" opens your outfits, \"/glink config\" opens settings",
        });

        PluginInterface.UiBuilder.Draw += DrawThemed;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        _telemetry?.Dispose();
        _telemetry = null;

        CommandManager.RemoveHandler(CommandName);

        PluginInterface.UiBuilder.Draw -= DrawThemed;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();

        _eorzeaClient.Dispose();
        _importer.Dispose();
    }

    /// <summary>
    /// One wrap point for the whole plugin: no window class knows the theme
    /// exists, and the pop is guaranteed even if a window throws mid-draw —
    /// ImGui's style stack is global, so an unbalanced push corrupts every
    /// plugin drawing after this one.
    /// </summary>
    private void DrawThemed()
    {
        // Dalamud calls this every frame whether or not a window is open, and the
        // theme pushes 78 style entries. Drawing nothing costs nothing.
        if (!_mainWindow.IsOpen && !_configWindow.IsOpen && !_libraryWindow.IsOpen)
            return;

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
        if (arg.Equals("library", StringComparison.OrdinalIgnoreCase))
        {
            ToggleLibraryUi();
            return;
        }
        ImportFromCommand(arg);
    }

    private void ImportFromCommand(string arg)
    {
        if (!EorzeaUrl.TryParseId(arg, out var id, out var error))
        {
            ChatGui.PrintError($"[GlamourLink] {error}");
            return;
        }

        _ = Task.Run(async () =>
        {
            var result = await _eorzeaClient.FetchAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (result.Status == FetchStatus.Ok && result.Glamour is not null)
            {
                var glamour = result.Glamour;
                var slotCount = glamour.Gear!.Enumerate().Count(e => e.Item is not null);
                ChatGui.Print($"[GlamourLink] {glamour.Name} by {glamour.Character} — {slotCount} slots");
            }
            else
            {
                ChatGui.PrintError($"[GlamourLink] {result.Message}");
            }
        });
    }

    public void ToggleMainUi() => _mainWindow.Toggle();
    public void ToggleConfigUi() => _configWindow.Toggle();
    public void ToggleLibraryUi() => _libraryWindow.Toggle();
}
