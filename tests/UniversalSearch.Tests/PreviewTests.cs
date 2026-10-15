using UniversalSearch.Core;
using Xunit;

public class PreviewTests
{
    [Theory]
    [InlineData("photo.PNG", EntryKind.File, 100, PreviewKind.Image)]
    [InlineData("photo.png", EntryKind.File, 67108865, PreviewKind.Metadata)]
    [InlineData("video.mp4", EntryKind.File, 1000000000, PreviewKind.Video)]
    [InlineData("document.pdf", EntryKind.File, 100, PreviewKind.Metadata)]
    [InlineData("https://example.com/photo.png", EntryKind.History, 100, PreviewKind.Metadata)]
    [InlineData("folder.png", EntryKind.Folder, 100, PreviewKind.Metadata)]
    [InlineData("photo.png.lnk", EntryKind.Application, 100, PreviewKind.Metadata)]
    public void RoutesOnlySupportedFiles(string path, EntryKind kind, long bytes, PreviewKind expected)
        => Assert.Equal(expected, PreviewPolicy.Classify(new(path, path, kind), bytes));
}
