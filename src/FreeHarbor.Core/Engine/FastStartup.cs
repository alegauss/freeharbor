using System.Diagnostics.Eventing.Reader;
using System.Globalization;

namespace FreeHarbor.Core.Engine;

/// <summary>How this Windows session began, as the kernel recorded it (DD353).</summary>
/// <param name="At">When the boot was recorded, local time, as the journal's stamps are.</param>
/// <param name="Hybrid">
/// Whether it resumed a session Shut down had hibernated, which is what Fast Startup makes of a
/// Shut down.
/// </param>
public sealed record LastBoot(DateTime At, bool Hybrid)
{
    /// <summary>The boot type Microsoft-Windows-Kernel-Boot event 27 records for a hybrid boot.</summary>
    private const uint HybridBootType = 1;

    /// <summary>Read once: the answer cannot change until the next boot, which ends this process.</summary>
    private static readonly Lazy<LastBoot?> Once = new(ReadNow);

    /// <summary>This session's boot, or <see langword="null"/> where the System log would not say.</summary>
    /// <returns>The reading.</returns>
    public static LastBoot? Read() => Once.Value;

    /// <summary>Ask the System log for its newest event 27.</summary>
    /// <returns>The reading, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The event log and not <c>HiberbootEnabled</c>: the setting says Shut down <em>would</em> hibernate,
    /// and a user who pressed Restart booted cold with the setting still on. Event 27 is what this boot
    /// actually was. Readable without elevation; any failure to read it is an absence, never a guess.
    /// </remarks>
    private static LastBoot? ReadNow()
    {
        try
        {
            var query = new EventLogQuery(
                "System",
                PathType.LogName,
                "*[System[Provider[@Name='Microsoft-Windows-Kernel-Boot'] and (EventID=27)]]")
            {
                ReverseDirection = true,
            };
            using var reader = new EventLogReader(query);
            using var record = reader.ReadEvent();
            if (record?.TimeCreated is not { } at || record.Properties.Count == 0)
            {
                return null;
            }

            var type = Convert.ToUInt32(record.Properties[0].Value, CultureInfo.InvariantCulture);
            return new LastBoot(at, type == HybridBootType);
        }
        catch (Exception exception) when (exception is EventLogException
            or UnauthorizedAccessException or InvalidCastException or FormatException
            or OverflowException or PlatformNotSupportedException)
        {
            return null;
        }
    }
}

/// <summary>
/// Whether Fast Startup carried a stuck WSL across a Shut down, and the sentence that says so (DD353).
/// </summary>
/// <remarks>
/// <para>With Fast Startup on, Shut down hibernates the kernel session and the next power-on resumes
/// it, WslService and the HCS state under it included. Measured on 15 and 16 November 2026: WSL stuck
/// at 15:09, Shut down at 22:02, and every <c>wsl.exe</c> hung again from logon the next morning. A
/// user who reads "restart your computer" and presses Shut down has restarted nothing.</para>
///
/// <para>Read-only. Turning Fast Startup off is the user's decision about their machine, needs
/// administrator rights and has costs outside this product, so the most said here is its name.</para>
/// </remarks>
public static class FastStartup
{
    /// <summary>What the stuck-WSL notice and the Engine page add when this is the case.</summary>
    public const string Sentence =
        "Windows started with Fast Startup, which brings WSL back as it was before Shut down. "
        + "If Restart WSL does not clear it, use Restart, not Shut down.";

    /// <summary>
    /// Whether WSL has not answered once since a hybrid boot, so its stuck state predates the boot.
    /// </summary>
    /// <param name="journal">The engine journal's lines, oldest first.</param>
    /// <param name="boot">This session's boot, or <see langword="null"/> where it is unknown.</param>
    /// <returns>
    /// <see langword="true"/> where the boot was hybrid, a line since it says WSL is not answering, and
    /// no line since it says the engine answered.
    /// </returns>
    /// <remarks>
    /// Since the boot and not "in the first minutes after logon": a WSL that has never answered since
    /// a hybrid boot is the case however late the page is opened, and one that answered once and hung
    /// later is a new failure the boot has nothing to do with. A line whose stamp does not parse is
    /// skipped, so an unreadable line can only withhold the sentence, never produce it.
    /// </remarks>
    public static bool BroughtItBack(IReadOnlyList<string> journal, LastBoot? boot)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (boot is not { Hybrid: true })
        {
            return false;
        }

        var stuck = false;
        foreach (var line in journal)
        {
            if (line.Length < JournalDigest.StampWidth
                || !DateTime.TryParseExact(line[..JournalDigest.StampWidth], "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var stamp)
                || stamp < boot.At)
            {
                continue;
            }

            if (line.Contains($"  {EngineState.Running}  ", StringComparison.Ordinal))
            {
                return false;
            }

            stuck |= WslFailure.SaysWslIsNotAnswering(line);
        }

        return stuck;
    }
}
