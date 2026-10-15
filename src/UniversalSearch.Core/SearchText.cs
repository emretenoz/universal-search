using System.Globalization;
using System.Text;

namespace UniversalSearch.Core;

public static class SearchText
{
    public static string Fold(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            result.Append(c is 'ı' or 'I' or 'İ' ? 'i' : char.ToLowerInvariant(c));
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}

public static class ApplicationResults
{
    public static IReadOnlyList<SearchEntry> Collapse(IEnumerable<SearchEntry> entries, IReadOnlyDictionary<string, string> identities)
    {
        return entries.GroupBy(e => e.Kind == EntryKind.Application && identities.TryGetValue(e.Path, out var id)
                ? "app:" + id : "path:" + e.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(e => e.Favorite).ThenByDescending(e => e.Score).First())
            .ToArray();
    }
}

