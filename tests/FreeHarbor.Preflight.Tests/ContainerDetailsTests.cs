using FreeHarbor.Core.Api;
using FreeHarbor.Core.Details;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>A container's title, description, icon and links, resolved in one order (DD294).</summary>
public sealed class ContainerDetailsTests
{
    private static ContainerSummary Container(
        string image = "example/app:1",
        string name = "/shop-api-1",
        Dictionary<string, string>? labels = null,
        params PortBinding[] ports) => new()
    {
        Id = "a1",
        Image = image,
        Names = [name],
        Labels = labels,
        Ports = ports,
    };

    private static PortBinding Tcp(int published) =>
        new() { PrivatePort = 80, PublicPort = published, Type = "tcp" };

    [Fact]
    public void A_port_that_does_not_speak_http_is_not_made_a_web_link()
    {
        // DD334. The edit dialog offered http://localhost:5432 for a Postgres, ready to be saved as
        // though somebody had named it. The port's chip still copies the address; it is not a link.
        var details = ContainerDetails.Resolve(Container(
            image: "postgres:16",
            ports:
            [
                new PortBinding { PrivatePort = 5432, PublicPort = 5432, Type = "tcp" },
                Tcp(8080),
            ]));

        var link = Assert.Single(details.Links, link => link.Served);
        Assert.Equal("http://localhost:8080", link.Url);
        Assert.DoesNotContain(details.Links, link => link.Url.Contains("5432", StringComparison.Ordinal));
    }

    [Fact]
    public void A_container_with_nothing_set_is_titled_by_its_service_and_never_blank()
    {
        var details = ContainerDetails.Resolve(Container(labels: new()
        {
            [ContainerDetails.ServiceLabel] = "api",
        }));

        Assert.Equal("api", details.Title);
        Assert.Null(details.Description);
        Assert.Equal(new DetailIcon(IconKind.Monogram, "A"), details.Icon);
    }

    [Fact]
    public void A_container_outside_compose_is_titled_by_its_own_name()
    {
        Assert.Equal("nervous_tesla",
            ContainerDetails.Resolve(Container(name: "/nervous_tesla")).Title);
    }

    [Fact]
    public void Freeharbor_labels_win_over_homepage_and_homepage_over_the_image()
    {
        var details = ContainerDetails.Resolve(Container(labels: new()
        {
            ["freeharbor.title"] = "Public site",
            ["homepage.name"] = "Homepage name",
            ["homepage.description"] = "From homepage",
            ["org.opencontainers.image.title"] = "Image title",
            ["org.opencontainers.image.description"] = "From the image",
        }));

        Assert.Equal("Public site", details.Title);
        Assert.Equal("From homepage", details.Description);
    }

    [Fact]
    public void An_inspected_image_answers_what_the_container_labels_left_out()
    {
        var details = ContainerDetails.Resolve(
            Container(),
            new Dictionary<string, string> { ["org.opencontainers.image.title"] = "Mail catcher" });

        Assert.Equal("Mail catcher", details.Title);
    }

    [Fact]
    public void A_remote_icon_is_never_fetched_and_the_image_answers_instead()
    {
        var details = ContainerDetails.Resolve(Container(
            image: "postgres:17",
            labels: new() { ["homepage.icon"] = "https://example.com/pg.png" }));

        Assert.Equal(new DetailIcon(IconKind.Bundled, "postgres"), details.Icon);
    }

    [Fact]
    public void A_local_icon_file_is_kept_as_a_path()
    {
        var path = Path.Combine(Path.GetTempPath(), "storefront.svg");
        var details = ContainerDetails.Resolve(Container(labels: new() { ["freeharbor.icon"] = path }));

        Assert.Equal(new DetailIcon(IconKind.File, path), details.Icon);
    }

    [Fact]
    public void A_homepage_icon_spelled_as_a_file_name_finds_the_bundled_one()
    {
        var details = ContainerDetails.Resolve(Container(labels: new() { ["homepage.icon"] = "redis.png" }));

        Assert.Equal(new DetailIcon(IconKind.Bundled, "redis"), details.Icon);
    }

    [Theory]
    [InlineData("postgres", "postgres")]
    [InlineData("docker.io/library/postgres:17-alpine", "postgres")]
    [InlineData("bitnami/postgresql@sha256:abc", "postgres")]
    [InlineData("localhost:5000/team/nginx:1.27", "nginx")]
    [InlineData("example/app:1", null)]
    public void An_image_name_finds_its_bundled_icon(string image, string? expected)
    {
        Assert.Equal(expected, ContainerDetails.BundledFor(image));
    }

    [Fact]
    public void Published_tcp_ports_are_the_links_when_nothing_is_named()
    {
        var details = ContainerDetails.Resolve(Container(
            ports: [Tcp(8081), Tcp(8080), Tcp(8080), new PortBinding { PrivatePort = 53, PublicPort = 53, Type = "udp" }]));

        Assert.Equal(
            [new DetailLink(":8080", "http://localhost:8080"), new DetailLink(":8081", "http://localhost:8081")],
            details.Links);
        Assert.Equal(":8080", details.Primary?.Name);
    }

    [Fact]
    public void Named_links_replace_the_ports_and_primary_picks_among_them()
    {
        var details = ContainerDetails.Resolve(Container(
            labels: new()
            {
                ["freeharbor.url.inbox"] = "http://localhost:8025",
                ["freeharbor.url.admin"] = "http://localhost:8081/admin",
                ["freeharbor.primary"] = "inbox",
            },
            ports: [Tcp(8025)]));

        Assert.Equal(["admin", "inbox"], details.Links.Select(link => link.Name));
        Assert.Equal("http://localhost:8025", details.Primary?.Url);
    }

    [Fact]
    public void The_images_project_site_is_a_link_but_never_what_open_follows()
    {
        var details = ContainerDetails.Resolve(Container(
            labels: new() { ["org.opencontainers.image.url"] = "https://github.com/axllent/mailpit" }));

        var project = Assert.Single(details.Links);
        Assert.False(project.Served);
        Assert.Null(details.Primary);
    }

    [Fact]
    public void The_hue_is_the_same_on_every_run_and_inside_the_set()
    {
        // FNV-1a over the name, so this value is a constant of the algorithm and not of the process.
        Assert.Equal(ContainerDetails.HueOf("api"), ContainerDetails.HueOf("api"));
        Assert.All(
            new[] { "api", "db", "web", "nervous_tesla", "" },
            name => Assert.InRange(ContainerDetails.HueOf(name), 0, ContainerDetails.HueCount - 1));
        Assert.NotEqual(ContainerDetails.HueOf("api"), ContainerDetails.HueOf("db"));
    }

    [Fact]
    public void The_group_tier_is_read_from_its_label()
    {
        Assert.Equal("Data", ContainerDetails.Resolve(
            Container(labels: new() { ["freeharbor.group"] = "Data" })).Group);
    }
}
