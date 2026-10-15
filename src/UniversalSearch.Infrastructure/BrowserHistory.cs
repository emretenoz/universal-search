using Microsoft.Data.Sqlite;
using UniversalSearch.Core;

namespace UniversalSearch.Infrastructure;

public sealed record HistorySource(string Path, bool Firefox);
public sealed class BrowserHistory(SearchStore store, Configuration config, IReadOnlyList<HistorySource>? sourcesOverride = null)
{
    public (int imported, int unavailable) Import(CancellationToken token)
    {
        if (!config.Settings.BrowserHistory) { store.DeleteKind(EntryKind.History); return (0, 0); }
        IReadOnlyList<HistorySource> sources;
        try { sources = sourcesOverride ?? Discover(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { config.Log(ex); return (0,1); }
        int imported = 0, unavailable = 0;
        using var target = store.Open();
        foreach (var (path, ff) in sources)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path, Mode=SqliteOpenMode.ReadOnly, Pooling=false, DefaultTimeout=1 }.ToString());
                source.Open(); using var cmd = source.CreateCommand();
                cmd.CommandText = ff ? "SELECT url,coalesce(title,url),coalesce(last_visit_date,0) FROM moz_places WHERE last_visit_date IS NOT NULL ORDER BY last_visit_date DESC LIMIT 10000" : "SELECT url,coalesce(title,url),last_visit_time FROM urls ORDER BY last_visit_time DESC LIMIT 10000";
                using var reader = cmd.ExecuteReader(); using var tx = target.BeginTransaction();
                while (reader.Read())
                {
                    token.ThrowIfCancellationRequested(); var url = reader.GetString(0);
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) continue;
                    store.Upsert(target, new(url, reader.GetString(1), EntryKind.History, "", reader.GetInt64(2))); imported++;
                }
                tx.Commit();
            }
            catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException) { config.Log(ex); unavailable++; }
        }
        return (imported, unavailable);
    }
    static IReadOnlyList<HistorySource> Discover()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sources = new List<HistorySource>();
        foreach (var dir in new[] { Path.Combine(local,"Google/Chrome/User Data"), Path.Combine(local,"Microsoft/Edge/User Data") })
            if (Directory.Exists(dir)) foreach (var profile in Directory.EnumerateDirectories(dir)) { var path = Path.Combine(profile,"History"); if (File.Exists(path)) sources.Add(new(path,false)); }
        var firefox = Path.Combine(roaming,"Mozilla/Firefox/Profiles");
        if (Directory.Exists(firefox)) foreach (var profile in Directory.EnumerateDirectories(firefox)) { var path = Path.Combine(profile,"places.sqlite"); if (File.Exists(path)) sources.Add(new(path,true)); }
        return sources;
    }
}
