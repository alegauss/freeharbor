using System.Text.RegularExpressions;

namespace FreeHarbor.Tray.Ui;

/// <summary>
/// Whether a log line reads as an error: by the level it declares, and by its stream only where it
/// declares none.
/// </summary>
/// <remarks>
/// <para>Colouring every stderr line red made a healthy MariaDB look like it was failing: mariadbd,
/// postgres, nginx and many others write their whole log to stderr, "[Note] ready for connections"
/// included, so the stream says where a line went and not what it means. A line that names its own
/// level is taken at its word; a line that names none (a shell's "permission denied", a stack trace's
/// second line) keeps the old rule, because stderr is still where those say why.</para>
///
/// <para>The first level in the line wins, so "[Note] InnoDB: error log rotated" is a note. A
/// bracketed or <c>level=</c> form is read in any case; a bare word only in capitals ("LOG:",
/// "ERROR:", "WARN "), so ordinary prose that happens to say "error" or "log" is not a level.</para>
/// </remarks>
internal static partial class LogLevel
{
    private static readonly HashSet<string> Errors = new(StringComparer.OrdinalIgnoreCase)
    {
        "error", "err", "fatal", "critical", "crit", "panic", "emerg", "emergency", "alert", "severe",
    };

    private static readonly HashSet<string> Others = new(StringComparer.OrdinalIgnoreCase)
    {
        "warning", "warn", "info", "information", "note", "notice", "debug", "trace", "verbose", "log",
    };

    [GeneratedRegex(@"\[(?<word>[A-Za-z]+)\]|level=""?(?<word>[A-Za-z]+)|\b(?<bare>[A-Z]{3,11})\b")]
    private static partial Regex Candidate();

    /// <summary>Whether a line is drawn as an error.</summary>
    /// <param name="text">The line.</param>
    /// <param name="fromStdErr">Whether it came off stderr.</param>
    /// <returns>True for an error-level line, or a stderr line that declares no level.</returns>
    public static bool IsError(string text, bool fromStdErr) => Declared(text) ?? fromStdErr;

    /// <summary>What the line's own level says, or <see langword="null"/> where it names none.</summary>
    /// <param name="text">The line.</param>
    /// <returns>True for an error level, false for any other level, null for none.</returns>
    internal static bool? Declared(string text)
    {
        foreach (Match match in Candidate().Matches(text))
        {
            var word = match.Groups["word"].Success ? match.Groups["word"].Value : match.Groups["bare"].Value;
            if (Errors.Contains(word))
            {
                return true;
            }

            if (Others.Contains(word))
            {
                return false;
            }
        }

        return null;
    }
}
