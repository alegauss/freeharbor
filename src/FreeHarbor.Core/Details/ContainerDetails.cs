using System.Globalization;
using FreeHarbor.Core.Api;

namespace FreeHarbor.Core.Details;

/// <summary>What kind of picture a container's icon tile holds.</summary>
public enum IconKind
{
    /// <summary>One of the icons this build carries, by name (<see cref="ContainerDetails.Bundled"/>).</summary>
    Bundled,

    /// <summary>A file on this machine, by absolute path.</summary>
    File,

    /// <summary>No picture: the first letter of the title, on the hue.</summary>
    Monogram,
}

/// <summary>The icon a tile draws.</summary>
/// <param name="Kind">What kind it is.</param>
/// <param name="Value">The bundled name, the file path, or the monogram's letter.</param>
public sealed record DetailIcon(IconKind Kind, string Value);

/// <summary>One link a container serves or names.</summary>
/// <param name="Name">What the link is called: a label's own name, or the port.</param>
/// <param name="Url">Where it goes.</param>
/// <param name="Served">
/// Whether the container serves it. The image's project site is a link it does not serve, and the
/// Open button never follows one of those.
/// </param>
public sealed record DetailLink(string Name, string Url, bool Served = true);

/// <summary>Everything a screen draws about one container beyond its state (DD294).</summary>
/// <param name="Title">Never blank: the service or container name where nothing better is set.</param>
/// <param name="Description">A line of what it is for, or <see langword="null"/>.</param>
/// <param name="Icon">What the tile holds.</param>
/// <param name="Hue">Which of the <see cref="ContainerDetails.HueCount"/> tile hues it sits on.</param>
/// <param name="Links">Named links first, then the links its published ports make.</param>
/// <param name="Primary">The link the Open button follows, or <see langword="null"/> where none.</param>
/// <param name="Group">The <c>freeharbor.group</c> tier, or <see langword="null"/>.</param>
public sealed record ResolvedDetails(
    string Title,
    string? Description,
    DetailIcon Icon,
    int Hue,
    IReadOnlyList<DetailLink> Links,
    DetailLink? Primary,
    string? Group)
{
    /// <summary>Where the title came from, in the words the edit dialog prints (DD296).</summary>
    public string TitleSource { get; init; } = "";

    /// <summary>Where the description came from, or <c>none</c>.</summary>
    public string DescriptionSource { get; init; } = "";

    /// <summary>Where the icon came from.</summary>
    public string IconSource { get; init; } = "";

    /// <summary>Where the links came from: a layer, <c>labels</c>, <c>published ports</c> or <c>none</c>.</summary>
    public string LinksSource { get; init; } = "";
}

/// <summary>One source of details beyond the labels, named for the dialog that shows it (DD296).</summary>
/// <param name="Source">What the dialog calls it: <c>edited here</c>, <c>.freeharbor</c>, <c>x-freeharbor</c>.</param>
/// <param name="Fields">What it says.</param>
public sealed record DetailLayer(string Source, DetailFields Fields);

/// <summary>
/// Resolves a container's title, description, icon and links, in one place and one order (DD294).
/// </summary>
/// <remarks>
/// <b>One resolver, because there will be four screens.</b> The list, the cards, the detail page and
/// the edit dialog all draw these facts, and a screen that resolved them on its own would sooner or
/// later disagree with the others about which label wins.
///
/// <para><b>The order, field by field.</b> <c>freeharbor.*</c> labels first, because they are this
/// product's own and somebody wrote them for it. Then <c>homepage.*</c>, the Homepage dashboard's,
/// so a machine already labelled for that needs no edits. Then the image's OCI labels. Docker copies
/// an image's <c>LABEL</c>s into every container made from it, so those usually arrive on the list
/// response already; the image labels <see cref="Resolve"/> takes are for an inspect that
/// found more. Last, a fallback that never leaves a field blank where a screen needs one.</para>
///
/// <para><b>Nothing remote.</b> An icon given as a URL is skipped, not fetched, because the product
/// does not phone home. The next source down answers instead.</para>
/// </remarks>
public static class ContainerDetails
{
    /// <summary>How many tile hues there are; <see cref="ResolvedDetails.Hue"/> is below it.</summary>
    public const int HueCount = 8;

    /// <summary>The name of the link to an image's project site, from its OCI <c>url</c> label.</summary>
    public const string ProjectLink = "Project";

    /// <summary>The compose label naming a container's service.</summary>
    public const string ServiceLabel = "com.docker.compose.service";

    /// <summary>
    /// The icons this build carries, by the name a label or an image resolves to.
    /// </summary>
    /// <remarks>
    /// A name here is a promise that the window has a picture for it. The screen that draws the tile
    /// is where that promise is tested, so a name added here without its picture fails there.
    /// </remarks>
    public static readonly IReadOnlySet<string> Bundled = new HashSet<string>(StringComparer.Ordinal)
    {
        "elasticsearch", "grafana", "mailpit", "mariadb", "minio", "mongo", "mysql", "nginx",
        "node", "postgres", "prometheus", "python", "rabbitmq", "redis", "traefik", "wordpress",
    };

    // Image names that are one of the bundled icons under another spelling.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["mongodb"] = "mongo",
        ["postgresql"] = "postgres",
        ["valkey"] = "redis",
    };

    /// <summary>What a source says for a value saved in FreeHarbor (DD296).</summary>
    public const string EditedHere = "edited here";

    /// <summary>Resolve one container.</summary>
    /// <param name="container">The container, as the list endpoint reported it.</param>
    /// <param name="imageLabels">Its image's labels from an inspect, or <see langword="null"/>.</param>
    /// <param name="layers">
    /// What was said about it outside its labels, first source first: what was saved in FreeHarbor
    /// (DD296), then the project's files (DD295). Every one of them comes before every label, because
    /// each is somebody naming this service on purpose, read on every refresh where a label needs a
    /// recreate to change.
    /// </param>
    /// <returns>Its details. Never null, and no field a screen needs is blank.</returns>
    public static ResolvedDetails Resolve(
        ContainerSummary container,
        IReadOnlyDictionary<string, string>? imageLabels = null,
        IReadOnlyList<DetailLayer>? layers = null)
    {
        ArgumentNullException.ThrowIfNull(container);

        var labels = container.Labels ?? new Dictionary<string, string>();
        var image = imageLabels ?? new Dictionary<string, string>();
        var given = layers ?? [];

        (string? Value, string Source) First(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (labels.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return (value.Trim(), $"label {key}");
                }

                if (image.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    return (value.Trim(), $"image label {key}");
                }
            }

            return (null, "");
        }

        (string? Value, string Source) Layered(Func<DetailFields, string?> field, params string[] keys) =>
            Pick(given, field) is { Value: not null } found ? found : First(keys);

        var service = labels.TryGetValue(ServiceLabel, out var named) && named.Length > 0 ? named : null;
        var name = service ?? container.DisplayName;

        var (titled, titleSource) = Layered(
            fields => fields.Title, "freeharbor.title", "homepage.name", "org.opencontainers.image.title");
        var title = titled ?? name;
        var (description, descriptionSource) = Layered(
            fields => fields.Description,
            "freeharbor.description", "homepage.description", "org.opencontainers.image.description");
        var (iconValue, iconSource) = Layered(fields => fields.Icon, "freeharbor.icon", "homepage.icon");
        var (icon, iconFrom) = Icon(iconValue, iconSource, container.Image, title);

        // A layer's links replace what labels and ports would give, the way a named label already
        // replaces the ports: somebody said which addresses matter (DD296).
        var layered = given.FirstOrDefault(layer => layer.Fields.HasLinks);
        var links = layered is not null
            ? [.. layered.Fields.Links!.Select(link => link with { Served = true })]
            : Links(labels, image, container.Ports);
        var linksSource = layered?.Source
            ?? (labels.Keys.Any(key => key.StartsWith("freeharbor.url", StringComparison.Ordinal)
                    || key == "homepage.href")
                ? "labels"
                : links.Any(link => link.Served) ? "published ports" : "none");
        var primaryName = First("freeharbor.primary").Value;

        // The image's project site is a link and never what Open follows: Open is for what this
        // container serves.
        var served = links.Where(link => link.Served).ToList();
        var primary = (primaryName is null
                ? null
                : served.FirstOrDefault(link => string.Equals(link.Name, primaryName, StringComparison.Ordinal)))
            ?? served.FirstOrDefault();

        return new ResolvedDetails(
            title,
            description,
            icon,
            HueOf(name),
            links,
            primary,
            First("freeharbor.group").Value)
        {
            TitleSource = titled is null ? (service is null ? "container name" : "service name") : titleSource,
            DescriptionSource = description is null ? "none" : descriptionSource,
            IconSource = iconFrom,
            LinksSource = linksSource,
        };
    }

    /// <summary>Resolve a compose project as a whole (DD295).</summary>
    /// <param name="project">The project's compose name.</param>
    /// <param name="layers">What was saved and what its files say, first source first.</param>
    /// <returns>
    /// Its details: a layer's title or the compose name, a layer's description or none, a layer's
    /// icon or a monogram. No links: a project's Open is its primary service's, which is DD297's.
    /// </returns>
    /// <remarks>
    /// No labels here, because no label describes a project: labels are a container's, and a
    /// project is not one. That gap is the whole reason the project files exist.
    /// </remarks>
    public static ResolvedDetails ResolveProject(string project, IReadOnlyList<DetailLayer>? layers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        var given = layers ?? [];

        var (titled, titleSource) = Pick(given, fields => fields.Title);
        var (description, descriptionSource) = Pick(given, fields => fields.Description);
        var (iconValue, iconSource) = Pick(given, fields => fields.Icon);
        var title = titled ?? project;
        var (icon, iconFrom) = Icon(iconValue, iconSource, image: "", title);

        return new ResolvedDetails(title, description, icon, HueOf(project), [], null, null)
        {
            TitleSource = titled is null ? "compose project name" : titleSource,
            DescriptionSource = description is null ? "none" : descriptionSource,
            IconSource = iconFrom,
        };
    }

    /// <summary>The first layer that sets a field, and which layer that was.</summary>
    private static (string? Value, string Source) Pick(
        IReadOnlyList<DetailLayer> layers, Func<DetailFields, string?> field)
    {
        foreach (var layer in layers)
        {
            if (field(layer.Fields) is { } value)
            {
                return (value, layer.Source);
            }
        }

        return (null, "");
    }

    /// <summary>Which hue a name sits on: the same one on every machine and every run.</summary>
    /// <param name="name">The service or container name.</param>
    /// <returns>A hue below <see cref="HueCount"/>.</returns>
    /// <remarks>
    /// FNV-1a and not <see cref="string.GetHashCode()"/>, which .NET randomises per process, so a
    /// container would change colour every time the window opened.
    /// </remarks>
    public static int HueOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var hash = 2166136261u;
        foreach (var unit in name)
        {
            hash = unchecked((hash ^ unit) * 16777619u);
        }

        return (int)(hash % HueCount);
    }

    /// <summary>The bundled icon an image's name stands for, if any.</summary>
    /// <param name="image">The image reference, such as <c>docker.io/library/postgres:17</c>.</param>
    /// <returns>The bundled name, or <see langword="null"/>.</returns>
    public static string? BundledFor(string image)
    {
        ArgumentNullException.ThrowIfNull(image);

        // The repository's last path segment, without its tag or digest: library/postgres:17-alpine
        // is postgres, bitnami/postgresql@sha256:... is postgresql.
        var repository = image.Split('@')[0];
        var slash = repository.LastIndexOf('/');
        var last = slash < 0 ? repository : repository[(slash + 1)..];
        var colon = last.IndexOf(':', StringComparison.Ordinal);
        var bare = (colon < 0 ? last : last[..colon]).ToLowerInvariant();

        return Known(bare);
    }

    private static string? Known(string name)
    {
        var canonical = Aliases.TryGetValue(name, out var alias) ? alias : name;
        return Bundled.Contains(canonical) ? canonical : null;
    }

    private static (DetailIcon Icon, string Source) Icon(
        string? given, string givenSource, string image, string title)
    {
        if (given is not null && !IsRemote(given))
        {
            if (Path.IsPathFullyQualified(given))
            {
                return (new DetailIcon(IconKind.File, given), givenSource);
            }

            // Homepage spells a bundled icon as a file name, postgres.png or postgres.svg.
            var stem = Path.GetFileNameWithoutExtension(given).ToLowerInvariant();
            if (Known(stem) is { } bundled)
            {
                return (new DetailIcon(IconKind.Bundled, bundled), givenSource);
            }
        }

        if (BundledFor(image) is { } byImage)
        {
            return (new DetailIcon(IconKind.Bundled, byImage), $"bundled icon for {byImage}");
        }

        var letter = title.FirstOrDefault(char.IsLetterOrDigit);
        return (new DetailIcon(
            IconKind.Monogram,
            letter == default ? "?" : char.ToUpper(letter, CultureInfo.InvariantCulture).ToString()),
            "the title's first letter");
    }

    private static bool IsRemote(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static List<DetailLink> Links(
        IReadOnlyDictionary<string, string> labels,
        IReadOnlyDictionary<string, string> image,
        IReadOnlyList<PortBinding> ports)
    {
        var links = new List<DetailLink>();
        void Add(string name, string? url, bool served = true)
        {
            if (!string.IsNullOrWhiteSpace(url)
                && !links.Any(link => string.Equals(link.Url, url.Trim(), StringComparison.Ordinal)))
            {
                links.Add(new DetailLink(name, url.Trim(), served));
            }
        }

        labels.TryGetValue("freeharbor.url", out var own);
        Add("Open", own);

        // freeharbor.url.<name>, ordered by name so two reads of one container list them alike.
        foreach (var (key, value) in labels
                     .Where(pair => pair.Key.StartsWith("freeharbor.url.", StringComparison.Ordinal))
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Add(key["freeharbor.url.".Length..], value);
        }

        labels.TryGetValue("homepage.href", out var homepage);
        Add("Open", homepage);

        // Nothing named, so the published TCP ports are the links, the way DD7 made them. A named
        // link is somebody saying which address matters, and the ports would only repeat it.
        if (links.Count == 0)
        {
            foreach (var port in ports.Where(p => p.PublicPort is not null
                         && string.Equals(p.Type, "tcp", StringComparison.Ordinal))
                         .Select(p => p.PublicPort!.Value)
                         .Distinct()
                         .Order())
            {
                var number = port.ToString(CultureInfo.InvariantCulture);
                Add($":{number}", $"http://localhost:{number}");
            }
        }

        // Last, whatever else there is: the image's own URL is its project's site and not something
        // this container serves, so it never stands in front of a link that answers.

        string? project = null;
        if (labels.TryGetValue("org.opencontainers.image.url", out var fromContainer))
        {
            project = fromContainer;
        }
        else if (image.TryGetValue("org.opencontainers.image.url", out var fromImage))
        {
            project = fromImage;
        }

        Add(ProjectLink, project, served: false);
        return links;
    }
}
