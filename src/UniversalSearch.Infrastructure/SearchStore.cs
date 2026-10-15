using Microsoft.Data.Sqlite;
using UniversalSearch.Core;

namespace UniversalSearch.Infrastructure;

public sealed class SearchStore : ISearchStore
{
    readonly string connectionString;
    public SearchStore(string path)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, DefaultTimeout = 10, Pooling = true }.ToString();
        using var db = Open();
        Execute(db, """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS entries(id INTEGER PRIMARY KEY, path TEXT NOT NULL COLLATE NOCASE UNIQUE,
              name TEXT NOT NULL, kind INTEGER NOT NULL, extension TEXT NOT NULL, modified INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_entries_name ON entries(name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS ix_entries_kind ON entries(kind);
            CREATE TABLE IF NOT EXISTS usage(path TEXT PRIMARY KEY COLLATE NOCASE, favorite INTEGER NOT NULL DEFAULT 0,
              uses INTEGER NOT NULL DEFAULT 0,last_used INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS recent(query TEXT PRIMARY KEY, timestamp INTEGER NOT NULL);
            CREATE VIRTUAL TABLE IF NOT EXISTS search USING fts5(name,path,content='entries',content_rowid='id',tokenize='unicode61 remove_diacritics 2');
            CREATE TRIGGER IF NOT EXISTS entries_ai AFTER INSERT ON entries BEGIN
              INSERT INTO search(rowid,name,path) VALUES(new.id,new.name,new.path); END;
            CREATE TRIGGER IF NOT EXISTS entries_ad AFTER DELETE ON entries BEGIN
              INSERT INTO search(search,rowid,name,path) VALUES('delete',old.id,old.name,old.path); END;
            CREATE TRIGGER IF NOT EXISTS entries_au AFTER UPDATE ON entries BEGIN
              INSERT INTO search(search,rowid,name,path) VALUES('delete',old.id,old.name,old.path);
              INSERT INTO search(rowid,name,path) VALUES(new.id,new.name,new.path); END;

            """);
        UpgradeSearch(db);
    }
    public SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); db.CreateFunction<string,string>("search_fold", SearchText.Fold, isDeterministic: true); return db; }
    static void UpgradeSearch(SqliteConnection db)
    {
        using var version = db.CreateCommand(); version.CommandText = "PRAGMA user_version";
        if (Convert.ToInt32(version.ExecuteScalar()) >= 2) return;
        using var tx = db.BeginTransaction();
        Execute(db, """
            DROP TRIGGER IF EXISTS entries_ai; DROP TRIGGER IF EXISTS entries_ad; DROP TRIGGER IF EXISTS entries_au;
            DROP TABLE IF EXISTS search;
            CREATE VIEW IF NOT EXISTS entries_search AS SELECT id,search_fold(name) AS name,search_fold(path) AS path FROM entries;
            CREATE VIRTUAL TABLE search USING fts5(name,path,content='entries_search',content_rowid='id',tokenize='unicode61 remove_diacritics 2');
            CREATE TRIGGER entries_ai AFTER INSERT ON entries BEGIN
              INSERT INTO search(rowid,name,path) VALUES(new.id,search_fold(new.name),search_fold(new.path)); END;
            CREATE TRIGGER entries_ad AFTER DELETE ON entries BEGIN
              INSERT INTO search(search,rowid,name,path) VALUES('delete',old.id,search_fold(old.name),search_fold(old.path)); END;
            CREATE TRIGGER entries_au AFTER UPDATE ON entries BEGIN
              INSERT INTO search(search,rowid,name,path) VALUES('delete',old.id,search_fold(old.name),search_fold(old.path));
              INSERT INTO search(rowid,name,path) VALUES(new.id,search_fold(new.name),search_fold(new.path)); END;
            INSERT INTO search(search) VALUES('rebuild');
            PRAGMA user_version=2;
            """);
        tx.Commit();
    }
    internal static void Execute(SqliteConnection db, string sql, params (string, object)[] args)
    { using var cmd = db.CreateCommand(); cmd.CommandText = sql; foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v); cmd.ExecuteNonQuery(); }
    public void Upsert(SqliteConnection db, SearchEntry item)
    {
        Execute(db, """
            INSERT INTO entries(path,name,kind,extension,modified) VALUES($p,$n,$k,$e,$m)
            ON CONFLICT(path) DO UPDATE SET name=excluded.name,kind=excluded.kind,extension=excluded.extension,modified=excluded.modified
            WHERE entries.modified<>excluded.modified OR entries.name<>excluded.name OR entries.kind<>excluded.kind;
            """, ("$p", item.Path), ("$n", item.Name), ("$k", (int)item.Kind), ("$e", item.Extension), ("$m", item.Modified));
    }
    public void DeleteTree(string path)
    {
        using var db = Open(); Execute(db, "DELETE FROM entries WHERE path=$p OR substr(path,1,length($prefix))=$prefix COLLATE NOCASE", ("$p",path), ("$prefix", path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar));
    }
    public void DeleteKind(EntryKind kind)
    {
        using var db = Open(); using var tx = db.BeginTransaction();
        Execute(db, "DELETE FROM usage WHERE path IN (SELECT path FROM entries WHERE kind=$k); DELETE FROM entries WHERE kind=$k", ("$k", (int)kind)); tx.Commit();
    }
    public long Count() { using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT count(*) FROM entries"; return (long)cmd.ExecuteScalar()!; }
    public IReadOnlyList<SearchEntry> Search(SearchQuery query, CancellationToken cancellationToken)
    {
        using var db = Open();
        var result = Read(false);
        if (result.Count == 0 && query.Text.Length >= 3 && !query.Text.Contains(' '))
        {
            foreach (var e in Read(true))
            {
                var distance = Matching.Distance(query.Text, Path.GetFileNameWithoutExtension(e.Name));
                if (distance <= (query.Text.Length >= 6 ? 2 : 1) && result.All(x => x.Path != e.Path)) result.Add(e with { Score = e.Score - 12 * distance });
            }
        }
        return result.OrderByDescending(e => e.Score).Take(80).ToArray();

        List<SearchEntry> Read(bool fuzzy)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var cmd = db.CreateCommand();
            string match = query.Fts;
            bool fts = match.Length > 0 && !fuzzy;
            cmd.CommandText = $"""
                SELECT e.path,e.name,e.kind,e.extension,e.modified,coalesce(u.favorite,0),coalesce(u.uses,0),coalesce(u.last_used,0),{(fts ? "-bm25(search,8,1)" : "0")}
                FROM entries e {(fts ? "JOIN search ON search.rowid=e.id" : "")} LEFT JOIN usage u ON u.path=e.path
                WHERE ($ext='' OR e.extension=$ext) AND ($loc='' OR instr(lower(e.path),lower($loc))>0)
                AND ($kind=-1 OR e.kind=$kind) AND ($fav=0 OR u.favorite=1)
                {(fts ? "AND search MATCH $match" : "")}
                {(fuzzy ? "AND search_fold(e.name) LIKE $prefix ESCAPE '\\'" : "")}
                ORDER BY {(fts ? "bm25(search,8,1)," : "")} coalesce(u.favorite,0) DESC,coalesce(u.uses,0) DESC,coalesce(u.last_used,0) DESC LIMIT {(fuzzy ? 500 : 200)}
                """;
            cmd.Parameters.AddWithValue("$ext", query.Extension); cmd.Parameters.AddWithValue("$loc", query.Location);
            cmd.Parameters.AddWithValue("$kind", query.Kind.HasValue ? (int)query.Kind.Value : -1); cmd.Parameters.AddWithValue("$fav", query.Favorites ? 1 : 0);
            if (fts) cmd.Parameters.AddWithValue("$match", match);
            if (fuzzy) cmd.Parameters.AddWithValue("$prefix", SearchText.Fold(query.Text)[..1].Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%");
            using var reg = cancellationToken.Register(cmd.Cancel);
            using var reader = cmd.ExecuteReader(); var found = new List<SearchEntry>();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var favorite = reader.GetInt32(5) != 0; var uses = reader.GetInt32(6); var last = reader.GetInt64(7);
                var name = reader.GetString(1);
                var score = reader.GetDouble(8) + (favorite ? 30 : 0) + Math.Log(uses + 1) * 5
                    + (last > 0 ? 5 / (1 + (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last) / 86400d) : 0)
                    + (SearchText.Fold(name).Equals(SearchText.Fold(query.Text), StringComparison.Ordinal) ? 50 : SearchText.Fold(name).StartsWith(SearchText.Fold(query.Text), StringComparison.Ordinal) ? 15 : 0);
                found.Add(new(reader.GetString(0), name, (EntryKind)reader.GetInt32(2), reader.GetString(3), reader.GetInt64(4), favorite, uses, last, score));
            }
            return found;
        }
    }
    public void RecordUse(string path, string query)
    {
        using var db = Open(); using var tx = db.BeginTransaction();
        Execute(db, "INSERT INTO usage(path,uses,last_used) VALUES($p,1,$t) ON CONFLICT(path) DO UPDATE SET uses=uses+1,last_used=$t", ("$p",path),("$t", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        if (!string.IsNullOrWhiteSpace(query)) Execute(db, "INSERT OR REPLACE INTO recent VALUES($q,$t)", ("$q",query), ("$t",DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        Execute(db, "DELETE FROM recent WHERE query NOT IN (SELECT query FROM recent ORDER BY timestamp DESC LIMIT 30)"); tx.Commit();
    }
    public void ToggleFavorite(string path) { using var db = Open(); Execute(db, "INSERT INTO usage(path,favorite) VALUES($p,1) ON CONFLICT(path) DO UPDATE SET favorite=1-favorite", ("$p",path)); }
    public string[] Recent()
    { using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT query FROM recent ORDER BY timestamp DESC LIMIT 10"; using var r = cmd.ExecuteReader(); var list = new List<string>(); while (r.Read()) list.Add(r.GetString(0)); return list.ToArray(); }
    public void Clear(bool index)
    {
        using var db = Open(); Execute(db, "PRAGMA secure_delete=ON; DELETE FROM recent; DELETE FROM usage;" + (index ? "DELETE FROM entries; INSERT INTO search(search) VALUES('rebuild');" : ""));
        Execute(db, "PRAGMA wal_checkpoint(TRUNCATE); VACUUM;");
    }
}



