using FreeHarbor.Core.Details;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>The project file FreeHarbor owns, <c>.freeharbor/project.yaml</c> (DD295).</summary>
public sealed class ProjectFileTests : IDisposable
{
    private readonly string _project =
        Path.Combine(Path.GetTempPath(), $"freeharbor-project-{Guid.NewGuid():N}");

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

    private string Folder => Path.Combine(_project, ProjectFile.Folder);

    [Fact]
    public void The_specs_own_example_reads_field_for_field()
    {
        // docs/specs/DD294-containers-redesign.md, section 8, character for character.
        var details = ProjectFile.Parse(
            """
            title: Storefront
            description: Shop API, admin panel and the Postgres behind them
            icon: icons/storefront.svg        # relative to .freeharbor/, or a bundled icon name
            services:
              admin:
                title: Back office
                icon: icons/backoffice.png
              db:
                description: Orders and customers
            """,
            Folder);

        Assert.Equal("Storefront", details.Project.Title);
        Assert.Equal("Shop API, admin panel and the Postgres behind them", details.Project.Description);
        Assert.Equal(Path.Combine(Folder, "icons", "storefront.svg"), details.Project.Icon);
        Assert.Equal("Back office", details.Services["admin"].Title);
        Assert.Equal(Path.Combine(Folder, "icons", "backoffice.png"), details.Services["admin"].Icon);
        Assert.Equal("Orders and customers", details.Services["db"].Description);
        Assert.Null(details.Services["db"].Title);
    }

    [Fact]
    public void A_bare_icon_word_stays_a_bundled_name()
    {
        Assert.Equal("postgres", ProjectFile.Parse("icon: postgres", Folder).Project.Icon);
    }

    [Fact]
    public void Quotes_keep_a_hash_and_a_colon_inside_the_value()
    {
        var details = ProjectFile.Parse(
            """
            title: "Shop #1: the storefront"
            description: 'It''s the shop' # and this is a comment
            """,
            Folder);

        Assert.Equal("Shop #1: the storefront", details.Project.Title);
        Assert.Equal("It's the shop", details.Project.Description);
    }

    [Fact]
    public void What_the_format_does_not_have_is_skipped_rather_than_guessed_at()
    {
        var details = ProjectFile.Parse(
            """
            title: Shop
            links:
              - http://localhost:8080
            services:
              api: {}
            """,
            Folder);

        Assert.Equal("Shop", details.Project.Title);
        Assert.Empty(details.Services);
    }

    [Fact]
    public void A_folder_with_no_file_or_no_folder_at_all_is_ordinary()
    {
        Assert.Null(ProjectFile.For(_project));
        Assert.Null(ProjectFile.For("/home/dev/shop"));
        Assert.Null(ProjectFile.For(null));
    }

    [Fact]
    public void An_edit_shows_on_the_next_read_with_nothing_recreated()
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, ProjectFile.FileName);
        File.WriteAllText(path, "title: First");
        Assert.Equal("First", ProjectFile.For(_project)?.Project.Title);

        File.WriteAllText(path, "title: Second");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));

        Assert.Equal("Second", ProjectFile.For(_project)?.Project.Title);
    }
}
