using System.Windows;
using FreeHarbor.Core.Preflight;

namespace FreeHarbor.Tray.Ui.Pages;

/// <summary>One preflight row, as the page draws it.</summary>
/// <param name="Mark">The verdict, in the column shape the console prints.</param>
/// <param name="Title">The row's name.</param>
/// <param name="Detail">What was found.</param>
/// <param name="Remedy">What fixes it, or empty.</param>
/// <param name="RemedyShown">Whether the remedy line is drawn.</param>
internal sealed record SetupRow(string Mark, string Title, string Detail, string Remedy, Visibility RemedyShown)
{
    /// <summary>Draw a row from the preflight's own record.</summary>
    /// <param name="check">The row.</param>
    /// <returns>What the page binds to.</returns>
    internal static SetupRow Of(PreflightCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        var mark = check.Verdict switch
        {
            Verdict.Pass => "[ok  ]",
            Verdict.Warn => "[warn]",
            Verdict.Fail => "[FAIL]",
            _ => "[ ?? ]",
        };

        var remedy = check.Verdict is Verdict.Pass ? null : check.Remedy;
        return new SetupRow(
            mark, check.Title, check.Detail, remedy ?? "",
            string.IsNullOrEmpty(remedy) ? Visibility.Collapsed : Visibility.Visible);
    }
}

/// <summary>A Store copy's first launch: what the setup wizard did, as a page (DD316).</summary>
/// <remarks>
/// <para>The machine is read through the same <see cref="PreflightInspection"/> the wizard and
/// <c>--preflight</c> run, so there is still one opinion about it. The read is off the thread that
/// draws, because the WSL2 row can wait on <c>wsl.exe</c> for seconds.</para>
///
/// <para>Nothing happens without a button. Turning WSL2 on is one elevated command somebody asked
/// for, and the download is a quarter of a gigabyte somebody chose to spend.</para>
/// </remarks>
internal partial class SetupPage : System.Windows.Controls.UserControl
{
    private readonly FirstRunSeams _seams;
    private string? _command;
    private bool _working;

    /// <summary>Construct the page.</summary>
    /// <param name="seams">What it reads and can do.</param>
    internal SetupPage(FirstRunSeams seams)
    {
        ArgumentNullException.ThrowIfNull(seams);
        InitializeComponent();
        _seams = seams;

        WslWords.Text = FirstRun.Wsl2InPlainWords;
        EngineLead.Text =
            $"Downloads the container engine, {FirstRun.DownloadSize}, from the pinned sources About "
            + "names, checks each file against its SHA-256, and installs it in a WSL2 distribution of "
            + "its own called freeharbor.";
        Overflow.Text = FirstRun.OverflowNote;
        AgentCommands.Text = string.Join(Environment.NewLine, FirstRun.AgentCommands(seams.Skill));
        AllowLine.Text = FirstRun.AllowLine;
    }

    /// <summary>Read the machine and draw what it says.</summary>
    /// <returns>A task that completes when the page is drawn.</returns>
    internal async Task CheckAsync()
    {
        if (_working)
        {
            return;
        }

        CheckAgain.IsEnabled = false;
        VerdictLine.Text = "Checking…";
        try
        {
            var report = await Task.Run(() => PreflightInspection.Run(_seams.Facts())).ConfigureAwait(true);
            Draw(report);
        }
        finally
        {
            CheckAgain.IsEnabled = true;
        }
    }

    private void Draw(PreflightReport report)
    {
        Rows.ItemsSource = report.Checks.Select(SetupRow.Of).ToList();
        VerdictLine.Text = report.CanHostEngine
            ? "This machine can host the engine."
            : "Something below has to change before the engine can run here. Each row says what.";

        _command = FirstRun.WslCommand(report);
        WslPanel.Visibility = _command is null ? Visibility.Collapsed : Visibility.Visible;
        Command.Text = _command ?? "";

        Download.IsEnabled = report.CanHostEngine && !_working;
        Download.ToolTip = report.CanHostEngine
            ? null
            : "The rows above have to be green first: the engine cannot be installed on this machine yet.";
    }

    private async void CheckAgainClicked(object sender, RoutedEventArgs e) =>
        await CheckAsync().ConfigureAwait(true);

    private void CopyClicked(object sender, RoutedEventArgs e)
    {
        if (_command is null)
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(_command);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another process held the clipboard. Worth a word, not a dialog.
            Say(WslSaid, "Windows would not hand over the clipboard. Try that again.");
        }
    }

    /// <summary>Run the WSL2 command elevated, once somebody pressed the button (DD132).</summary>
    private async void TurnOnClicked(object sender, RoutedEventArgs e)
    {
        if (_command is null)
        {
            return;
        }

        var (fileName, arguments) = FirstRun.Split(_command);
        TurnOn.IsEnabled = false;
        Say(WslSaid, "Waiting for the administrator prompt…");
        try
        {
            var run = await Task.Run(() => _seams.Elevation.Run(fileName, arguments)).ConfigureAwait(true);
            if (run.Succeeded)
            {
                _seams.AskForTheWindowAfterRestart();
            }

            Say(WslSaid, FirstRun.AfterTurnOn(run, _seams.RestartPending()));
        }
        finally
        {
            TurnOn.IsEnabled = true;
        }
    }

    /// <summary>Download and install the engine, a line per step, then start it (DD119).</summary>
    private async void DownloadClicked(object sender, RoutedEventArgs e)
    {
        _working = true;
        Download.IsEnabled = false;
        CheckAgain.IsEnabled = false;
        Steps.Text = "";
        Steps.Visibility = Visibility.Visible;
        Say(EngineSaid, "Downloading and installing the engine. This can take several minutes.");

        try
        {
            void Said(string line) => Dispatcher.BeginInvoke(() =>
                Steps.Text = Steps.Text.Length == 0 ? line : Steps.Text + Environment.NewLine + line);

            var failure = await Task.Run(() => _seams.Provision(Said)).ConfigureAwait(true);
            if (failure is null)
            {
                Say(EngineSaid,
                    "The engine is installed and starting. The other pages fill in once it answers.");
                _seams.StartEngine();
                return;
            }

            Say(EngineSaid, $"{failure} The button tries again, and keeps what was already downloaded.");
            Download.IsEnabled = true;
        }
        finally
        {
            _working = false;
            CheckAgain.IsEnabled = true;
        }
    }

    private static void Say(System.Windows.Controls.TextBlock where, string text)
    {
        where.Text = text;
        where.Visibility = Visibility.Visible;
    }
}
