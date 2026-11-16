using System.Diagnostics;
using FreeHarbor.Core.Engine;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>What a host that is done leaves behind (DD354).</summary>
public sealed class LeftAliveTests
{
    [Fact]
    public void Nothing_left_alive_writes_no_line() =>
        Assert.Null(LeftAlive.Clause([]));

    [Fact]
    public void Each_survivor_is_named_by_pid_and_command_line()
    {
        var clause = LeftAlive.Clause(
        [
            new LeftAlive(4120, "wsl.exe", @"C:\WINDOWS\system32\wsl.exe --list --running --quiet"),
            new LeftAlive(4188, "wsl.exe", null),
        ]);

        Assert.Equal(
            @"still alive under this host: 2 (4120 C:\WINDOWS\system32\wsl.exe --list --running --quiet; "
            + "4188 wsl.exe)",
            clause);
    }

    [Fact]
    public void A_long_command_line_is_cut_to_what_a_journal_line_carries()
    {
        var clause = LeftAlive.Clause([new LeftAlive(1, "wsl.exe", new string('x', 300))])!;

        Assert.EndsWith("…)", clause, StringComparison.Ordinal);
        Assert.True(clause.Length < 130, clause);
    }

    [Fact]
    public void A_running_child_of_this_process_is_found()
    {
        using var child = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "ping.exe"), "-n 30 127.0.0.1")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;

        try
        {
            var alive = LeftAlive.ChildrenOf(Environment.ProcessId);

            Assert.Contains(alive, survivor => survivor.Pid == child.Id
                && survivor.Name.Equals("ping.exe", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(alive, survivor => survivor.Name.Equals("conhost.exe", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            child.Kill(entireProcessTree: true);
            child.WaitForExit();
        }
    }
}
