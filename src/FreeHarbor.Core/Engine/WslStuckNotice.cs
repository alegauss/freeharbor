namespace FreeHarbor.Core.Engine;

/// <summary>
/// Whether the tray should say, once per outage, that WSL itself has stopped answering (DD345).
/// </summary>
/// <remarks>
/// <para>DD183's balloon fires fifteen seconds after the engine goes away and says there is nothing
/// to click. That is true for a blip and false for a hung WSL: the host's retry terminates the
/// distribution and starts it again, which cannot reach a service that will not hand out a virtual
/// machine. The timeout is only known once a start has spent its sixty-second budget, so the first
/// balloon cannot carry it, and this is the follow-up that can.</para>
///
/// <para>One of these per outage, created when the engine goes away and dropped when it answers or
/// somebody stops it. The tray asks it on a timer, and what the timer asks is the file's length,
/// the way the Engine page does: a 64 KB file already in the cache, read whole only when it moved,
/// and never a question put to the engine.</para>
///
/// <para>It never restarts WSL. A shutdown takes every other distribution on the machine with it,
/// so the consent stays in the Engine page's dialog, which names them.</para>
/// </remarks>
public sealed class WslStuckNotice
{
    /// <summary>How often the tray asks whether the journal moved.</summary>
    /// <remarks>
    /// Slower than the Engine page's second, because nobody is watching this one and the fact it
    /// waits for took a sixty-second start budget to produce.
    /// </remarks>
    public static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(5);

    /// <summary>What the balloon says.</summary>
    public const string Text =
        "WSL itself has stopped answering, so FreeHarbor cannot bring the engine back on its own. "
        + "Click here to open the Engine page, where Restart WSL is waiting.";

    /// <summary>What the balloon says when Fast Startup carried the stuck WSL over (DD353).</summary>
    /// <remarks>
    /// Its own sentence rather than <see cref="Text"/> with <see cref="FastStartup.Sentence"/> after it,
    /// because a balloon holds 255 characters and the two together do not fit.
    /// </remarks>
    public const string FastStartupText =
        "WSL is still stuck from before Shut down: Fast Startup brought it back as it was. "
        + "Click here for Restart WSL. If that fails, use Restart, not Shut down.";

    /// <summary>The balloon's text for this journal and this boot.</summary>
    /// <param name="journal">The journal's lines, oldest first.</param>
    /// <param name="boot">This session's boot, or <see langword="null"/>.</param>
    /// <returns><see cref="FastStartupText"/> where <see cref="FastStartup.BroughtItBack"/>, else <see cref="Text"/>.</returns>
    public static string TextFor(IReadOnlyList<string> journal, LastBoot? boot) =>
        FastStartup.BroughtItBack(journal, boot) ? FastStartupText : Text;

    /// <summary>The length the journal had when it was last read, or -1 before the first look.</summary>
    private long _length = -1;

    /// <summary>Whether this outage has been announced already.</summary>
    private bool _said;

    /// <summary>
    /// Whether to announce now, given how long the journal is.
    /// </summary>
    /// <param name="length">The journal's length in bytes, or 0 where there is no file.</param>
    /// <param name="read">Reads the journal's lines, oldest first; called only when the length moved.</param>
    /// <returns><see langword="true"/> once, the first time the journal says WSL is not answering now.</returns>
    /// <remarks>
    /// A length that has not moved since the last look means nothing new was written, so the file
    /// is not read. The first look always reads, because the timeout may already be in the file by
    /// the time the engine is noticed gone. "Now" is <see cref="WslFailure.WslIsNotAnsweringNow"/>'s
    /// reading, so an engine that answered after the last timeout is never announced as stuck.
    /// </remarks>
    public bool Due(long length, Func<IReadOnlyList<string>> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (_said || length == _length)
        {
            return false;
        }

        _length = length;
        _said = WslFailure.WslIsNotAnsweringNow(read());
        return _said;
    }
}
