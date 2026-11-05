namespace FreeHarbor.Tray.Ui;

/// <summary>One address a project serves, as the detail page's Endpoints grid draws it (DD299).</summary>
/// <param name="Label">What the link is called: its own name, or the port.</param>
/// <param name="Service">The service that serves it.</param>
/// <param name="Address">What the card reads, without the scheme.</param>
/// <param name="Url">Where Open goes, or <see langword="null"/> where it does not speak HTTP.</param>
public sealed record Endpoint(string Label, string Service, string Address, string? Url)
{
    /// <summary>Whether the card offers Open: only an address a browser can show.</summary>
    public bool CanOpen => Url is not null;

    /// <summary>What Copy puts on the clipboard: the whole URL where there is one, else the address.</summary>
    public string CopyText => Url ?? Address;
}

/// <summary>One service that stopped and did not mean to, in the daemon's own words (DD299).</summary>
/// <param name="Row">The container, which the line's Start is addressed to.</param>
/// <param name="Sentence">What the line says.</param>
public sealed record DetailProblem(ContainerRow Row, string Sentence)
{
    /// <summary>What the line's button says: Start, and whom.</summary>
    public string StartLabel => $"Start {Row.Title}";
}

/// <summary>
/// Everything the detail page shows about one project or one standalone container (DD299).
/// </summary>
/// <param name="Key">What the page was opened on: a project's header id or a container's id.</param>
/// <param name="Head">The row the page is about: the project's header, or the container itself.</param>
/// <param name="Subtitle">The line under the description: where it comes from.</param>
/// <param name="Problems">Each service that stopped and did not mean to.</param>
/// <param name="Endpoints">Every link every service serves, including ones folded away elsewhere.</param>
/// <param name="Services">Every service, by name.</param>
/// <remarks>
/// <b>Built from the rows in hand</b> and rebuilt on every redraw, so the page follows the engine's
/// events exactly as the list does and never asks the daemon for anything of its own (L5).
/// </remarks>
public sealed record DetailView(
    string Key,
    ContainerRow Head,
    string Subtitle,
    IReadOnlyList<DetailProblem> Problems,
    IReadOnlyList<Endpoint> Endpoints,
    IReadOnlyList<ContainerRow> Services)
{
    /// <summary>Whether any service needs a failure line.</summary>
    public bool HasProblems => Problems.Count > 0;

    /// <summary>Whether there is any address to list.</summary>
    public bool HasEndpoints => Endpoints.Count > 0;

    /// <summary>The key a row opens the page on: its project for a header or a service, else itself.</summary>
    /// <param name="row">The row whose Details was chosen.</param>
    /// <returns>The key.</returns>
    public static string KeyFor(ContainerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Project is { } project ? ContainerRow.ProjectId(project) : row.Id;
    }

    /// <summary>Build the page for a key, or nothing where what it named is gone.</summary>
    /// <param name="key">From <see cref="KeyFor"/>.</param>
    /// <param name="rows">Every container in hand, dressed.</param>
    /// <returns>The page, or <see langword="null"/>.</returns>
    public static DetailView? For(string key, IReadOnlyList<ContainerRow> rows)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(rows);

        var children = rows
            .Where(row => row.IsContainer
                && row.Project is { } project
                && string.Equals(ContainerRow.ProjectId(project), key, StringComparison.Ordinal))
            .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToList();

        if (children.Count > 0)
        {
            var head = ContainerRow.ProjectHeader(children[0].Project!, children, collapsed: false);
            var files = children[0].ConfigFiles is { } named
                ? " · " + string.Join(", ", named.Split(',').Select(file => System.IO.Path.GetFileName(file.Trim())))
                : "";
            return Build(key, head, $"compose project {children[0].Project}{files}", children);
        }

        var alone = rows.FirstOrDefault(row => row.IsContainer
            && row.Project is null
            && string.Equals(row.Id, key, StringComparison.Ordinal));
        return alone is null
            ? null
            : Build(key, alone, $"standalone container {alone.Name} · {alone.Image}", [alone]);
    }

    private static DetailView Build(
        string key, ContainerRow head, string subtitle, IReadOnlyList<ContainerRow> services)
    {
        // The daemon's own sentence and nothing paraphrased: a refused action's words where the last
        // click failed, otherwise the status line that says how it exited.
        var problems = services
            .Where(service => service.HasFailure || service.Tone == RowTone.Bad)
            .Select(service => new DetailProblem(service, $"{service.Title}: {service.Failure ?? service.Status}"))
            .ToList();

        return new DetailView(key, head, subtitle, problems, [.. services.SelectMany(EndpointsOf)], services);
    }

    /// <summary>
    /// What one service serves: its named links where it has any, else its published ports.
    /// </summary>
    /// <remarks>
    /// The ports and not the links the resolver made from them, because a port knows what it is
    /// inside the container and so whether it speaks HTTP (DD297); the link made from it does not.
    /// </remarks>
    private static IEnumerable<Endpoint> EndpointsOf(ContainerRow service)
    {
        if (service.Details is { } details && details.LinksSource is not ("published ports" or "none" or ""))
        {
            return details.Links.Select(link => new Endpoint(link.Name, service.Title, Bare(link.Url), link.Url));
        }

        return service.Ports
            .Where(port => !port.IsText)
            .Select(port => new Endpoint(
                port.Text, service.Title, port.Url is { } url ? Bare(url) : port.Address!, port.Url));
    }

    private static string Bare(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? url["http://".Length..]
        : url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? url["https://".Length..]
        : url;
}
