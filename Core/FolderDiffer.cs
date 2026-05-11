namespace CompareIt.Core;

public enum FileStatus { Same, Modified, LeftOnly, RightOnly }

public record FolderDiffEntry(
    string RelativePath,
    FileStatus Status,
    DateTime? LeftModified,
    long? LeftSize,
    DateTime? RightModified,
    long? RightSize);

public static class FolderDiffer
{
    public static IReadOnlyList<FolderDiffEntry> Compare(string leftPath, string rightPath)
    {
        var results = new List<FolderDiffEntry>();
        var leftFiles  = GetRelativeFiles(leftPath);
        var rightFiles = GetRelativeFiles(rightPath);

        var allKeys = leftFiles.Keys
            .Union(rightFiles.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

        foreach (var rel in allKeys)
        {
            bool inLeft  = leftFiles.TryGetValue(rel, out var li);
            bool inRight = rightFiles.TryGetValue(rel, out var ri);

            FileStatus status = (inLeft, inRight) switch
            {
                (true, true)  => AreIdentical(li!, ri!) ? FileStatus.Same : FileStatus.Modified,
                (true, false) => FileStatus.LeftOnly,
                _             => FileStatus.RightOnly
            };

            results.Add(new FolderDiffEntry(
                rel, status,
                li?.LastWriteTime, li?.Length,
                ri?.LastWriteTime, ri?.Length));
        }

        return results;
    }

    private static Dictionary<string, FileInfo> GetRelativeFiles(string rootPath)
    {
        var dict = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        var root = new DirectoryInfo(rootPath);
        if (!root.Exists) return dict;
        foreach (var file in root.EnumerateFiles("*", SearchOption.AllDirectories))
            dict[Path.GetRelativePath(rootPath, file.FullName)] = file;
        return dict;
    }

    /// <summary>
    /// Quick identity check: size + last-write timestamp.
    /// If timestamps are unreliable (e.g. copies that strip metadata), a content
    /// hash can be used instead — but that is expensive for large trees.
    /// </summary>
    private static bool AreIdentical(FileInfo left, FileInfo right)
        => left.Length == right.Length
        && left.LastWriteTimeUtc == right.LastWriteTimeUtc;
}
