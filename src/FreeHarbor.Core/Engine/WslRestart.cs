namespace FreeHarbor.Core.Engine;

/// <summary>
/// Restart WSL itself, for the one failure restarting the engine cannot reach (DD344).
/// </summary>
/// <remarks>
/// <para>Measured on 11 November 2026: the host spent hours on starts that each ended in
/// <see cref="WslFailure.WslNotAnswering"/>, <c>wsl --list --running</c> hung, and one
/// <c>wsl --shutdown</c> typed at a prompt had the engine back fifteen seconds later. The host's
/// own retry terminates this distribution and starts it again, which is no help when the service
/// underneath will not hand out a virtual machine at all.</para>
///
/// <para><b>Offered and never run unasked.</b> <c>wsl --shutdown</c> stops every distribution on
/// the machine, which is somebody's Ubuntu shell in another window, and the quit path refuses it
/// for that reason. What changes here is that the user has asked, after being told who else goes
/// down.</para>
///
/// <para>Three steps: the engine down the announced way, so the host does not put it back under
/// the shutdown; the shutdown; then one question to WSL, because a shutdown that returned is not
/// yet a WSL that answers, and that difference is the whole report.</para>
/// </remarks>
public sealed class WslRestart
{
    /// <summary>The step that shuts WSL down. Named once, for the page and the verb.</summary>
    public const string ShutdownStep = "shut WSL down";

    /// <summary>The step that asks WSL whether it answers again.</summary>
    public const string AnswerStep = "ask WSL whether it answers";

    private readonly IWsl _wsl;
    private readonly Func<RepairStep> _stopEngine;
    private readonly TimeSpan _shutdownBudget;

    /// <summary>Construct the restart.</summary>
    /// <param name="wsl">WSL.</param>
    /// <param name="stopEngine">Takes the engine down the announced way, and says so.</param>
    /// <param name="shutdownBudget">
    /// How long the shutdown may take, or <see langword="null"/> for a minute. A minute because a
    /// shutdown that has not returned by then is the service hanging too, and the remedy for that is
    /// outside this tool: restarting the service needs administrator rights, and so does a reboot's
    /// worth of anything else.
    /// </param>
    public WslRestart(IWsl wsl, Func<RepairStep> stopEngine, TimeSpan? shutdownBudget = null)
    {
        ArgumentNullException.ThrowIfNull(wsl);
        ArgumentNullException.ThrowIfNull(stopEngine);
        _wsl = wsl;
        _stopEngine = stopEngine;
        _shutdownBudget = shutdownBudget ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>Run it.</summary>
    /// <param name="report">Called with each step as it lands.</param>
    /// <returns>What was done.</returns>
    public RepairOutcome Run(Action<RepairStep>? report = null)
    {
        var steps = new List<RepairStep>();
        void Land(RepairStep step)
        {
            steps.Add(step);
            report?.Invoke(step);
        }

        Land(_stopEngine());

        var down = _wsl.Run(_shutdownBudget, "--shutdown");
        if (!down.Succeeded)
        {
            Land(new RepairStep(
                ShutdownStep,
                false,
                "WSL would not shut down either, so the service itself is stuck: restarting it "
                + "(Restart-Service WslService, as administrator) or restarting Windows is what is "
                + $"left. wsl.exe said: {down.Detail()}"));
            return new RepairOutcome(steps);
        }

        Land(new RepairStep(ShutdownStep, true, "every WSL distribution on this machine was stopped"));

        var answer = _wsl.Run(WslBudget.Probe, "--list", "--quiet");
        Land(answer.Succeeded
            ? new RepairStep(AnswerStep, true, "WSL answers again")
            : new RepairStep(
                AnswerStep,
                false,
                $"WSL shut down but still does not answer: {answer.Detail()}"));

        return new RepairOutcome(steps);
    }
}
