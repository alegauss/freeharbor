using FreeHarbor.Core.Engine;
using FreeHarbor.Core.Settings;
using FreeHarbor.Tray.Cli;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>
/// The engine's API floor, the setting that lowers it and the launch that applies it (DD288).
/// </summary>
[Collection(ConsoleCollection.Name)]
public sealed class EngineApiFloorTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"freeharbor-floor-{Guid.NewGuid():N}");

    private string File => Path.Combine(_directory, "settings.json");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A temp directory that outlives the run costs nothing worth failing a suite over.
        }
    }

    [Theory]
    [InlineData("1.24", "1.24")]
    [InlineData("1.32", "1.32")]
    [InlineData(" 1.40 ", "1.40")]
    public void A_version_from_the_lowest_to_the_default_is_taken(string typed, string kept)
    {
        Assert.True(EngineApiFloor.TryRead(typed, out var version));
        Assert.Equal(kept, version);
    }

    [Theory]
    [InlineData("1.10")] // dockerd refuses to start below 1.24, measured
    [InlineData("1.23")]
    [InlineData("1.41")] // raising the floor is not this setting, and the maximum moves
    [InlineData("1.60")]
    [InlineData("2.0")]
    [InlineData("1.3a")]
    [InlineData("")]
    [InlineData(null)]
    public void A_version_dockerd_could_refuse_is_not(string? typed)
    {
        Assert.False(EngineApiFloor.TryRead(typed, out _));
    }

    [Fact]
    public void An_engine_with_nothing_set_starts_exactly_as_before()
    {
        Assert.Equal(
            $"exec /usr/local/bin/dockerd -H unix://{EngineLifecycle.SocketPath} "
            + $">>{EngineLifecycle.LogPath} 2>&1",
            WslDaemonProcess.LaunchScript(null));
    }

    [Fact]
    public void A_lowered_floor_is_exported_before_the_daemon_is_execd()
    {
        var script = WslDaemonProcess.LaunchScript("1.24");

        Assert.StartsWith("export DOCKER_MIN_API_VERSION=1.24; exec /usr/local/bin/dockerd ", script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_hand_edited_value_dockerd_would_refuse_costs_the_setting_and_not_the_engine()
    {
        // A refused DOCKER_MIN_API_VERSION is a daemon that does not start, so the launch checks it
        // again rather than trusting a file the user can open in Notepad.
        Assert.Equal(WslDaemonProcess.LaunchScript(null), WslDaemonProcess.LaunchScript("1.10"));
    }

    [Fact]
    public void The_verb_stores_the_floor_and_default_takes_it_away()
    {
        using var said = new StringWriter();

        Assert.Equal(0, EngineCommand.MinimumApi("1.24", File, said));
        Assert.Equal("1.24", TraySettings.Read(File).EngineMinimumApi);
        Assert.Contains("1.24  set here", said.ToString(), StringComparison.Ordinal);

        Assert.Equal(0, EngineCommand.MinimumApi("default", File, said));
        Assert.Null(TraySettings.Read(File).EngineMinimumApi);
    }

    [Fact]
    public void The_verb_keeps_the_other_setting_in_the_file()
    {
        new TraySettings { StartWithTheTray = false }.Write(File);

        Assert.Equal(0, EngineCommand.MinimumApi("1.32", File, new StringWriter()));

        Assert.False(TraySettings.Read(File).StartWithTheTray);
    }

    [Fact]
    public void A_version_the_setting_refuses_is_a_usage_error_and_writes_nothing()
    {
        var was = Console.Error;
        try
        {
            Console.SetError(new StringWriter());
            Assert.Equal(2, EngineCommand.MinimumApi("1.10", File, new StringWriter()));
        }
        finally
        {
            Console.SetError(was);
        }

        Assert.False(System.IO.File.Exists(File));
    }

    [Fact]
    public void Status_with_nothing_set_names_the_engines_own_floor()
    {
        using var said = new StringWriter();

        Assert.Equal(0, EngineCommand.MinimumApi("status", File, said));

        Assert.Contains($"{EngineApiFloor.MobyDefault}  the engine's own floor", said.ToString(),
            StringComparison.Ordinal);
        Assert.False(System.IO.File.Exists(File));
    }
}
