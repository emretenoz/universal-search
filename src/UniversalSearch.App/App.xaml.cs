using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using UniversalSearch.Infrastructure;
using UniversalSearch.Core;
using Forms = System.Windows.Forms;

namespace UniversalSearch.App;
public partial class App : Application
{
    Mutex? mutex;
    Forms.NotifyIcon? tray;
    HotkeyService? hotkey;
    SettingsWindow? settingsWindow;
    public Configuration Config { get; private set; } = null!;
    public SearchStore Store { get; private set; } = null!;
    public FileIndexer Indexer { get; private set; } = null!;
    public MainViewModel VM { get; private set; } = null!;
    public bool Quitting { get; private set; }
    readonly SemaphoreSlim settingsGate = new(1, 1);
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var dataArg = Array.IndexOf(e.Args, "--data-dir");
        string? dataPath = dataArg >= 0 && e.Args.Length > dataArg + 1 ? Path.GetFullPath(e.Args[dataArg + 1]) : null;
        var identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(dataPath ?? Environment.UserName)))[..16];
        mutex = new Mutex(true, "Local\\UniversalSearch-" + identity, out bool created);
        if (!created) { MessageBox.Show("Universal Search is already running. Use its tray icon or global shortcut.","Universal Search"); Shutdown(); return; }
        try
        {
            Config = new Configuration(dataPath);
            ThemeManager.Apply(Config.Settings.Theme);
            Store = await Task.Run(() => new SearchStore(Config.DatabasePath));
            VM = new(this,Store,Config); Indexer = new(Store,Config);
            Indexer.Progress += (state,count) => Dispatcher.BeginInvoke(() => VM.Status = VM.L[state switch { "indexing" => "Indexing", "partial" => "Partial", "error" => "Error", _ => "Ready" }] + $" · {count:N0}");
            var window = new MainWindow(VM); MainWindow = window;
            hotkey = new(window, () => { if (window.IsVisible) window.Hide(); else window.Reveal(); });
            if (!hotkey.Register(Config.Settings.Hotkey)) VM.Notice = VM.L["HotkeyBusy"];
            tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "Universal Search", Visible = true };
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(window.Reveal); RebuildTray();
            if (!e.Args.Contains("--background")) window.Show();
            await settingsGate.WaitAsync();
            try
            {
                await Indexer.StartAsync(); await ApplicationCatalog.RefreshAsync(Store,Config); await VM.RefreshAsync();
                if (Config.Settings.BrowserHistory) await ImportHistoryAsync(); else await Task.Run(() => Store.DeleteKind(EntryKind.History));
            }
            finally { settingsGate.Release(); }
        }
        catch (Exception ex) { Config?.Log(ex); MessageBox.Show("Universal Search could not start. Check the local app log.","Universal Search"); await QuitAsync(); }
    }
    void RebuildTray()
    {
        if (tray == null) return;
        var old = tray.ContextMenuStrip;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(VM.L["Show"],null,(_,_) => Dispatcher.Invoke(() => ((MainWindow)MainWindow).Reveal()));
        menu.Items.Add(VM.L["Settings"],null,(_,_) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add(VM.L["Quit"],null,(_,_) => Dispatcher.Invoke(() => VM.Run(QuitAsync)));
        tray.ContextMenuStrip = menu; old?.Dispose();
    }
    public void OpenSettings()
    {
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow(this); settingsWindow.Closed += (_,_) => settingsWindow = null; settingsWindow.Show();
    }
    public async Task<bool> ApplySettingsAsync(AppSettings settings)
    {
        await settingsGate.WaitAsync();
        try
        {
            if (hotkey == null || !hotkey.Register(settings.Hotkey)) { VM.Notice = VM.L["HotkeyBusy"]; MessageBox.Show(VM.L["HotkeyBusy"]); return false; }
            VM.Notice = "";
            var current = Config.Settings;
            bool sourcesUnchanged = current.Roots.SequenceEqual(settings.Roots, StringComparer.OrdinalIgnoreCase)
                && current.Exclusions.SequenceEqual(settings.Exclusions, StringComparer.OrdinalIgnoreCase)
                && current.BrowserHistory == settings.BrowserHistory;
            if (sourcesUnchanged)
            {
                if (settings.StartWithWindows != current.StartWithWindows) StartupShortcut.Set(settings.StartWithWindows);
                Config.Save(settings); ThemeManager.Apply(settings.Theme); VM.LanguageChanged(); RebuildTray();
                return true;
            }
            await Indexer.StopAsync();
            // Only manage this application's per-user startup shortcut on a changed preference.
            if (settings.StartWithWindows != Config.Settings.StartWithWindows)
            {
                StartupShortcut.Set(settings.StartWithWindows);
            }
            Config.Save(settings); ThemeManager.Apply(settings.Theme); VM.LanguageChanged(); RebuildTray();
            await Task.Run(() => Store.DeleteKind(EntryKind.History));
            await Indexer.StartAsync();
            await ApplicationCatalog.RefreshAsync(Store,Config);
            if (settings.BrowserHistory) await ImportHistoryAsync();
            await VM.RefreshAsync(); return true;
        }
        finally { settingsGate.Release(); }
    }
    async Task ImportHistoryAsync()
    {
        var result = await Task.Run(() => new BrowserHistory(Store,Config).Import(CancellationToken.None));
        if (result.unavailable > 0) VM.Status = VM.L["BrowserUnavailable"];
    }
    public async Task ClearAsync(bool index)
    {
        if (MessageBox.Show(VM.L["ConfirmClear"],"Universal Search",MessageBoxButton.YesNo,MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await settingsGate.WaitAsync();
        try { if (index) await Indexer.StopAsync(); await Task.Run(() => Store.Clear(index)); await VM.RefreshAsync(); if (index) VM.Status = VM.L["Paused"]; }
        finally { settingsGate.Release(); }
    }
    public async Task QuitAsync()
    {
        if (Quitting) return; Quitting = true;
        hotkey?.Dispose(); tray?.Dispose(); VM?.Dispose();
        await settingsGate.WaitAsync();
        try { if (Indexer != null) await Indexer.StopAsync(); }
        finally { settingsGate.Release(); }
        mutex?.Dispose(); Shutdown();
    }
}



