namespace UniversalSearch.Core;

public enum PreviewKind { Metadata, Image, Video }
public static class PreviewPolicy
{
    public static PreviewKind Classify(SearchEntry entry, long bytes)
    {
        if (entry.Kind != EntryKind.File || bytes < 0) return PreviewKind.Metadata;
        var extension = Path.GetExtension(entry.Path).ToLowerInvariant();
        if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" }.Contains(extension) && bytes <= 64L * 1024 * 1024) return PreviewKind.Image;
        if (new[] { ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".webm" }.Contains(extension)) return PreviewKind.Video;
        return PreviewKind.Metadata;
    }
}
