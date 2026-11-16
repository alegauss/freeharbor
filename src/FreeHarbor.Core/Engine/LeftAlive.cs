using System.Management;
using System.Runtime.InteropServices;

namespace FreeHarbor.Core.Engine;

/// <summary>A child process still running when its parent is about to end (DD354).</summary>
/// <param name="Pid">Its process id.</param>
/// <param name="Name">Its image name.</param>
/// <param name="CommandLine">How it was started, where Windows would say.</param>
public sealed record LeftAlive(int Pid, string Name, string? CommandLine)
{
    /// <summary>How much of a command line goes into the journal.</summary>
    private const int Shown = 80;

    /// <summary>The children of a process that are still running.</summary>
    /// <param name="parent">The parent's process id.</param>
    /// <returns>
    /// Them, without the console host Windows gives every console process, which goes with its
    /// owner and is never what was left behind. Empty where WMI would not answer.
    /// </returns>
    /// <remarks>
    /// <para>Measured on 16 November 2026 with WSL stuck: the host wrote "this host is done" at
    /// 07:04:01 and was still running half an hour later, under it a dockerd launcher, a socat relay
    /// channel and a <c>wsl --list --running --quiet</c> probe. Every one of those is killed with its
    /// whole tree before that line (the daemon's stop, the relay's channel, the probe's timeout), so
    /// what survived was something a terminate could not finish, which is what a process blocked
    /// inside a hung WSL service looks like.</para>
    ///
    /// <para>That is a reading and not yet a measurement, and this is what turns it into one: the
    /// journal names what is still alive at the moment the host says it is done. A host that names
    /// nothing and still lingers points at itself; one that names its children points at them.</para>
    /// </remarks>
    public static IReadOnlyList<LeftAlive> ChildrenOf(int parent)
    {
        var children = new List<LeftAlive>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, Name, CommandLine FROM Win32_Process "
                + $"WHERE ParentProcessId = {parent}");
            foreach (ManagementBaseObject row in searcher.Get())
            {
                using (row)
                {
                    if (row["ProcessId"] is uint pid && row["Name"] is string name
                        && !name.Equals("conhost.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        children.Add(new LeftAlive((int)pid, name, row["CommandLine"] as string));
                    }
                }
            }
        }
        catch (Exception exception) when (exception is ManagementException
            or COMException or UnauthorizedAccessException or InvalidOperationException
            or NotSupportedException)
        {
            // An unanswered question leaves the line out; it never claims nothing was left.
            return [];
        }

        return children;
    }

    /// <summary>The journal clause for what is still alive.</summary>
    /// <param name="alive">The children.</param>
    /// <returns>The clause, or <see langword="null"/> where nothing is.</returns>
    public static string? Clause(IReadOnlyList<LeftAlive> alive)
    {
        ArgumentNullException.ThrowIfNull(alive);
        if (alive.Count == 0)
        {
            return null;
        }

        var each = alive.Select(child => $"{child.Pid} {Shorten(child.CommandLine ?? child.Name)}");
        return $"still alive under this host: {alive.Count} "
            + $"({string.Join("; ", each)})";
    }

    /// <summary>A command line cut to what a journal line can carry.</summary>
    private static string Shorten(string said)
    {
        var one = said.ReplaceLineEndings(" ").Trim();
        return one.Length <= Shown ? one : one[..(Shown - 1)] + "…";
    }
}
