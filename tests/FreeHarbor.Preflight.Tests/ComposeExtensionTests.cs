using FreeHarbor.Core.Agent;
using FreeHarbor.Core.Details;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>The compose file's <c>x-freeharbor</c> block, read through compose (DD295).</summary>
public sealed class ComposeExtensionTests : IDisposable
{
    /// <summary>
    /// What <c>docker compose config --format json</c> wrote for a one-service project carrying the
    /// block, on 5 November 2026: compose keeps the extension at the top level, keys sorted.
    /// </summary>
    private const string Answered = """
        {
          "name": "xprobe",
          "networks": { "default": { "name": "xprobe_default", "ipam": {} } },
          "services": { "web": { "command": null, "entrypoint": null, "image": "nginx:1.27" } },
          "x-freeharbor": {
            "description": "Shop API #1",
            "services": { "web": { "title": "Public site" } },
            "title": "Storefront"
          }
        }
        """;

    private readonly string _project =
        Path.Combine(Path.GetTempPath(), $"freeharbor-compose-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_project))
            {
                Directory.Delete(_project, recursive: true);
            }
        }
        catch (IOException)
        {
            // A temp directory that outlives the run costs nothing worth failing a suite over.
        }
    }

    private sealed class FakeCli(string output, int exit = 0) : IComposeCli
    {
        public List<string[]> Calls { get; } = [];

        public ComposeResult Run(string workingDirectory, params string[] arguments)
        {
            lock (Calls)
            {
                Calls.Add(arguments);
            }

            return new ComposeResult(exit, output, null);
        }
    }

    private string ComposeFile()
    {
        Directory.CreateDirectory(_project);
        var path = Path.Combine(_project, "compose.yaml");
        File.WriteAllText(path, "services: {}");
        return path;
    }

    [Fact]
    public void Composes_own_answer_reads_field_for_field()
    {
        var details = ComposeExtension.Parse(Answered, _project);

        Assert.NotNull(details);
        Assert.Equal("Storefront", details.Project.Title);
        Assert.Equal("Shop API #1", details.Project.Description);
        Assert.Equal("Public site", details.Services["web"].Title);
    }

    [Fact]
    public void A_compose_file_without_the_block_says_nothing()
    {
        Assert.Null(ComposeExtension.Parse("""{ "name": "x", "services": {} }""", _project));
        Assert.Null(ComposeExtension.Parse("not json", _project));
    }

    [Fact]
    public void Every_file_is_handed_to_compose_in_its_own_order()
    {
        var cli = new FakeCli(Answered);
        var reader = new ComposeExtension(cli);

        _ = reader.Read(_project, [@"C:\p\compose.yaml", @"C:\p\compose.override.yaml"]);

        Assert.Equal(
            ["compose", "-f", @"C:\p\compose.yaml", "-f", @"C:\p\compose.override.yaml", "config", "--format", "json"],
            Assert.Single(cli.Calls));
    }

    [Fact]
    public void A_refusal_from_compose_is_absence_and_not_a_failure()
    {
        Assert.Null(new ComposeExtension(new FakeCli("", exit: 1)).Read(_project, [@"C:\p\compose.yaml"]));
    }

    [Fact]
    public async Task The_first_ask_never_waits_and_the_answer_arrives_with_an_event()
    {
        var file = ComposeFile();
        var cli = new FakeCli(Answered);
        var reader = new ComposeExtension(cli);
        var arrived = new TaskCompletionSource();
        reader.Changed += (_, _) => arrived.TrySetResult();

        Assert.Null(reader.For(_project, file));
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("Storefront", reader.For(_project, file)?.Project.Title);

        // Unchanged files are not read again: one subprocess, however many refreshes.
        _ = reader.For(_project, file);
        Assert.Single(cli.Calls);
    }

    [Fact]
    public void Files_that_are_not_there_are_not_handed_to_compose()
    {
        var cli = new FakeCli(Answered);

        Assert.Null(new ComposeExtension(cli).For(_project, Path.Combine(_project, "gone.yaml")));
        Assert.Null(new ComposeExtension(cli).For(_project, null));
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public void The_project_file_wins_field_by_field_over_the_compose_block()
    {
        var mine = ProjectFile.Parse(
            """
            title: Storefront (team)
            services:
              web:
                description: Hot reload on
            """,
            _project);
        var block = ComposeExtension.Parse(Answered, _project);

        var merged = ProjectFile.Merge(mine, block);

        Assert.NotNull(merged);
        Assert.Equal("Storefront (team)", merged.Project.Title);
        Assert.Equal("Shop API #1", merged.Project.Description);
        Assert.Equal("Public site", merged.Services["web"].Title);
        Assert.Equal("Hot reload on", merged.Services["web"].Description);
    }
}
