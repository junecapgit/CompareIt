using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace CompareIt.Core;

public static class FileDiffer
{
    /// <summary>
    /// Returns a side-by-side diff model. Both sides have the same number of lines;
    /// DiffPlex inserts Imaginary (blank placeholder) lines on the side without a change.
    /// </summary>
    public static SideBySideDiffModel Compare(string leftText, string rightText)
        => SideBySideDiffBuilder.Diff(leftText, rightText);

    /// <summary>
    /// Detects binary content by scanning the first 8 KB for null bytes.
    /// </summary>
    public static bool IsBinary(string filePath)
    {
        try
        {
            const int sampleSize = 8192;
            using var fs = File.OpenRead(filePath);
            var buffer = new byte[Math.Min(sampleSize, fs.Length)];
            int read = fs.Read(buffer, 0, buffer.Length);
            for (int i = 0; i < read; i++)
                if (buffer[i] == 0) return true;
        }
        catch { /* treat as non-binary on error */ }
        return false;
    }
}
