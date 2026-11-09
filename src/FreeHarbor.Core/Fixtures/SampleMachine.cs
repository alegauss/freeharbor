using System.Globalization;
using FreeHarbor.Core.Api;

namespace FreeHarbor.Core.Fixtures;

/// <summary>
/// A machine that is always there, so a window can be looked at without one running (DD38).
/// </summary>
/// <remarks>
/// <b>Chosen to cover the states, not to look plausible.</b> Running and exited, a published port and
/// an exposed-only one, a kill with its exit code beside a clean exit, a dangling image beside an
/// in-use one, an anonymous volume beside a named one a container still holds, and a volume large
/// enough that the size column says something. Each row is here because something in the window
/// renders differently for it; a row that changed no pixel would be a row nobody could justify keeping
/// correct.
///
/// <para><b>And the four projects the redesign's boards draw (DD329).</b> Storefront, all running,
/// with an <c>x-freeharbor</c> block under its <c>.freeharbor</c> file; Search lab, one service killed
/// with 143; Team wiki, stopped; and Platform, twenty services in four <c>freeharbor.group</c> tiers
/// with two failures and ten named links. Between them they carry every source the resolver reads (a
/// service with <c>freeharbor.*</c> labels, one with Homepage's, one with only an image label, one
/// with nothing) and a standalone container with a name docker made up, which are the rows spec
/// section 10 lists. Icons are bundled names only, because a file icon would name a path no
/// capturing machine has.</para>
///
/// <para><b>Every name begins with <see cref="Prefix"/>.</b> A screenshot of this in a README should be
/// obviously a fixture. The alternative — plausible names — is how somebody's real project ends up in
/// documentation, which is half of why this exists.</para>
///
/// <para><b>Writes refuse, and say so in the engine's voice.</b> A preview whose buttons deleted things
/// would be a preview you have to be careful with, and the refusal line under a row is one of the
/// states worth seeing anyway. So every write here throws the same
/// <see cref="DockerApiException"/> the daemon's refusal arrives as.</para>
/// </remarks>
public sealed class SampleMachine : IEngineClient
{
    /// <summary>What every name here starts with, so a capture is never mistaken for a real machine.</summary>
    public const string Prefix = "sample-";

    /// <summary>What a write answers with, in the shape a refusal arrives in.</summary>
    public const string ReadOnly = "this is a fixture: nothing here is a real container";

    /// <summary>
    /// The engine this fixture pretends to be, pinned so a capture of the About page is the same
    /// picture on every machine (DD83).
    /// </summary>
    /// <remarks>
    /// The manifest's own engine version, so the fixture and the thing this install would place do
    /// not disagree in a screenshot somebody reads as documentation.
    /// </remarks>
    public static EngineVersion Version { get; } = new()
    {
        Version = Engine.EngineManifest.Current.Engine.Version,
        ApiVersion = "1.55",
        MinApiVersion = "1.24",
        Os = "linux",
        Arch = "amd64",
    };

    /// <inheritdoc/>
    public Task<EngineVersion> VersionAsync(CancellationToken cancellation = default) =>
        Task.FromResult(Version);

    /// <summary>
    /// One container of the sample machine.
    /// </summary>
    /// <param name="id">Its id.</param>
    /// <param name="name">Its name, without the leading slash the daemon adds.</param>
    /// <param name="image">What it runs.</param>
    /// <param name="state">One word.</param>
    /// <param name="status">The daemon's own sentence.</param>
    /// <param name="ports">What it publishes or exposes.</param>
    /// <param name="details">
    /// Labels a screen reads its title, description and icon from (DD294), or nothing.
    /// </param>
    /// <returns>The container, outside any compose project.</returns>
    private static ContainerSummary Container(
        string id, string name, string image, string state, string status,
        IReadOnlyList<PortBinding>? ports = null, Dictionary<string, string>? details = null) => new()
    {
        Id = id,
        Names = ["/" + name],
        Image = image,
        ImageId = IdOf(image),
        State = state,
        Status = status,
        Ports = ports ?? [],
        Labels = details,
    };

    /// <summary>One service of a compose project, named the way compose names its container.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="project">The project it belongs to.</param>
    /// <param name="service">The service, which with the project makes the container's name.</param>
    /// <param name="image">What it runs.</param>
    /// <param name="state">One word.</param>
    /// <param name="status">The daemon's own sentence.</param>
    /// <param name="ports">What it publishes or exposes.</param>
    /// <param name="dependsOn">
    /// The services it waits for (DD113). Spelled the way compose spells it — <c>&lt;service&gt;:
    /// &lt;condition&gt;:&lt;restart&gt;</c> — because a fixture that simplified the format would
    /// exercise a parser this project does not ship.
    /// </param>
    /// <param name="details">Labels a screen reads its details from (DD294), or nothing.</param>
    /// <returns>The container.</returns>
    private static ContainerSummary Service(
        string id, ComposeProject project, string service, string image, string state, string status,
        IReadOnlyList<PortBinding>? ports = null, string? dependsOn = null,
        Dictionary<string, string>? details = null) =>
        Container(id, $"{project.Name}-{service}-1", image, state, status, ports) with
        {
            Labels = Merged(ComposeLabels(project, service, dependsOn), details),
        };

    /// <summary>Labels, written as pairs so a row reads as what it carries.</summary>
    private static Dictionary<string, string> Labels(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static Dictionary<string, string>? Merged(
        Dictionary<string, string>? compose, Dictionary<string, string>? details)
    {
        if (details is null)
        {
            return compose;
        }

        var labels = compose ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in details)
        {
            labels[key] = value;
        }

        return labels;
    }

    /// <summary>
    /// Which image a tag names here, resolved the way the daemon resolves it (DD167).
    /// </summary>
    /// <remarks>
    /// The images page joins on image id and nothing else, and no container here carried one — so
    /// every image read as held by nobody, the USED BY column was blank in every capture, and the
    /// test named for the in-use half asserted only the dangling one.
    ///
    /// <para>A reference this machine has no image for resolves to itself, which is exactly what the
    /// daemon does once a tag is rebuilt or removed under a running container: it has only the
    /// digest left to report. That is the state DD167 draws, and one row here is in it.</para>
    /// </remarks>
    /// <param name="image">The reference the container was created with.</param>
    /// <returns>The image's id, or the reference itself where nothing answers to it.</returns>
    private static string IdOf(string image) =>
        Tagged.TryGetValue(image, out var id) ? id : image;

    /// <summary>What the project's own service images are called.</summary>
    private const string ApiImage = "sample/api:1.4.2";

    /// <summary>The database's image.</summary>
    private const string DbImage = "postgres:16-alpine";

    /// <summary>The cache's image.</summary>
    private const string CacheImage = "redis:7-alpine";

    // DD329: the images the other projects run. Upstream names where the bundled icon is the point,
    // because the resolver reads the icon off the image's name, and sample/ where it is not.
    private const string NodeImage = "node:22-alpine";
    private const string SearchImage = "elasticsearch:8.15.0";
    private const string IndexerImage = "sample/indexer:0.9";
    private const string WikiImage = "wordpress:6.6";
    private const string WikiDbImage = "mariadb:11";
    private const string AlpineImage = "alpine:3.20";
    private const string GatewayImage = "traefik:3.1";
    private const string PlatformImage = "sample/platform:2.0";
    private const string QueueImage = "rabbitmq:3.13-management";
    private const string GrafanaImage = "grafana/grafana:11.2.0";
    private const string MailImage = "axllent/mailpit:v1.20";
    private const string BusyboxImage = "busybox:1.36";

    private const string Running = "running";
    private const string Exited = "exited";

    /// <summary>
    /// The digest of an image this machine no longer has (DD167).
    /// </summary>
    /// <remarks>
    /// Deliberately in no tag and in no image row: a container running on it is the state where the
    /// containers page has only a digest to show and the images page truthfully holds nothing in
    /// use, which without a note is the pair that reads as two lists disagreeing.
    /// </remarks>
    private const string GoneImage = "sha256:6666666666666666";

    /// <summary>The tags this machine's images answer to, and the id behind each.</summary>
    private static readonly Dictionary<string, string> Tagged = new(StringComparer.Ordinal)
    {
        [ApiImage] = "sha256:1111111111111111",
        [DbImage] = "sha256:2222222222222222",
        [CacheImage] = "sha256:3333333333333333",
        [NodeImage] = "sha256:7777777777777777",
        [SearchImage] = "sha256:8888888888888888",
        [IndexerImage] = "sha256:9999999999999999",
        [WikiImage] = "sha256:aaaaaaaaaaaaaaaa",
        [WikiDbImage] = "sha256:bbbbbbbbbbbbbbbb",
        [AlpineImage] = "sha256:cccccccccccccccc",
        [GatewayImage] = "sha256:dddddddddddddddd",
        [PlatformImage] = "sha256:eeeeeeeeeeeeeeee",
        [QueueImage] = "sha256:ffffffffffffffff",
        [GrafanaImage] = "sha256:1212121212121212",
        [MailImage] = "sha256:1313131313131313",
        [BusyboxImage] = "sha256:1414141414141414",
    };

    /// <summary>A compose project of the sample machine: its name and the folder it came up from.</summary>
    /// <param name="Name">The compose name, which begins every container's name.</param>
    /// <param name="Folder">The folder compose ran in, which exists on no machine.</param>
    /// <param name="ConfigFiles">The compose files it read, or nothing where none is named.</param>
    private sealed record ComposeProject(string Name, string Folder, string? ConfigFiles = null);

    /// <summary>
    /// Storefront. Still called <c>sample</c>, because that is the name the spec's capture command
    /// and every test before DD329 open it by.
    /// </summary>
    private static readonly ComposeProject Storefront =
        new("sample", ProjectFolder, ProjectFolder + @"\compose.yaml");

    private static readonly ComposeProject SearchLab = new(Prefix + "search", @"C:\Users\example\search-lab");
    private static readonly ComposeProject TeamWiki = new(Prefix + "wiki", @"C:\Users\example\wiki");
    private static readonly ComposeProject Platform = new(Prefix + "platform", @"C:\Users\example\platform");

    /// <summary>The compose labels a service's container carries.</summary>
    /// <param name="project">Its project.</param>
    /// <param name="service">The service.</param>
    /// <param name="dependsOn">What it waits for, or nothing.</param>
    /// <returns>The labels.</returns>
    private static Dictionary<string, string> ComposeLabels(
        ComposeProject project, string service, string? dependsOn)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["com.docker.compose.project"] = project.Name,
            ["com.docker.compose.service"] = service,
            [Details.ProjectFile.WorkingDirLabel] = project.Folder,
        };

        if (project.ConfigFiles is not null)
        {
            labels[Details.ComposeExtension.ConfigFilesLabel] = project.ConfigFiles;
        }

        // Absent rather than empty where a service waits for nothing, because that is what compose
        // writes — a fixture carrying an empty label would exercise a case no machine produces.
        if (dependsOn is not null)
        {
            labels["com.docker.compose.depends_on"] = dependsOn;
        }

        return labels;
    }

    /// <summary>The folder the sample project was brought up from, which exists on no machine.</summary>
    public const string ProjectFolder = @"C:\Users\example\shop";

    /// <summary>
    /// What each sample project's own files say (DD295), by folder, in the order the window reads
    /// them: <c>.freeharbor/project.yaml</c> first, then the compose file's <c>x-freeharbor</c> block.
    /// </summary>
    /// <param name="folder">The folder a container names as its working directory.</param>
    /// <param name="configFiles">Its compose files, which only Storefront names.</param>
    /// <returns>A project's details for its own folder, and nothing for any other.</returns>
    /// <remarks>
    /// Parsed from the text a user would write rather than built as records, so a capture exercises
    /// the readers as well as the screen. Storefront carries both files: its block names the project
    /// and the back office, and its <c>.freeharbor</c> file only renames the worker, which is the
    /// field-by-field override the two exist for. The other three carry the file alone.
    /// </remarks>
    public static IReadOnlyList<Details.ProjectFileDetails> ProjectFiles(string? folder, string? configFiles)
    {
        if (folder is null || !Files.TryGetValue(folder, out var files))
        {
            return [];
        }

        var read = new List<Details.ProjectFileDetails>
        {
            Details.ProjectFile.Parse(files.Yaml, Path.Combine(folder, Details.ProjectFile.Folder)),
        };

        if (files.Compose is not null && Details.ComposeExtension.Parse(files.Compose, folder) is { } block)
        {
            read.Add(block);
        }

        return read;
    }

    /// <summary>Each project folder's <c>project.yaml</c>, and what <c>compose config</c> answers for it.</summary>
    private static readonly Dictionary<string, (string Yaml, string? Compose)> Files =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ProjectFolder] = (
                """
                services:
                  worker:
                    title: Order worker
                    description: Charges cards and sends receipts
                """,
                """
                {
                  "name": "sample",
                  "services": {},
                  "x-freeharbor": {
                    "title": "Storefront",
                    "description": "Shop API, admin panel and the Postgres behind them",
                    "icon": "shop",
                    "services": {
                      "admin": { "title": "Back office", "description": "Refunds, stock and staff accounts" }
                    }
                  }
                }
                """),
            [SearchLab.Folder] = (
                """
                title: Search lab
                description: Search engine with a sample index and its indexer
                icon: search
                """,
                null),
            [TeamWiki.Folder] = (
                """
                title: Team wiki
                description: WordPress with its database and a nightly backup
                icon: wordpress
                """,
                null),
            [Platform.Folder] = (
                """
                title: Platform
                description: Twenty services behind one gateway, in four tiers
                icon: traefik
                services:
                  api-gateway:
                    title: Gateway
                """,
                null),
        };

    private static PortBinding Published(int host, int inside) => new()
    {
        Ip = "0.0.0.0", PublicPort = host, PrivatePort = inside, Type = "tcp",
    };

    private static PortBinding Exposed(int inside) => new()
    {
        PrivatePort = inside, Type = "tcp",
    };

    private readonly List<ContainerSummary> _containers =
    [
        // ---- Storefront: four services, all running, details from both project files ----------

        // Running with a published port and two named links (DD294), the primary one first, so Open
        // carries a name rather than reading a port.
        //
        // It also waits for db, and that is DD113: without a depends_on anywhere in this fixture,
        // `ComposeOrder` saw a project with no edges every time the fixture was used, took its
        // fallback and looked exactly as it would if it did not exist. Every other service waiting
        // on db is the smallest shape that orders differently from the list — stopping this project
        // reaches the rest before db rather than db first.
        Service("c1aaaaaaaaaa0000", Storefront, "api", ApiImage, Running,
            "Up 2 hours", [Published(8080, 8080)],
            dependsOn: "db:service_healthy:true",

            // DD294: this product's own labels, the first source the resolver reads.
            details: Labels(
                ("freeharbor.title", "Shop API"),
                ("freeharbor.description", "Orders, carts and checkout"),
                ("freeharbor.url.Site", "http://localhost:8080"),
                ("freeharbor.url.Admin", "http://localhost:8080/admin"),
                ("freeharbor.primary", "Site"))),

        // Running and healthy, so the status column carries something other than a duration. The
        // condition above is why it is healthy rather than merely up: a fixture whose db said
        // nothing about health would be one where the label it is depended on by made no sense.
        // 5432 does not answer HTTP, so its port is the chip that copies rather than opens.
        Service("c2bbbbbbbbbb0000", Storefront, "db", DbImage, Running,
            "Up 2 hours (healthy)", [Published(5432, 5432)],

            // DD294: Homepage's labels, the second source, so a machine labelled for that dashboard
            // reads the same here.
            details: Labels(
                ("homepage.name", "PostgreSQL"),
                ("homepage.description", "Orders and customers"))),

        // Titled by the compose file's x-freeharbor block and nothing else; its icon is node's.
        Service("c7aaaaaaaaaa0000", Storefront, "admin", NodeImage, Running,
            "Up 2 hours", dependsOn: "db:service_started:false"),

        // Renamed by the .freeharbor file, over a block that says nothing about it.
        Service("c3cccccccccc0000", Storefront, "worker", ApiImage, Running,
            "Up 2 hours", dependsOn: "db:service_started:false"),

        // ---- Search lab: one service killed, one with nothing, one with only an image label ----

        Service("c8aaaaaaaaaa0000", SearchLab, "engine", SearchImage, Running,
            "Up 40 minutes", [Published(2700, 9200)],
            details: Labels(
                ("freeharbor.title", "Search engine"),
                ("freeharbor.url.Console", "http://localhost:2700"),
                ("freeharbor.primary", "Console"))),

        // No label of any kind and an image with no bundled icon: the monogram and the bare name.
        // 143 is a SIGTERM that was not caught, the failure the detail page's banner is for.
        Service("c9aaaaaaaaaa0000", SearchLab, "indexer", IndexerImage, Exited,
            "Exited (143) 3 minutes ago", dependsOn: "engine:service_started:false"),

        // Exposed but not published: the port that is text rather than a link.
        Service("c4dddddddddd0000", SearchLab, "cache", CacheImage, Running,
            "Up 40 minutes", [Exposed(6379)],

            // DD294: only the image's own OCI label, the third source, which docker copies onto
            // every container made from the image.
            details: Labels(("org.opencontainers.image.title", "Session cache"))),

        // ---- Team wiki: stopped, every service a clean exit -----------------------------------

        // A clean exit, which reads differently from a kill and shares a row shape with it. The
        // named link stays when the container stops, so the project's Open is drawn and disabled.
        Service("c5eeeeeeeeee0000", TeamWiki, "wordpress", WikiImage, Exited,
            "Exited (0) 20 hours ago", dependsOn: "db:service_started:false",
            details: Labels(("freeharbor.url.Site", "http://localhost:8090"))),
        Service("caaaaaaaaaaa0000", TeamWiki, "db", WikiDbImage, Exited, "Exited (0) 20 hours ago"),
        Service("cbaaaaaaaaaa0000", TeamWiki, "backup", AlpineImage, Exited, "Exited (0) 20 hours ago"),

        // ---- Platform: twenty services in four tiers, two failures, ten links -----------------

        .. PlatformServices(),

        // ---- standalone -----------------------------------------------------------------------

        // Running on an image this machine no longer has (DD167). The only row whose image column
        // is a digest and the only one carrying a note, which is the whole state: the images page
        // holds nothing for it, and without the note the two pages read as disagreeing. Outside a
        // project, because a rebuilt tag is not a compose problem and grouping it under one would
        // suggest it is.
        Container("c6ffffffffff0000", Prefix + "legacy-1", GoneImage, Running,
            "Up 3 days", ports: [Exposed(9000)]),

        // A standalone container with details and its own Open, which the board draws as a row
        // with a tile and an Inbox button.
        Container("ccaaaaaaaaaa0000", Prefix + "mailpit", MailImage, Running,
            "Up 3 days", [Published(8025, 8025), Published(1025, 1025)],
            Labels(
                ("freeharbor.title", "Mail catcher"),
                ("freeharbor.description", "Catches outgoing mail"),
                ("freeharbor.url.Inbox", "http://localhost:8025"))),

        // `docker run` with no --name: the adjective_scientist docker made up, behind the prefix, with
        // nothing to resolve, which is the row that says "No details yet".
        Container("cdaaaaaaaaaa0000", Prefix + "nervous_tesla", BusyboxImage, Running, "Up 5 minutes"),
    ];

    /// <summary>
    /// The large project (DD298): twenty services in four <c>freeharbor.group</c> tiers, eighteen
    /// running and two failed, with ten named links of which two are pinned.
    /// </summary>
    /// <returns>Its containers.</returns>
    /// <remarks>
    /// The gateway is the first linked service by name, which is what makes Gateway the project's
    /// Open. Admin and Grafana are pinned by their own services, so the header has a split Open, two
    /// buttons beside it and a menu of ten.
    /// </remarks>
    private static IEnumerable<ContainerSummary> PlatformServices()
    {
        var services = new (string Service, string Tier, string Image, string? Failed, (string Name, int Port)[] Links, bool Pinned)[]
        {
            ("api-gateway", "Edge", GatewayImage, null, [("Gateway", 8000), ("Docs", 8000), ("Health", 8000)], false),
            ("backoffice", "Edge", NodeImage, null, [("Admin", 8001)], true),
            ("catalog", "APIs", PlatformImage, null, [("Catalog", 8010)], false),
            ("cart", "APIs", PlatformImage, null, [], false),
            ("orders", "APIs", PlatformImage, null, [("Orders", 8012)], false),
            ("payments", "APIs", PlatformImage, null, [], false),
            ("users", "APIs", PlatformImage, null, [("Users", 8014)], false),
            ("inventory", "APIs", PlatformImage, null, [], false),
            ("pricing", "APIs", PlatformImage, null, [], false),
            ("reviews", "APIs", PlatformImage, null, [], false),
            ("shipping", "APIs", PlatformImage, null, [], false),
            ("search", "APIs", PlatformImage, null, [("Search", 8019)], false),
            ("payments-worker", "Workers", PlatformImage, "Exited (1) 2 minutes ago", [], false),
            ("notifications", "Workers", PlatformImage, "Exited (137) 40 seconds ago", [], false),
            ("search-indexer", "Workers", PlatformImage, null, [], false),
            ("mailer", "Workers", PlatformImage, null, [], false),
            ("postgres", "Data and tools", DbImage, null, [], false),
            ("redis", "Data and tools", CacheImage, null, [], false),
            ("queue", "Data and tools", QueueImage, null, [("Queue", 15672)], false),
            ("grafana", "Data and tools", GrafanaImage, null, [("Grafana", 3000)], true),
        };

        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Docs"] = "/docs",
            ["Health"] = "/healthz",
        };

        for (var i = 0; i < services.Length; i++)
        {
            var (service, tier, image, failed, links, pinned) = services[i];
            var labels = Labels(("freeharbor.group", tier));
            foreach (var (name, port) in links)
            {
                labels["freeharbor.url." + name] = string.Create(
                    CultureInfo.InvariantCulture, $"http://localhost:{port}{paths.GetValueOrDefault(name)}");
            }

            if (links.Length > 0)
            {
                labels["freeharbor.primary"] = links[0].Name;
            }

            if (pinned)
            {
                labels["freeharbor.pinned"] = links[0].Name;
            }

            yield return Service(
                string.Create(CultureInfo.InvariantCulture, $"d{i:x2}aaaaaaaaa0000"), Platform, service, image,
                failed is null ? Running : Exited, failed ?? "Up 2 hours", details: labels);
        }
    }

    private readonly List<ImageSummary> _images =
    [
        new() { Id = Tagged[ApiImage], RepoTags = [ApiImage], Size = 184320000 },
        new() { Id = Tagged[DbImage], RepoTags = [DbImage], Size = 247000000 },
        new() { Id = Tagged[CacheImage], RepoTags = [CacheImage], Size = 41200000 },
        new() { Id = Tagged[NodeImage], RepoTags = [NodeImage], Size = 158000000 },
        new() { Id = Tagged[SearchImage], RepoTags = [SearchImage], Size = 1290000000 },
        new() { Id = Tagged[IndexerImage], RepoTags = [IndexerImage], Size = 96000000 },
        new() { Id = Tagged[WikiImage], RepoTags = [WikiImage], Size = 702000000 },
        new() { Id = Tagged[WikiDbImage], RepoTags = [WikiDbImage], Size = 406000000 },
        new() { Id = Tagged[AlpineImage], RepoTags = [AlpineImage], Size = 7800000 },
        new() { Id = Tagged[GatewayImage], RepoTags = [GatewayImage], Size = 178000000 },
        new() { Id = Tagged[PlatformImage], RepoTags = [PlatformImage], Size = 221000000 },
        new() { Id = Tagged[QueueImage], RepoTags = [QueueImage], Size = 251000000 },
        new() { Id = Tagged[GrafanaImage], RepoTags = [GrafanaImage], Size = 453000000 },
        new() { Id = Tagged[MailImage], RepoTags = [MailImage], Size = 26000000 },
        new() { Id = Tagged[BusyboxImage], RepoTags = [BusyboxImage], Size = 4300000 },

        // Dangling, twice: enough for the prune button to have a number worth confirming.
        new() { Id = "sha256:4444444444444444", RepoTags = ["<none>:<none>"], Size = 183900000 },
        new() { Id = "sha256:5555555555555555", RepoTags = ["<none>:<none>"], Size = 96400000 },
    ];

    private readonly List<VolumeSummary> _volumes =
    [
        new() { Name = "sample_db-data", Driver = "local" },
        new() { Name = "sample_uploads", Driver = "local" },

        // Anonymous: the sixty-four hex characters `docker run -v /data` leaves behind, and the one
        // the prune button is actually for. Labelled the way the daemon labels it (DD169) — the
        // list reads that label in preference to the name's shape, and a fixture carrying only the
        // shape would exercise the fallback on every capture and the rule on none.
        new()
        {
            Name = "9f2c1d4b6a8e0f3c5d7b9a1e2f4c6d8b0a2e4f6c8d0b2a4e6f8c0d2b4a6e8f0c",
            Driver = "local",
            Labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["com.docker.volume.anonymous"] = "",
            },
        },
    ];

    /// <inheritdoc/>
    public Task<IReadOnlyList<ContainerSummary>> ContainersAsync(
        bool all = true, CancellationToken cancellation = default) =>
        Task.FromResult<IReadOnlyList<ContainerSummary>>(
            all ? _containers : [.. _containers.Where(c => c.State == "running")]);

    /// <inheritdoc/>
    public Task<ContainerInspect> InspectAsync(string id, CancellationToken cancellation = default) =>
        Task.FromResult(new ContainerInspect
        {
            Id = id,
            Name = "/" + (_containers.FirstOrDefault(c => c.Id == id)?.DisplayName ?? id),
            State = new ContainerState { Status = "running" },
            HostConfig = new ContainerHostConfig(),
            Mounts = [],
        });

    /// <summary>A log with something in it, including a line on stderr so the red one is visible.</summary>
    /// <remarks>
    /// Framed the way the daemon frames a log from a container with no TTY: an eight-byte header per
    /// chunk whose first byte is the stream. Faking the text without the frames would have made the
    /// preview prove the opposite of what it is for — the de-framing is part of what the log window is.
    /// </remarks>
    public Task<Stream> LogsAsync(
        string id,
        int tail = 2000,
        bool follow = true,
        bool timestamps = false,
        DateTimeOffset? since = null,
        CancellationToken cancellation = default)
    {
        var body = new MemoryStream();
        foreach (var (stream, line) in Lines)
        {
            var text = System.Text.Encoding.UTF8.GetBytes(line + "\n");
            var header = new byte[8];
            header[0] = stream;
            header[4] = (byte)(text.Length >> 24);
            header[5] = (byte)(text.Length >> 16);
            header[6] = (byte)(text.Length >> 8);
            header[7] = (byte)text.Length;
            body.Write(header);
            body.Write(text);
        }

        body.Position = 0;
        return Task.FromResult<Stream>(body);
    }

    private static readonly (byte Stream, string Line)[] Lines =
    [
        (1, "listening on :8080"),
        (1, "connected to postgres://sample-db-1:5432/sample"),
        (1, "GET /healthz 200 1ms"),
        (2, "warn: retrying migration 0004 (attempt 2)"),
        (1, "GET /api/orders 200 14ms"),
        (2, "java.net.BindException: Address already in use"),
        (1, "shutting down"),
    ];

    /// <inheritdoc/>
    public Task<IReadOnlyList<ImageSummary>> ImagesAsync(CancellationToken cancellation = default) =>
        Task.FromResult<IReadOnlyList<ImageSummary>>(_images);

    /// <inheritdoc/>
    public Task<IReadOnlyList<VolumeSummary>> VolumesAsync(CancellationToken cancellation = default) =>
        Task.FromResult<IReadOnlyList<VolumeSummary>>(_volumes);

    /// <summary>The same volumes, measured.</summary>
    /// <remarks>
    /// A separate answer because it is a separate call: the list is metadata and comes back at once,
    /// the sizes walk the filesystem. The window fills that column after the list is already up, and a
    /// fixture answering both from one payload would hide the shape of that.
    /// </remarks>
    public Task<IReadOnlyList<VolumeSummary>> VolumeSizesAsync(
        CancellationToken cancellation = default) =>
        Task.FromResult<IReadOnlyList<VolumeSummary>>(
        [
            new() { Name = "sample_db-data", Driver = "local",
                    UsageData = new VolumeUsage { Size = 412000000, RefCount = 1 } },
            new() { Name = "sample_uploads", Driver = "local",
                    UsageData = new VolumeUsage { Size = 1870000000, RefCount = 0 } },
            new() { Name = _volumes[2].Name, Driver = "local",
                    UsageData = new VolumeUsage { Size = 24000000, RefCount = 0 } },
        ]);

    /// <inheritdoc/>
    public Task StartContainerAsync(string id, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task StopContainerAsync(string id, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task RestartContainerAsync(string id, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task RemoveContainerAsync(
        string id, bool force = false, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task RemoveImageAsync(
        string id, bool force = false, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task RemoveVolumeAsync(string name, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task<ImagesPruned> PruneDanglingImagesAsync(CancellationToken cancellation = default) =>
        Refuse<ImagesPruned>();

    /// <inheritdoc/>
    public Task<VolumesPruned> PruneAnonymousVolumesAsync(CancellationToken cancellation = default) =>
        Refuse<VolumesPruned>();

    /// <summary>No shell, so the row says so rather than opening a terminal that closes.</summary>
    public Task<int> RunInContainerAsync(
        string id, IReadOnlyList<string> command, CancellationToken cancellation = default) =>
        Task.FromResult(1);

    /// <summary>How many containers this machine has, for a test that would otherwise count by hand.</summary>
    public int ContainerCount => _containers.Count;

    /// <summary>How many images, likewise.</summary>
    public int ImageCount => _images.Count;

    /// <summary>How many volumes, likewise.</summary>
    public int VolumeCount => _volumes.Count;

    /// <summary>What every published host port is, so a test can name one without a literal.</summary>
    public IReadOnlyList<int> PublishedPorts =>
        [.. _containers.SelectMany(c => c.Ports)
            .Where(p => p.PublicPort is > 0)
            .Select(p => p.PublicPort!.Value)
            .Order()];

    private static Task Refuse() =>
        Task.FromException(new DockerApiException(ReadOnly, detail: ReadOnly));

    private static Task<T> Refuse<T>() =>
        Task.FromException<T>(new DockerApiException(ReadOnly, detail: ReadOnly));

    /// <summary>The size column's own words, so a test can assert against the fixture's intent.</summary>
    public static string Bytes(long size) =>
        (size / 1_000_000d).ToString("0", CultureInfo.InvariantCulture) + " MB";
}
