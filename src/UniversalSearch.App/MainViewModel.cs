using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UniversalSearch.Core;
using UniversalSearch.Infrastructure;

namespace UniversalSearch.App;

public sealed class ActionCommand(Action<object?> execute) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
public sealed record ResultItem(SearchEntry Entry, ImageSource? Icon, string Detail)
{
    public string Name =>
        Entry.Kind is EntryKind.File or EntryKind.Application
        && !string.IsNullOrEmpty(Entry.Extension)
        && Entry.Name.Length > Entry.Extension.Length
        && Entry.Name.EndsWith(Entry.Extension, StringComparison.OrdinalIgnoreCase)
            ? Entry.Name[..^Entry.Extension.Length]
            : Entry.Name;
    public string Star => Entry.Favorite ? "★" : "";
    public string Glyph => Entry.Kind switch { EntryKind.Calculator => "=", EntryKind.Web or EntryKind.History => "↗", EntryKind.Command => ">", EntryKind.Application when Icon == null => "▦", _ => "" };
    public override string ToString() => Name;
}
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    readonly App app;
    readonly SearchStore store;
    readonly Configuration config;
    CancellationTokenSource? search;
    string query = "", status = "", filter = "All", notice = "";
    bool busy;
    readonly LaunchGate launchGate = new();
    ResultItem? selected;
    public MainViewModel(App app, SearchStore store, Configuration config)
    {
        this.app = app; this.store = store; this.config = config;
        Open = new ActionCommand(_ => Run(OpenAsync));
        Settings = new ActionCommand(_ => app.OpenSettings());
        Filter = new ActionCommand(p => { var requested = p?.ToString() ?? "All"; filter = filter == requested ? "All" : requested; Changed(nameof(ActiveFilter)); Changed(nameof(FilterKey)); _ = RefreshAsync(); });
        CopyPath = new ActionCommand(p => Run(() => { var r = p as ResultItem ?? Selected; if (r != null) Clipboard.SetText(r.Entry.Path); return Task.CompletedTask; }));
        OpenLocation = new ActionCommand(p => Run(() => { var r = p as ResultItem ?? Selected; if (r?.Entry.Kind <= EntryKind.Application && !r.Entry.Path.StartsWith("shell:")) Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "/select,\"" + r.Entry.Path + "\"", UseShellExecute = true }); return Task.CompletedTask; }));
        Favorite = new ActionCommand(p => Run(async () => { var r = p as ResultItem ?? Selected; if (r != null && r.Entry.Kind <= EntryKind.History) { await Task.Run(() => store.ToggleFavorite(r.Entry.Path)); await RefreshAsync(); } }));
        RecentSearch = new ActionCommand(p => Query = p?.ToString() ?? "");
    }
    public Strings L => new(config.Settings.Language == "en");
    public ObservableCollection<ResultItem> Results { get; } = [];
    public ObservableCollection<string> Recent { get; } = [];
    public string Query { get => query; set { if (query == value) return; query = value; Changed(); _ = RefreshAsync(); } }
    public string Status { get => status; set { status = value; Changed(); } }
    public string Notice { get => notice; set { notice = value; Changed(); } }
    public string ActiveFilter => L[filter];
    public string FilterKey => filter;
    public bool Busy { get => busy; set { busy = value; Changed(); } }
    public bool Welcome => config.Settings.Roots.Count == 0 && string.IsNullOrWhiteSpace(Query) && filter == "All";
    public bool ShowRecent => string.IsNullOrWhiteSpace(Query) && Recent.Count > 0;
    public bool Empty => !Busy && Results.Count == 0 && !Welcome;
    public ResultItem? Selected { get => selected; set { selected = value; Changed(); } }
    public ICommand Open { get; }
    public ICommand Settings { get; }
    public ICommand Filter { get; }
    public ICommand CopyPath { get; }
    public ICommand OpenLocation { get; }
    public ICommand Favorite { get; }
    public ICommand RecentSearch { get; }
    public async Task RefreshAsync()
    {
        search?.Cancel(); search?.Dispose(); search = new(); var token = search.Token;
        var text = Query; var currentFilter = filter; Busy = true; Changed(nameof(Welcome)); Changed(nameof(Empty));
        try
        {
            await Task.Delay(140, token);
            var items = await Task.Run(() =>
            {
                var entries = new List<SearchEntry>();
                if (text.StartsWith('>'))
                {
                    var commands = new[] { new SearchEntry("settings", L["Settings"], EntryKind.Command), new SearchEntry("reindex", L["Reindex"], EntryKind.Command), new SearchEntry("quit", L["Quit"], EntryKind.Command) };
                    entries.AddRange(commands.Where(e => (e.Name + " " + e.Path).Contains(text[1..].Trim(), StringComparison.OrdinalIgnoreCase)));
                }
                else
                {
                    var calc = Calculator.Evaluate(text);
                    if (calc != null) entries.Add(new(calc, calc, EntryKind.Calculator));
                    var parts = text.Split(' ', 2, StringSplitOptions.TrimEntries);
                    var engine = config.Settings.WebEngines.FirstOrDefault(e => parts.Length == 2 && e.Keyword.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
                    if (engine != null) entries.Add(new(engine.Url.Replace("{query}", Uri.EscapeDataString(parts[1])), engine.Name + " · " + parts[1], EntryKind.Web));
                    var q = SearchQuery.Parse(text);
                    q = currentFilter switch { "Files" => q with { Kind = EntryKind.File }, "Apps" => q with { Kind = EntryKind.Application }, "Folders" => q with { Kind = EntryKind.Folder }, "Favorites" => q with { Favorites = true }, _ => q };
                    entries.AddRange(store.Search(q, token));
                }
                return ApplicationCatalog.Collapse(entries).Select(e => new ResultItem(e, ShellIcons.Get(e), e.Kind == EntryKind.Calculator ? L["Calculator"] : e.Kind == EntryKind.Command ? "> " + e.Path : L[e.Kind.ToString()])).ToArray();
            }, token);
            token.ThrowIfCancellationRequested(); Results.Clear(); if (!Welcome) foreach (var item in items) Results.Add(item); Selected = Results.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(text)) { var recents = await Task.Run(store.Recent, token); token.ThrowIfCancellationRequested(); Recent.Clear(); foreach (var r in recents) Recent.Add(r); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!token.IsCancellationRequested) { config.Log(ex); Status = L["Error"]; Results.Clear(); } }
        finally { if (!token.IsCancellationRequested) { Busy = false; Changed(nameof(Empty)); Changed(nameof(ShowRecent)); } }
    }
    public void LanguageChanged() { Changed(nameof(L)); Changed(nameof(ActiveFilter)); Changed(nameof(Welcome)); _ = RefreshAsync(); }
    public async void Run(Func<Task> action)
    { try { await action(); } catch (Exception ex) { config.Log(ex); Status = L["Error"]; } }
    async Task OpenAsync()
    {
        var item = Selected?.Entry; if (item == null || Busy) return;
        if (item.Kind == EntryKind.Calculator) { Clipboard.SetText(item.Path); Status = L["Copy"]; return; }
        if (item.Kind == EntryKind.Command)
        {
            switch (item.Path) { case "settings": app.OpenSettings(); break; case "reindex": app.Indexer.RequestScan(); break; case "quit": await app.QuitAsync(); break; } return;
        }
        if (item.Kind is EntryKind.History or EntryKind.Web)
        { if (!Uri.TryCreate(item.Path, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return; }
        var currentQuery = Query;
        var launched = await launchGate.RunAsync(async () =>
        {
            Notice = L["Opening"];
            try { await ShellLauncher.OpenAsync(item.Path); }
            catch { Notice = L["OpenFailed"]; app.Indexer.RequestScan(); throw; }
        }, () => { Notice = ""; app.MainWindow.Hide(); });
        if (!launched) return;
        // Optional ranking/history writes must never hold the search window open.
        try { await Task.Run(() => store.RecordUse(item.Path, currentQuery)); }
        catch (Exception ex) { config.Log(ex); Notice = L["HistorySaveFailed"]; }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public void Dispose() { search?.Cancel(); search?.Dispose(); }
}







