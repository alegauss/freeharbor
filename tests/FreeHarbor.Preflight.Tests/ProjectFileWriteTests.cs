using FreeHarbor.Core.Api;
using FreeHarbor.Core.Details;
using FreeHarbor.Tray.Ui;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>Saving an edit into the project's own <c>.freeharbor/project.yaml</c> (DD296).</summary>
public sealed class ProjectFileWriteTests : IDisposable
{
    private readonly string _project =
        Path.Combine(Path.GetTempPath(), $"freeharbor-write-{Guid.NewGuid():N}");

    public ProjectFileWriteTests() => Directory.CreateDirectory(_project);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_project, recursive: true);
        }
        catch (IOException)
        {
            // A temp directory that outlives the run costs nothing worth failing a suite over.
        }
    }

    [Fact]
    public void What_is_written_reads_back_as_itself_however_it_is_spelled()
    {
        var icon = Path.Combine(_project, ProjectFile.Folder, "icons", "shop.png");
        var written = new ProjectFileDetails(
            new DetailFields("Shop #1: \"the\" storefront", @"C:\path and a # hash", icon),
            new Dictionary<string, DetailFields> { ["db"] = new("PostgreSQL", null, "postgres") });

        var path = ProjectFile.Write(_project, written);

        Assert.NotNull(path);
        var read = ProjectFile.Read(path);
        Assert.NotNull(read);
        Assert.Equal(written.Project, read.Project);
        Assert.Equal(written.Services["db"], read.Services["db"]);

        // Relative inside .freeharbor, so a teammate's checkout somewhere else reads the same file.
        Assert.Contains("icon: \"icons/shop.png\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void A_save_to_the_file_keeps_every_field_the_dialog_did_not_change()
    {
        var beneath = new ResolvedDetails("From the file", "Orders", new DetailIcon(IconKind.Monogram, "F"), 0, [], null, null);
        var existing = new DetailFields("From the file", "Orders", "redis");

        var written = EditDetailsWindow.ForTheFile(existing, beneath, "Renamed", "Orders", "redis");

        Assert.Equal(new DetailFields("Renamed", "Orders", "redis"), written);
    }

    [Fact]
    public void Clearing_a_field_removes_it_from_the_file()
    {
        var beneath = new ResolvedDetails("From the file", "Orders", new DetailIcon(IconKind.Monogram, "F"), 0, [], null, null);

        var written = EditDetailsWindow.ForTheFile(new DetailFields(null, "Orders", null), beneath, "From the file", "", null);

        Assert.Null(written.Description);
    }

    [Fact]
    public void The_file_is_offered_for_a_compose_service_whose_folder_is_here_and_nothing_else()
    {
        ContainerRow Row(string? project, string? service, string? dir) => ContainerRow.From(new ContainerSummary
        {
            Id = "c1",
            Image = "example/app:1",
            Names = ["/shop-api-1"],
            Labels = new Dictionary<string, string?>
            {
                ["com.docker.compose.project"] = project,
                ["com.docker.compose.service"] = service,
                [ProjectFile.WorkingDirLabel] = dir,
            }.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!),
        }, null);

        Assert.True(EditDetailsWindow.CanSaveToFile(Row("shop", "api", _project)));
        Assert.False(EditDetailsWindow.CanSaveToFile(Row("shop", "api", "/home/dev/shop")));
        Assert.False(EditDetailsWindow.CanSaveToFile(Row(null, null, null)));

        var header = ContainerRow.ProjectHeader("shop", [Row("shop", "api", _project)], collapsed: false);
        Assert.True(EditDetailsWindow.CanSaveToFile(header));
        Assert.Equal("shop", header.DetailKey);
    }
}
