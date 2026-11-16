using System.Text;
using FreeHarbor.Core.Engine;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>Placing a file over a copy that is running (DD361).</summary>
public sealed class InUseFileTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"freeharbor-inuse-{Guid.NewGuid():N}");

    public InUseFileTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A temp directory; the next run uses its own.
        }
    }

    private string Target => Path.Combine(_directory, "docker.exe");

    private static Func<Stream> Bytes(string text) => () => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void A_missing_target_is_written()
    {
        Assert.True(InUseFile.Place(Bytes("v2"), 2, Target));
        Assert.Equal("v2", File.ReadAllText(Target));
    }

    [Fact]
    public void The_same_bytes_are_left_alone_and_nothing_is_written()
    {
        File.WriteAllText(Target, "v2");
        var before = File.GetLastWriteTimeUtc(Target);

        Assert.False(InUseFile.Place(Bytes("v2"), 2, Target));
        Assert.Equal(before, File.GetLastWriteTimeUtc(Target));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public void Different_bytes_replace_the_target()
    {
        File.WriteAllText(Target, "v1");

        Assert.True(InUseFile.Place(Bytes("v2"), 2, Target));
        Assert.Equal("v2", File.ReadAllText(Target));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public void A_target_held_the_way_a_running_image_is_held_is_replaced_by_moving_it_aside()
    {
        // A running image refuses an overwrite and allows a rename. Held here sharing read and
        // delete and never write, which refuses the overwrite the same way. It is weaker on one
        // point: a mapped image also refuses the delete, so a real running copy stays aside until
        // its process is gone (measured against PING.EXE on 16 November), where this one may be
        // swept at once.
        File.WriteAllText(Target, "v1");
        using (var running = new FileStream(
            Target, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            Assert.Throws<IOException>(() => File.WriteAllText(Target, "overwrite"));

            Assert.True(InUseFile.Place(Bytes("v2"), 2, Target));
            Assert.Equal("v2", File.ReadAllText(Target));
        }

        // Once nothing holds it, nothing is left aside.
        InUseFile.Sweep(Target);
        Assert.Empty(Directory.GetFiles(_directory, "docker.exe.*" + InUseFile.AsideSuffix));
    }

    [Fact]
    public void Nothing_named_new_is_left_behind()
    {
        File.WriteAllText(Target, "v1");

        InUseFile.Place(Bytes("v2"), null, Target);

        Assert.Empty(Directory.GetFiles(_directory, "*.new"));
    }
}
