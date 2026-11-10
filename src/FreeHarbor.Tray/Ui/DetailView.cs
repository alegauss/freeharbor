using FreeHarbor.Core.Api;

namespace FreeHarbor.Tray.Ui;

/// <summary>One line of a service's recent output (DD299).</summary>
/// <param name="Text">The line, without its ending.</param>
/// <param name="IsError">Whether it came on stderr, which the pane draws in the danger colour.</param>
public sealed record OutputLine(string Text, bool IsError);

/// <summary>One address a project serves, as the detail page's Endpoints grid draws it (DD299).</summary>
/// <param name="Label">What the link is called: its own name, or the port.</param>
/// <param name="Service">The service that serves it.</param>
/// <param name="Address">What the card reads, without the scheme.</param>
/// <param name="Url">Where Open goes, or <see langword="null"/> where it does not speak HTTP.</param>
public sealed record Endpoint(string Label, string Service, string Address, string? Url)
{
    /// <summary>Whether its service's <c>freeharbor.pinned</c> label names it (DD298).</summary>
    public bool IsPinned { get; init; }

    /// <summary>Whether the card offers Open: only an address a browser can show.</summary>
    public bool CanOpen => Url is not null;

    /// <summary>Whether the address can only be copied: the complement, spelled for markup.</summary>
    public bool CopiesOnly => Url is null;

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

/// <summary>One warning on a project card: one kind of failure, however many services it hit (DD337).</summary>
/// <param name="Sentence">What the card says, in plain words: <c>22 services failed</c>.</param>
/// <param name="Evidence">The tooltip: what the kind means, and each service with the daemon's own words.</param>
public sealed record CardAlert(string Sentence, string Evidence);

/// <summary>
/// What a non-zero exit means to someone watching, rather than to someone who knows signal numbers
/// (DD337).
/// </summary>
/// <param name="Singular">The verb after one service's name: <c>was killed</c>.</param>
/// <param name="Plural">The verb after several: <c>were killed</c>.</param>
/// <param name="Explains">The tooltip's first line, which keeps the code for whoever wants it.</param>
public sealed record StopReason(string Singular, string Plural, string Explains)
{
    /// <summary>Read an exit code.</summary>
    /// <param name="code">The code from the status line, or nothing where it had none.</param>
    /// <returns>The reason.</returns>
    public static StopReason Of(int? code) => code switch
    {
        137 => new("was killed", "were killed",
            "Killed (exit 137): usually out of memory, sometimes a stop that ran out of its grace period."),
        143 => new("was shut down", "were shut down",
            "Shut down (exit 143): it was asked to stop, by docker stop or by the engine stopping."),
        130 => new("was interrupted", "were interrupted", "Interrupted (exit 130): Ctrl+C or SIGINT."),
        null => new("stopped unexpectedly", "stopped unexpectedly", "Stopped without an exit code."),
        _ => new("failed", "failed",
            "Failed (non-zero exit): it stopped with an error. Its log is the next thing to read."),
    };
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

    /// <summary>
    /// The links a card draws beside its Open button (DD300): every endpoint but the one Open follows.
    /// </summary>
    public IReadOnlyList<Endpoint> SecondaryLinks =>
        [.. Endpoints.Where(endpoint => !string.Equals(endpoint.Url, Head.OpenLink?.Url, StringComparison.Ordinal))];

    /// <summary>How many services make a project large, the spec's threshold for its card (DD298).</summary>
    public const int LargeFrom = 5;

    /// <summary>Whether the card lists only the services needing attention instead of every one.</summary>
    public bool IsLarge => Services.Count >= LargeFrom;

    /// <summary>How many service rows a card draws at most (DD337); the rest are on the project's page.</summary>
    public const int CardServiceLimit = 6;

    /// <summary>
    /// The services a card lists: every one on a small project, only those needing attention on a
    /// large one, and never more than <see cref="CardServiceLimit"/>, so a project of twenty-two
    /// failed services does not draw a card taller than the window (DD337).
    /// </summary>
    public IReadOnlyList<ContainerRow> CardServices =>
    [
        .. (IsLarge ? Services.Where(service => service.Tone is RowTone.Bad or RowTone.Warn) : Services)
            .Take(CardServiceLimit),
    ];

    /// <summary>Whether the card leaves services out, and so offers Show all.</summary>
    public bool HasMoreServices => CardServices.Count < Services.Count;

    /// <summary>What the button that opens the project's page from a card that leaves services out says.</summary>
    public string ShowAllLabel =>
        $"Show all {Services.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} services";

    /// <summary>
    /// The card's warnings (DD337): the failures grouped by what happened, in plain words, one line
    /// per kind rather than one per service. The exit codes stay in each line's tooltip and on the
    /// project's page, where someone who wants them goes looking.
    /// </summary>
    public IReadOnlyList<CardAlert> CardAlerts
    {
        get
        {
            // A refused click is its own sentence and says something no grouping can.
            var refused = Problems
                .Where(problem => problem.Row.HasFailure)
                .Select(problem => new CardAlert(problem.Sentence, problem.Sentence));

            var stopped = Problems
                .Where(problem => !problem.Row.HasFailure)
                .Select(problem => problem.Row)
                .GroupBy(row => StopReason.Of(row.ExitCode).Plural, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .Select(group => Alert([.. group]));

            return [.. refused, .. stopped];
        }
    }

    private static CardAlert Alert(IReadOnlyList<ContainerRow> rows)
    {
        var reason = StopReason.Of(rows[0].ExitCode);

        // DD339: names only where they tell the services apart. "DSpace and DSpace were shut down"
        // says less than "2 services were shut down", and the tooltip lists them either way.
        var distinct = rows.Select(row => row.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rows.Count;
        var sentence = rows.Count switch
        {
            1 => Ago(rows[0].Status) is { Length: > 0 } ago
                ? $"{rows[0].Title} {reason.Singular} {ago}"
                : $"{rows[0].Title} {reason.Singular}",
            <= 3 when distinct =>
                $"{string.Join(", ", rows.Take(rows.Count - 1).Select(row => row.Title))} and {rows[^1].Title} {reason.Plural}",
            _ => $"{rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} services {reason.Plural}",
        };

        const int Named = 10;
        var lines = new List<string> { reason.Explains, "" };
        lines.AddRange(rows.Take(Named).Select(row => $"{row.Title}: {row.Status}"));
        if (rows.Count > Named)
        {
            lines.Add($"and {(rows.Count - Named).ToString(System.Globalization.CultureInfo.InvariantCulture)} more");
        }

        return new CardAlert(sentence, string.Join("\n", lines));
    }

    /// <summary>The "5 days ago" end of a status line such as <c>Exited (143) 5 days ago</c>, or nothing.</summary>
    internal static string Ago(string status)
    {
        var close = status.IndexOf(')', StringComparison.Ordinal);
        return close >= 0 ? status[(close + 1)..].Trim() : "";
    }

    /// <summary>How many links a menu holds before it offers a filter box (DD298).</summary>
    public const int LinkFilterFrom = 6;

    /// <summary>The links a menu's filter keeps: those whose name, service or address holds the text.</summary>
    /// <param name="links">Every link.</param>
    /// <param name="filter">What the box holds, or nothing.</param>
    /// <returns>The links, in their order.</returns>
    public static IReadOnlyList<Endpoint> Matching(IReadOnlyList<Endpoint> links, string? filter)
    {
        ArgumentNullException.ThrowIfNull(links);
        if (string.IsNullOrWhiteSpace(filter))
        {
            return links;
        }

        var text = filter.Trim();
        return
        [
            .. links.Where(link => link.Label.Contains(text, StringComparison.OrdinalIgnoreCase)
                || link.Service.Contains(text, StringComparison.OrdinalIgnoreCase)
                || link.Address.Contains(text, StringComparison.OrdinalIgnoreCase)),
        ];
    }

    /// <summary>
    /// The chips a card draws after Open: every other link, or on a project with many only its
    /// pinned ones, the rest being behind the menu (DD298).
    /// </summary>
    public IReadOnlyList<Endpoint> CardLinks => Head.HasLinkMenu ? Head.PinnedLinks : SecondaryLinks;

    /// <summary>The card's links a browser can open, drawn as buttons beside Open (DD326).</summary>
    public IReadOnlyList<Endpoint> CardOpenLinks => [.. CardLinks.Where(link => link.CanOpen)];

    /// <summary>The card's addresses that only copy, drawn as dashed chips on a line of their own (DD326).</summary>
    public IReadOnlyList<Endpoint> CardCopyLinks => [.. CardLinks.Where(link => link.CopiesOnly)];

    /// <summary>The card's meta line under the description (DD326): what kind of thing, which one, how big.</summary>
    public string CardMeta
    {
        get
        {
            var count = Services.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return $"compose · {Head.Project} · {count} {(Services.Count == 1 ? "service" : "services")}";
        }
    }

    /// <summary>
    /// What the card's footer says on its left (DD326): the first running service's status, which
    /// says how long the project has been up, or the first service's where none runs.
    /// </summary>
    /// <remarks>
    /// DD337: a stopped service's line loses its exit code here (<c>Stopped 18 hours ago</c>); the
    /// code is in the warning's tooltip and on the project's page.
    /// </remarks>
    public string Uptime =>
        (Services.FirstOrDefault(service => service.IsRunning) ?? Services.FirstOrDefault()) switch
        {
            null => "",
            { IsRunning: false, ExitCode: not null } stopped =>
                Ago(stopped.Status) is { Length: > 0 } ago ? $"Stopped {ago}" : "Stopped",
            var service => service.Status,
        };

    /// <summary>
    /// The cards view (DD300): one page's worth of content per project, in the list's order, and the
    /// standalone containers after them.
    /// </summary>
    /// <param name="rows">Every container in hand, dressed.</param>
    /// <param name="shape">The sort and filter the page is holding, which both views share.</param>
    /// <returns>The project cards, and the containers outside compose.</returns>
    /// <remarks>
    /// The same rows, the same filter and the same sort as the list, so switching views keeps what
    /// was narrowed. A card is built exactly as the detail page is, from the rows the filter kept, so
    /// a card and the page it opens cannot disagree about a project.
    /// </remarks>
    public static (IReadOnlyList<DetailView> Projects, IReadOnlyList<ContainerRow> Standalone) Cards(
        IReadOnlyList<ContainerRow> rows, ListShape shape)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(shape);

        var kept = ContainerRow.Shaped(rows, shape);
        var projects = ContainerRow.Grouped(rows, shape, new HashSet<string>(StringComparer.Ordinal))
            .Where(row => row is { IsProject: true, IsTier: false })
            .Select(header => For(header.Id, kept))
            .OfType<DetailView>()
            .ToList();

        return (projects, [.. kept.Where(row => row.Project is null)]);
    }

    /// <summary>How many lines the Recent output pane keeps.</summary>
    public const int OutputLines = 14;

    /// <summary>
    /// The service the Recent output pane tails: the first that failed, else the first there is.
    /// </summary>
    public ContainerRow? OutputService => Problems.Count > 0 ? Problems[0].Row : Services.FirstOrDefault();

    /// <summary>The last lines of a log, split into lines, each knowing which stream it came on.</summary>
    /// <param name="chunks">The log, as <see cref="LogFrames"/> read it.</param>
    /// <param name="keep">How many lines to keep, newest last.</param>
    /// <returns>The lines.</returns>
    /// <remarks>
    /// A chunk is not a line: a frame can end mid-sentence, so the unfinished end of each stream is
    /// carried to that stream's next chunk rather than drawn as a line of its own.
    /// </remarks>
    public static IReadOnlyList<OutputLine> Tail(IEnumerable<LogChunk> chunks, int keep)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var lines = new List<OutputLine>();
        var pending = new Dictionary<LogStream, string>();
        foreach (var chunk in chunks)
        {
            var text = (pending.GetValueOrDefault(chunk.Stream) ?? "") + chunk.Text;
            var parts = text.Split('\n');
            pending[chunk.Stream] = parts[^1];
            lines.AddRange(parts[..^1]
                .Select(part => part.TrimEnd('\r'))
                .Where(part => part.Length > 0)
                .Select(part => new OutputLine(part, chunk.Stream == LogStream.StdErr)));
        }

        lines.AddRange(pending
            .Where(rest => rest.Value.TrimEnd('\r').Length > 0)
            .Select(rest => new OutputLine(rest.Value.TrimEnd('\r'), rest.Key == LogStream.StdErr)));

        return lines.Count <= keep ? lines : lines[^keep..];
    }

    /// <summary>
    /// The rail's compose projects: one header per project, by title, narrowed by the rail's filter.
    /// </summary>
    /// <param name="rows">Every container in hand.</param>
    /// <param name="filter">What the rail's box holds, or nothing.</param>
    /// <returns>The headers.</returns>
    public static IReadOnlyList<ContainerRow> RailProjects(IReadOnlyList<ContainerRow> rows, string? filter)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return
        [
            .. rows
                .Where(row => row.IsContainer && row.Project is not null)
                .GroupBy(row => row.Project!, StringComparer.Ordinal)
                .Select(project => ContainerRow.ProjectHeader(project.Key, [.. project], collapsed: false))
                .Where(header => Matches(header, filter))
                .OrderBy(header => header.Title, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>The rail's standalone containers, by title, narrowed by the rail's filter.</summary>
    /// <param name="rows">Every container in hand.</param>
    /// <param name="filter">What the rail's box holds, or nothing.</param>
    /// <returns>The containers.</returns>
    public static IReadOnlyList<ContainerRow> RailStandalone(IReadOnlyList<ContainerRow> rows, string? filter)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return
        [
            .. rows
                .Where(row => row.IsContainer && row.Project is null && Matches(row, filter))
                .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static bool Matches(ContainerRow row, string? filter) =>
        string.IsNullOrWhiteSpace(filter)
        || row.Title.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)
        || row.Name.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

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
    internal static IEnumerable<Endpoint> EndpointsOf(ContainerRow service)
    {
        if (service.Details is { } details && details.LinksSource is not ("published ports" or "none" or ""))
        {
            return details.Links.Select(link => new Endpoint(link.Name, service.Title, Bare(link.Url), link.Url)
            {
                IsPinned = details.Pinned.Contains(link.Name, StringComparer.Ordinal),
            });
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
