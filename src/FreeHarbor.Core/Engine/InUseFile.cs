using System.Security.Cryptography;

namespace FreeHarbor.Core.Engine;

/// <summary>
/// Puts an executable in place even while the copy it replaces is running (DD361).
/// </summary>
/// <remarks>
/// <para>Measured on 16 November 2026: a reinstall stopped at PlaceCli with "being used by another
/// process", twice in a row. VS Code's container extension runs <c>docker context ls</c> through the
/// shim every second or two, so <c>cli\docker.exe</c> was almost always running, and nothing a user
/// could see said so. Twenty separate runs in thirty seconds.</para>
///
/// <para><b>Windows will not overwrite a running image and will rename one.</b> Measured the same
/// day against a copy of PING.EXE: the overwrite failed, the rename worked, a new file took the old
/// name, and the renamed one could be deleted only once the process had gone. So the old copy is
/// moved aside, the new one takes its name, and what is left aside is swept on the next run.</para>
///
/// <para><b>Same bytes, no write at all.</b> A reinstall of the same version is the common case,
/// and it then touches nothing a running process holds.</para>
/// </remarks>
public static class InUseFile
{
    /// <summary>What a copy moved aside is named after, so a later run can find and sweep it.</summary>
    public const string AsideSuffix = ".old";

    /// <summary>Put the content <paramref name="open"/> yields at <paramref name="target"/>.</summary>
    /// <param name="open">Opens the new content. Called once to compare and once to write.</param>
    /// <param name="length">The new content's length, or null where it is not known up front.</param>
    /// <param name="target">Where it goes.</param>
    /// <returns><see langword="true"/> where a file was written; false where it already matched.</returns>
    public static bool Place(Func<Stream> open, long? length, string target)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        Sweep(target);

        if (File.Exists(target) && Same(open, length, target))
        {
            return false;
        }

        var fresh = target + ".new";
        using (var source = open())
        using (var written = new FileStream(fresh, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            source.CopyTo(written);
        }

        if (!File.Exists(target))
        {
            File.Move(fresh, target);
            return true;
        }

        var aside = $"{target}.{Guid.NewGuid():N}{AsideSuffix}";
        File.Move(target, aside);
        try
        {
            File.Move(fresh, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Put back exactly what was there. A target with no file at all is the one outcome
            // worse than the failure being reported.
            File.Move(aside, target);
            throw;
        }

        Sweep(target);
        return true;
    }

    /// <summary>Delete what earlier runs moved aside, leaving whatever is still running.</summary>
    /// <param name="target">The file whose old copies are swept.</param>
    public static void Sweep(string target)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(target))!;
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var old in Directory.EnumerateFiles(
            directory, Path.GetFileName(target) + ".*" + AsideSuffix))
        {
            try
            {
                File.Delete(old);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                // Still running. The next run gets it.
            }
        }
    }

    private static bool Same(Func<Stream> open, long? length, string target)
    {
        if (length is { } expected && new FileInfo(target).Length != expected)
        {
            return false;
        }

        using var source = open();
        using var present = new FileStream(
            target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(present));
    }
}
