using System.Text.Json;
using UniversalSearch.Core;

namespace UniversalSearch.Infrastructure;

public sealed class Configuration
{
    public string DirectoryPath { get; }
    public string DatabasePath => Path.Combine(DirectoryPath, "index.db");
    public AppSettings Settings { get; private set; } = new();
    public Configuration(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalSearch");
        Directory.CreateDirectory(DirectoryPath);
        try { if (File.Exists(FilePath)) Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception ex) { Log(ex); }
        Settings.Roots ??= []; Settings.Exclusions ??= []; Settings.WebEngines ??= [];
        Settings.Roots.RemoveAll(string.IsNullOrWhiteSpace); Settings.Exclusions.RemoveAll(string.IsNullOrWhiteSpace);
        Settings.WebEngines.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.Keyword) || string.IsNullOrWhiteSpace(e.Url));
        if (string.IsNullOrWhiteSpace(Settings.Hotkey)) Settings.Hotkey = "Alt+Space";
    }
    string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public void Save(AppSettings settings)
    {
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true); Settings = settings;
    }
    readonly object logLock = new();
    public void Log(Exception ex)
    {
        lock (logLock) try
        {
            var path = Path.Combine(DirectoryPath, "app.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true);
            // Do not log search queries, browser URLs or file paths.
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {ex.GetType().Name} HResult={ex.HResult}\n");
        } catch { /* Logging must never crash the app. */ }
    }
}

