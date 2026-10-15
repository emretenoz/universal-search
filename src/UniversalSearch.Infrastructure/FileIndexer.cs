using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using UniversalSearch.Core;

namespace UniversalSearch.Infrastructure;

public sealed class FileIndexer(SearchStore store, Configuration configuration) : IAsyncDisposable
{
    readonly List<FileSystemWatcher> watchers = [];
    readonly Channel<string> changes = Channel.CreateBounded<string>(new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    CancellationTokenSource? lifetime;
    Task? worker;
    int needsScan;
    public event Action<string, long>? Progress;
    public static string[] StartMenuRoots => [Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)];
    string[] Roots => configuration.Settings.Roots.Concat(StartMenuRoots).Where(Directory.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    bool Allowed(string path) => !Matching.IsWithin(path, configuration.DirectoryPath) && !configuration.Settings.Exclusions.Any(x => Matching.IsWithin(path, x));
    public async Task StartAsync()
    {
        await StopAsync(); lifetime = new(); var token = lifetime.Token;
        foreach (var root in Roots)
        {
            try
            {
                var w = new FileSystemWatcher(root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite, InternalBufferSize = 32768 };
                w.Created += (_, e) => Queue(e.FullPath); w.Changed += (_, e) => Queue(e.FullPath); w.Deleted += (_, e) => Queue(e.FullPath);
                w.Renamed += (_, e) => { Queue(e.OldFullPath); Queue(e.FullPath); };
                w.Error += (_, _) => Interlocked.Exchange(ref needsScan, 1);
                w.EnableRaisingEvents = true; watchers.Add(w);
            }
            catch (Exception ex) { configuration.Log(ex); }
        }
        worker = Task.Run(async () =>
        {
            try
            {
                Reconcile(token);
                var lastScan = DateTime.UtcNow;
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(650, token);
                    var batch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    while (batch.Count < 1024 && changes.Reader.TryRead(out var path)) batch.Add(path);
                    using (var db = store.Open())
                    using (var tx = db.BeginTransaction())
                    {
                        foreach (var path in batch)
                        {
                            token.ThrowIfCancellationRequested();
                            if (!Allowed(path)) continue;
                            if (Directory.Exists(path)) { Interlocked.Exchange(ref needsScan, 1); continue; }
                            if (File.Exists(path)) { var entry = GetEntry(path); if (entry != null) store.Upsert(db, entry); }
                            else SearchStore.Execute(db, "DELETE FROM entries WHERE path=$p OR substr(path,1,length($prefix))=$prefix COLLATE NOCASE", ("$p",path),("$prefix",path.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar));
                        }
                        tx.Commit();
                    }
                    if ((Volatile.Read(ref needsScan) != 0 && DateTime.UtcNow - lastScan > TimeSpan.FromSeconds(10)) || DateTime.UtcNow - lastScan > TimeSpan.FromMinutes(15))
                    { Interlocked.Exchange(ref needsScan, 0); Reconcile(token); lastScan = DateTime.UtcNow; }
                    else if (batch.Count > 0) Progress?.Invoke("ready", store.Count());
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { configuration.Log(ex); Progress?.Invoke("error", 0); }
        }, token);
    }
    public void RequestScan() => Interlocked.Exchange(ref needsScan, 1);
    void Queue(string path) { if (!changes.Writer.TryWrite(path)) Interlocked.Exchange(ref needsScan, 1); }
    public void Reconcile(CancellationToken token)
    {
        using var db = store.Open();
        SearchStore.Execute(db, "CREATE TEMP TABLE IF NOT EXISTS seen(path TEXT PRIMARY KEY COLLATE NOCASE); DELETE FROM seen;");
        var roots = Roots; var failed = new List<string>(); var batch = new List<SearchEntry>(256); long count = 0;
        Progress?.Invoke("indexing", count);
        foreach (var root in roots)
        {
            bool shortcutsOnly = StartMenuRoots.Contains(root,StringComparer.OrdinalIgnoreCase)
                && !configuration.Settings.Roots.Any(r => Matching.IsWithin(root,r));
            var stack = new Stack<string>(); stack.Push(root);
            while (stack.TryPop(out var dir))
            {
                token.ThrowIfCancellationRequested(); if (!Allowed(dir)) continue;
                try
                {
                    foreach (var path in Directory.EnumerateFileSystemEntries(dir))
                    {
                        token.ThrowIfCancellationRequested(); if (!Allowed(path)) continue;
                        var entry = GetEntry(path, failed); if (entry == null) continue;
                        if (entry.Kind == EntryKind.Folder) stack.Push(path);
                        if (shortcutsOnly && entry.Kind != EntryKind.Application) continue;
                        batch.Add(entry);
                        if (batch.Count >= 256) Flush();
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed.Add(dir); configuration.Log(ex); }
            }
        }
        Flush(); token.ThrowIfCancellationRequested();
        // Keep rows beneath temporarily inaccessible locations; never prune from a cancelled scan.
        foreach (var path in failed.Concat(configuration.Settings.Roots.Where(x => !Directory.Exists(x))))
            SearchStore.Execute(db, "INSERT OR IGNORE INTO seen SELECT path FROM entries WHERE path=$p OR substr(path,1,length($prefix))=$prefix COLLATE NOCASE", ("$p",path),("$prefix",path.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar));
        SearchStore.Execute(db, "DELETE FROM entries WHERE kind IN (0,1,2) AND path NOT LIKE 'shell:AppsFolder\\%' AND path NOT IN (SELECT path FROM seen)");
        Progress?.Invoke(failed.Count > 0 ? "partial" : "ready", store.Count());
        void Flush()
        {
            using var tx = db.BeginTransaction();
            foreach (var item in batch) { store.Upsert(db, item); SearchStore.Execute(db, "INSERT OR IGNORE INTO seen VALUES($p)", ("$p",item.Path)); }
            tx.Commit(); count += batch.Count; batch.Clear(); Progress?.Invoke("indexing", count);
        }
    }
    SearchEntry? GetEntry(string path, List<string>? failed = null)
    {
        try
        {
            var attr = File.GetAttributes(path);
            // Avoid junction loops and cloud hydration / offline placeholders.
            if ((attr & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0) return null;
            bool folder = (attr & FileAttributes.Directory) != 0;
            var ext = folder ? "" : Path.GetExtension(path).ToLowerInvariant();
            var kind = folder ? EntryKind.Folder : ext is ".lnk" or ".exe" or ".appref-ms" or ".url" ? EntryKind.Application : EntryKind.File;
            return new(path, Path.GetFileName(path), kind, ext, File.GetLastWriteTimeUtc(path).Ticks);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed?.Add(path); configuration.Log(ex); return null; }
    }
    public async Task StopAsync()
    {
        foreach (var w in watchers) w.Dispose(); watchers.Clear();
        if (lifetime != null) { await lifetime.CancelAsync(); if (worker != null) await worker; lifetime.Dispose(); lifetime = null; }
        while (changes.Reader.TryRead(out _)) { }
    }
    public async ValueTask DisposeAsync() => await StopAsync();
}
