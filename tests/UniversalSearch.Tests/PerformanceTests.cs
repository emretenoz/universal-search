using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using UniversalSearch.Core;
using UniversalSearch.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace UniversalSearch.Tests;
public sealed class PerformanceTests(ITestOutputHelper output)
{
    [Fact] public void FiftyThousandSyntheticEntriesHaveBoundedResults()
    {
        var dir = Path.Combine(Path.GetTempPath(),"UniversalSearch-bench-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
        try
        {
            var store = new SearchStore(Path.Combine(dir,"index.db")); var timer=Stopwatch.StartNew();
            using (var db=store.Open()) using (var tx=db.BeginTransaction())
            {
                for (int i=0;i<50000;i++) store.Upsert(db,new($"C:\\Bench\\report{i:D6}.pdf",$"report{i:D6}.pdf",EntryKind.File,".pdf",i));
                tx.Commit();
            }
            var indexing=timer.Elapsed.TotalMilliseconds;
            var times=new List<double>();
            for (int i=0;i<60;i++)
            {
                timer.Restart(); var hits=store.Search(SearchQuery.Parse($"report{i*791:D6} ext:pdf"),default); timer.Stop();
                Assert.Single(hits); times.Add(timer.Elapsed.TotalMilliseconds);
            }
            Assert.InRange(store.Search(SearchQuery.Parse("report"),default).Count,1,80);
            times.Sort(); var metrics = new { entries=50000, ingestMs=indexing, searchMedianMs=times[30], searchP95Ms=times[56], dataset="synthetic filenames; exact prefix with extension filter; no disk enumeration" };
            var json=JsonSerializer.Serialize(metrics,new JsonSerializerOptions { WriteIndented=true }); output.WriteLine(json);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"benchmark.json"),json);
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(dir,true); }
    }
}
