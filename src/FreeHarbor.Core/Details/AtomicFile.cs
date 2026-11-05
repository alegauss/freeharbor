namespace FreeHarbor.Core.Details;

/// <summary>
/// Replaces a file whole, through a temporary beside it, and survives a brief lock (DD319).
/// </summary>
/// <remarks>
/// <b>Why it retries.</b> On Windows a scanner or the indexer opens a file the moment it sees it
/// written, and holds it for milliseconds. A write or a move that lands in that moment fails with a
/// sharing violation, which is an <see cref="IOException"/>, or with
/// <see cref="UnauthorizedAccessException"/> when the holder asked for delete access. Neither says
/// anything about the file, so a save that met one is tried again, a few times, before the caller
/// is told it failed. A lock that outlasts the retries is a real refusal and is let through.
///
/// <para><b>Why here.</b> The details store and the project file both replaced their file this way
/// and both had the same window; one place to get it right is the fix for the class.</para>
/// </remarks>
public static class AtomicFile
{
    /// <summary>How many times a replace is tried before the failure is let through.</summary>
    public const int Attempts = 4;

    /// <summary>How long to wait between tries: longer than a scanner holds a file it just saw.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(50);

    /// <summary>Write <paramref name="text"/> to a temporary beside <paramref name="path"/>, then move it over.</summary>
    /// <param name="path">The file to replace, which need not exist.</param>
    /// <param name="text">Its new contents.</param>
    /// <exception cref="IOException">A lock or a fault outlasted every try.</exception>
    /// <exception cref="UnauthorizedAccessException">Access was refused on every try.</exception>
    public static void Replace(string path, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(text);

        var temporary = path + ".tmp";
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.WriteAllText(temporary, text);
                File.Move(temporary, path, overwrite: true);
                return;
            }
            catch (Exception failure) when (attempt < Attempts
                && failure is IOException or UnauthorizedAccessException
                && failure is not DirectoryNotFoundException and not PathTooLongException)
            {
                Thread.Sleep(Pause);
            }
        }
    }
}
