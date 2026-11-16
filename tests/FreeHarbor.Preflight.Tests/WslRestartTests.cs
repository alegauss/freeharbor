using FreeHarbor.Core.Engine;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>Restarting WSL for a WSL that stopped answering (DD344).</summary>
public sealed class WslRestartTests
{
    private const string Timeout =
        "2026-11-11 19:39:30  Stopped   the daemon exited while starting: wsl.exe exited -1: "
        + "O tempo limite da operação foi atingido. Código de erro: "
        + "Wsl/Service/CreateInstance/HCS_E_CONNECTION_TIMEOUT";

    private const string Answered =
        "2026-11-11 19:47:10  Running   the engine answered on \\\\.\\pipe\\docker_engine";

    private static RepairStep Stopped() =>
        new(FilesystemRepair.StopStep, true, "told the host to stop");

    // ---- recognising it ----------------------------------------------------------------------

    [Fact]
    public void The_timeout_code_is_wsl_not_answering_in_any_language() =>
        Assert.True(WslFailure.SaysWslIsNotAnswering(Timeout));

    [Theory]
    [InlineData("wsl.exe exited -1: Wsl/Service/CreateInstance/HCS_E_HYPERV_NOT_INSTALLED")]
    [InlineData("the daemon exited while starting: wsl.exe exited 0 without a word")]
    [InlineData(null)]
    public void Other_failures_are_not_offered_a_restart(string? said) =>
        Assert.False(WslFailure.SaysWslIsNotAnswering(said));

    [Fact]
    public void A_journal_ending_in_a_hung_list_says_wsl_is_not_answering_now() =>
        Assert.True(WslFailure.WslIsNotAnsweringNow([Answered,
            "2026-11-16 06:59:02  Starting  the launcher is alive, but wsl --list did not answer within 15s, "
            + WslFailure.WslHung + " and the pipe did not answer within 60s (quiet)"]));

    [Fact]
    public void A_journal_ending_in_the_timeout_says_wsl_is_not_answering_now() =>
        Assert.True(WslFailure.WslIsNotAnsweringNow([Answered, Timeout, "2026-11-11 19:40:00  Starting  quiet"]));

    [Fact]
    public void An_engine_that_answered_since_the_timeout_needs_nothing_restarted() =>
        Assert.False(WslFailure.WslIsNotAnsweringNow([Timeout, Answered]));

    [Fact]
    public void An_empty_journal_offers_nothing() =>
        Assert.False(WslFailure.WslIsNotAnsweringNow([]));

    // ---- the tray's follow-up (DD345) --------------------------------------------------------

    [Fact]
    public void The_tray_says_it_once_when_the_journal_names_the_timeout()
    {
        var notice = new WslStuckNotice();
        string[] journal = [Answered, "2026-11-11 19:39:00  tray      the engine stopped answering"];

        Assert.False(notice.Due(100, () => journal));

        journal = [.. journal, Timeout];
        Assert.True(notice.Due(200, () => journal));

        // Said once per outage, however much more the journal grows.
        journal = [.. journal, Timeout];
        Assert.False(notice.Due(300, () => journal));
    }

    [Fact]
    public void The_tray_reads_the_journal_only_when_its_length_moved()
    {
        var notice = new WslStuckNotice();
        var reads = 0;
        IReadOnlyList<string> Read()
        {
            reads++;
            return [Answered];
        }

        notice.Due(100, Read);
        notice.Due(100, Read);
        notice.Due(100, Read);
        notice.Due(140, Read);

        Assert.Equal(2, reads);
    }

    [Fact]
    public void The_tray_says_nothing_for_a_timeout_the_engine_has_answered_since()
    {
        var notice = new WslStuckNotice();

        Assert.False(notice.Due(100, () => [Timeout, Answered]));
    }

    [Fact]
    public void The_follow_up_names_the_page_and_the_button_it_opens_on() =>
        Assert.Contains("Restart WSL", WslStuckNotice.Text, StringComparison.Ordinal);

    // ---- the sequence ------------------------------------------------------------------------

    [Fact]
    public void It_stops_the_engine_then_shuts_wsl_down_then_asks_it()
    {
        var wsl = new FakeWsl();
        var reported = new List<RepairStep>();

        var outcome = new WslRestart(wsl, Stopped).Run(reported.Add);

        Assert.True(outcome.Succeeded);
        Assert.True(outcome.EngineWentDown);
        Assert.Equal(
            [FilesystemRepair.StopStep, WslRestart.ShutdownStep, WslRestart.AnswerStep],
            reported.Select(step => step.What));
        Assert.Equal(["--shutdown"], wsl.Invocations[0]);
        Assert.Equal(["--list", "--quiet"], wsl.Invocations[1]);
    }

    [Fact]
    public void A_shutdown_that_hangs_too_names_the_service_and_asks_nothing_more()
    {
        var wsl = new FakeWsl().Answer(null, "", "wsl.exe did not finish within 60s");

        var outcome = new WslRestart(wsl, Stopped).Run();

        Assert.False(outcome.Succeeded);
        Assert.Equal(WslRestart.ShutdownStep, outcome.Failure!.What);
        Assert.Contains("wslservice.exe", outcome.Failure.Detail, StringComparison.Ordinal);
        Assert.Single(wsl.Invocations);
    }

    [Fact]
    public void A_shutdown_that_hangs_without_elevation_says_restart_rather_than_shut_down()
    {
        var wsl = new FakeWsl().Answer(null, "", "wsl.exe did not finish within 60s");

        var outcome = new WslRestart(wsl, Stopped).Run();

        Assert.Contains("Restart rather than Shut down", outcome.Failure!.Detail, StringComparison.Ordinal);
    }

    // ---- ending the service when the shutdown hangs (DD351) -------------------------------------

    private sealed class FakeElevated(ElevatedRun answer) : IElevated
    {
        public List<(string File, string Arguments)> Calls { get; } = [];

        public ElevatedRun Run(string fileName, string arguments)
        {
            Calls.Add((fileName, arguments));
            return answer;
        }
    }

    [Fact]
    public void A_shutdown_that_hangs_ends_the_service_elevated_and_then_asks_wsl()
    {
        var wsl = new FakeWsl().Answer(null, "", "wsl.exe did not finish within 60s").Answer(0);
        var elevated = new FakeElevated(new ElevatedRun(Ran: true, ExitCode: 0));
        var reported = new List<RepairStep>();

        var outcome = new WslRestart(wsl, Stopped, elevated).Run(reported.Add);

        Assert.True(outcome.Succeeded);
        Assert.Equal(
            [FilesystemRepair.StopStep, WslRestart.ServiceStep, WslRestart.AnswerStep],
            reported.Select(step => step.What));
        var call = Assert.Single(elevated.Calls);
        Assert.Equal("cmd.exe", call.File);
        Assert.Contains("taskkill.exe\" /f /t /im wslservice.exe", call.Arguments, StringComparison.Ordinal);
        Assert.Contains("net.exe\" start WSLService", call.Arguments, StringComparison.Ordinal);
        Assert.Equal(["--list", "--quiet"], wsl.Invocations[1]);
    }

    [Fact]
    public void A_service_already_started_again_by_wsl_is_not_a_failure()
    {
        var wsl = new FakeWsl().Answer(null, "", "hung").Answer(0);

        var outcome = new WslRestart(
            wsl, Stopped, new FakeElevated(new ElevatedRun(Ran: true, ExitCode: 2))).Run();

        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public void A_declined_prompt_leaves_the_service_and_names_restart()
    {
        var wsl = new FakeWsl().Answer(null, "", "hung");

        var outcome = new WslRestart(
            wsl, Stopped, new FakeElevated(new ElevatedRun(Ran: false, Refused: true))).Run();

        Assert.False(outcome.Succeeded);
        Assert.Equal(WslRestart.ServiceStep, outcome.Failure!.What);
        Assert.Contains("declined", outcome.Failure.Detail, StringComparison.Ordinal);
        Assert.Contains("Restart rather than Shut down", outcome.Failure.Detail, StringComparison.Ordinal);
        Assert.Single(wsl.Invocations);
    }

    [Fact]
    public void A_shutdown_that_worked_never_asks_for_administrator_rights()
    {
        var elevated = new FakeElevated(new ElevatedRun(Ran: true, ExitCode: 0));

        var outcome = new WslRestart(new FakeWsl(), Stopped, elevated).Run();

        Assert.True(outcome.Succeeded);
        Assert.Empty(elevated.Calls);
    }

    [Fact]
    public void The_service_command_writes_its_words_to_the_log_it_is_given()
    {
        var command = WslRestart.ServiceCommand(@"C:\Users\a b\fh\wsl-restart.log");

        Assert.Contains("> \"C:\\Users\\a b\\fh\\wsl-restart.log\" 2>&1", command, StringComparison.Ordinal);
        Assert.Contains(">> \"C:\\Users\\a b\\fh\\wsl-restart.log\" 2>&1", command, StringComparison.Ordinal);
        Assert.StartsWith("/c \"\"", command, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wsl_still_silent_after_every_check_is_reported_as_that()
    {
        var wsl = new FakeWsl().Answer(0)
            .Answer(null, "", "wsl.exe did not finish")
            .Answer(null, "", "wsl.exe did not finish")
            .Answer(null, "", "wsl.exe did not finish");

        var outcome = new WslRestart(wsl, Stopped, answerChecks: 3, answerPause: TimeSpan.Zero).Run();

        Assert.False(outcome.Succeeded);
        Assert.Equal(WslRestart.AnswerStep, outcome.Failure!.What);
        Assert.Contains("after 3 checks", outcome.Failure.Detail, StringComparison.Ordinal);
        Assert.Equal(4, wsl.Invocations.Count);
    }

    // ---- waiting for WSL to come back (DD360) --------------------------------------------------

    [Fact]
    public void A_wsl_that_comes_back_at_a_later_check_is_a_restart_that_worked()
    {
        // Measured on 16 November: the ended service was back minutes later, started by Windows.
        var wsl = new FakeWsl().Answer(null, "", "hung")
            .Answer(null, "", "wsl.exe did not finish")
            .Answer(null, "", "wsl.exe did not finish")
            .Answer(0, "freeharbor");
        var said = new List<string>();

        var outcome = new WslRestart(
                wsl,
                Stopped,
                new FakeElevated(new ElevatedRun(Ran: true, ExitCode: 2)),
                answerPause: TimeSpan.Zero)
            .Run(saying: said.Add);

        Assert.True(outcome.Succeeded);
        Assert.Contains("at check 3", outcome.Steps[^1].Detail, StringComparison.Ordinal);
        Assert.Equal(3, said.Count);
        Assert.Contains("check 1 of 8", said[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Progress_is_said_beside_the_steps_and_never_lands_as_one()
    {
        var reported = new List<RepairStep>();
        var wsl = new FakeWsl().Answer(0).Answer(null, "", "slow").Answer(0);

        new WslRestart(wsl, Stopped, answerPause: TimeSpan.Zero).Run(reported.Add, _ => { });

        Assert.Equal(
            [FilesystemRepair.StopStep, WslRestart.ShutdownStep, WslRestart.AnswerStep],
            reported.Select(step => step.What));
    }

    // ---- what the page says ------------------------------------------------------------------

    [Fact]
    public void The_question_names_the_other_distributions_it_stops()
    {
        var plan = RepairPrompt.RestartWslConfirmation(["Ubuntu", "Debian"]);

        Assert.Contains("Ubuntu, Debian", plan, StringComparison.Ordinal);
        Assert.Contains("every WSL distribution", plan, StringComparison.Ordinal);
        Assert.EndsWith("Restart WSL now?", plan, StringComparison.Ordinal);
    }

    [Fact]
    public void The_question_still_warns_when_wsl_could_not_say_who_else_is_up() =>
        Assert.Contains(
            "any other distribution you have open",
            RepairPrompt.RestartWslConfirmation([]),
            StringComparison.Ordinal);

    [Fact]
    public void A_restart_that_worked_starts_docker_again()
    {
        var prompt = RepairPrompt.AfterRestart(
            new WslRestart(new FakeWsl(), Stopped).Run());

        Assert.True(prompt.StartsAgain);
        Assert.Equal("WSL was restarted", prompt.Headline);
    }

    [Fact]
    public void A_restart_that_failed_after_the_stop_still_starts_docker_so_the_host_keeps_trying()
    {
        var prompt = RepairPrompt.AfterRestart(
            new WslRestart(new FakeWsl().Answer(1, "", "refused"), Stopped).Run());

        Assert.True(prompt.StartsAgain);
        Assert.Equal("WSL did not come back", prompt.Headline);
    }
}
