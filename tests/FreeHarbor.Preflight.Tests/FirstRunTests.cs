using FreeHarbor.Core.Engine;
using FreeHarbor.Core.Preflight;
using FreeHarbor.Tray.Ui;
using FreeHarbor.Tray.Ui.Pages;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>A Store copy's first launch, in place of the setup wizard (DD316).</summary>
public sealed class FirstRunTests
{
    private static readonly FakeMachine WithoutWsl2 = FakeMachine.Healthy with
    {
        Wsl = new WslInstallation { CommandPresent = true, FeatureInstalled = false },
    };

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    public void Only_a_packaged_copy_with_no_engine_and_no_settings_opens_on_setup(
        bool packaged, bool registered, bool settings, bool due) =>
        Assert.Equal(due, FirstRun.IsDue(packaged, registered, settings));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void The_marker_reopens_the_window_only_under_a_package(bool packaged, bool marker, bool opens) =>
        Assert.Equal(opens, FirstRun.ReopensAfterRestart(packaged, marker));

    [Fact]
    public void The_marker_lives_under_the_install_root()
    {
        var paths = new EnginePaths(@"C:\root");
        Assert.Equal(@"C:\root\setup-after-restart", FirstRun.RestartMarker(paths));
    }

    [Fact]
    public void A_machine_without_wsl2_is_offered_the_command_its_row_names()
    {
        var report = PreflightInspection.Run(WithoutWsl2);

        Assert.Equal("wsl.exe --install --no-distribution", FirstRun.WslCommand(report));
    }

    [Fact]
    public void The_capture_fixture_is_a_machine_whose_one_blocker_is_wsl2()
    {
        var report = PreflightInspection.Run(new Core.Fixtures.SampleMachineFacts());

        Assert.Equal([PreflightInspection.Rows.Wsl2], report.Blockers.Select(row => row.Id));
        Assert.NotNull(FirstRun.WslCommand(report));
    }

    [Fact]
    public void A_healthy_machine_is_offered_no_command() =>
        Assert.Null(FirstRun.WslCommand(PreflightInspection.Run(FakeMachine.Healthy)));

    [Fact]
    public void A_wsl_that_did_not_answer_is_not_offered_the_turn_on()
    {
        // DD357: installed and stuck is a restart, and turning the feature on is the wrong remedy.
        var report = PreflightInspection.Run(FakeMachine.Healthy with
        {
            Wsl = new WslInstallation { CommandPresent = true, FeatureInstalled = true, Unreadable = "no answer" },
        });

        Assert.Null(FirstRun.WslCommand(report));
    }

    [Theory]
    [InlineData("Run `wsl --update` now.", "wsl --update")]
    [InlineData("No command here.", null)]
    [InlineData("Unclosed `wsl", null)]
    [InlineData(null, null)]
    public void The_command_is_what_the_remedy_puts_in_backticks(string? remedy, string? command) =>
        Assert.Equal(command, FirstRun.Backticked(remedy));

    [Fact]
    public void A_bare_program_resolves_under_system32()
    {
        var (fileName, arguments) = FirstRun.Split("wsl.exe --install --no-distribution");

        Assert.Equal(Path.Combine(Environment.SystemDirectory, "wsl.exe"), fileName);
        Assert.Equal("--install --no-distribution", arguments);
        Assert.EndsWith(@"\wsl.exe", FirstRun.Split("wsl --update").FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_ending_of_the_elevated_run_says_what_to_do_next()
    {
        Assert.Contains("not granted", FirstRun.AfterTurnOn(new ElevatedRun(Ran: false, Refused: true), false), StringComparison.Ordinal);
        Assert.Contains("did not start", FirstRun.AfterTurnOn(new ElevatedRun(Ran: false, Failure: "x"), false), StringComparison.Ordinal);
        Assert.Contains("code 5", FirstRun.AfterTurnOn(new ElevatedRun(Ran: true, ExitCode: 5), false), StringComparison.Ordinal);
        Assert.Contains("WSL2 is on", FirstRun.AfterTurnOn(new ElevatedRun(Ran: true, ExitCode: 0), true), StringComparison.Ordinal);
        Assert.Contains("Restart Windows", FirstRun.AfterTurnOn(new ElevatedRun(Ran: true, ExitCode: 0), false), StringComparison.Ordinal);
    }

    [Fact]
    public void The_agent_commands_name_this_copy_skill_and_change_nothing()
    {
        var commands = FirstRun.AgentCommands(@"C:\pkg\agent\SKILL.md");

        Assert.Contains(commands, line => line.Contains(@"C:\pkg\agent\SKILL.md", StringComparison.Ordinal));
        Assert.All(commands, line => Assert.True(
            line.StartsWith("mkdir ", StringComparison.Ordinal) || line.StartsWith("copy ", StringComparison.Ordinal)));
        Assert.Equal("\"Bash(freeharbor read:*)\"", FirstRun.AllowLine);
    }

    [Fact]
    public void A_row_shows_its_remedy_only_when_it_is_not_green()
    {
        var report = PreflightInspection.Run(WithoutWsl2);

        var wsl = SetupRow.Of(report[PreflightInspection.Rows.Wsl2]!);
        var build = SetupRow.Of(report[PreflightInspection.Rows.WindowsBuild]!);

        Assert.Equal("[FAIL]", wsl.Mark);
        Assert.Equal(System.Windows.Visibility.Visible, wsl.RemedyShown);
        Assert.Equal("[ok  ]", build.Mark);
        Assert.Equal(System.Windows.Visibility.Collapsed, build.RemedyShown);
    }

    [Fact]
    public void Setup_is_hidden_in_the_strip_and_never_reopened_from_memory()
    {
        var root = RepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(root, @"src\FreeHarbor.Tray\Ui\MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, @"src\FreeHarbor.Tray\Ui\MainWindow.xaml.cs"));

        Assert.Contains("Tag=\"Setup\" Checked=\"Destination_Checked\" Visibility=\"Collapsed\"", markup, StringComparison.Ordinal);
        Assert.Contains("string.Equals(_recall.Destination, FirstRun.Destination", code, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FreeHarbor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
