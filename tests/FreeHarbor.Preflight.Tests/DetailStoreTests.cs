using FreeHarbor.Core.Api;
using FreeHarbor.Core.Details;
using FreeHarbor.Tray.Ui;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>Details saved in FreeHarbor, and what the edit dialog writes there (DD296).</summary>
public sealed class DetailStoreTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"freeharbor-details-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
            // A temp file that outlives the run costs nothing worth failing a suite over.
        }
    }

    private static ContainerSummary Db() => new()
    {
        Id = "c2",
        Image = "postgres:17",
        Names = ["/shop-db-1"],
        Labels = new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = "shop",
            ["com.docker.compose.service"] = "db",
            ["homepage.name"] = "PostgreSQL",
        },
    };

    [Fact]
    public void A_save_is_read_back_by_a_store_opened_later()
    {
        Assert.True(new DetailStore(_file).Save("shop/db", new DetailFields("Orders DB", null, "redis")));

        Assert.Equal(new DetailFields("Orders DB", null, "redis"), new DetailStore(_file).For("shop/db"));
    }

    [Fact]
    public void Saving_nothing_removes_the_entry()
    {
        var store = new DetailStore(_file);
        store.Save("shop/db", new DetailFields("Orders DB", null, null));

        store.Save("shop/db", new DetailFields(null, null, null));

        Assert.Null(new DetailStore(_file).For("shop/db"));
    }

    [Fact]
    public void A_damaged_file_reads_as_nothing_saved()
    {
        File.WriteAllText(_file, "{ not json");

        Assert.Null(new DetailStore(_file).For("shop/db"));
    }

    [Fact]
    public void Keys_survive_a_recreate_because_they_name_the_service_and_not_the_id()
    {
        Assert.Equal("shop/db", DetailStore.ServiceKey("shop", "db"));
        Assert.Equal("shop", DetailStore.ProjectKey("shop"));
        Assert.Equal("container:mailpit", DetailStore.ContainerKey("mailpit"));
    }

    [Fact]
    public void A_save_beats_the_project_file_and_the_project_file_beats_the_label()
    {
        var store = new DetailStore(_file);
        var file = new ProjectFileDetails(
            new DetailFields("Storefront", null, null),
            new Dictionary<string, DetailFields> { ["db"] = new("From the file", "Orders", null) });

        var before = ContainerRow.From(Db(), (_, _) => [file], store);
        Assert.Equal("From the file", before.Title);
        Assert.Equal(ProjectFile.Folder, before.Details?.TitleSource);

        store.Save("shop/db", new DetailFields("Mine", null, null));
        var after = ContainerRow.From(Db(), (_, _) => [file], store);

        Assert.Equal("Mine", after.Title);
        Assert.Equal(ContainerDetails.EditedHere, after.Details?.TitleSource);
        Assert.Equal("Orders", after.Description);
        Assert.Equal(ProjectFile.Folder, after.Details?.DescriptionSource);
    }

    [Fact]
    public void Each_field_names_the_label_or_fallback_it_came_from()
    {
        var details = ContainerRow.From(Db(), null).Details!;

        Assert.Equal("label homepage.name", details.TitleSource);
        Assert.Equal("none", details.DescriptionSource);
        Assert.Equal("bundled icon for postgres", details.IconSource);
    }

    [Fact]
    public void Pressing_save_without_changing_anything_stores_nothing()
    {
        var beneath = ContainerRow.From(Db(), null).Details!;

        Assert.True(EditDetailsWindow.Difference(beneath, "PostgreSQL", "", null).IsEmpty);
    }

    [Fact]
    public void Only_the_fields_that_differ_from_their_sources_are_stored()
    {
        var beneath = ContainerRow.From(Db(), null).Details!;

        Assert.Equal(
            new DetailFields(null, "Orders and customers", "redis"),
            EditDetailsWindow.Difference(beneath, " PostgreSQL ", "Orders and customers", "redis"));
    }

    [Fact]
    public void A_project_header_reads_what_was_saved_for_the_project()
    {
        var store = new DetailStore(_file);
        store.Save("shop", new DetailFields("Storefront", "Shop and its database", null));

        var child = ContainerRow.From(Db(), null, store);
        var header = ContainerRow.ProjectHeader("shop", [child], collapsed: false);

        Assert.Equal("Storefront", header.Title);
        Assert.Equal("Shop and its database", header.Description);
    }
}
