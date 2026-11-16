namespace FreeHarbor.Core.Engine;

/// <summary>What removing the engine did.</summary>
/// <param name="Lines">One line per step, in order, ready to print.</param>
/// <param name="Freed">Bytes the removed folders held.</param>
/// <param name="Succeeded">Whether the distribution is gone and every folder with it.</param>
public sealed record EngineRemovalReport(IReadOnlyList<string> Lines, long Freed, bool Succeeded);

/// <summary>
/// Take the engine off this machine: the distribution, its disk and what the provision placed (DD317).
/// </summary>
/// <remarks>
/// <para>The Inno uninstaller does this in its own Pascal (DD14, DD121). Removing an MSIX package
/// runs no code at all, and DD309 measured what that leaves: the distribution and its disk, the
/// whole root, and a Run value naming an exe that is gone. So the removal is a verb the user runs
/// first, and this is that verb's work.</para>
///
/// <para><b>Unregister before deleting, and delete nothing if it fails.</b> A folder deleted from
/// under a distribution WSL still has registered leaves an entry <c>wsl --list</c> shows broken,
/// which is worse than the disk it was meant to free. Terminate first, per DD209, because an
/// open virtual disk is a file that survives its own deletion.</para>
///
/// <para>Settings and details stay unless asked, because they are the user's choices rather
/// than the engine, and a reinstall that remembers them is the common reason to remove.</para>
/// </remarks>
public sealed class EngineRemoval
{
    /// <summary>The folders the provision fills, removed with the engine.</summary>
    public static readonly IReadOnlyList<string> EngineFolders = ["distro", "downloads", "cli", "cli-plugins"];

    /// <summary>The user's own files, removed only when everything is asked for.</summary>
    public static readonly IReadOnlyList<string> UserFiles = ["settings.json", "details.json"];

    /// <summary>How many times a folder WSL still holds open is tried again.</summary>
    /// <remarks>
    /// WSL keeps a virtual disk open for a moment after the unregister returns, which the Inno
    /// uninstaller's own message already names, so a first refusal is expected and passes.
    /// </remarks>
    private const int DeleteAttempts = 5;

    private readonly IWsl _wsl;
    private readonly EnginePaths _paths;
    private readonly Action<TimeSpan> _wait;

    /// <summary>Construct a removal.</summary>
    /// <param name="wsl">The launcher.</param>
    /// <param name="paths">The install this removes the engine of.</param>
    /// <param name="wait">How a retry waits; a test passes one that does not.</param>
    public EngineRemoval(IWsl wsl, EnginePaths paths, Action<TimeSpan>? wait = null)
    {
        ArgumentNullException.ThrowIfNull(wsl);
        ArgumentNullException.ThrowIfNull(paths);
        _wsl = wsl;
        _paths = paths;
        _wait = wait ?? Thread.Sleep;
    }

    /// <summary>Remove the engine.</summary>
    /// <param name="all">Also remove the user's settings and details.</param>
    /// <returns>What was done, and whether all of it worked.</returns>
    public EngineRemovalReport Remove(bool all)
    {
        var lines = new List<string>();
        var name = _paths.DistributionName;

        _wsl.Run(WslBudget.Work, "--terminate", name);

        var unregistered = _wsl.Run(WslBudget.Work, "--unregister", name);
        if (unregistered.Succeeded)
        {
            lines.Add($"unregistered {name}");
        }
        else if (WslFailure.SaysNoSuchDistribution(unregistered.Output))
        {
            lines.Add($"{name} was not registered");
        }
        else
        {
            lines.Add($"could not unregister {name}: {unregistered.Detail()}");
            lines.Add("nothing was deleted, so the distribution and its disk are as they were");
            return new EngineRemovalReport(lines, 0, Succeeded: false);
        }

        var targets = EngineFolders.Select(folder => Path.Combine(_paths.Root, folder))
            .Concat(all ? UserFiles.Select(file => Path.Combine(_paths.Root, file)) : [])
            .ToList();

        long freed = 0;
        var succeeded = true;
        foreach (var target in targets)
        {
            var size = SizeOf(target);
            if (size is null)
            {
                continue;
            }

            if (Delete(target) is { } failure)
            {
                lines.Add($"could not delete {target}: {failure}");
                succeeded = false;
                continue;
            }

            freed += size.Value;
            lines.Add($"deleted {target}");
        }

        lines.Add($"{Spell(freed)} came back");
        return new EngineRemovalReport(lines, freed, succeeded);
    }

    /// <summary>What a confirmation says before the engine is removed (DD317).</summary>
    /// <param name="diskBytes">What the distribution's folder holds, or <see langword="null"/>.</param>
    /// <returns>The paragraph a person consents to.</returns>
    public static string Confirmation(long? diskBytes)
    {
        var size = diskBytes is { } bytes ? $" ({Spell(bytes)})" : "";
        return $"This unregisters the {EnginePaths.CurrentDistribution} distribution and deletes its "
            + $"disk{size}. Every image, container and volume goes with it, and there is no undo.\n\n"
            + "Your settings stay. Removing the engine first is what lets the app be uninstalled "
            + "without leaving that disk behind.\n\nRemove the engine?";
    }

    /// <summary>What the distribution's folder holds now, or <see langword="null"/> where it is absent.</summary>
    /// <param name="paths">The install to measure.</param>
    /// <returns>The bytes.</returns>
    public static long? DiskBytes(EnginePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        try
        {
            return SizeOf(paths.Distribution);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>How many bytes a file or folder holds, or <see langword="null"/> where it is absent.</summary>
    private static long? SizeOf(string path)
    {
        if (File.Exists(path))
        {
            return new FileInfo(path).Length;
        }

        if (!Directory.Exists(path))
        {
            return null;
        }

        return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(file => file.Length);
    }

    /// <summary>Delete a file or folder, retrying while something still holds it.</summary>
    /// <returns>Why it could not be deleted, or <see langword="null"/>.</returns>
    private string? Delete(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else
                {
                    Directory.Delete(path, recursive: true);
                }

                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == DeleteAttempts)
                {
                    return $"{exception.Message} (if WSL still holds it, wsl --shutdown and delete it by hand)";
                }

                _wait(TimeSpan.FromSeconds(1));
            }
        }
    }

    /// <summary>A size, in the unit a reader would say.</summary>
    internal static string Spell(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0} MB",
        _ => $"{bytes / 1024.0:0} KB",
    };
}
