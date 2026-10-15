using Microsoft.Data.Sqlite;
using UniversalSearch.Core;
using UniversalSearch.Infrastructure;
using Xunit;

namespace UniversalSearch.Tests;
public sealed class IntegrationTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(),"UniversalSearch-tests-"+Guid.NewGuid());
    readonly SearchStore store;
    readonly Configuration config;
    public IntegrationTests() { Directory.CreateDirectory(dir); config = new(Path.Combine(dir,"data")); store = new(config.DatabasePath); }
    [Fact] public void FtsFiltersFuzzyFavoritesAndHistoryPersist()
    {
        using (var db = store.Open())
        {
            store.Upsert(db,new(@"C:\Work\report.pdf","report.pdf",EntryKind.File,".pdf",1));
            store.Upsert(db,new(@"C:\Other\report.txt","report.txt",EntryKind.File,".txt",1));
        }
        Assert.Single(store.Search(SearchQuery.Parse("report ext:pdf in:Work"),default));
        Assert.Single(store.Search(SearchQuery.Parse("repor ext:pdf"),default));
        Assert.Single(store.Search(SearchQuery.Parse("repotr ext:pdf"),default));
        store.ToggleFavorite(@"C:\Work\report.pdf"); store.RecordUse(@"C:\Work\report.pdf","report");
        var reopened = new SearchStore(config.DatabasePath);
        Assert.Single(reopened.Search(SearchQuery.Parse("is:favorite"),default)); Assert.Contains("report",reopened.Recent());
        store.DeleteTree(@"C:\Work"); Assert.Empty(store.Search(SearchQuery.Parse("ext:pdf"),default));
        store.Clear(true); Assert.Equal(0,store.Count()); Assert.Empty(store.Recent());
    }
    [Fact] public void UpdatingEntryRefreshesFtsAndUnchangedEntryIsNotRewritten()
    {
        using var db = store.Open(); var e = new SearchEntry("test","alpha",EntryKind.File,"",1);
        store.Upsert(db,e); store.Upsert(db,e);
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT changes()"; Assert.Equal(0L,cmd.ExecuteScalar());
        store.Upsert(db,e with { Name="beta",Modified=2 });
        Assert.Empty(store.Search(SearchQuery.Parse("alpha"),default)); Assert.Single(store.Search(SearchQuery.Parse("beta"),default));
    }
    [Fact] public async Task RealFileWatcherHandlesCreateRenameDeleteAndExclusions()
    {
        var root = Path.Combine(dir,"files"); var excluded = Path.Combine(root,"excluded"); Directory.CreateDirectory(excluded);
        File.WriteAllText(Path.Combine(excluded,"secret-unique.txt"),"private");
        config.Save(new AppSettings { Roots=[root],Exclusions=[excluded] });
        await using var indexer = new FileIndexer(store,config); await indexer.StartAsync();
        var file = Path.Combine(root,"watcher-unique.txt"); File.WriteAllText(file,"hello");
        await Until(() => Find(file));
        var renamed = Path.Combine(root,"renamed-unique.txt"); File.Move(file,renamed);
        await Until(() => Find(renamed) && !Find(file));
        File.Delete(renamed); await Until(() => !Find(renamed));
        Assert.Empty(store.Search(SearchQuery.Parse("secret-unique"),default));
        bool Find(string path) => store.Search(SearchQuery.Parse("in:\""+path+"\""),default).Any(e => e.Path == path);
    }
    [Fact] public void BrowserHistoryIsOffWithoutConsent()
    { Assert.Equal((0,0),new BrowserHistory(store,config).Import(default)); }
    [Theory][InlineData(false)][InlineData(true)]
    public void BrowserFixturesAreReadOnlyAndConsentRevocationRemovesUsage(bool firefox)
    {
        var path = Path.Combine(dir,"browser.db");
        using (var db = new SqliteConnection("Data Source="+path+";Pooling=False"))
        {
            db.Open(); using var cmd = db.CreateCommand();
            cmd.CommandText = firefox ? "CREATE TABLE moz_places(url TEXT,title TEXT,last_visit_date INTEGER); INSERT INTO moz_places VALUES('https://example.com','Research notes',123),('javascript:alert(1)','Unsafe',124)" : "CREATE TABLE urls(url TEXT,title TEXT,last_visit_time INTEGER); INSERT INTO urls VALUES('https://example.com','Research notes',123),('javascript:alert(1)','Unsafe',124)";
            cmd.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(path);
        config.Settings.BrowserHistory = true;
        Assert.Equal((1,0),new BrowserHistory(store,config,[new(path,firefox)]).Import(default));
        Assert.Single(store.Search(SearchQuery.Parse("Research"),default));
        Assert.Equal(before,File.ReadAllBytes(path));
        store.RecordUse("https://example.com","Research");
        config.Settings.BrowserHistory = false;
        new BrowserHistory(store,config,[new(path,firefox)]).Import(default);
        Assert.Empty(store.Search(SearchQuery.Parse("Research"),default));
        using var target = store.Open(); using var check = target.CreateCommand(); check.CommandText = "SELECT count(*) FROM usage"; Assert.Equal(0L,check.ExecuteScalar());
    }
    [Fact] public async Task RestartReconcilesOfflineChangesAndRemovedRoots()
    {
        var root = Path.Combine(dir,"restart"); Directory.CreateDirectory(root); var file=Path.Combine(root,"offline-unique.txt"); File.WriteAllText(file,"v1");
        config.Save(new AppSettings { Roots=[root] });
        await using var indexer = new FileIndexer(store,config); indexer.Reconcile(default);
        Assert.Single(store.Search(SearchQuery.Parse("offline-unique"),default));
        File.Delete(file); indexer.Reconcile(default);
        Assert.Empty(store.Search(SearchQuery.Parse("offline-unique"),default));
        File.WriteAllText(file,"v2"); indexer.Reconcile(default);
        config.Save(new AppSettings()); indexer.Reconcile(default);
        Assert.Empty(store.Search(SearchQuery.Parse("offline-unique"),default));
    }
    [Fact] public void CancelledReconciliationDoesNotPruneExistingRows()
    {
        using (var db = store.Open()) store.Upsert(db,new("keep","keep",EntryKind.File));
        var token = new CancellationToken(true);
        var indexer = new FileIndexer(store,config);
        Assert.Throws<OperationCanceledException>(() => indexer.Reconcile(token));
        Assert.Single(store.Search(SearchQuery.Parse("keep"),default));
    }
    [Fact] public void CancelledSearchStops()
    { var cts = new CancellationTokenSource(); cts.Cancel(); Assert.Throws<OperationCanceledException>(() => store.Search(SearchQuery.Parse("report"),cts.Token)); }
    [Fact] public void QueryOperatorsCannotInjectSqlOrFts()
    { foreach (var query in new[] { "\" OR 1=1 --", "* : ()", "NEAR(foo)", "ext:' OR 1=1" }) store.Search(SearchQuery.Parse(query),default); }
    static async Task Until(Func<bool> condition)
    { for (int i=0;i<100;i++) { if (condition()) return; await Task.Delay(150); } Assert.True(condition(),"Watcher change was not reflected within 15 seconds."); }
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(dir,true); }
}

