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
    string? Group);

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

    /// <summary>Resolve one container.</summary>
    /// <param name="container">The container, as the list endpoint reported it.</param>
    /// <param name="imageLabels">Its image's labels from an inspect, or <see langword="null"/>.</param>
    /// <returns>Its details. Never null, and no field a screen needs is blank.</returns>
    public static ResolvedDetails Resolve(
        ContainerSummary container, IReadOnlyDictionary<string, string>? imageLabels = null)
    {
        ArgumentNullException.ThrowIfNull(container);

        var labels = container.Labels ?? new Dictionary<string, string>();
        var image = imageLabels ?? new Dictionary<string, string>();

        string? First(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (labels.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }

                if (image.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        var name = labels.TryGetValue(ServiceLabel, out var service) && service.Length > 0
            ? service
            : container.DisplayName;

        var title = First("freeharbor.title", "homepage.name", "org.opencontainers.image.title")
            ?? name;
        var description = First(
            "freeharbor.description", "homepage.description", "org.opencontainers.image.description");

        var links = Links(labels, image, container.Ports);
        var primaryName = First("freeharbor.primary");
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
            Icon(First("freeharbor.icon", "homepage.icon"), container.Image, title),
            HueOf(name),
            links,
            primary,
            First("freeharbor.group"));
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

    private static DetailIcon Icon(string? labelled, string image, string title)
    {
        if (labelled is not null && !IsRemote(labelled))
        {
            if (Path.IsPathFullyQualified(labelled))
            {
                return new DetailIcon(IconKind.File, labelled);
            }

            // Homepage spells a bundled icon as a file name, postgres.png or postgres.svg.
            var stem = Path.GetFileNameWithoutExtension(labelled).ToLowerInvariant();
            if (Known(stem) is { } bundled)
            {
                return new DetailIcon(IconKind.Bundled, bundled);
            }
        }

        if (BundledFor(image) is { } byImage)
        {
            return new DetailIcon(IconKind.Bundled, byImage);
        }

        var letter = title.FirstOrDefault(char.IsLetterOrDigit);
        return new DetailIcon(
            IconKind.Monogram,
            letter == default ? "?" : char.ToUpper(letter, CultureInfo.InvariantCulture).ToString());
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
