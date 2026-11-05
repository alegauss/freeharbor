namespace FreeHarbor.Core.Details;

/// <summary>The fields a project file can set for a project or one of its services (DD295).</summary>
/// <param name="Title">The title, or <see langword="null"/>.</param>
/// <param name="Description">The line of what it is for, or <see langword="null"/>.</param>
/// <param name="Icon">
/// A bundled icon's name or an absolute path to a picture, already resolved against the folder the
/// file sits in; or <see langword="null"/>.
/// </param>
/// <param name="Links">
/// The links it serves, by name, which replace what labels and ports would give (DD296); or
/// <see langword="null"/>, which leaves those to answer.
/// </param>
public sealed record DetailFields(
    string? Title, string? Description, string? Icon, IReadOnlyList<DetailLink>? Links = null)
{
    /// <summary>Whether this sets nothing at all.</summary>
    public bool IsEmpty => Title is null && Description is null && Icon is null && !HasLinks;

    /// <summary>Whether it names any link.</summary>
    public bool HasLinks => Links is { Count: > 0 };

    /// <summary>Equal field for field, the links by their contents and order.</summary>
    /// <param name="other">The other fields.</param>
    /// <returns>Whether they say the same.</returns>
    /// <remarks>
    /// Written out because a list compares by reference, and a record read back from the store would
    /// then never equal the one that was saved.
    /// </remarks>
    public bool Equals(DetailFields? other) =>
        other is not null && Title == other.Title && Description == other.Description && Icon == other.Icon
        && (Links ?? []).SequenceEqual(other.Links ?? []);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Title, Description, Icon, Links?.Count ?? 0);
}

/// <summary>What one project file says.</summary>
/// <param name="Project">The project's own fields.</param>
/// <param name="Services">Each service's fields, by the service's compose name.</param>
/// <param name="Source">Which file said it, as the edit dialog names it (DD296).</param>
public sealed record ProjectFileDetails(
    DetailFields Project, IReadOnlyDictionary<string, DetailFields> Services,
    string Source = ProjectFile.Folder);

/// <summary>
/// Reads <c>.freeharbor/project.yaml</c>, the project file FreeHarbor owns (DD295).
/// </summary>
/// <remarks>
/// <b>A reader for this file's shape and nothing wider.</b> ComposeUp refuses a YAML parser for a
/// compose file because the CLI is the authority on what somebody's file means. This file is ours and
/// its shape is fixed: three scalar keys, and a <c>services</c> map of the same three per service
/// (docs/specs/DD294-containers-redesign.md, section 8). So this reads maps of plain, quoted or
/// commented scalars by indentation. A list, an anchor or a multi-line scalar is not part of the
/// format; the line holding it is skipped rather than guessed at.
///
/// <para><b>Absence is ordinary.</b> The folder compose recorded can be gone, moved or a WSL path
/// Windows cannot open, so every failure answers <see langword="null"/> and the labels speak
/// instead.</para>
///
/// <para><b>Read, never cached past a change.</b> <see cref="For"/> keeps what it read beside the
/// file's write time and reads again when that moves, so an edit shows on the next refresh with no
/// <c>compose up</c>, and an unchanged file costs one stat per refresh.</para>
/// </remarks>
public static class ProjectFile
{
    /// <summary>The folder the file lives in, inside the project's own.</summary>
    public const string Folder = ".freeharbor";

    /// <summary>The file's name.</summary>
    public const string FileName = "project.yaml";

    /// <summary>The compose label naming the folder compose ran in.</summary>
    public const string WorkingDirLabel = "com.docker.compose.project.working_dir";

    private static readonly Dictionary<string, (DateTime Written, ProjectFileDetails? Details)> Seen =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What the project file in a folder says, read again only when it changed.</summary>
    /// <param name="projectFolder">The folder compose recorded as <c>working_dir</c>.</param>
    /// <returns>The details, or <see langword="null"/> where there is no readable file.</returns>
    public static ProjectFileDetails? For(string? projectFolder)
    {
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            return null;
        }

        string path;
        DateTime written;
        try
        {
            if (!Path.IsPathFullyQualified(projectFolder))
            {
                return null;
            }

            path = Path.Combine(projectFolder, Folder, FileName);
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return null;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }

        lock (Seen)
        {
            if (Seen.TryGetValue(path, out var known) && known.Written == written)
            {
                return known.Details;
            }
        }

        var details = Read(path);
        lock (Seen)
        {
            Seen[path] = (written, details);
        }

        return details;
    }

    /// <summary>Read one project file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>What it says, or <see langword="null"/> where it cannot be read.</returns>
    public static ProjectFileDetails? Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
            return Parse(File.ReadAllText(path), folder);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>Read a project file's text.</summary>
    /// <param name="text">The file's contents.</param>
    /// <param name="folder">The <c>.freeharbor</c> folder, which relative icon paths are under.</param>
    /// <returns>What it says. Never null: an empty file sets nothing.</returns>
    public static ProjectFileDetails Parse(string text, string folder)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(folder);

        var root = Tree(text);
        var services = new Dictionary<string, DetailFields>(StringComparer.Ordinal);
        if (root.TryGetValue("services", out var node) && node is Dictionary<string, object> map)
        {
            foreach (var (name, value) in map)
            {
                if (value is Dictionary<string, object> service && Fields(service, folder) is { IsEmpty: false } fields)
                {
                    services[name] = fields;
                }
            }
        }

        return new ProjectFileDetails(Fields(root, folder), services);
    }

    private static DetailFields Fields(Dictionary<string, object> map, string folder)
    {
        string? Scalar(string key) =>
            map.TryGetValue(key, out var value) && value is string text && text.Length > 0 ? text : null;

        IReadOnlyList<DetailLink>? links = null;
        if (map.TryGetValue("links", out var node) && node is Dictionary<string, object> named)
        {
            links = [.. named
                .Where(pair => pair.Value is string url && url.Length > 0)
                .Select(pair => new DetailLink(pair.Key, (string)pair.Value))];
        }

        return new DetailFields(
            Scalar("title"), Scalar("description"), IconPath(Scalar("icon"), folder), links is { Count: > 0 } ? links : null);
    }

    /// <summary>An icon as a file gives it: a path resolved against the file's folder, or a name.</summary>
    /// <param name="icon">What the file says, or <see langword="null"/>.</param>
    /// <param name="folder">The folder a relative path is under.</param>
    /// <returns>An absolute path, a bundled icon's name or a URL as given, or null.</returns>
    /// <remarks>
    /// A bare word stays a name for the resolver to look up. Anything with a separator or an
    /// extension is a path. A URL is left alone, and the resolver skips it, because nothing remote is
    /// fetched.
    /// </remarks>
    public static string? IconPath(string? icon, string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (icon is null || Uri.TryCreate(icon, UriKind.Absolute, out _)
            || !(icon.Contains('/', StringComparison.Ordinal) || icon.Contains('\\', StringComparison.Ordinal)
                || Path.HasExtension(icon)))
        {
            return icon;
        }

        try
        {
            return Path.GetFullPath(Path.Combine(folder, icon));
        }
        catch (Exception failure) when (failure is ArgumentException or NotSupportedException
            or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>Write a project file in the shape <see cref="Parse"/> reads (DD296).</summary>
    /// <param name="projectFolder">The project's folder; the file goes in its <c>.freeharbor</c>.</param>
    /// <param name="details">What to write. Icon paths under <c>.freeharbor</c> are written relative.</param>
    /// <returns>The file written, or <see langword="null"/> where the folder could not be written.</returns>
    /// <remarks>
    /// Every value is double-quoted, so a title holding a colon or a hash reads back as itself and
    /// no value is ever taken for a map. The file is ours, so it is rewritten whole: there is no
    /// formatting of somebody else's to keep, which is the opposite of the compose file's case.
    /// </remarks>
    public static string? Write(string projectFolder, ProjectFileDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentNullException.ThrowIfNull(details);

        var folder = Path.Combine(projectFolder, Folder);
        var text = new System.Text.StringBuilder();
        text.AppendLine("# Written by FreeHarbor. Titles, descriptions and icons for this compose project;")
            .AppendLine("# it overrides the compose file and can be committed for the whole team.")
            .Append(Body(details, folder, indent: ""));

        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, FileName);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, text.ToString());
            File.Move(temporary, path, overwrite: true);
            return path;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>The keys of a project file, at an indent: the file's own body, or an <c>x-freeharbor</c> block's.</summary>
    /// <param name="details">What to write.</param>
    /// <param name="folder">The folder icon paths are written relative to.</param>
    /// <param name="indent">The indent of the top-level keys.</param>
    /// <returns>The lines, each ending in a newline.</returns>
    internal static string Body(ProjectFileDetails details, string folder, string indent)
    {
        var text = new System.Text.StringBuilder();
        Lines(text, details.Project, folder, indent);
        var services = details.Services
            .Where(pair => !pair.Value.IsEmpty)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();
        if (services.Count > 0)
        {
            text.Append(indent).AppendLine("services:");
            foreach (var (name, fields) in services)
            {
                text.Append(indent).Append("  ").Append(Quoted(name)).AppendLine(":");
                Lines(text, fields, folder, indent + "    ");
            }
        }

        return text.ToString();
    }

    private static void Lines(System.Text.StringBuilder text, DetailFields fields, string folder, string indent)
    {
        void Line(string key, string? value)
        {
            if (value is not null)
            {
                text.Append(indent).Append(key).Append(": ").AppendLine(Quoted(value));
            }
        }

        Line("title", fields.Title);
        Line("description", fields.Description);

        // Relative where the picture is inside .freeharbor, so the file reads the same on a
        // teammate's machine with the project checked out somewhere else.
        var icon = fields.Icon;
        if (icon is not null && Path.IsPathFullyQualified(icon))
        {
            var relative = Path.GetRelativePath(folder, icon);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            {
                icon = relative.Replace('\\', '/');
            }
        }

        Line("icon", icon);

        if (fields.HasLinks)
        {
            text.Append(indent).AppendLine("links:");
            foreach (var link in fields.Links!)
            {
                text.Append(indent).Append("  ").Append(Quoted(link.Name)).Append(": ").AppendLine(Quoted(link.Url));
            }
        }
    }

    private static string Quoted(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>Two sources, field by field: the first one's value wherever it has one.</summary>
    /// <param name="first">The source read first, <c>.freeharbor</c> in the window.</param>
    /// <param name="second">The one under it, <c>x-freeharbor</c>.</param>
    /// <returns>The merged details, or <see langword="null"/> where neither says anything.</returns>
    /// <remarks>
    /// By field and not by file, so a <c>.freeharbor</c> that only renames a project keeps the
    /// description the compose file's block gives it. That is the override a
    /// <c>compose.override.yaml</c> makes, which is the model the format follows.
    /// </remarks>
    public static ProjectFileDetails? Merge(ProjectFileDetails? first, ProjectFileDetails? second)
    {
        if (first is null || second is null)
        {
            return first ?? second;
        }

        static DetailFields Over(DetailFields top, DetailFields? under) => under is null
            ? top
            : new DetailFields(
                top.Title ?? under.Title, top.Description ?? under.Description, top.Icon ?? under.Icon,
                top.HasLinks ? top.Links : under.Links);

        var services = new Dictionary<string, DetailFields>(StringComparer.Ordinal);
        foreach (var (name, fields) in second.Services)
        {
            services[name] = fields;
        }

        foreach (var (name, fields) in first.Services)
        {
            services[name] = Over(fields, second.Services.GetValueOrDefault(name));
        }

        return new ProjectFileDetails(Over(first.Project, second.Project), services);
    }

    /// <summary>Indented maps of scalars, the whole of what the format uses.</summary>
    private static Dictionary<string, object> Tree(string text)
    {
        var root = new Dictionary<string, object>(StringComparer.Ordinal);
        var stack = new List<(int Indent, Dictionary<string, object> Map)> { (-1, root) };

        foreach (var raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var content = line.TrimStart(' ');
            if (content.Length == 0 || content[0] == '#' || content.StartsWith('-') || content.Contains('\t'))
            {
                continue;
            }

            var colon = content.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            var indent = line.Length - content.Length;
            while (stack.Count > 1 && stack[^1].Indent >= indent)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            var key = Unquote(content[..colon].Trim());
            var rest = WithoutComment(content[(colon + 1)..]).Trim();
            var parent = stack[^1].Map;
            if (rest.Length == 0)
            {
                var child = new Dictionary<string, object>(StringComparer.Ordinal);
                parent[key] = child;
                stack.Add((indent, child));
            }
            else
            {
                parent[key] = Unquote(rest);
            }
        }

        return root;
    }

    private static string WithoutComment(string value)
    {
        var quote = '\0';
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (quote == '\0' && (c == '"' || c == '\''))
            {
                quote = c;
            }
            else if (c == quote)
            {
                quote = '\0';
            }
            else if (quote == '\0' && c == '#' && (i == 0 || value[i - 1] == ' '))
            {
                return value[..i];
            }
        }

        return value;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return value;
    }
}
