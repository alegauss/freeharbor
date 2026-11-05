using System.Text.Json;
using FreeHarbor.Core.Agent;

namespace FreeHarbor.Core.Details;

/// <summary>
/// The <c>x-freeharbor</c> block in a compose file, read through compose itself (DD295).
/// </summary>
/// <remarks>
/// <b>Compose reads the compose file, not this.</b> The block sits in somebody's own file, and the rule
/// ComposeUp states holds here: the CLI is the authority on what a compose project says, so this asks
/// <c>compose config --format json</c> for every file the project was brought up with, in compose's
/// order, and reads the block out of the merged answer. The spec reserves <c>x-</c> keys for
/// extensions, compose keeps them in that answer, and the engine never sees them.
///
/// <para><b>Half a second, so never on the UI thread.</b> Measured at 0.47 s for a one-service
/// project. <see cref="For"/> therefore answers from what it already has and reads in the background
/// when the files changed since, raising <see cref="Changed"/> when the new answer is in, so the
/// window redraws once with it instead of waiting on a subprocess in every refresh.</para>
/// </remarks>
public sealed class ComposeExtension
{
    /// <summary>The top-level key the block lives under.</summary>
    public const string Key = "x-freeharbor";

    /// <summary>The compose label listing the files a project was brought up with.</summary>
    public const string ConfigFilesLabel = "com.docker.compose.project.config_files";

    private readonly IComposeCli _cli;
    private readonly Dictionary<string, Entry> _seen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Construct over a compose CLI.</summary>
    /// <param name="cli">The CLI that reads the files, which is this install's own in the window.</param>
    public ComposeExtension(IComposeCli cli)
    {
        ArgumentNullException.ThrowIfNull(cli);
        _cli = cli;
    }

    /// <summary>Raised on a background thread when a read finished with a different answer.</summary>
    public event EventHandler? Changed;

    /// <summary>What the block says for a project, as far as is known now.</summary>
    /// <param name="workingDir">The folder compose ran in, from <see cref="ProjectFile.WorkingDirLabel"/>.</param>
    /// <param name="configFiles">The files, comma-separated, from <see cref="ConfigFilesLabel"/>.</param>
    /// <returns>
    /// The last answer read, or <see langword="null"/> where none is in yet or the files say nothing.
    /// Never waits on compose.
    /// </returns>
    public ProjectFileDetails? For(string? workingDir, string? configFiles)
    {
        var files = Files(configFiles);
        if (files.Count == 0)
        {
            return null;
        }

        var key = string.Join('|', files);
        var stamp = Stamp(files);
        if (stamp is null)
        {
            return null;
        }

        lock (_seen)
        {
            if (_seen.TryGetValue(key, out var entry) && (entry.Stamp == stamp || entry.Reading))
            {
                return entry.Details;
            }

            _seen[key] = new Entry(entry?.Stamp, entry?.Details, Reading: true);
        }

        var folder = Directory.Exists(workingDir) ? workingDir! : Path.GetDirectoryName(files[0])!;
        _ = Task.Run(() =>
        {
            var details = Read(folder, files);
            bool moved;
            lock (_seen)
            {
                moved = !_seen.TryGetValue(key, out var was) || was.Details != details;
                _seen[key] = new Entry(stamp, details, Reading: false);
            }

            if (moved)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        });

        lock (_seen)
        {
            return _seen[key].Details;
        }
    }

    /// <summary>Read the block now, waiting on compose. For tests and for a caller off the UI thread.</summary>
    /// <param name="folder">Where compose runs, which relative icon paths are under.</param>
    /// <param name="files">The project's files, in compose's order.</param>
    /// <returns>What the block says, or <see langword="null"/> where compose refused or it is absent.</returns>
    public ProjectFileDetails? Read(string folder, IReadOnlyList<string> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(files);

        var result = _cli.Run(folder, ComposeUp.ConfigArguments(files));
        return result.Succeeded ? Parse(result.Output, folder) : null;
    }

    /// <summary>Read the block out of what <c>compose config --format json</c> wrote.</summary>
    /// <param name="json">Compose's answer.</param>
    /// <param name="folder">The project's folder, which a relative icon path is under.</param>
    /// <returns>What the block says, or <see langword="null"/> where there is none.</returns>
    public static ProjectFileDetails? Parse(string json, string folder)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(folder);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(Key, out var block)
                || block.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var services = new Dictionary<string, DetailFields>(StringComparer.Ordinal);
            if (block.TryGetProperty("services", out var map) && map.ValueKind == JsonValueKind.Object)
            {
                foreach (var service in map.EnumerateObject())
                {
                    if (service.Value.ValueKind == JsonValueKind.Object
                        && Fields(service.Value, folder) is { IsEmpty: false } fields)
                    {
                        services[service.Name] = fields;
                    }
                }
            }

            return new ProjectFileDetails(Fields(block, folder), services, Key);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DetailFields Fields(JsonElement element, string folder)
    {
        string? Scalar(string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
                ? text
                : null;

        return new DetailFields(Scalar("title"), Scalar("description"), ProjectFile.IconPath(Scalar("icon"), folder));
    }

    private static List<string> Files(string? configFiles) =>
        string.IsNullOrWhiteSpace(configFiles)
            ? []
            : [.. configFiles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(Path.IsPathFullyQualified)];

    /// <summary>The newest write time across the files, or null where one cannot be read.</summary>
    private static DateTime? Stamp(IReadOnlyList<string> files)
    {
        var newest = DateTime.MinValue;
        foreach (var file in files)
        {
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists)
                {
                    return null;
                }

                newest = info.LastWriteTimeUtc > newest ? info.LastWriteTimeUtc : newest;
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException or System.Security.SecurityException)
            {
                return null;
            }
        }

        return newest;
    }

    private sealed record Entry(DateTime? Stamp, ProjectFileDetails? Details, bool Reading);
}
