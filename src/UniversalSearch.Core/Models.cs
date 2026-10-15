using System.Globalization;
using System.Text.RegularExpressions;

namespace UniversalSearch.Core;

public enum EntryKind { File, Folder, Application, History, Calculator, Command, Web }
public sealed record SearchEntry(string Path, string Name, EntryKind Kind, string Extension = "", long Modified = 0,
    bool Favorite = false, int Uses = 0, long LastUsed = 0, double Score = 0);
public sealed record WebEngine(string Keyword, string Name, string Url);
public sealed class AppSettings
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "tr";
    public List<string> Roots { get; set; } = [];
    public List<string> Exclusions { get; set; } = [];
    public bool BrowserHistory { get; set; }
    public bool StartWithWindows { get; set; }
    public string Hotkey { get; set; } = "Alt+Space";
    public List<WebEngine> WebEngines { get; set; } = [new("g", "Google", "https://www.google.com/search?q={query}"), new("b", "Bing", "https://www.bing.com/search?q={query}")];
}
public sealed record SearchQuery(string Text, string Extension, string Location, EntryKind? Kind, bool Favorites)
{
    public static SearchQuery Parse(string input)
    {
        string ext = "", location = ""; EntryKind? kind = null; bool favorites = false;
        var words = new List<string>();
        foreach (Match m in Regex.Matches(input, "(?:[^\\s\"]|\"[^\"]*\")+"))
        {
            var token = m.Value.Replace("\"", ""); var parts = token.Split(':', 2);
            if (parts.Length == 2)
            {
                switch (parts[0].ToLowerInvariant())
                {
                    case "ext": ext = "." + parts[1].TrimStart('.').ToLowerInvariant(); continue;
                    case "in": location = parts[1]; continue;
                    case "type": if (Enum.TryParse<EntryKind>(parts[1], true, out var k)) kind = k; continue;
                    case "is" when parts[1] == "favorite": favorites = true; continue;
                }
            }
            words.Add(token);
        }
        return new(string.Join(' ', words), ext, location, kind, favorites);
    }
    public string Fts => string.Join(" AND ", Regex.Matches(SearchText.Fold(Text), @"[\p{L}\p{N}_]+").Select(m => "\"" + m.Value + "\"*"));
}
public static class Matching
{
    public static int Distance(string a, string b)
    {
        a = SearchText.Fold(a); b = SearchText.Fold(b);
        if (a.Length > 256 || b.Length > 256) return 256;
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (int i = 1; i <= a.Length; i++)
        {
            int prev = row[0]; row[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int old = row[j]; row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), prev + (a[i - 1] == b[j - 1] ? 0 : 1)); prev = old;
            }
        }
        return row[b.Length];
    }
    public static bool IsWithin(string path, string root) => path.Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
public static class Calculator
{
    // Small recursive-descent arithmetic grammar. No scripting or process execution.
    public static string? Evaluate(string input)
    {
        if (input.Length > 200 || !Regex.IsMatch(input, @"^[\d\s.,+*/%()^\-]+$") || !input.Any(c => "+-*/%^".Contains(c))) return null;
        try { var p = new Parser(input.Replace(',', '.')); var value = p.Expression(); p.Space(); return p.End && double.IsFinite(value) ? value.ToString("G12", CultureInfo.InvariantCulture) : null; }
        catch { return null; }
    }
    private sealed class Parser(string text)
    {
        int pos;
        public bool End => pos == text.Length;
        public void Space() { while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++; }
        bool Take(char c) { Space(); if (pos >= text.Length || text[pos] != c) return false; pos++; return true; }
        public double Expression() { double x = Term(); while (true) { if (Take('+')) x += Term(); else if (Take('-')) x -= Term(); else return x; } }
        double Term() { double x = Unary(); while (true) { if (Take('*')) x *= Unary(); else if (Take('/')) x /= Unary(); else if (Take('%')) x %= Unary(); else return x; } }
        double Unary() { if (Take('+')) return Unary(); if (Take('-')) return -Unary(); double x = Atom(); if (Take('^')) x = Math.Pow(x, Unary()); return x; }
        double Atom() { if (Take('(')) { var x = Expression(); if (!Take(')')) throw new FormatException(); return x; } Space(); int start = pos; while (pos < text.Length && (char.IsDigit(text[pos]) || text[pos] == '.')) pos++; return double.Parse(text[start..pos], CultureInfo.InvariantCulture); }
    }
}
public interface ISearchStore
{
    IReadOnlyList<SearchEntry> Search(SearchQuery query, CancellationToken cancellationToken);
    void RecordUse(string path, string query);
    void ToggleFavorite(string path);
}

