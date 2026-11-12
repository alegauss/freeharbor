using FreeHarbor.Core.Api;
using FreeHarbor.Tray.Ui;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>A log line's colour comes from the level it declares, and from its stream only where it declares none.</summary>
public sealed class LogLevelTests
{
    [Theory]
    // mariadbd writes its whole log to stderr: a note is not a failure.
    [InlineData("2026-11-12 11:42:24 0 [Note] mariadbd: ready for connections.", false)]
    [InlineData("2026-11-12 11:45:18 0 [Note] InnoDB: error log rotated", false)]
    [InlineData("2026-11-12 11:42:24 0 [Warning] Aborted connection 3 to db", false)]
    [InlineData("2026-11-12 11:42:24 0 [ERROR] InnoDB: Unable to lock ./ibdata1", true)]
    // postgres, also on stderr.
    [InlineData("2026-11-12 11:42:24.010 UTC [1] LOG:  database system is ready to accept connections", false)]
    [InlineData("2026-11-12 11:42:24.010 UTC [7] FATAL:  password authentication failed for user \"app\"", true)]
    // nginx's error log, which is named for the file and not the level.
    [InlineData("2026/11/12 11:42:24 [notice] 1#1: start worker processes", false)]
    [InlineData("2026/11/12 11:42:24 [emerg] 1#1: bind() to 0.0.0.0:80 failed", true)]
    // logfmt and the common bare words.
    [InlineData("time=2026-11-12T11:42:24Z level=info msg=\"listening on :8080\"", false)]
    [InlineData("time=2026-11-12T11:42:24Z level=error msg=\"dial tcp: refused\"", true)]
    [InlineData("INFO:root:started", false)]
    // No level: the stream decides, so a shell's complaint on stderr stays red.
    [InlineData("sh: /entrypoint.sh: Permission denied", true)]
    public void A_stderr_line_is_red_only_when_its_level_says_so_or_it_names_none(string line, bool red)
    {
        Assert.Equal(red, LogLevel.IsError(line, fromStdErr: true));
    }

    [Fact]
    public void A_declared_error_on_stdout_is_red_and_plain_stdout_is_not()
    {
        Assert.True(LogLevel.IsError("[ERROR] could not open config", fromStdErr: false));
        Assert.False(LogLevel.IsError("listening on :8080", fromStdErr: false));
    }

    [Fact]
    public void Prose_that_mentions_an_error_in_lower_case_is_not_a_level()
    {
        // A bare word counts only in capitals; otherwise every "no error" would turn a line red.
        Assert.Null(LogLevel.Declared("retrying after an error, attempt 2"));
    }

    [Fact]
    public void The_project_pane_draws_a_healthy_mariadb_without_red()
    {
        var tail = DetailView.Tail(
            [
                new LogChunk(LogStream.StdErr, "2026-11-12 11:42:24 0 [Note] mariadbd: ready for connections.\n"),
                new LogChunk(LogStream.StdErr, "2026-11-12 11:45:18 0 [Note] mariadbd: Shutdown complete\n"),
            ],
            keep: 10);

        Assert.All(tail, line => Assert.False(line.IsError, line.Text));
    }
}
