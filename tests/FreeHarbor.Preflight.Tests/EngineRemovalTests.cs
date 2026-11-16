using FreeHarbor.Core.Engine;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>Taking the engine off a machine before the app goes (DD317).</summary>
public sealed class EngineRemovalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fh-removal-{Guid.NewGuid():N}");

    public EngineRemovalTests()
    {
        foreach (var folder in EngineRemoval.EngineFolders)
        {
            Directory.CreateDirectory(Path.Combine(_root, folder));
            File.WriteAllBytes(Path.Combine(_root, folder, "payload.bin"), new byte[1024]);
        }

        foreach (var file in EngineRemoval.UserFiles)
        {
            File.WriteAllText(Path.Combine(_root, file), "{}");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private EngineRemoval Removal(FakeWsl wsl) => new(wsl, new EnginePaths(_root), _ => { });

    [Fact]
    public void It_terminates_then_unregisters_then_deletes_the_engine_and_keeps_the_settings()
    {
        var wsl = new FakeWsl();

        var report = Removal(wsl).Remove(all: false);

        Assert.True(report.Succeeded);
        Assert.Equal(["--terminate", "--unregister"], wsl.Invocations.Select(argv => argv[0]));
        Assert.All(EngineRemoval.EngineFolders, folder => Assert.False(Directory.Exists(Path.Combine(_root, folder))));
        Assert.All(EngineRemoval.UserFiles, file => Assert.True(File.Exists(Path.Combine(_root, file))));
        Assert.Equal(4 * 1024, report.Freed);
        Assert.Contains("unregistered freeharbor", report.Lines);
    }

    [Fact]
    public void All_also_deletes_the_settings_and_details()
    {
        var report = Removal(new FakeWsl()).Remove(all: true);

        Assert.True(report.Succeeded);
        Assert.All(EngineRemoval.UserFiles, file => Assert.False(File.Exists(Path.Combine(_root, file))));
    }

    [Fact]
    public void A_failed_unregister_deletes_nothing()
    {
        // A folder deleted under a distribution WSL still has registered leaves a broken entry.
        var wsl = new FakeWsl();
        wsl.Answer(0).Answer(1, "Wsl/Service/E_ACCESSDENIED");

        var report = Removal(wsl).Remove(all: true);

        Assert.False(report.Succeeded);
        Assert.All(EngineRemoval.EngineFolders, folder => Assert.True(Directory.Exists(Path.Combine(_root, folder))));
        Assert.Contains(report.Lines, line => line.StartsWith("could not unregister", StringComparison.Ordinal));
    }

    [Fact]
    public void A_distribution_already_gone_still_has_its_folders_cleaned()
    {
        // The case a bare package removal leaves after a reinstall: the folders, nothing registered.
        var wsl = new FakeWsl();
        wsl.Answer(1, "Wsl/Service/WSL_E_DISTRO_NOT_FOUND").Answer(1, "Wsl/Service/WSL_E_DISTRO_NOT_FOUND");

        var report = Removal(wsl).Remove(all: false);

        Assert.True(report.Succeeded);
        Assert.Contains("freeharbor was not registered", report.Lines);
        Assert.False(Directory.Exists(Path.Combine(_root, "distro")));
    }

    [Fact]
    public void The_verb_is_routed_to_the_engine_surface()
    {
        Assert.Contains("--remove-engine", FreeHarbor.Tray.Cli.CommandLine.EngineVerbs);
        Assert.Contains("--remove-engine", FreeHarbor.Tray.Cli.CommandLine.HelpText, StringComparison.Ordinal);
    }

    [Fact]
    public void The_confirmation_states_the_disk_size_and_what_goes_with_it()
    {
        var said = EngineRemoval.Confirmation(EngineRemoval.DiskBytes(new EnginePaths(_root)));

        Assert.Contains("(1 KB)", said, StringComparison.Ordinal);
        Assert.Contains("Every image, container and volume", said, StringComparison.Ordinal);
        Assert.Contains("no undo", said, StringComparison.Ordinal);
    }

    [Fact]
    public void An_absent_disk_is_confirmed_without_a_size() =>
        Assert.DoesNotContain("(", EngineRemoval.Confirmation(EngineRemoval.DiskBytes(new EnginePaths(Path.Combine(_root, "none")))), StringComparison.Ordinal);

    [Fact]
    public void Settings_offers_Remove_engine_through_the_verb_own_stop()
    {
        // One removal: the page runs the stop the verb runs, and the same EngineRemoval.
        var page = File.ReadAllText(RepositoryFile(@"src\FreeHarbor.Tray\Ui\Pages\SettingsPage.xaml.cs"));

        Assert.Contains("Cli.EngineCommand.StopForRemoval();", page, StringComparison.Ordinal);
        Assert.Contains("EngineRemoval.Confirmation(", page, StringComparison.Ordinal);
        Assert.Contains("Window.GetWindow(this)", page, StringComparison.Ordinal);
    }

    private static string RepositoryFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "FreeHarbor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, relative);
    }
}
