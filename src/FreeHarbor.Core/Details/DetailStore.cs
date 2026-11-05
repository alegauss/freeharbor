using System.Text.Json;

namespace FreeHarbor.Core.Details;

/// <summary>
/// Details saved in FreeHarbor on this machine, the default place an edit lands (DD296).
/// </summary>
/// <remarks>
/// <b>Personal, and first.</b> Most containers come from somebody else's compose file or a one-off
/// <c>docker run</c>, so the place an edit can always go is this tool's own file, beside the window's
/// memory and the settings. Nothing in a project changes and nobody else sees it. It is read before
/// every other source: an edit made here is somebody saying what they want to see on their own
/// screen, and a label written by whoever built the image does not outrank that.
///
/// <para><b>Keyed to survive a recreate.</b> A compose container is saved under its project and
/// service, never its id, so <c>compose up</c> recreating it keeps the details. A standalone
/// container has only its name, which a new <c>docker run</c> may not reuse; the dialog says so.</para>
///
/// <para><b>Every failure reads as empty</b>, the way <c>TraySettings</c> reads: a truncated file is
/// not a reason to draw the list differently from how the labels would.</para>
/// </remarks>
public sealed class DetailStore
{
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    private readonly string _path;
    private Dictionary<string, DetailFields> _entries;

    /// <summary>Open the store over a file, reading what it holds.</summary>
    /// <param name="path">The file, which need not exist yet.</param>
    public DetailStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _entries = ReadAll(path);
    }

    /// <summary>The key a compose service is saved under.</summary>
    /// <param name="project">The compose project.</param>
    /// <param name="service">The service.</param>
    /// <returns>The key, such as <c>storefront/db</c>.</returns>
    public static string ServiceKey(string project, string service) => $"{project}/{service}";

    /// <summary>The key a compose project as a whole is saved under.</summary>
    /// <param name="project">The compose project.</param>
    /// <returns>The key, the project's name.</returns>
    public static string ProjectKey(string project) => project;

    /// <summary>The key a container outside compose is saved under: its name.</summary>
    /// <param name="name">The container's name.</param>
    /// <returns>The key, such as <c>container:mailpit</c>.</returns>
    public static string ContainerKey(string name) => $"container:{name}";

    /// <summary>What is saved under a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The fields, or <see langword="null"/> where nothing is.</returns>
    public DetailFields? For(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_entries)
        {
            return _entries.GetValueOrDefault(key);
        }
    }

    /// <summary>Save under a key, or remove what is there when the fields set nothing.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fields">What to keep.</param>
    /// <returns>Whether it reached the disk; on false the edit holds until the window closes.</returns>
    public bool Save(string key, DetailFields fields)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(fields);

        Dictionary<string, DetailFields> snapshot;
        lock (_entries)
        {
            if (fields.IsEmpty)
            {
                _ = _entries.Remove(key);
            }
            else
            {
                _entries[key] = fields;
            }

            snapshot = new Dictionary<string, DetailFields>(_entries, StringComparer.Ordinal);
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);

            // DD319: through a helper that waits out a scanner holding the file it just saw written.
            AtomicFile.Replace(_path, JsonSerializer.Serialize(snapshot, Layout));
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
            or NotSupportedException)
        {
            return false;
        }
    }

    private static Dictionary<string, DetailFields> ReadAll(string path)
    {
        try
        {
            if (File.Exists(path)
                && JsonSerializer.Deserialize<Dictionary<string, DetailFields>>(File.ReadAllText(path))
                    is { } read)
            {
                return new Dictionary<string, DetailFields>(
                    read.Where(pair => pair.Value is not null), StringComparer.Ordinal);
            }
        }
        catch (Exception failure) when (failure is IOException or JsonException
            or UnauthorizedAccessException or NotSupportedException)
        {
        }

        return new Dictionary<string, DetailFields>(StringComparer.Ordinal);
    }
}
