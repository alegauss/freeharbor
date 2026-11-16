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

    /// <summary>The step that ends the WSL service with administrator rights (DD351).</summary>
    public const string ServiceStep = "restart the WSL service";

    /// <summary>The step that asks WSL whether it answers again.</summary>
    public const string AnswerStep = "ask WSL whether it answers";

    /// <summary>What is left once nothing this tool can run will reach the service (DD351).</summary>
    /// <remarks>
    /// Restart and never Shut down, by name. Measured on 16 November 2026: a WSL stuck the evening
    /// before came back stuck the next morning, because Fast Startup turns Shut down into a
    /// hibernation of the kernel and the service's state rides across it. Restart is the one that
    /// boots Windows cold.
    /// </remarks>
    public const string RestartWindows =
        "restarting Windows is what is left, and it has to be Restart rather than Shut down: with "
        + "Fast Startup on, Shut down hibernates the kernel and WSL comes back as stuck as it was";

    private readonly IWsl _wsl;
    private readonly Func<RepairStep> _stopEngine;
    private readonly IElevated? _elevated;
    private readonly string? _logPath;
    private readonly TimeSpan _shutdownBudget;
    private readonly int _answerChecks;
    private readonly TimeSpan _answerPause;

    /// <summary>How many times WSL is asked whether it answers, after it went down (DD360).</summary>
    /// <remarks>
    /// Eight, each bounded by the probe budget and three seconds apart: about two and a half minutes
    /// on a WSL that never comes back, and one question on the usual one that does.
    /// </remarks>
    public const int AnswerChecks = 8;

    /// <summary>Construct the restart.</summary>
    /// <param name="wsl">WSL.</param>
    /// <param name="stopEngine">Takes the engine down the announced way, and says so.</param>
    /// <param name="elevated">
    /// How a command is run with administrator rights, or <see langword="null"/> where it is not
    /// offered. Used only when <c>wsl --shutdown</c> hangs too (DD351).
    /// </param>
    /// <param name="logPath">
    /// Where the elevated commands write what they said, or <see langword="null"/> for nowhere. A
    /// file because an elevated child's handles cannot be read from this side.
    /// </param>
    /// <param name="shutdownBudget">
    /// How long the shutdown may take, or <see langword="null"/> for a minute. A minute because a
    /// shutdown that has not returned by then is the service hanging too, and only ending the
    /// service reaches that, which needs administrator rights.
    /// </param>
    /// <param name="answerChecks">How many times to ask, or <see langword="null"/> for <see cref="AnswerChecks"/>.</param>
    /// <param name="answerPause">
    /// The pause between two questions, or <see langword="null"/> for three seconds. Overridden only
    /// by a test, which cannot wait on the failing path.
    /// </param>
    public WslRestart(
        IWsl wsl,
        Func<RepairStep> stopEngine,
        IElevated? elevated = null,
        string? logPath = null,
        TimeSpan? shutdownBudget = null,
        int? answerChecks = null,
        TimeSpan? answerPause = null)
    {
        _answerChecks = Math.Max(1, answerChecks ?? AnswerChecks);
        _answerPause = answerPause ?? TimeSpan.FromSeconds(3);
        ArgumentNullException.ThrowIfNull(wsl);
        ArgumentNullException.ThrowIfNull(stopEngine);
        _wsl = wsl;
        _stopEngine = stopEngine;
        _elevated = elevated;
        _logPath = logPath;
        _shutdownBudget = shutdownBudget ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// The elevated command line that ends the WSL service and starts it again (DD351).
    /// </summary>
    /// <param name="logPath">Where it writes what it said, or <see langword="null"/>.</param>
    /// <returns>The arguments for <c>cmd.exe</c>.</returns>
    /// <remarks>
    /// <para><b>Ended rather than stopped.</b> A service that will not answer <c>wsl --shutdown</c>
    /// will not answer the service manager's stop either, and <c>Restart-Service</c> then waits as
    /// long as the button did. <c>taskkill /f /t</c> ends <c>wslservice.exe</c> and what it started,
    /// and the utility VM goes with the process that owned it.</para>
    ///
    /// <para>The start is not judged here. <c>net start</c> exits 2 for a service a COM call already
    /// started again, which is not a failure; the question to WSL afterwards is what decides.</para>
    ///
    /// <para>Full System32 paths, for the rule <see cref="ElevatedCompaction"/> already keeps: what
    /// runs with administrator rights is not a name an earlier directory on the PATH answers for.</para>
    /// </remarks>
    public static string ServiceCommand(string? logPath)
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var taskkill = Path.Combine(system, "taskkill.exe");
        var net = Path.Combine(system, "net.exe");
        var first = logPath is null ? "" : $" > \"{logPath}\" 2>&1";
        var then = logPath is null ? "" : $" >> \"{logPath}\" 2>&1";
        return $"/c \"\"{taskkill}\" /f /t /im wslservice.exe{first} & \"{net}\" start WSLService{then}\"";
    }

    /// <summary>Run it.</summary>
    /// <param name="report">Called with each step as it lands.</param>
    /// <param name="saying">
    /// Called with what the running step is doing, for a progress line beside the steps (DD360).
    /// </param>
    /// <returns>What was done.</returns>
    public RepairOutcome Run(Action<RepairStep>? report = null, Action<string>? saying = null)
    {
        var steps = new List<RepairStep>();
        void Land(RepairStep step)
        {
            steps.Add(step);
            report?.Invoke(step);
        }

        Land(_stopEngine());

        var down = _wsl.Run(_shutdownBudget, "--shutdown");
        if (!down.Succeeded && _elevated is null)
        {
            Land(new RepairStep(
                ShutdownStep,
                false,
                "WSL would not shut down either, so the service itself is stuck: ending "
                + $"wslservice.exe as administrator, or {RestartWindows}. "
                + $"wsl.exe said: {down.Detail()}"));
            return new RepairOutcome(steps);
        }

        if (down.Succeeded)
        {
            Land(new RepairStep(
                ShutdownStep, true, "every WSL distribution on this machine was stopped"));
        }
        else
        {
            // The shutdown is not landed as a failure of its own. What it was for, WSL down, is
            // what this step does by other means, and a failed step here would make a restart that
            // worked read as one that did not.
            var service = EndTheService(down);
            Land(service);
            if (!service.Ok)
            {
                return new RepairOutcome(steps);
            }
        }

        Land(Answer(saying));
        return new RepairOutcome(steps);
    }

    /// <summary>Ask WSL whether it answers, until it does or the checks run out (DD360).</summary>
    /// <param name="saying">Told which check is running, for the page's progress line.</param>
    /// <returns>The step.</returns>
    /// <remarks>
    /// Measured on 16 November 2026: the service ended at 09:01:48 was still stopped a minute later
    /// and answering a few minutes after that, started again by Windows. One question straight after
    /// the kill would have reported a restart that worked as one that did not. Progress is said
    /// beside the steps and never as one, for the reason <see cref="ElevatedCompaction"/> gives.
    /// </remarks>
    private RepairStep Answer(Action<string>? saying)
    {
        WslResult? last = null;
        for (var check = 1; check <= _answerChecks; check++)
        {
            saying?.Invoke(
                $"asking WSL whether it answers, check {check} of {_answerChecks}; Windows can "
                + "take a minute or two to bring the service back");

            last = _wsl.Run(WslBudget.Probe, "--list", "--quiet");
            if (last.Succeeded)
            {
                return new RepairStep(
                    AnswerStep,
                    true,
                    check == 1 ? "WSL answers again" : $"WSL answers again, at check {check}");
            }

            if (check < _answerChecks)
            {
                Thread.Sleep(_answerPause);
            }
        }

        return new RepairStep(
            AnswerStep,
            false,
            $"WSL went down but still did not answer after {_answerChecks} checks: "
            + (last?.Detail() ?? "nothing was asked"));
    }

    /// <summary>End the service with administrator rights, after a shutdown that hung (DD351).</summary>
    /// <param name="down">What the shutdown said.</param>
    /// <returns>The step.</returns>
    private RepairStep EndTheService(WslResult down)
    {
        var hung = $"wsl --shutdown did not answer ({down.Detail()}), so the service itself was stuck";
        if (_logPath is not null)
        {
            try
            {
                File.Delete(_logPath);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                // A stale log only costs a misleading complaint on the failing path.
            }
        }

        var run = _elevated!.Run("cmd.exe", ServiceCommand(_logPath));
        if (run.Refused)
        {
            return new RepairStep(
                ServiceStep,
                false,
                $"{hung}, and administrator rights were declined, so it was left as it was: "
                + $"{RestartWindows}");
        }

        if (!run.Ran)
        {
            return new RepairStep(
                ServiceStep,
                false,
                $"{hung}, and it could not be ended with administrator rights "
                + $"({run.Failure ?? "no reason was given"}): {RestartWindows}");
        }

        return new RepairStep(
            ServiceStep,
            true,
            $"{hung}; it was ended and started again with administrator rights"
            + (run.ExitCode is 0 or 2 ? "" : $" (exited {run.ExitCode}: {Said()})"));
    }

    /// <summary>What the elevated commands wrote, on one line.</summary>
    private string Said()
    {
        if (_logPath is null)
        {
            return "nothing was kept";
        }

        try
        {
            var said = File.ReadAllText(_logPath).Trim().ReplaceLineEndings(" ");
            return said.Length > 0 ? said : $"it wrote nothing to {_logPath}";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return $"its log could not be read from {_logPath}";
        }
    }
}
