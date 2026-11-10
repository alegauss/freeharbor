using FreeHarbor.Core.Api;
using FreeHarbor.Core.Details;
using FreeHarbor.Core.Engine;

namespace FreeHarbor.Tray.Ui;

/// <summary>One published port, and where clicking it goes.</summary>
/// <param name="Text">
/// What the chip reads: the host port, <c>:8080</c>, with its protocol where that is not TCP
/// (DD325); or, for a port only exposed, the daemon's own <c>6379/tcp</c>, since it has no host port.
/// </param>
/// <param name="Url">
/// Where to open, or <see langword="null"/> when there is nowhere: a port the container exposes and
/// nothing published has no address on this machine, so it is text and not a link.
/// </param>
public sealed record PortLink(string Text, string? Url)
{
    /// <summary>
    /// The address a click copies, for a published port that does not speak HTTP (DD297), or
    /// <see langword="null"/>.
    /// </summary>
    public string? Address { get; init; }

    /// <summary>
    /// What is bound to what, <c>8080-&gt;80/tcp</c>: the chip's text before DD325, which moved to its
    /// tooltip so the chip reads as the address somebody types and still says what it maps to.
    /// </summary>
    public string Mapping { get; init; } = "";

    /// <summary>Whether this one can be clicked to open a browser.</summary>
    public bool IsLink => Url is not null;

    /// <summary>Whether this one can be clicked to copy its address.</summary>
    public bool IsCopy => Address is not null;

    /// <summary>What the copy chip's tooltip says, or empty where it does not copy.</summary>
    public string CopyHint => Address is null ? "" : $"Copy {Address} · {Mapping}";

    /// <summary>What the link chip's tooltip says: where it goes, and what is bound to what.</summary>
    public string LinkHint => Url is null ? Mapping : $"{Url} · {Mapping}";

    /// <summary>Whether this one is plain text: exposed only, so there is nowhere to go.</summary>
    public bool IsText => !IsLink && !IsCopy;

    /// <summary>
    /// The ports a container listens on that are known not to answer HTTP (DD297): the resolver's
    /// list since DD334, so a chip and a link cannot disagree about a port.
    /// </summary>
    /// <remarks>
    /// A list and not a probe, because a probe per port per refresh is a call this window does not
    /// make; a port missing from it stays a link, which is what it was before.
    /// </remarks>
    public static IReadOnlySet<int> NotHttp => ContainerDetails.NotHttp;

    /// <summary>Read a binding.</summary>
    /// <param name="port">The binding.</param>
    /// <returns>The cell.</returns>
    public static PortLink From(PortBinding port)
    {
        ArgumentNullException.ThrowIfNull(port);

        var mapping = port.ToString();
        if (port.PublicPort is not { } published)
        {
            return new PortLink(mapping, null) { Mapping = mapping };
        }

        // Only HTTP gets a link. A published UDP port, or a database's, is real and
        // http://localhost:x is not where it is: a link that opens a browser onto nothing is worse
        // than an address somebody can paste into the client that does speak it.
        var tcp = port.Type.Equals("tcp", StringComparison.OrdinalIgnoreCase);
        var http = tcp && !NotHttp.Contains(port.PrivatePort);
        var number = published.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var text = tcp ? $":{number}" : $":{number}/{port.Type}";

        return http
            ? new PortLink(text, $"http://localhost:{number}") { Mapping = mapping }
            : new PortLink(text, null) { Address = $"localhost:{number}", Mapping = mapping };
    }
}

/// <summary>A container, as the window shows it.</summary>
/// <param name="Name">The name, or the short id when it has none.</param>
/// <param name="Image">The image.</param>
/// <param name="State">One word.</param>
/// <param name="Status">The daemon's own sentence: <c>Up 3 minutes</c>, <c>Exited (0) …</c>.</param>
/// <param name="Ports">The ports, deduplicated, each with its link or without one.</param>
/// <param name="Id">The full id, which is what an action is addressed to.</param>
public sealed record ContainerRow(
    string Name,
    string Image,
    string State,
    string Status,
    IReadOnlyList<PortLink> Ports,
    string Id)
{
    /// <summary>
    /// The compose project this container belongs to, or nothing (DD106).
    /// </summary>
    /// <remarks>
    /// Read off the label the list response already carries, so the hierarchy costs no second call —
    /// the same label DD24 addresses containers by. A container carrying none stays a top-level row
    /// rather than joining an "other" group, because that group would name nothing.
    /// </remarks>
    public string? Project { get; init; }

    /// <summary>
    /// Its title, description, icon and links, resolved once by <see cref="ContainerDetails"/> (DD294).
    /// </summary>
    /// <remarks>
    /// On a project's header these are the project's own (DD295), from its project file or its
    /// compose name. Every property the tile and the title draw reads this one, so no binding
    /// resolves a label on its own.
    /// </remarks>
    public ResolvedDetails? Details { get; init; }

    /// <summary>
    /// The details of the project this container is in, which its header draws (DD295).
    /// </summary>
    /// <remarks>
    /// Carried on every container rather than looked up by the header, because the header is built
    /// from its children and the folder the project file is in is a label only a container has.
    /// </remarks>
    public ResolvedDetails? ProjectDetails { get; init; }

    /// <summary>The project's details with nothing saved in FreeHarbor, for its header's dialog (DD296).</summary>
    public ResolvedDetails? ProjectDetailsBeneath { get; init; }

    /// <summary>
    /// What the row would show with nothing saved in FreeHarbor (DD296): what the edit dialog
    /// compares each field with, and what its Reset puts back.
    /// </summary>
    /// <remarks>
    /// Resolved here, beside <see cref="Details"/>, so the dialog never projects the container a
    /// second time. The resolver is a pure function over what is already in hand, so the price is a
    /// second call to it and no read of anything.
    /// </remarks>
    public ResolvedDetails? DetailsBeneath { get; init; }

    /// <summary>The key an edit of this row is saved under (DD296): project/service, or the name.</summary>
    public string? DetailKey { get; init; }

    /// <summary>The folder compose ran in, which the edit dialog names, or nothing outside compose.</summary>
    public string? WorkingDir { get; init; }

    /// <summary>The compose files, comma-separated as compose stamps them, or nothing.</summary>
    public string? ConfigFiles { get; init; }

    /// <summary>What the name cell draws: the resolved title, or the name on a header.</summary>
    public string Title => Details?.Title ?? Name;

    /// <summary>The line under the title, or empty, in which case nothing is drawn.</summary>
    public string Description => Details?.Description ?? "";

    /// <summary>Whether there is a description to draw.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>
    /// The name cell's tooltip: the container's own name, which the title replaced on screen.
    /// </summary>
    public string NameEvidence => IsProject || Details is null
        || string.Equals(Title, Name, StringComparison.Ordinal)
        ? Name
        : $"{Name} ({Image})";

    /// <summary>Whether the row has an icon tile: every project header and every standalone container.</summary>
    /// <remarks>
    /// Not a service inside a project (DD324): its project's header carries the tile, and a 40 px
    /// square on every row under it took the width the title and description needed.
    /// </remarks>
    public bool HasTile => Details is not null && !IsTier && !HasStateShape;

    /// <summary>
    /// Whether the row leads with its state's shape instead of a tile: a service inside a project
    /// (DD324, spec section 2's "state shape, title over description").
    /// </summary>
    public bool HasStateShape => IsContainer && Project is not null;

    /// <summary>
    /// The first port's chip text, for a card's service row (DD326), or empty where it has none.
    /// </summary>
    public string FirstPort => Ports.Count > 0 ? Ports[0].Text : "";

    /// <summary>
    /// Every port's chip text on one line, for a standalone card's details line (DD327), or a phrase
    /// saying there is none rather than a gap between two separators.
    /// </summary>
    public string PortsLine => IsProject ? "" : Ports.Count > 0
        ? string.Join(" ", Ports.Select(port => port.Text))
        : "no published ports";

    /// <summary>
    /// Where Open goes, without the scheme, which a card writes after the button's name (DD326):
    /// <c>localhost:8080</c>. Empty where there is no Open.
    /// </summary>
    public string OpenHost => OpenLink?.Url is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        ? uri.Authority + uri.PathAndQuery.TrimEnd('/')
        : "";

    /// <summary>
    /// The row's tone as a colour, for a header and a container alike (DD328): what the detail page's
    /// rail draws its shape in.
    /// </summary>
    public System.Windows.Media.Brush ToneInk => Palette.ToneInk(Tone);

    /// <summary>
    /// What the rail writes under a title (DD328): a project's count, a container's status.
    /// </summary>
    public string RailNote => IsProject ? ChipWord : Status;

    /// <summary>Whether this is the item the detail page is open on, which the rail marks (DD328).</summary>
    public bool IsRailSelected { get; init; }

    /// <summary>What the state shape is drawn in: its tone's colour, not the chip's text colour.</summary>
    public System.Windows.Media.Brush? ShapeInk => HasStateShape ? Palette.ToneInk(Tone) : null;

    /// <summary>
    /// Whether the row is drawn inside a group card (DD324): every row of a project, and every
    /// standalone container once a project sits above them.
    /// </summary>
    public bool InCard { get; init; }

    /// <summary>Whether the row opens its card: a project's header, or the first standalone container.</summary>
    public bool CardStarts { get; init; }

    /// <summary>Whether the row closes its card: the last row drawn for a project or the standalone block.</summary>
    public bool CardEnds { get; init; }

    /// <summary>The card's corners on this row: rounded where the card starts and where it ends.</summary>
    public System.Windows.CornerRadius CardCorners => InCard
        ? new System.Windows.CornerRadius(CardStarts ? 8 : 0, CardStarts ? 8 : 0, CardEnds ? 8 : 0, CardEnds ? 8 : 0)
        : default;

    /// <summary>
    /// The card's stroke on this row. The top edge is drawn on every row, so on a row below the first
    /// it is the hairline between the two; the bottom edge only where the card ends.
    /// </summary>
    public System.Windows.Thickness CardEdges => InCard
        ? new System.Windows.Thickness(1, 1, 1, CardEnds ? 1 : 0)
        : default;

    /// <summary>The space under a card, so two cards read as two groups.</summary>
    public System.Windows.Thickness CardGap => InCard && CardEnds
        ? new System.Windows.Thickness(0, 0, 0, 12)
        : default;

    /// <summary>
    /// The standalone block's count, <c>n of m running</c>, carried by the row that opens the block
    /// (DD324), where its header strip is drawn.
    /// </summary>
    public string StandaloneCount { get; init; } = "";

    /// <summary>
    /// Whether the description runs under the title across NAME to STATUS instead of stopping at NAME
    /// (DD324): a header's, which has no image or status of its own to share the width with.
    /// </summary>
    public bool SpansDescription => IsProject && HasDescription;

    /// <summary>How many columns the name cell covers: four on a header with a description, else one.</summary>
    public int NameSpan => SpansDescription ? 4 : 1;

    /// <summary>
    /// Where the row's cells sit: at the top on a header with a description, so the chip stays beside
    /// the title and clear of the description under it, and centred on every other row.
    /// </summary>
    public System.Windows.VerticalAlignment TitleAlignment => SpansDescription
        ? System.Windows.VerticalAlignment.Top
        : System.Windows.VerticalAlignment.Center;

    /// <summary>The tile's hue.</summary>
    public System.Windows.Media.Brush? TileFill =>
        Details is { } details ? Palette.TileBrushes[details.Hue % Palette.TileBrushes.Count] : null;

    /// <summary>The bundled glyph, or empty where the tile draws a picture or a letter.</summary>
    public string TileGlyph => Details is { } details ? IconTile.GlyphFor(details.Icon) ?? "" : "";

    /// <summary>The picture from disk, or null.</summary>
    public System.Windows.Media.ImageSource? TilePicture =>
        Details is { } details ? IconTile.PictureFor(details.Icon) : null;

    /// <summary>The letter, drawn only when there is neither a glyph nor a picture.</summary>
    public string TileLetter => Details is { } details && TileGlyph.Length == 0 && TilePicture is null
        ? IconTile.LetterFor(details.Icon, details.Title)
        : "";

    /// <summary>The compose service this container is an instance of, or nothing (DD107).</summary>
    public string? Service { get; init; }

    /// <summary>The services this one waits for, as its own label names them (DD107).</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = [];

    /// <summary>
    /// Whether this row is a project's header rather than a container (DD106).
    /// </summary>
    /// <remarks>
    /// One row type and one template with a trigger, rather than two of each. The header fills the
    /// columns it has an answer for — the name, the count, its chevron — and the rest read empty,
    /// which is what the trigger is for. A header with no answer for the image column must read as
    /// blank and never as a container with no image.
    /// </remarks>
    public bool IsProject { get; init; }

    /// <summary>Whether this row is a container. The complement, spelled for markup.</summary>
    public bool IsContainer => !IsProject;

    /// <summary>How many of the project's shown containers are running.</summary>
    public int Running { get; init; }

    /// <summary>How many containers the project is showing.</summary>
    public int Total { get; init; }

    /// <summary>Whether the project's children are hidden.</summary>
    public bool Collapsed { get; init; }

    /// <summary>
    /// The disclosure glyph, in the two codepoints Segoe MDL2 Assets spells a chevron with.
    /// </summary>
    /// <remarks>
    /// The same pair claude-tray's call tree uses, because that window is this project's reference
    /// for interface formatting and a second glyph vocabulary would read as a second application. It
    /// is the affordance and not decoration: a row that opens and one that does not must not look
    /// alike.
    /// </remarks>
    public string Chevron => !IsProject ? "" : Collapsed ? "" : "";

    /// <summary>
    /// What the header says instead of a status: how much of the project is up.
    /// </summary>
    /// <remarks>
    /// Of the containers actually under it rather than of the project as the daemon knows it. The
    /// filter can hide some, and a header describing rows that are not on screen is a count nobody
    /// can check against what they are looking at.
    /// </remarks>
    public string ProjectCount => IsProject
        ? $"{Running.ToString(System.Globalization.CultureInfo.InvariantCulture)} of "
            + $"{Total.ToString(System.Globalization.CultureInfo.InvariantCulture)} running"
        : "";

    /// <summary>How many of the project's shown containers stopped and did not mean to (DD297).</summary>
    public int Failed { get; init; }

    /// <summary>
    /// Whether this header is a <c>freeharbor.group</c> tier inside a project rather than the project
    /// itself (DD298).
    /// </summary>
    /// <remarks>
    /// A header and not a third kind of row: a tier folds, counts and carries a chip exactly as a
    /// project does, and what it lacks is verbs, which <see cref="HasPrimary"/> and
    /// <see cref="CanRemove"/> withhold. A Stop on a tier would read as stopping the tier and, sent
    /// through the project's fan-out, would stop the whole project.
    /// </remarks>
    public bool IsTier { get; init; }

    /// <summary>The tier a container is drawn under, or a tier header's own name (DD298).</summary>
    public string? Tier { get; init; }

    /// <summary>
    /// How many of a tier's containers are a problem: stopped and not meaning to, or stuck in between.
    /// </summary>
    /// <remarks>
    /// A clean exit is not one, so a tier holding a finished migration still reads as healthy and
    /// stays folded.
    /// </remarks>
    public int Problems { get; init; }

    /// <summary>The name a container with no <c>freeharbor.group</c> is drawn under, in a tiered project.</summary>
    public const string OtherTier = "Other";

    /// <summary>
    /// What the state chip says: the container's state, or the project's count (DD297).
    /// </summary>
    /// <remarks>
    /// The header's count moved into the STATE column as a chip of its own, so the column a reader
    /// scans for trouble says it for a project too, in the tone of what is under it.
    /// </remarks>
    public string ChipWord => this switch
    {
        { IsTier: true, Problems: 1 } => "1 problem",
        { IsTier: true, Problems: > 1 } =>
            $"{Problems.ToString(System.Globalization.CultureInfo.InvariantCulture)} problems",
        { IsTier: true, Total: > 0 } when Running == Total => "all running",

        // DD320: the count alone, as the spec's cards write it. The STATE column is narrow at the
        // window's default width and trimmed "running" to "runni…"; the tooltip says the sentence.
        { IsProject: true } =>
            $"{Running.ToString(System.Globalization.CultureInfo.InvariantCulture)} of "
            + Total.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => StateWord,
    };

    /// <summary>
    /// The state as the window writes it (DD341): <c>failed</c> for a container that stopped and did
    /// not mean to, <c>stopped</c> for one that finished cleanly, and the daemon's word otherwise.
    /// </summary>
    /// <remarks>
    /// The daemon says <c>exited</c> for both, which left the colour alone to tell a crash from a
    /// job that did its work. The daemon's word stays in <see cref="State"/>, which the filters and
    /// the CLI read.
    /// </remarks>
    public string StateWord => this switch
    {
        { State: "exited" or "dead", Tone: RowTone.Bad } => "failed",
        { State: "exited" } => "stopped",
        _ => State,
    };

    /// <summary>The dot before the chip's word, which the tone's colour paints.</summary>
    /// <remarks>
    /// DD338: one dot for every state. Four shapes (dot, square, triangle, half circle) read as four
    /// kinds of information when there is only one, the state. The word beside the dot (running,
    /// exited, "0 of 22") is what carries the state for anyone who cannot tell the colours apart,
    /// and the chip's tooltip says it in a sentence.
    /// </remarks>
    public string ChipShape => "●";

    /// <summary>Why the chip says what it says: a container's evidence, or a sentence about the project's.</summary>
    public string ChipEvidence => IsContainer ? StateEvidence : $"{ProjectCount}. {HeaderEvidence}";

    /// <summary>What a header's chip adds to its count: which kind of stopped the rest are.</summary>
    private string HeaderEvidence => this switch
    {
        { Failed: > 0 } => $"{Failed.ToString(System.Globalization.CultureInfo.InvariantCulture)} of them "
            + "stopped and did not mean to. Their logs say why.",
        _ when Running == Total => "Every container under it is up.",
        { Running: 0 } => "Nothing under it is running.",
        _ => $"{(Total - Running).ToString(System.Globalization.CultureInfo.InvariantCulture)} of them "
            + "stopped cleanly.",
    };

    /// <summary>
    /// The link the Open button follows (DD297): a container's primary, or on a header the first
    /// primary among its containers by name.
    /// </summary>
    /// <remarks>
    /// By name and not in draw order, so re-sorting the list never changes where a project's Open
    /// goes. A project has no link of its own (DD295), so its Open is one of its services'.
    /// </remarks>
    public DetailLink? OpenLink { get; init; }

    /// <summary>Every link a project's services serve, by service name (DD298); empty on a container.</summary>
    public IReadOnlyList<Endpoint> ProjectLinks { get; init; } = [];

    /// <summary>How many links a project needs before the rest go behind a menu.</summary>
    /// <remarks>The spec's threshold: under three links nothing about a large project applies.</remarks>
    public const int LinkMenuFrom = 3;

    /// <summary>Whether the header draws its links' menu beside Open: three links or more.</summary>
    public bool HasLinkMenu => ProjectLinks.Count >= LinkMenuFrom;

    /// <summary>
    /// The pinned links drawn as buttons beside Open on a header with a menu: at most two, and never
    /// the one Open already follows.
    /// </summary>
    public IReadOnlyList<Endpoint> PinnedLinks => HasLinkMenu
        ? [.. ProjectLinks
            .Where(link => link.IsPinned && !string.Equals(link.Url, OpenLink?.Url, StringComparison.Ordinal))
            .Take(2)]
        : [];

    /// <summary>What the menu's button says: how many links it holds.</summary>
    public string LinkMenuLabel => HasLinkMenu
        ? $"{ProjectLinks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} ▾"
        : "";

    /// <summary>
    /// Whether the row draws an Open button: a project's header, or a container outside compose.
    /// </summary>
    /// <remarks>
    /// A service under a header leaves Open to the header and keeps its link chips, which is the
    /// spec's split. A row with nothing to open draws no button, because a button that can never be
    /// pressed says nothing a missing one does not.
    /// </remarks>
    public bool ShowsOpen => OpenLink is not null && (IsProject || Project is null);

    /// <summary>Whether Open can be pressed: disabled, and not hidden, while nothing is running.</summary>
    public bool CanOpen => ShowsOpen && (IsProject ? Running > 0 : IsRunning);

    /// <summary>
    /// What the Open button says: the link's own name, or <c>Open</c> where the name is a port.
    /// </summary>
    public string OpenLabel => OpenLink is { Name: { Length: > 0 } name }
        && !name.StartsWith(':') && !string.Equals(name, "Open", StringComparison.Ordinal)
            ? name
            : "Open";

    /// <summary>
    /// The port chips the PORTS cell draws: every port, less the one the Open button already opens.
    /// </summary>
    public IReadOnlyList<PortLink> PortChips => ShowsOpen
        ? [.. Ports.Where(port => !string.Equals(port.Url, OpenLink!.Url, StringComparison.Ordinal))]
        : Ports;

    /// <summary>
    /// Whether the row's title is a link to the detail page (DD299): a project's header, or a
    /// container outside compose. A service's page is its project's, reached from the header.
    /// </summary>
    public bool OpensDetail => IsProject ? !IsTier : Project is null;

    /// <summary>Whether the title is plain text: the complement, spelled for markup.</summary>
    public bool TitleIsText => !OpensDetail;

    /// <summary>
    /// Whether this row opens the Standalone containers block (DD297), and so draws its heading.
    /// </summary>
    /// <remarks>
    /// A flag on the first such row rather than a third kind of row: the heading has no columns, no
    /// verbs and no state, and one row type with a trigger is the shape DD106 chose and DD111 guards.
    /// </remarks>
    public bool StartsStandalone { get; init; }

    /// <summary>
    /// Whether the row offers to add details: a container outside compose with no description.
    /// </summary>
    /// <remarks>
    /// The spec's one exception to "a missing description draws nothing". A service has its project
    /// file to be described in; a standalone container has only this window.
    /// </remarks>
    public bool InvitesDetails => IsContainer && Project is null && Details is not null && !HasDescription;

    /// <summary>How far this row is pushed in — the whole signal that it belongs to the one above.</summary>
    public System.Windows.Thickness Indent => this switch
    {
        // DD298: a tier sits one step in under its project, and a container in a tier one further.
        { IsTier: true } => new System.Windows.Thickness(18, 0, 0, 0),
        { IsProject: true } or { Project: null } => default,
        { Tier: not null } => new System.Windows.Thickness(36, 0, 0, 0),
        _ => new System.Windows.Thickness(18, 0, 0, 0),
    };

    /// <summary>The id a project's header is reconciled by (DD70), which is not a container's.</summary>
    /// <param name="project">The project.</param>
    /// <returns>The id.</returns>
    /// <remarks>
    /// Prefixed rather than the bare name, so a header can never collide with a container id — and
    /// so <see cref="LiveRows{T}"/>'s arrive-and-leave fade works on projects with nothing added to
    /// it, a project appearing being exactly the event that fade exists to show.
    /// </remarks>
    public static string ProjectId(string project) => "compose:" + project;

    /// <summary>Build a project's header row.</summary>
    /// <param name="project">The project's name.</param>
    /// <param name="children">The containers shown under it.</param>
    /// <param name="collapsed">Whether its children are hidden.</param>
    /// <returns>The header.</returns>
    public static ContainerRow ProjectHeader(
        string project, IReadOnlyList<ContainerRow> children, bool collapsed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentNullException.ThrowIfNull(children);

        return new ContainerRow(project, "", "", "", [], ProjectId(project))
        {
            IsProject = true,

            // DD295. Its children all read one project file, so the first one's copy is the
            // project's; a header with no children has only its compose name.
            Details = children.Select(child => child.ProjectDetails).FirstOrDefault(details => details is not null)
                ?? ContainerDetails.ResolveProject(project, null),

            // DD296: what its edit dialog needs, from the child that knows the folder.
            DetailsBeneath = children.Select(child => child.ProjectDetailsBeneath)
                    .FirstOrDefault(details => details is not null)
                ?? ContainerDetails.ResolveProject(project, null),
            DetailKey = DetailStore.ProjectKey(project),
            WorkingDir = children.Select(child => child.WorkingDir).FirstOrDefault(dir => dir is not null),
            ConfigFiles = children.Select(child => child.ConfigFiles).FirstOrDefault(files => files is not null),
            Project = project,
            Running = children.Count(child => child.IsRunning),
            Total = children.Count,
            Failed = children.Count(child => child.Tone == RowTone.Bad),

            // DD297: the project's Open is the first primary among its services, by name, so a re-sort
            // never moves where it goes.
            OpenLink = children
                .OrderBy(child => child.Name, StringComparer.Ordinal)
                .Select(child => child.OpenLink)
                .FirstOrDefault(link => link is not null),

            // DD298: every link, so a project of ten can put the rest behind a menu.
            ProjectLinks = [.. children
                .OrderBy(child => child.Name, StringComparer.Ordinal)
                .SelectMany(DetailView.EndpointsOf)],
            Collapsed = collapsed,
        };
    }

    /// <summary>
    /// What this row is waiting for — <c>Stopping…</c> — or <see langword="null"/> when idle.
    /// </summary>
    /// <remarks>
    /// Set the moment the button is pressed and cleared by the event that confirms it, because a
    /// stop can take the daemon's full ten-second grace period and a row that sits unchanged for
    /// ten seconds reads as a click that did nothing.
    /// </remarks>
    public string? Pending { get; init; }

    /// <summary>The engine's own sentence about why the last action failed, or nothing.</summary>
    public string? Failure { get; init; }

    /// <summary>Whether this container is running now.</summary>
    public bool IsRunning => State.Equals("running", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the container is up in any sense the four verbs care about.
    /// </summary>
    /// <remarks>
    /// Paused and restarting are not <c>running</c> and are not stopped either. Offering "Start" for
    /// them would be a button whose only outcome is a 409 from the daemon.
    /// </remarks>
    public bool IsLive => State.Equals("running", StringComparison.OrdinalIgnoreCase)
        || State.Equals("paused", StringComparison.OrdinalIgnoreCase)
        || State.Equals("restarting", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether an action is in flight on this row.</summary>
    public bool IsPending => Pending is not null;

    /// <summary>Whether the last action on this row failed.</summary>
    public bool HasFailure => Failure is not null;

    /// <summary>
    /// Whether anything this row stands for is up (DD107).
    /// </summary>
    /// <remarks>
    /// The one place a project's verb differs from a container's, and everything below reads it
    /// rather than <see cref="IsLive"/>: a header carries no state of its own, so what decides
    /// whether it offers Stop or Start is how many of its containers are running. One partly-up
    /// project offers Stop, because the button that finishes the job is the useful one.
    /// </remarks>
    public bool AnyUp => IsProject ? Running > 0 : IsLive;

    /// <summary>Whether the row offers Start.</summary>
    public bool CanStart => !IsPending && !AnyUp;

    /// <summary>Whether the row offers Stop.</summary>
    public bool CanStop => !IsPending && AnyUp;

    /// <summary>Whether the row offers Restart.</summary>
    public bool CanRestart => !IsPending && AnyUp;

    /// <summary>Whether the row offers Remove. Anything not already busy can be removed.</summary>
    /// <remarks>
    /// A project too, since DD107 gave the header verbs of its own — the removal is fanned across
    /// its containers and its dialog names the count rather than a container.
    /// </remarks>
    public bool CanRemove => !IsPending && !IsTier;

    /// <summary>
    /// Whether a shell can be opened: running, and nothing else in flight.
    /// </summary>
    /// <remarks>
    /// Running and not merely live. <c>docker exec</c> against a paused container is accepted and
    /// then hangs until somebody unpauses it, with no window and no error — which is worse than a
    /// button that is visibly off.
    /// </remarks>
    public bool CanShell => IsContainer && !IsPending && IsRunning;

    /// <summary>
    /// Why the Shell button is off, or what it does when it is on.
    /// </summary>
    /// <remarks>
    /// Unlike the other four, this one is shown disabled rather than hidden, so it owes an
    /// explanation: a disabled control that says why is a limitation, and one that does not is a
    /// bug the user is left to guess at.
    /// </remarks>
    public string ShellReason => this switch
    {
        // Before the pending case: a project has no shell whether or not something is in flight on
        // it, and "wait for the action" would promise one that never arrives.
        { IsProject: true } => "A project has no shell. Open one on a container under it.",
        { IsPending: true } => "Wait for the action already running on this container.",
        { IsRunning: true } => "Open a shell in this container.",
        { State: "paused" } => "This container is paused. Unpause it before opening a shell.",
        { State: "restarting" } => "This container is restarting. Wait for it to come up.",
        _ => "This container is not running. Start it before opening a shell.",
    };


    /// <summary>
    /// What the state chip asserts, in the three tones a glance tells apart (DD36).
    /// </summary>
    /// <remarks>
    /// A clean exit is muted rather than red: it stopped because it was finished, and a migration
    /// container that did its job is not a problem to draw attention to. Only a non-zero exit is bad,
    /// which is the distinction the tertiary grey it used to be drawn in could not make.
    /// </remarks>
    public RowTone Tone => this switch
    {
        // DD297: a header's chip is its project's, which is failure when any container under it
        // failed, up when all are, stopped when none are, and in between otherwise.
        // DD298: a tier is healthy unless something in it is a problem, so a clean exit beside running
        // services leaves it muted rather than in between.
        { IsTier: true, Failed: > 0 } => RowTone.Bad,
        { IsTier: true, Problems: > 0 } => RowTone.Warn,
        { IsTier: true, Total: > 0 } when Running == Total => RowTone.Good,
        { IsTier: true } => RowTone.Muted,
        { IsProject: true, Failed: > 0 } => RowTone.Bad,
        { IsProject: true, Total: > 0 } when Running == Total => RowTone.Good,
        { IsProject: true, Running: 0 } => RowTone.Muted,
        { IsProject: true } => RowTone.Warn,
        { IsRunning: true } => RowTone.Good,
        { State: "paused" } or { State: "restarting" } or { State: "created" } => RowTone.Warn,
        _ when ExitCode is 0 => RowTone.Muted,
        { IsLive: false } => RowTone.Bad,
        _ => RowTone.Muted,
    };

    /// <summary>
    /// The exit code the daemon put in the status line, where it put one there.
    /// </summary>
    /// <remarks>
    /// Parsed out of <c>Exited (137) 12 seconds ago</c> rather than asked for: it is already in the
    /// list response, and an inspect per row to read one integer is the call this window does not make.
    /// </remarks>
    public int? ExitCode
    {
        get
        {
            var open = Status.IndexOf('(', StringComparison.Ordinal);
            var close = Status.IndexOf(')', StringComparison.Ordinal);
            return open >= 0 && close > open + 1
                   && int.TryParse(
                       Status.AsSpan(open + 1, close - open - 1),
                       System.Globalization.NumberStyles.Integer,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out var code)
                ? code
                : null;
        }
    }

    /// <summary>
    /// Why the chip says what it says.
    /// </summary>
    /// <remarks>
    /// A chip is an assertion, so it owes evidence — and the evidence has to be more than the status
    /// column already shows, or the tooltip is the same sentence twice. 137 is the one worth spelling
    /// out: it is SIGKILL, it is what the kernel's memory limit looks like from here, and it is the
    /// exit code the diagnostic half of this product exists for.
    /// </remarks>
    public string StateEvidence => this switch
    {
        { IsRunning: true } => Status.Length > 0 ? Status : "The daemon reports this container running.",
        { State: "paused" } => "Paused: its processes are frozen, not stopped.",
        { State: "restarting" } => "Restarting: the daemon is bringing it back up.",
        { State: "created" } => "Created and never started.",
        { ExitCode: 0 } => "Exited 0: it finished and meant to.",
        { ExitCode: 137 } => "Exited 137: SIGKILL. Usually the memory limit, sometimes a stop that "
            + "ran out of its grace period.",
        { ExitCode: { } code } => $"Exited {code.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            + ": it stopped and did not mean to. Its log is the next thing to read.",
        _ => Status,
    };

    /// <summary>
    /// Whether the image this container runs on has left the store (DD167).
    /// </summary>
    /// <remarks>
    /// The one fact that explains a digest in this column and an images page holding nothing in
    /// use: those are two correct answers to the same question, and without this they read as two
    /// lists disagreeing.
    /// </remarks>
    public bool ImageIsGone { get; init; }

    /// <summary>What the note beside the image says, or nothing where the image is still there.</summary>
    public string ImageNote => ImageIsGone ? "image gone" : "";

    /// <summary>
    /// Why the note says it, and what to do about it.
    /// </summary>
    /// <remarks>
    /// A note is an assertion and owes its evidence, the same debt <see cref="StateEvidence"/> pays.
    /// The last sentence is the actionable half: a restart reuses the image the container already
    /// holds, so the reflex that would fix almost anything else does nothing here.
    /// </remarks>
    public string ImageEvidence => ImageIsGone
        ? "This container's image is no longer in the image list: it was rebuilt or removed "
          + "while the container kept running, so the daemon has only its digest left to report. "
          + "Recreating the container is what picks up the current image; restarting it will not."
        : "";

    /// <summary>The fill the chip is drawn with, set once per render from <see cref="RowStyle"/>.</summary>
    public System.Windows.Media.Brush? ChipFill { get; init; }

    /// <summary>What the chip's word is written in.</summary>
    public System.Windows.Media.Brush? ChipText { get; init; }

    /// <summary>
    /// The one verb this row is opened for, beside Logs.
    /// </summary>
    /// <remarks>
    /// The other three moved behind the overflow (DD36): six word captions per row is two hundred of
    /// them on a list of forty, and the eye has nothing to skip past. Which one this is depends on the
    /// state, because a running container is opened to be stopped and a stopped one to be started.
    /// </remarks>
    public string PrimaryVerb => CanStop ? "Stop" : "Start";

    /// <summary>Whether the primary verb is offered at all.</summary>
    public bool HasPrimary => !IsTier && (CanStop || CanStart);

    /// <summary>
    /// Whether the Logs button is drawn, and how it is not (DD107).
    /// </summary>
    /// <remarks>
    /// Hidden rather than collapsed, which is the whole reason this is a
    /// <see cref="System.Windows.Visibility"/> and not another <c>bool</c> through the converter the
    /// rest of the row uses. A project has no log of its own, but collapsing the button closes the
    /// gap and shifts the header's Stop a button's width left of every Stop below it — and a control
    /// that does not sit under the one it matches is what L3 is about. The space is kept; only the
    /// button goes.
    /// </remarks>
    public System.Windows.Visibility LogsVisibility =>
        IsContainer ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;

    /// <summary>Dress this row in the theme's brushes.</summary>
    /// <param name="style">The brushes, resolved once for the whole render.</param>
    /// <returns>The row, with its chip filled in.</returns>
    public ContainerRow WithChip(RowStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return this with { ChipFill = style.Fill(Tone), ChipText = style.Text(Tone) };
    }


    /// <summary>
    /// Which of this type's members belong to which of the two rows it describes (DD111).
    /// </summary>
    /// <remarks>
    /// DD106 chose one row type and one template with a trigger, and that was right — two templates
    /// would mean two column layouts, and the guard pairing a header's columns with its rows exists
    /// because those drift. What followed by accretion was the type: seven members that mean nothing
    /// on a container sat beside a dozen that mean nothing on a header, with nothing saying which was
    /// which, and three guards written one at a time as each member was found to be wrong.
    ///
    /// <para><b>This is the rule the next member is checked against</b>, which is the part that was
    /// actually missing. Every public member has to be named in exactly one of these three sets, so
    /// adding one without deciding what a header answers for it fails a test instead of being
    /// discovered by looking at the window — which is how <see cref="AnyUp"/> was found.</para>
    /// </remarks>
    public static class Members
    {
        /// <summary>What only a container answers. A header returns each of these empty.</summary>
        public static readonly IReadOnlySet<string> OfAContainer =
            new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(Image), nameof(State), nameof(Status), nameof(Ports), nameof(Service),
                nameof(DependsOn), nameof(ExitCode), nameof(StateEvidence), nameof(IsRunning),
                nameof(IsLive), nameof(ImageIsGone),
                nameof(ImageNote), nameof(ImageEvidence),

                // DD297: a header has no ports to chip, opens no standalone block and is described in
                // its project file rather than invited to be.
                nameof(PortChips), nameof(StartsStandalone), nameof(InvitesDetails),

                // DD324: a service leads with its state's shape, and the standalone block's count is
                // carried by the container that opens it.
                nameof(HasStateShape), nameof(ShapeInk), nameof(StandaloneCount),

                // DD326, DD327: a card's service row reads its first port, a standalone card all of them.
                nameof(FirstPort), nameof(PortsLine),

                // DD295: a header draws its project's details from its own Details, so the copy each
                // container carries for it is a container's alone.
                nameof(ProjectDetails),
                nameof(ProjectDetailsBeneath),

                // DD341: the state as the window writes it, failed or stopped where the daemon says exited.
                nameof(StateWord),
            };

        /// <summary>What only a header answers. A container returns each of these empty.</summary>
        public static readonly IReadOnlySet<string> OfAProject =
            new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(Running), nameof(Total), nameof(Collapsed), nameof(Chevron),
                nameof(ProjectCount), nameof(Failed),

                // DD298: only a header can be a tier, and only a tier counts problems.
                nameof(IsTier), nameof(Problems),

                // DD298: a project's links, and the menu that holds them once there are many.
                nameof(ProjectLinks), nameof(HasLinkMenu), nameof(PinnedLinks), nameof(LinkMenuLabel),
            };

        /// <summary>
        /// What both answer, each in its own terms.
        /// </summary>
        /// <remarks>
        /// The interesting set, and the one worth reading before adding a member: these are where a
        /// header is not a container with the fields blanked out but a row with an answer of its own.
        /// <see cref="AnyUp"/> is the shape — a container reads its state and a header counts its
        /// children — and <see cref="ShellReason"/>, <see cref="PrimaryVerb"/> and
        /// <see cref="Indent"/> each differ the same way.
        /// </remarks>
        public static readonly IReadOnlySet<string> OfBoth =
            new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(Name), nameof(Id), nameof(Project), nameof(IsProject), nameof(IsContainer),
                nameof(Title), nameof(NameEvidence),

                // DD294 and DD295: a container's details or a project's, drawn by the same tile.
                nameof(Details), nameof(DetailsBeneath), nameof(DetailKey), nameof(WorkingDir),
                nameof(ConfigFiles), nameof(Description), nameof(HasDescription), nameof(HasTile),
                nameof(TileFill), nameof(TileGlyph), nameof(TilePicture), nameof(TileLetter),
                nameof(Indent), nameof(Pending), nameof(Failure), nameof(IsPending),
                nameof(HasFailure), nameof(AnyUp), nameof(CanStart), nameof(CanStop),
                nameof(CanRestart), nameof(CanRemove), nameof(CanShell), nameof(ShellReason),
                nameof(Tone), nameof(PrimaryVerb), nameof(HasPrimary), nameof(LogsVisibility),

                // DD297: the header's count is a chip in the STATE column, in its project's tone, and
                // a header and a standalone container each have an Open of their own.
                nameof(ChipFill), nameof(ChipText), nameof(ChipWord), nameof(ChipShape),
                nameof(ChipEvidence), nameof(OpenLink), nameof(ShowsOpen), nameof(CanOpen),
                nameof(OpenLabel), nameof(OpenHost),

                // DD298: a tier header's own name, and the tier a container is drawn under.
                nameof(Tier),

                // DD299: a header's title opens its project's page, and a standalone container's its own.
                nameof(OpensDetail), nameof(TitleIsText),

                // DD324: both are drawn inside a group card, and where a description goes differs.
                nameof(InCard), nameof(CardStarts), nameof(CardEnds), nameof(CardCorners),
                nameof(CardEdges), nameof(CardGap), nameof(SpansDescription), nameof(NameSpan),
                nameof(TitleAlignment),

                // DD328: the rail draws a project and a standalone container alike.
                nameof(ToneInk), nameof(RailNote), nameof(IsRailSelected),
            };
    }

    /// <summary>The headings this list sorts on, spelled once so markup and code cannot disagree.</summary>
    public static class Columns
    {
        /// <summary>The container's name.</summary>
        public const string Name = "NAME";

        /// <summary>What it runs.</summary>
        public const string Image = "IMAGE";

        /// <summary>Running, exited, paused.</summary>
        public const string State = "STATE";

        /// <summary>The daemon's own sentence, which carries the duration.</summary>
        public const string Status = "STATUS";

        /// <summary>What it publishes.</summary>
        public const string Ports = "PORTS";
    }

    /// <summary>
    /// The order a container list opens in.
    /// </summary>
    /// <remarks>
    /// Running first, then alphabetical inside each group. The window is opened to act on something,
    /// and the things that can be stopped, shelled into or read are the running ones; a stopped
    /// container is usually looked up rather than scanned for. Creation order — what the daemon
    /// returns — answers neither question.
    ///
    /// <para>Stable either way: two rows never swap places unless their state actually changed, which
    /// matters on a list that redraws on every engine event.</para>
    /// </remarks>
    public const string DefaultColumn = Columns.State;

    /// <summary>Shape a list of rows: narrowed, then ordered.</summary>
    /// <param name="rows">What the daemon just returned.</param>
    /// <param name="shape">The sort and filter the page is holding.</param>
    /// <returns>The rows to draw.</returns>
    public static IReadOnlyList<ContainerRow> Shaped(
        IEnumerable<ContainerRow> rows, ListShape shape)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(shape);

        return Ordered(Kept(rows, shape), shape);
    }

    /// <summary>
    /// The same list, with each compose project gathered under a header of its own (DD106).
    /// </summary>
    /// <param name="rows">What the daemon just returned.</param>
    /// <param name="shape">The sort and filter the page is holding.</param>
    /// <param name="collapsed">The projects whose children are hidden.</param>
    /// <returns>Headers and rows, flat and in draw order.</returns>
    /// <remarks>
    /// <b>The filter runs before the grouping</b>, which is what keeps a header honest: a project
    /// whose containers all went produces no group at all, and a header with nothing under it is
    /// worse than no header. The project name is one of the fields the filter matches, so typing it
    /// keeps the whole project rather than emptying it.
    ///
    /// <para><b>The sort runs twice</b>, and on the same key both times: inside a project, and over
    /// the projects. A project is ordered by the child that sorts first under whatever column is
    /// being sorted — so with the default STATE sort a project holding something running sits with
    /// the running rows, which is where a reader looking for it would look. Ordering the projects
    /// alphabetically instead would put a stopped project above a running container and make the one
    /// column everybody scans stop meaning anything.</para>
    ///
    /// <para><b>A collapsed project keeps its header and drops its children</b>, so the count on the
    /// header is what is left saying anything about it. Which projects those are is the page's to
    /// hold, for DD37's reason: this list is rebuilt on every engine event, so a collapse living in
    /// the ListView would spring open while somebody was reading it.</para>
    /// </remarks>
    /// <param name="openTiers">
    /// The healthy tiers somebody opened, by <see cref="TierKey"/> (DD298); every other healthy tier
    /// starts folded. Omitted, none.
    /// </param>
    public static IReadOnlyList<ContainerRow> Grouped(
        IEnumerable<ContainerRow> rows,
        ListShape shape,
        IReadOnlySet<string> collapsed,
        IReadOnlySet<string>? openTiers = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(collapsed);
        openTiers ??= new HashSet<string>(StringComparer.Ordinal);

        var kept = Kept(rows, shape).ToList();

        // A unit is a whole project or a single loose container, and both are ordered by the row
        // that leads them — which is what makes one comparison serve both.
        var units = new List<(ContainerRow Leads, IReadOnlyList<ContainerRow> Draws)>();

        foreach (var row in kept.Where(row => row.Project is null))
        {
            units.Add((row, [row]));
        }

        foreach (var project in kept
            .Where(row => row.Project is not null)
            .GroupBy(row => row.Project!, StringComparer.Ordinal))
        {
            var children = Ordered(project, shape);
            var header = ProjectHeader(project.Key, children, collapsed.Contains(project.Key));
            units.Add((children[0], collapsed.Contains(project.Key)
                ? [header]
                : [header, .. Tiered(project.Key, children, openTiers)]));
        }

        // Ordered by the leading row and never by the header: a header carries no state, no status
        // and no ports, so sorting on one would file every project under the empty string.
        var order = Ordered(units.Select(unit => unit.Leads), shape);
        var at = order.Select((row, index) => (row.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);
        var sorted = units
            .OrderBy(unit => at.TryGetValue(unit.Leads.Id, out var index) ? index : int.MaxValue)
            .ToList();

        // DD297: the projects first, then the containers started without compose in a block of their
        // own, each half in the sort's order. The block has a heading only where there is a project
        // above it to be told apart from; a machine running no compose project is the flat list it
        // always was.
        //
        // DD324: and each of those is one card, a project's header with every row drawn under it, so
        // the group reads as one thing rather than loose rows under a strip. The standalone block is
        // a card only where it has a heading, for the same reason it has one.
        var projects = sorted.Where(unit => unit.Leads.Project is not null)
            .SelectMany(unit => Carded(unit.Draws))
            .ToList();
        var standalone = sorted.Where(unit => unit.Leads.Project is null).Select(unit => unit.Leads).ToList();
        if (projects.Count > 0 && standalone.Count > 0)
        {
            var running = standalone.Count(row => row.IsRunning).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var total = standalone.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            standalone = [.. Carded(standalone)];
            standalone[0] = standalone[0] with
            {
                StartsStandalone = true,
                StandaloneCount = $"{running} of {total} running",
            };
        }

        return [.. projects, .. standalone];
    }

    /// <summary>Rows drawn as one card: the first opens it and the last closes it (DD324).</summary>
    private static IEnumerable<ContainerRow> Carded(IReadOnlyList<ContainerRow> rows) =>
        rows.Select((row, index) => row with
        {
            InCard = true,
            CardStarts = index == 0,
            CardEnds = index == rows.Count - 1,
        });

    /// <summary>The key a tier's fold is remembered by: its project and its name.</summary>
    /// <param name="project">The project.</param>
    /// <param name="tier">The tier.</param>
    /// <returns>The key.</returns>
    public static string TierKey(string project, string tier) => project + "\u001f" + tier;

    /// <summary>The project a <see cref="TierKey"/> belongs to.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The project.</returns>
    public static string ProjectOfTierKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var at = key.IndexOf('\u001f', StringComparison.Ordinal);
        return at < 0 ? key : key[..at];
    }

    /// <summary>
    /// A project's containers split into its <c>freeharbor.group</c> tiers, each under a header of its
    /// own (DD298).
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="children">Its shown containers, already in the sort's order.</param>
    /// <param name="openTiers">The healthy tiers somebody opened.</param>
    /// <returns>The rows to draw under the project's header.</returns>
    /// <remarks>
    /// <b>No label, no tiers</b>: a project nobody grouped is the plain list, so this costs nothing
    /// where it is not asked for. A container left out of a grouped project goes in a last
    /// <see cref="OtherTier"/>, because hiding it would be worse than an untidy name.
    ///
    /// <para><b>Tiers in first-seen order, by container name</b>, and never by the sort: a tier that
    /// moved each time a service stopped would make the one stable landmark on a large project move.
    /// </para>
    ///
    /// <para><b>A healthy tier starts folded and a problem is never folded away.</b> The fold that is
    /// remembered is the opposite of the default, the tiers somebody opened, so a tier that goes bad
    /// opens on its own and one that recovers goes back to whatever the user last chose.</para>
    /// </remarks>
    private static IReadOnlyList<ContainerRow> Tiered(
        string project, IReadOnlyList<ContainerRow> children, IReadOnlySet<string> openTiers)
    {
        if (!children.Any(child => child.Details?.Group is not null))
        {
            return children;
        }

        static string TierOf(ContainerRow row) => row.Details?.Group ?? OtherTier;

        var names = children
            .OrderBy(child => child.Name, StringComparer.Ordinal)
            .Select(TierOf)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => string.Equals(name, OtherTier, StringComparison.Ordinal) ? 1 : 0)
            .ToList();

        var drawn = new List<ContainerRow>();
        foreach (var name in names)
        {
            var members = children
                .Where(child => string.Equals(TierOf(child), name, StringComparison.Ordinal))
                .Select(child => child with { Tier = name })
                .ToList();
            var problems = members.Count(IsProblem);
            var folded = problems == 0 && !openTiers.Contains(TierKey(project, name));

            var titles = members.Select(member => member.Title).ToList();
            var count = members.Count == 1
                ? "1 container"
                : $"{members.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} containers";
            var summary = count + " · " + string.Join(", ", titles.Take(3)) + (titles.Count > 3 ? "…" : "");

            drawn.Add(new ContainerRow(name, "", "", "", [], ProjectId(project) + "/tier:" + name)
            {
                IsProject = true,
                IsTier = true,
                Tier = name,
                Project = project,
                Details = new ResolvedDetails(
                    name, summary, new DetailIcon(IconKind.Monogram, ""), 0, [], null, name),
                Running = members.Count(member => member.IsRunning),
                Total = members.Count,
                Failed = members.Count(member => member.Tone == RowTone.Bad),
                Problems = problems,
                Collapsed = folded,
            });

            if (!folded)
            {
                drawn.AddRange(members);
            }
        }

        return drawn;
    }

    /// <summary>Whether a container passes the state chip the page is holding (DD300).</summary>
    /// <param name="row">A container.</param>
    /// <param name="chip">The chip.</param>
    /// <returns>Whether it is drawn.</returns>
    /// <remarks>
    /// Stopped is everything not up, failures included, so Running and Stopped split the machine in
    /// two; Problems is the subset of it somebody has to look at, which is the same rule a tier folds
    /// by (DD298).
    /// </remarks>
    public static bool Passes(ContainerRow row, StateChip chip)
    {
        ArgumentNullException.ThrowIfNull(row);
        return chip switch
        {
            StateChip.Running => row.IsRunning,
            StateChip.Stopped => !row.IsLive,
            StateChip.Problems => IsProblem(row),
            _ => true,
        };
    }

    /// <summary>The line beside the chips: how many projects, standalone containers and running ones.</summary>
    /// <param name="rows">Every container in hand.</param>
    /// <returns>For instance <c>2 projects · 3 standalone · 9 of 14 running</c>.</returns>
    public static string Counts(IReadOnlyList<ContainerRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        static string N(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var projects = rows.Select(row => row.Project).Where(project => project is not null).Distinct(StringComparer.Ordinal).Count();
        var standalone = rows.Count(row => row.Project is null);
        return $"{N(projects)} {(projects == 1 ? "project" : "projects")} · {N(standalone)} standalone · "
            + $"{N(rows.Count(row => row.IsRunning))} of {N(rows.Count)} running";
    }

    /// <summary>Whether a container is a problem: not running, and not a clean exit either.</summary>
    private static bool IsProblem(ContainerRow row) =>
        row.IsContainer && !row.IsRunning && row.Tone is RowTone.Bad or RowTone.Warn;

    /// <summary>
    /// Give a project header the wait its containers are actually under (DD108).
    /// </summary>
    /// <param name="header">The header, already carrying whatever is known about it by id.</param>
    /// <param name="rows">Every container in hand, dressed.</param>
    /// <returns>The header, waiting exactly as long as something under it is.</returns>
    /// <remarks>
    /// DD8's rule is that the event confirms an action — a 204 from <c>/stop</c> means the daemon
    /// took the call, not that the container is down. Every container row obeys it and the header
    /// could not: it has no event of its own, so DD107 ended its wait when the last call was
    /// answered and the header stopped saying <c>Stopping…</c> while all four rows under it went on
    /// saying it for the daemon's grace period.
    ///
    /// <para><b>Derived rather than stored</b>, which is what makes it right without a timer and
    /// without the header needing an event: the children already end their own waits on the events
    /// that end them, so a header that simply reads them is correct whenever they are. It also
    /// cannot hang — a container that will never emit an event is one whose own row is stuck too,
    /// and that is a defect with one cause rather than two symptoms.</para>
    ///
    /// <para><b>One container clicked on its own counts.</b> The header reports on the group, and
    /// something in the group is in flight — so it says so, in that child's own word.</para>
    /// </remarks>
    public static ContainerRow WithProjectWait(ContainerRow header, IEnumerable<ContainerRow> rows)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(rows);

        if (!header.IsProject)
        {
            return header;
        }

        var waits = rows
            .Where(row => row.IsContainer
                && string.Equals(row.Project, header.Project, StringComparison.Ordinal)

                // DD298: a tier waits only for what is in it.
                && (!header.IsTier || string.Equals(row.Details?.Group ?? OtherTier, header.Tier, StringComparison.Ordinal)))
            .Select(row => row.Pending)
            .Where(pending => pending is not null)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();

        return waits.Count switch
        {
            0 => header,
            1 => header with { Pending = waits[0] },
            _ => header with { Pending = ContainerAction.MixedLabel },
        };
    }

    /// <summary>
    /// The folded-away projects that the daemon still has (DD112).
    /// </summary>
    /// <param name="folded">The projects somebody folded away.</param>
    /// <param name="rows">Every container the daemon just returned.</param>
    /// <returns>The same set, without the projects that are gone.</returns>
    /// <remarks>
    /// <b>A project's absence forgets its fold, and that was a decision rather than an omission.</b>
    /// The argument for the other answer is real — somebody who folds <c>shop</c> away is saying
    /// they are not working on <c>shop</c>, and L7 says the window remembers what the user set on
    /// purpose. What settles it is that bringing a project up is an explicit act whose whole point is
    /// to run it: a header that appears already folded hides the very thing that was just asked for,
    /// and the fold it is honouring was set on a different instance of the project. L7's subjects —
    /// a window size, a sort column — have no lifecycle, and a project does.
    ///
    /// <para>It also stops the set growing for the life of the window, which is the smaller half.</para>
    ///
    /// <para><b>Only where the daemon actually answered.</b> This is called beside
    /// <c>RowActivity.Prune</c> and for its reason: an engine that went away returns no list, so
    /// nothing on that path is evidence a project is gone, and narrowing against it would forget
    /// every fold in hand on the strength of one refused call.</para>
    /// </remarks>
    public static HashSet<string> FoldedStillThere(
        IReadOnlySet<string> folded, IEnumerable<ContainerRow> rows)
    {
        ArgumentNullException.ThrowIfNull(folded);
        ArgumentNullException.ThrowIfNull(rows);

        var present = rows
            .Select(row => row.Project)
            .Where(project => project is not null)
            .ToHashSet(StringComparer.Ordinal);

        return [.. folded.Where(present.Contains)];
    }

    /// <summary>Narrow a list to what the filter keeps.</summary>
    private static IEnumerable<ContainerRow> Kept(IEnumerable<ContainerRow> rows, ListShape shape) =>
        rows.Where(row => shape.Keeps(
            row.Name, row.Image, row.State, row.Status,
            // The chip's :8080 and the 8080->80/tcp it maps (DD325), so what used to match still does.
            string.Join(" ", row.Ports.Select(port => port.Text + " " + port.Mapping)),

            // DD106: typing a project's name keeps the project. Without it the one word a reader
            // knows the group by is the one word that empties the list.
            row.Project,

            // DD294: the title and the line under it are what the row now shows, and a filter that
            // could not find the words on screen would read as broken.
            row.Title, row.Description,

            // DD298: the tier a service is drawn under is a word on screen too.
            row.Details?.Group));

    /// <summary>Order a list under the shape's column.</summary>
    private static IReadOnlyList<ContainerRow> Ordered(
        IEnumerable<ContainerRow> kept, ListShape shape)
    {
        // Name is the tie-break under every column, and it is always ascending: a redraw must not
        // reshuffle rows that compare equal on whatever is being sorted, and flipping the direction
        // of the sorted column is not a reason to flip the tie-break under it.
        IOrderedEnumerable<ContainerRow> ordered = shape.Column switch
        {
            Columns.Image => By(kept, r => r.Image, shape.Descending, StringComparer.OrdinalIgnoreCase),
            Columns.Status => By(kept, r => r.Status, shape.Descending, StringComparer.OrdinalIgnoreCase),

            // Published before exposed-only before none, so "which of these can I open" is the
            // question this column answers.
            Columns.Ports => By(kept, r => r.Ports.Count == 0 ? "" : r.Ports[0].Text, shape.Descending,
                StringComparer.OrdinalIgnoreCase),

            // Running before anything else, which is what makes this the default rather than a
            // straight alphabetical sort on the word.
            Columns.State => By(kept, r => r.IsRunning ? 0 : r.IsLive ? 1 : 2, shape.Descending),
            // By what the cell shows (DD294): sorting on the container's name under a column of
            // titles would read as no sort at all.
            _ => By(kept, r => r.Title, shape.Descending, StringComparer.OrdinalIgnoreCase),
        };

        return [.. ordered.ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static IOrderedEnumerable<ContainerRow> By<TKey>(
        IEnumerable<ContainerRow> rows,
        Func<ContainerRow, TKey> key,
        bool descending,
        IComparer<TKey>? comparer = null) =>
        descending
            ? rows.OrderByDescending(key, comparer)
            : rows.OrderBy(key, comparer);

    /// <summary>Project one summary.</summary>
    /// <param name="container">What the API returned.</param>
    /// <returns>The row.</returns>
    public static ContainerRow From(ContainerSummary container) => From(container, null);

    /// <summary>Project one summary, reading its project's files (DD295).</summary>
    /// <param name="container">What the API returned.</param>
    /// <param name="files">
    /// What a project's own files say, first file first, given the folder compose ran in and the
    /// compose files it read; or <see langword="null"/> to read none. A test passes what it wants.
    /// </param>
    /// <param name="saved">What was saved in FreeHarbor (DD296), read before any file; or none.</param>
    /// <returns>The row.</returns>
    public static ContainerRow From(
        ContainerSummary container,
        Func<string?, string?, IReadOnlyList<ProjectFileDetails>>? files,
        DetailStore? saved = null)
    {
        ArgumentNullException.ThrowIfNull(container);

        // Deduplicated the same way the list renders them: a port published on both address
        // families comes back twice and would otherwise be two identical cells.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ports = new List<PortLink>();
        foreach (var port in container.Ports)
        {
            var link = PortLink.From(port);
            if (seen.Add(link.Mapping))
            {
                ports.Add(link);
            }
        }

        // Already on the list response, which is the whole reason the hierarchy costs no second call
        // (DD106). Blank is treated as absent: a label present and empty names no project, and a
        // group headed by the empty string is a group nobody can read.
        var project = Label(container, Core.Agent.ContextPack.ProjectLabel);
        var service = Label(container, Core.Agent.ContextPack.ServiceLabel);

        // DD295: the project's own files, found from the folder compose stamped on the container.
        var workingDir = Label(container, ProjectFile.WorkingDirLabel);
        var configFiles = Label(container, ComposeExtension.ConfigFilesLabel);
        IReadOnlyList<ProjectFileDetails> read = project is null || files is null
            ? []
            : files(workingDir, configFiles);

        // DD296: the key a save lands under, which is also the first layer read back.
        var key = project is not null && service is not null
            ? DetailStore.ServiceKey(project, service)
            : DetailStore.ContainerKey(container.DisplayName);

        var fromFiles = service is null
            ? []
            : read
                .Where(file => file.Services.ContainsKey(service))
                .Select(file => new DetailLayer(file.Source, file.Services[service]))
                .ToList();
        var layers = saved?.For(key) is { } mine
            ? [new DetailLayer(ContainerDetails.EditedHere, mine), .. fromFiles]
            : fromFiles;

        var projectFromFiles = read.Select(file => new DetailLayer(file.Source, file.Project)).ToList();
        var projectLayers = project is not null && saved?.For(DetailStore.ProjectKey(project)) is { } mineToo
            ? [new DetailLayer(ContainerDetails.EditedHere, mineToo), .. projectFromFiles]
            : projectFromFiles;

        var details = ContainerDetails.Resolve(container, layers: layers);
        return new ContainerRow(
            container.DisplayName,
            container.ImageName,
            container.State,
            container.Status,
            ports,
            container.Id)
        {
            Project = project,
            Details = details,
            DetailsBeneath = ContainerDetails.Resolve(container, layers: fromFiles),
            ProjectDetails = project is null
                ? null
                : ContainerDetails.ResolveProject(project, projectLayers),
            ProjectDetailsBeneath = project is null
                ? null
                : ContainerDetails.ResolveProject(project, projectFromFiles),
            DetailKey = key,
            WorkingDir = workingDir,
            ConfigFiles = configFiles,
            ImageIsGone = container.ImageIsGone,
            Service = service,
            OpenLink = Openable(details, ports),
            DependsOn = ComposeOrder.DependenciesIn(
                Label(container, ComposeOrder.DependsOnLabel)),
        };
    }

    /// <summary>
    /// The link Open follows: the resolved primary, unless it was made from a port that does not
    /// speak HTTP.
    /// </summary>
    /// <remarks>
    /// The resolver turns every published TCP port into a link, because it has no list of which
    /// ports are HTTP (DD294). A port the row already draws as a copy chip (DD297) is not one to
    /// open in a browser, so Open goes to the first port that is a link, or nowhere. A link somebody
    /// named, in a label or a project file, is taken as given.
    /// </remarks>
    private static DetailLink? Openable(ResolvedDetails details, IReadOnlyList<PortLink> ports)
    {
        if (details.LinksSource is not "published ports")
        {
            return details.Primary;
        }

        var http = ports.Where(port => port.IsLink).Select(port => port.Url!).ToHashSet(StringComparer.Ordinal);
        return details.Primary is { } primary && http.Contains(primary.Url)
            ? primary
            : details.Links.FirstOrDefault(link => link.Served && http.Contains(link.Url));
    }

    /// <summary>One label, or nothing where it is absent or blank.</summary>
    private static string? Label(ContainerSummary container, string name) =>
        container.Labels is { } labels
        && labels.TryGetValue(name, out var value)
        && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}

/// <summary>The state chips over the containers page, shared by its list and its cards (DD300).</summary>
public enum StateChip
{
    /// <summary>Every container.</summary>
    All,

    /// <summary>Only what is running.</summary>
    Running,

    /// <summary>Only what is not up, cleanly exited or not.</summary>
    Stopped,

    /// <summary>Only what stopped and did not mean to, or is stuck in between.</summary>
    Problems,
}

/// <summary>What the window says when the list is empty.</summary>
/// <param name="Headline">One line, large.</param>
/// <param name="Detail">One sentence under it.</param>
/// <param name="OffersStart">Whether the empty state should offer to start the engine.</param>
public sealed record EmptyState(string Headline, string Detail, bool OffersStart)
{
    /// <summary>
    /// The empty state for an engine in <paramref name="engine"/>.
    /// </summary>
    /// <remarks>
    /// Empty is a designed state and not a blank grid. The first screen a new user sees is usually
    /// this one, and a table with headers and nothing under them is where a free alternative loses
    /// them — so the two reasons a list is empty say different things, and only one of them is
    /// something the user can act on.
    /// </remarks>
    /// <param name="engine">What the engine is doing.</param>
    /// <returns>What to show.</returns>
    public static EmptyState For(EngineState engine) => engine switch
    {
        EngineState.Running => new EmptyState(
            "No containers",
            "The engine is running and has nothing to show. Start one and it appears here.",
            OffersStart: false),

        EngineState.Starting => new EmptyState(
            "Starting the engine",
            "This takes a few seconds while WSL2 boots the distribution.",
            OffersStart: false),

        _ => new EmptyState(
            "The engine is not running",
            "Start it to see your containers.",
            OffersStart: true),
    };
}
