using FreeHarbor.Core.Details;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>
/// A whole-file replace that waits out a scanner holding the file it just saw written (DD319).
/// </summary>
/// <remarks>
/// The lock here is a real one, a handle opened with no sharing, because the failure being guarded
/// is the operating system's answer to exactly that and nothing else reproduces it.
/// </remarks>
public sealed class AtomicFileTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"freeharbor-atomic-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        foreach (var file in new[] { _path, _path + ".tmp" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A temp file that outlives the run costs nothing worth failing a suite over.
            }
        }
    }

    [Fact]
    public void A_replace_writes_the_whole_file_and_leaves_no_temporary()
    {
        File.WriteAllText(_path, "old");

        AtomicFile.Replace(_path, "new");

        Assert.Equal("new", File.ReadAllText(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void A_lock_that_lets_go_within_the_retries_is_waited_out()
    {
        File.WriteAllText(_path, "old");
        var held = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None);

        // Let go after one pause and a bit: the first try meets the lock, a later one does not.
        // On a thread of its own and not Task.Run (DD308): CI run 42626319551 had 83 pool items
        // queued, the release ran after every retry, and the test failed on the runner's load.
        var release = new Thread(() =>
        {
            Thread.Sleep(AtomicFile.Pause + TimeSpan.FromMilliseconds(20));
            held.Dispose();
        })
        { IsBackground = true };
        release.Start();

        AtomicFile.Replace(_path, "new");
        release.Join();

        Assert.Equal("new", File.ReadAllText(_path));
    }

    [Fact]
    public void A_lock_that_outlasts_every_retry_is_still_a_failure()
    {
        // The dialog says an edit did not reach the disk, and it must still be able to.
        File.WriteAllText(_path, "old");
        using (new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // A move over a held file is refused as access denied, a write into one as a sharing
            // violation: both are what the store and the project file catch and report.
            var refused = Record.Exception(() => AtomicFile.Replace(_path, "new"));
            Assert.True(
                refused is IOException or UnauthorizedAccessException,
                $"expected the lock to be reported, got {refused?.GetType().Name ?? "nothing"}");
        }

        Assert.Equal("old", File.ReadAllText(_path));
    }
}
