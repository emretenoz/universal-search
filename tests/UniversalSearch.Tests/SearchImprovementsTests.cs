using Microsoft.Data.Sqlite;
using UniversalSearch.Core;
using UniversalSearch.Infrastructure;
using Xunit;

public sealed class SearchImprovementsTests
{
    [Theory]
    [InlineData("ŞARKI", "sarki")]
    [InlineData("ÇAĞRI", "cagri")]
    [InlineData("İstanbul ışık", "istanbul isik")]
    [InlineData("ÖĞÜT", "ogut")]
    public void TurkishFolding(string input, string expected) => Assert.Equal(expected, SearchText.Fold(input));

    [Fact]
    public void SearchMigrationKeepsEntriesAndFavoritesAndSupportsAsciiTurkish()
    {
        var dir = Path.Combine(Path.GetTempPath(),"UniversalSearch-tr-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
        var path = Path.Combine(dir,"index.db");
        try
        {
            using(var legacy = new SqliteConnection("Data Source="+path))
            {
                legacy.Open(); using var command=legacy.CreateCommand();
                command.CommandText="""
                CREATE TABLE entries(id INTEGER PRIMARY KEY,path TEXT NOT NULL COLLATE NOCASE UNIQUE,name TEXT NOT NULL,kind INTEGER NOT NULL,extension TEXT NOT NULL,modified INTEGER NOT NULL);
                INSERT INTO entries VALUES(1,'C:\Şarkılar\item.mp3','çağrı.mp3',0,'.mp3',1);
                CREATE TABLE usage(path TEXT PRIMARY KEY COLLATE NOCASE,favorite INTEGER NOT NULL DEFAULT 0,uses INTEGER NOT NULL DEFAULT 0,last_used INTEGER NOT NULL DEFAULT 0);
                INSERT INTO usage VALUES('C:\Şarkılar\item.mp3',1,3,1);
                PRAGMA user_version=1;
                """;command.ExecuteNonQuery();
            }
            var store=new SearchStore(path);
            var result=Assert.Single(store.Search(SearchQuery.Parse("sarkilar cagri ext:mp3 is:favorite"),default));
            Assert.Equal("çağrı.mp3", result.Name); Assert.True(result.Favorite); Assert.Equal(3,result.Uses);
            Assert.Single(store.Search(SearchQuery.Parse("cagrl"),default));
            using(var db=store.Open())store.Upsert(db,new(result.Path,"ışık.mp3",EntryKind.File,".mp3",2));
            Assert.Single(store.Search(SearchQuery.Parse("isik"),default));
            Assert.Empty(store.Search(SearchQuery.Parse("cagri"),default));
            store=new SearchStore(path); Assert.Single(store.Search(SearchQuery.Parse("IŞIK"),default));
            store.DeleteTree(result.Path); Assert.Empty(store.Search(SearchQuery.Parse("isik"),default));
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(dir,true); }
    }

    [Fact]
    public void DuplicateAppsUseIdentityAndKeepFavoriteAlias()
    {
        var app=new SearchEntry("shell:AppsFolder\\id","App",EntryKind.Application,Score:10);
        var shortcut=new SearchEntry("C:\\App.lnk","App.lnk",EntryKind.Application,Favorite:true,Score:5);
        var other=new SearchEntry("C:\\Other.exe","App",EntryKind.Application);
        var file=new SearchEntry("C:\\App.txt","App",EntryKind.File);
        var ids=new Dictionary<string,string>{{app.Path,"id"},{shortcut.Path,"id"}};
        var results=ApplicationResults.Collapse([app,shortcut,other,file],ids);
        Assert.Equal(3,results.Count);Assert.Contains(shortcut,results);Assert.Contains(other,results);Assert.Contains(file,results);
    }
    [Fact]
    public void CalculatorPriorityAndDistinctFilesArePreserved()
    {
        var calculator=new SearchEntry("4","4",EntryKind.Calculator);
        var one=new SearchEntry("C:\\One\\report.txt","report.txt",EntryKind.File,Score:50);
        var two=new SearchEntry("C:\\Two\\report.txt","report.txt",EntryKind.File,Score:40);
        var result=ApplicationResults.Collapse([calculator,one,two],new Dictionary<string,string>());
        Assert.Equal(3,result.Count);Assert.Same(calculator,result[0]);
    }
}

