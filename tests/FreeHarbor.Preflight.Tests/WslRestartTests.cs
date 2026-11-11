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
    public void A_journal_ending_in_the_timeout_says_wsl_is_not_answering_now() =>
        Assert.True(WslFailure.WslIsNotAnsweringNow([Answered, Timeout, "2026-11-11 19:40:00  Starting  quiet"]));

    [Fact]
    public void An_engine_that_answered_since_the_timeout_needs_nothing_restarted() =>
        Assert.False(WslFailure.WslIsNotAnsweringNow([Timeout, Answered]));

    [Fact]
    public void An_empty_journal_offers_nothing() =>
        Assert.False(WslFailure.WslIsNotAnsweringNow([]));

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
        Assert.Contains("WslService", outcome.Failure.Detail, StringComparison.Ordinal);
        Assert.Single(wsl.Invocations);
    }

    [Fact]
    public void A_wsl_still_silent_after_the_shutdown_is_reported_as_that()
    {
        var wsl = new FakeWsl().Answer(0).Answer(null, "", "wsl.exe did not finish");

        var outcome = new WslRestart(wsl, Stopped).Run();

        Assert.False(outcome.Succeeded);
        Assert.Equal(WslRestart.AnswerStep, outcome.Failure!.What);
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
