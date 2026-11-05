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
    public void The_output_tail_keeps_lines_whole_across_frames_and_marks_stderr()
    {
        // A frame can end mid-sentence, so a line split across two chunks of one stream is one line.
        var tail = DetailView.Tail(
            [
                new LogChunk(LogStream.StdOut, "listening on :8080\nconn"),
                new LogChunk(LogStream.StdErr, "bind: address in use\r\n"),
                new LogChunk(LogStream.StdOut, "ected\nshutting down"),
            ],
            keep: 10);

        Assert.Equal(
            [
                new OutputLine("listening on :8080", false),
                new OutputLine("bind: address in use", true),
                new OutputLine("connected", false),
                new OutputLine("shutting down", false),
            ],
            tail);
    }

    [Fact]
    public void The_output_tail_keeps_only_the_newest_lines()
    {
        var chunks = Enumerable.Range(1, 30).Select(n => new LogChunk(LogStream.StdOut, $"line {n}\n"));

        var tail = DetailView.Tail(chunks, keep: 3);

        Assert.Equal(["line 28", "line 29", "line 30"], tail.Select(line => line.Text));
    }

    [Fact]
    public void The_output_pane_tails_the_failing_service_or_else_the_first()
    {
        var failing = Rows(Summary("a-1", project: "p"), Summary("b-1", "exited", "p"));
        var healthy = Rows(Summary("b-1", project: "p"), Summary("a-1", project: "p"));

        Assert.Equal("b-1-id", DetailView.For(ContainerRow.ProjectId("p"), failing)!.OutputService!.Id);
        Assert.Equal("a-1-id", DetailView.For(ContainerRow.ProjectId("p"), healthy)!.OutputService!.Id);
    }

    [Fact]
    public void The_rail_lists_projects_then_standalone_containers_and_narrows_by_name()
    {
        var rows = Rows(
            Summary("shop-1", project: "shop"),
            Summary("shop-2", project: "shop"),
            Summary("blog-1", project: "blog"),
            Summary("mailpit"));

        Assert.Equal(["blog", "shop"], DetailView.RailProjects(rows, null).Select(row => row.Title));
        Assert.Equal(["mailpit"], DetailView.RailStandalone(rows, null).Select(row => row.Name));
        Assert.Equal(["shop"], DetailView.RailProjects(rows, "sho").Select(row => row.Title));
        Assert.Empty(DetailView.RailStandalone(rows, "sho"));
    }

    [Fact]
    public void A_headers_and_a_standalone_containers_title_open_the_page_and_a_services_does_not()
    {
        var rows = Rows(Summary("shop-1", project: "shop"), Summary("mailpit"));
        var header = ContainerRow.ProjectHeader("shop", [rows[0]], collapsed: false);

        Assert.True(header.OpensDetail);
        Assert.True(rows[1].OpensDetail);
        Assert.False(rows[0].OpensDetail);
        Assert.True(rows[0].TitleIsText);
    }

    // ---- the cards view (DD300) ---------------------------------------------------------------

    private static readonly ListShape ByState = new(ContainerRow.DefaultColumn, Descending: false);

    [Fact]
    public void Cards_are_one_per_project_then_the_standalone_containers()
    {
        var rows = Rows(
            Summary("shop-1", project: "shop"),
            Summary("blog-1", "exited", "blog"),
            Summary("mailpit"));

        var (projects, standalone) = DetailView.Cards(rows, ByState);

        // In the list's order: the project led by a running container first.
        Assert.Equal([ContainerRow.ProjectId("shop"), ContainerRow.ProjectId("blog")], projects.Select(card => card.Key));
        Assert.Equal(["mailpit-id"], standalone.Select(row => row.Id));
    }

    [Fact]
    public void Cards_keep_the_lists_filter()
    {
        // Switching views keeps what was narrowed, because both draw the same rows through one shape.
        var rows = Rows(Summary("shop-1", project: "shop"), Summary("blog-1", project: "blog"), Summary("mailpit"));

        var (projects, standalone) = DetailView.Cards(rows, ByState.Narrowed("shop"));

        Assert.Equal([ContainerRow.ProjectId("shop")], projects.Select(card => card.Key));
        Assert.Empty(standalone);
    }

    [Fact]
    public void A_cards_secondary_links_leave_out_the_one_open_follows()
    {
        var rows = Rows(
            Summary("shop-web-1", project: "shop", privatePort: 80, published: 8080),
            Summary("shop-db-1", project: "shop", privatePort: 5432, published: 5432));

        var card = Assert.Single(DetailView.Cards(rows, ByState).Projects);

        Assert.Equal("http://localhost:8080", card.Head.OpenLink!.Url);
        var link = Assert.Single(card.SecondaryLinks);
        Assert.True(link.CopiesOnly);
    }

    [Fact]
    public void The_chosen_view_is_remembered_and_a_window_with_nothing_remembered_opens_on_the_list()
    {
        Assert.Equal(WindowMemory.ListView, new WindowMemory().ContainersView);

        var path = Path.Combine(Path.GetTempPath(), $"freeharbor-view-{Guid.NewGuid():N}.json");
        try
        {
            new WindowMemory { ContainersView = WindowMemory.CardsView }.Write(path);
            Assert.Equal(WindowMemory.CardsView, WindowMemory.Read(path)!.ContainersView);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_page_about_something_gone_is_no_page()
    {
        // Removed, or the engine stopped: the page closes rather than describe nothing.
        Assert.Null(DetailView.For(ContainerRow.ProjectId("shop"), Rows(Summary("elsewhere"))));
        Assert.Null(DetailView.For("missing-id", []));
    }
}
