using System.IO;
using FreeHarbor.Core.Engine;
using FreeHarbor.Core.Preflight;

namespace FreeHarbor.Tray.Ui;

/// <summary>
/// What the Store package does at its first launch, in place of the setup wizard it cannot run (DD316).
/// </summary>
/// <remarks>
/// <para>The Inno wizard checks the machine before writing anything (DD130), offers to turn WSL2 on
/// (DD131, DD132), downloads the engine behind a checkbox (DD119), and ends on a page about the tray
/// overflow and the agent files (DD21, DD32). Installing an MSIX package runs none of that: the
/// package runs no code until somebody opens it. So the first launch does it, as a page in the
/// window, from the same pieces.</para>
///
/// <para>The decisions are here, as plain functions of what is on the machine, so they are tested
/// without a window. The page draws them.</para>
/// </remarks>
internal static class FirstRun
{
    /// <summary>The destination's name in the nav strip, which is also what a capture asks for.</summary>
    internal const string Destination = "Setup";

    /// <summary>The file that asks the next sign-in to open the window on this page.</summary>
    internal const string RestartMarkerName = "setup-after-restart";

    /// <summary>Roughly what the engine download weighs, as the wizard's checkbox states it.</summary>
    internal const string DownloadSize = "about 250 MB";

    /// <summary>What a reader who has never heard of WSL2 needs to know first, as the wizard says it.</summary>
    internal const string Wsl2InPlainWords =
        "WSL2 is the Windows feature that runs a real Linux kernel beside Windows. A container is a "
        + "Linux process, so until the feature is on there is nothing for an engine to run one in.";

    /// <summary>Whether this launch is a packaged copy meeting a machine with no engine yet.</summary>
    /// <param name="packaged">Whether the process runs in an MSIX package.</param>
    /// <param name="distributionRegistered">Whether this tool's distribution is registered with WSL.</param>
    /// <param name="settingsExist">Whether this account has a settings file.</param>
    /// <returns>Whether the window opens on the setup page.</returns>
    /// <remarks>
    /// The installer's copy never: its wizard already did all of this before the first file. A
    /// settings file means somebody has used FreeHarbor here before, and a registered distribution
    /// means the engine is in place, and either one is not a first launch.
    /// </remarks>
    internal static bool IsDue(bool packaged, bool distributionRegistered, bool settingsExist) =>
        packaged && !distributionRegistered && !settingsExist;

    /// <summary>Whether a launch should open the window because the page asked the restart to.</summary>
    /// <param name="packaged">Whether the process runs in an MSIX package.</param>
    /// <param name="markerExists">Whether the page left its marker before the restart.</param>
    /// <returns>Whether to open the window even for the startup task's quiet launch.</returns>
    /// <remarks>
    /// The wizard left a RunOnce value for this. A package has its startup task instead, and that
    /// launch is quiet on purpose (DD311), so the page leaves a file saying the next one is not.
    /// </remarks>
    internal static bool ReopensAfterRestart(bool packaged, bool markerExists) =>
        packaged && markerExists;

    /// <summary>Where the marker lives.</summary>
    /// <param name="paths">The install.</param>
    /// <returns>The path.</returns>
    internal static string RestartMarker(EnginePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.Root, RestartMarkerName);
    }

    /// <summary>The command the WSL2 row asks for, where that row is what blocks.</summary>
    /// <param name="report">The preflight.</param>
    /// <returns>The command, or <see langword="null"/> where WSL2 is not the blocker.</returns>
    /// <remarks>
    /// Read out of the row's own remedy, as the wizard does (DD131), so there is one place that decides
    /// the command. Not for a WSL that is installed and did not answer (DD357): turning the feature on
    /// is the wrong remedy for that, and the row says to restart instead.
    /// </remarks>
    internal static string? WslCommand(PreflightReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report[PreflightInspection.Rows.Wsl2] is { Verdict: Verdict.Fail, Blocking: true } row
            ? Backticked(row.Remedy)
            : null;
    }

    /// <summary>What a remedy spells in backticks, which is the command a reader has to type.</summary>
    /// <param name="remedy">The remedy.</param>
    /// <returns>The command, or <see langword="null"/>.</returns>
    internal static string? Backticked(string? remedy)
    {
        if (remedy is null)
        {
            return null;
        }

        var opened = remedy.IndexOf('`', StringComparison.Ordinal);
        if (opened < 0)
        {
            return null;
        }

        var closed = remedy.IndexOf('`', opened + 1);
        return closed > opened + 1 ? remedy[(opened + 1)..closed] : null;
    }

    /// <summary>Split a command line into the program and the rest, for an elevated run.</summary>
    /// <param name="command">The command, as a remedy spells it.</param>
    /// <returns>The program, under System32 when it is named bare, and its arguments.</returns>
    internal static (string FileName, string Arguments) Split(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var space = command.IndexOf(' ', StringComparison.Ordinal);
        var program = space < 0 ? command : command[..space];
        var arguments = space < 0 ? "" : command[(space + 1)..];

        // A bare name resolves through System32, which is where wsl.exe lives. This process is 64-bit,
        // so none of the redirection the 32-bit wizard had to step round (DD356) applies here.
        if (!program.Contains('\\', StringComparison.Ordinal))
        {
            program = Path.Combine(
                Environment.SystemDirectory,
                program.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? program : program + ".exe");
        }

        return (program, arguments);
    }

    /// <summary>What the page says once the elevated command has been through.</summary>
    /// <param name="run">What the elevation reported.</param>
    /// <param name="restartPending">Whether Windows is now waiting for a restart.</param>
    /// <returns>The sentence.</returns>
    internal static string AfterTurnOn(ElevatedRun run, bool restartPending)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Refused)
        {
            return "Turning WSL2 on needs one administrator prompt, and it was not granted. Nothing "
                + "changed. The command above does the same from an administrator terminal.";
        }

        if (!run.Ran)
        {
            return $"The command did not start: {run.Failure}";
        }

        if (!run.Succeeded)
        {
            return $"The command exited with code {run.ExitCode}, so WSL2 may not be on. The command "
                + "above shows its own message when run from an administrator terminal.";
        }

        return restartPending
            ? "WSL2 is on. Restart Windows to finish putting it in place. FreeHarbor opens on this "
                + "page again after the restart."
            : "The command has run. Restart Windows before WSL2 is usable. FreeHarbor opens on this "
                + "page again after the restart.";
    }

    /// <summary>The note about where the icon went (DD21), for a package.</summary>
    internal const string OverflowNote =
        "Windows puts a notification icon it has not seen before into the overflow, behind the "
        + "chevron next to the clock, so FreeHarbor's icon is there and not on the taskbar. To keep "
        + "the engine's state in sight, click the chevron and drag the FreeHarbor icon onto the "
        + "taskbar once; Windows remembers it from then on.";

    /// <summary>The commands that make the agent surface findable (DD32), with the package's own path.</summary>
    /// <param name="skill">Where this copy's <c>agent\SKILL.md</c> is.</param>
    /// <returns>One line per command, ready to copy.</returns>
    /// <remarks>
    /// Printed and never run. The agent configuration is the user's, and an allowlist is the last file
    /// a tool should edit without asking, which is why the wizard prints these too.
    /// </remarks>
    internal static IReadOnlyList<string> AgentCommands(string skill)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skill);
        return
        [
            "mkdir \"%USERPROFILE%\\.claude\\skills\\freeharbor\"",
            $"copy \"{skill}\" \"%USERPROFILE%\\.claude\\skills\\freeharbor\\\"",
        ];
    }

    /// <summary>The one line that grants every read verb and no write (DD32).</summary>
    internal const string AllowLine = "\"Bash(freeharbor read:*)\"";
}

/// <summary>The setup page's seams, live and for a capture (DD316).</summary>
internal static class FirstRunDestination
{
    /// <summary>Where Windows records that a feature change is waiting for a restart.</summary>
    /// <remarks>Written by Component Based Servicing and readable without elevation, as the wizard reads it.</remarks>
    private const string RebootPending =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending";

    /// <summary>The seams for this machine.</summary>
    /// <param name="startEngine">The tray's own start, so the dot follows it.</param>
    /// <returns>The live set.</returns>
    internal static FirstRunSeams OnThisMachine(Action startEngine) => new(
        () => new Core.Preflight.Windows.WindowsMachineFacts(),
        ProvisionAsync,
        new Cli.WindowsElevation(),
        () =>
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RebootPending);
            return key is not null;
        },
        () =>
        {
            var paths = new EnginePaths();
            Directory.CreateDirectory(paths.Root);
            File.WriteAllText(FirstRun.RestartMarker(paths), DateTimeOffset.Now.ToString("O"));
        },
        startEngine,
        Path.Combine(AppContext.BaseDirectory, "agent", "SKILL.md"));

    /// <summary>The seams for a capture: a machine without WSL2, and nothing that acts.</summary>
    /// <returns>The fixture set.</returns>
    internal static FirstRunSeams Fixture() => new(
        () => new Core.Fixtures.SampleMachineFacts(),
        _ => Task.FromResult<string?>("a capture downloads nothing"),
        new Refusing(),
        () => false,
        () => { },
        () => { },
        @"C:\Program Files\WindowsApps\FreeHarbor\agent\SKILL.md");

    /// <summary>Download and install the engine, as <c>--provision</c> does, a line per step.</summary>
    private static async Task<string?> ProvisionAsync(Action<string> said)
    {
        var paths = new EnginePaths();
        using var fetcher = new HttpArtefactFetcher();
        var outcome = await new EngineProvisioner(
                EngineManifest.Current, new ArtefactStore(fetcher, paths.Downloads), new Wsl(), paths)
            .ProvisionAsync(step => said(Cli.EngineCommand.StepLine(step).Trim()))
            .ConfigureAwait(false);

        return outcome.Succeeded
            ? null
            : $"Stopped at {outcome.Failure!.Step}: {outcome.Failure.Detail}";
    }

    /// <summary>An elevation that never prompts, for a capture.</summary>
    private sealed class Refusing : IElevated
    {
        public ElevatedRun Run(string fileName, string arguments) => new(Ran: false, Refused: true);
    }
}

/// <summary>Everything the setup page reads and can do, behind seams (DD316).</summary>
/// <param name="Facts">Reads the machine, fresh each time the page checks.</param>
/// <param name="Provision">Downloads and installs the engine, reporting each line; answers the failure, or null.</param>
/// <param name="Elevation">Runs the WSL2 command with administrator rights.</param>
/// <param name="RestartPending">Whether Windows is waiting for a restart.</param>
/// <param name="AskForTheWindowAfterRestart">Leaves the marker that reopens this page.</param>
/// <param name="StartEngine">Starts the engine once it is installed.</param>
/// <param name="Skill">Where this copy's agent skill file is.</param>
/// <remarks>
/// The live set for this machine, and a fixture for a capture (L6) that shows a machine without
/// WSL2 and refuses to act, for the reason the Engine page's seams do.
/// </remarks>
internal sealed record FirstRunSeams(
    Func<IMachineFacts> Facts,
    Func<Action<string>, Task<string?>> Provision,
    IElevated Elevation,
    Func<bool> RestartPending,
    Action AskForTheWindowAfterRestart,
    Action StartEngine,
    string Skill);
