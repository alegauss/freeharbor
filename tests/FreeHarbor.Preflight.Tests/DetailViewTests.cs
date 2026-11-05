using FreeHarbor.Core.Agent;
using FreeHarbor.Core.Api;
using FreeHarbor.Tray.Ui;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>
/// The detail page's content, drawn from the rows in hand and nothing else (DD299).
/// </summary>
public sealed class DetailViewTests
{
    private static ContainerSummary Summary(
        string name, string state = "running", string? project = null, int? privatePort = null, int? published = null) =>
        new()
        {
            Id = name + "-id",
            Names = ["/" + name],
            Image = "img:1",
            State = state,
            Status = state == "running" ? "Up 4 minutes" : "Exited (1) 3 minutes ago",
            Ports = privatePort is { } inside
                ? [new PortBinding { PrivatePort = inside, PublicPort = published, Type = "tcp" }]
                : [],
            Labels = project is null
                ? null
                : new Dictionary<string, string>(StringComparer.Ordinal) { [ContextPack.ProjectLabel] = project },
        };

    private static IReadOnlyList<ContainerRow> Rows(params ContainerSummary[] containers) =>
        [.. containers.Select(ContainerRow.From)];

    [Fact]
    public void A_project_page_names_every_service_and_every_failure_in_the_daemons_words()
    {
        var rows = Rows(
            Summary("shop-web-1", project: "shop", privatePort: 80, published: 8080),
            Summary("shop-db-1", project: "shop", privatePort: 5432, published: 5432),
            Summary("shop-worker-1", "exited", "shop"),
            Summary("elsewhere"));

        var view = DetailView.For(DetailView.KeyFor(rows[0]), rows);

        Assert.NotNull(view);
        Assert.True(view.Head.IsProject);
        Assert.Equal(3, view.Services.Count);

        var problem = Assert.Single(view.Problems);
        Assert.Equal("shop-worker-1: Exited (1) 3 minutes ago", problem.Sentence);
        Assert.Equal("shop-worker-1-id", problem.Row.Id);

        // Every port, each with the verb it can answer: a browser for HTTP, Copy alone otherwise.
        Assert.Equal(2, view.Endpoints.Count);
        var db = Assert.Single(view.Endpoints, endpoint => endpoint.Address == "localhost:5432");
        Assert.False(db.CanOpen);
        Assert.Equal("localhost:5432", db.CopyText);
        var web = Assert.Single(view.Endpoints, endpoint => endpoint.Address == "localhost:8080");
        Assert.True(web.CanOpen);
        Assert.Equal("http://localhost:8080", web.CopyText);
    }

    [Fact]
    public void A_standalone_container_gets_the_same_page_with_one_service()
    {
        var rows = Rows(Summary("mailpit", privatePort: 8025, published: 8025), Summary("shop-1", project: "shop"));

        var view = DetailView.For(DetailView.KeyFor(rows[0]), rows);

        Assert.NotNull(view);
        Assert.Same(rows[0], view.Head);
        Assert.Single(view.Services);
        Assert.Contains("standalone container mailpit", view.Subtitle, StringComparison.Ordinal);
    }

    [Fact]
    public void A_service_opens_its_projects_page()
    {
        var rows = Rows(Summary("shop-1", project: "shop"));

        Assert.Equal(ContainerRow.ProjectId("shop"), DetailView.KeyFor(rows[0]));
    }

    [Fact]
    public void A_page_about_something_gone_is_no_page()
    {
        // Removed, or the engine stopped: the page closes rather than describe nothing.
        Assert.Null(DetailView.For(ContainerRow.ProjectId("shop"), Rows(Summary("elsewhere"))));
        Assert.Null(DetailView.For("missing-id", []));
    }
}
