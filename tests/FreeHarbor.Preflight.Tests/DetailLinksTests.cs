using FreeHarbor.Core.Api;
using FreeHarbor.Core.Details;
using FreeHarbor.Tray.Ui;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>Links a user names for a service, wherever they are saved (DD296).</summary>
public sealed class DetailLinksTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), $"freeharbor-links-{Guid.NewGuid():N}");

    public DetailLinksTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A temp directory that outlives the run costs nothing worth failing a suite over.
        }
    }

    private static readonly IReadOnlyList<DetailLink> Named =
    [
        new("Inbox", "http://localhost:8025"),
        new("Admin panel", "http://localhost:8081/admin"),
    ];

    private static ContainerSummary Mail() => new()
    {
        Id = "m1",
        Image = "axllent/mailpit",
        Names = ["/shop-mail-1"],
        Labels = new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = "shop",
            ["com.docker.compose.service"] = "mail",
        },
        Ports = [new PortBinding { PrivatePort = 8025, PublicPort = 8025, Type = "tcp" }],
    };

    [Fact]
    public void Links_survive_the_store()
    {
        var file = Path.Combine(_folder, "details.json");
        new DetailStore(file).Save("shop/mail", new DetailFields(null, null, null, Named));

        Assert.Equal(new DetailFields(null, null, null, Named), new DetailStore(file).For("shop/mail"));
    }

    [Fact]
    public void Links_survive_the_project_file_in_their_order()
    {
        var path = ProjectFile.Write(_folder, new ProjectFileDetails(
            new DetailFields("Shop", null, null),
            new Dictionary<string, DetailFields> { ["mail"] = new(null, null, null, Named) }));

        Assert.Equal(Named, ProjectFile.Read(path!)!.Services["mail"].Links!);
    }

    [Fact]
    public void Composes_answer_carries_them_too()
    {
        var json = """
            {"x-freeharbor":{"services":{"mail":{"links":{"Inbox":"http://localhost:8025","Admin panel":"http://localhost:8081/admin"}}}}}
            """;

        Assert.Equal(Named, ComposeExtension.Parse(json, _folder)!.Services["mail"].Links!);
    }

    [Fact]
    public void A_layers_links_replace_the_ports_and_say_where_they_came_from()
    {
        var details = ContainerDetails.Resolve(Mail(), layers: [new DetailLayer(".freeharbor", new(null, null, null, Named))]);

        Assert.Equal(Named, details.Links);
        Assert.Equal("Inbox", details.Primary?.Name);
        Assert.Equal(".freeharbor", details.LinksSource);
        Assert.Equal("published ports", ContainerDetails.Resolve(Mail()).LinksSource);
    }

    [Fact]
    public void The_box_reads_a_name_then_an_address_and_drops_what_is_not_one()
    {
        var links = EditDetailsWindow.ParseLinks(
            "Inbox  http://localhost:8025\r\nAdmin panel http://localhost:8081/admin\nnot a link\n\nhttps://example.com/docs\nftp://nope");

        Assert.Equal(
            [.. Named, new DetailLink("example.com", "https://example.com/docs")],
            links);
    }

    [Fact]
    public void Links_left_as_the_sources_give_them_are_not_stored()
    {
        var beneath = ContainerDetails.Resolve(Mail());

        Assert.Null(EditDetailsWindow.Difference(
            beneath, beneath.Title, "", null, EditDetailsWindow.LinksText(beneath.Links)).Links);
        Assert.Equal(Named, EditDetailsWindow.Difference(
            beneath, beneath.Title, "", null, "Inbox  http://localhost:8025\nAdmin panel  http://localhost:8081/admin").Links!);
    }
}
