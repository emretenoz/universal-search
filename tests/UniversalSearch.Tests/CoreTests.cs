using UniversalSearch.Core;
using Xunit;
namespace UniversalSearch.Tests;
public sealed class CoreTests
{
    [Theory]
    [InlineData("2+3*4", "14")]
    [InlineData("(2+3)*4", "20")]
    [InlineData("2^3^2", "512")]
    [InlineData("-2^2", "-4")]
    [InlineData("1,5+2", "3.5")]
    [InlineData("1/0", null)]
    [InlineData("2+", null)]
    [InlineData("Process.Start(1)", null)]
    public void CalculatorIsSafeAndRespectsPrecedence(string input, string? expected) => Assert.Equal(expected, Calculator.Evaluate(input));
    [Fact] public void FiltersAndQuotedPaths()
    {
        var q = SearchQuery.Parse("report ext:PDF in:\"C:\\My Work\" type:file is:favorite");
        Assert.Equal("report",q.Text); Assert.Equal(".pdf",q.Extension); Assert.Equal("C:\\My Work",q.Location); Assert.Equal(EntryKind.File,q.Kind); Assert.True(q.Favorites); Assert.Equal("\"report\"*",q.Fts);
    }
    [Theory][InlineData("report","reprot",2)][InlineData("REPORT","report",0)][InlineData("report","repor",1)]
    public void EditDistance(string a,string b,int expected) => Assert.Equal(expected,Matching.Distance(a,b));
    [Fact] public void ExclusionIsDirectoryBounded()
    { Assert.True(Matching.IsWithin(@"C:\private\file",@"C:\private")); Assert.False(Matching.IsWithin(@"C:\private-other\file",@"C:\private")); }
}
