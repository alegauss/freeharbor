using FreeHarbor.Core.Agent;
using FreeHarbor.Core.Api;
using FreeHarbor.Tray.Ui;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>Where edits are saved: chosen once in Settings, departed from per project (DD301).</summary>
public sealed class SavePreferenceTests
{
    private static readonly SaveDestination[] All =
        [SaveDestination.FreeHarbor, SaveDestination.ProjectFile, SaveDestination.ComposeFile];

    private static SavePreference Default(SaveDestination destination) =>
        SavePreference.None with { Default = destination };

    [Fact]
    public void Nothing_chosen_saves_in_freeharbor()
    {
        Assert.Equal(SaveDestination.FreeHarbor, SavePreference.None.For("D:/shop", All));
    }

    [Fact]
    public void The_dialog_opens_on_the_default_and_a_projects_own_choice_beats_it()
    {
        var preference = Default(SaveDestination.ProjectFile).Chose("D:/blog", SaveDestination.ComposeFile);

        Assert.Equal(SaveDestination.ProjectFile, preference.For("D:/shop", All));
        Assert.Equal(SaveDestination.ComposeFile, preference.For("D:/blog", All));
    }

    [Fact]
    public void A_destination_the_project_cannot_use_falls_back_to_freeharbor()
    {
        // A compose file this machine cannot open: the default means nothing there, and FreeHarbor
        // is always there.
        Assert.Equal(
            SaveDestination.FreeHarbor,
            Default(SaveDestination.ComposeFile).For("D:/shop", [SaveDestination.FreeHarbor]));
    }

    [Fact]
    public void Saving_to_the_default_forgets_the_projects_departure()
    {
        var departed = Default(SaveDestination.FreeHarbor).Chose("D:/shop", SaveDestination.ProjectFile);
        Assert.Single(departed.Projects);

        var back = departed.Chose("D:/shop", SaveDestination.FreeHarbor);
        Assert.Empty(back.Projects);

        Assert.Empty(departed.Follow("d:/SHOP").Projects);
    }

    [Fact]
    public void A_container_outside_compose_has_no_choice_to_remember()
    {
        var row = ContainerRow.From(new ContainerSummary { Id = "m", Names = ["/mailpit"] });

        Assert.Null(SavePreference.KeyFor(row));
        Assert.Same(SavePreference.None, SavePreference.None.Chose(null, SaveDestination.ProjectFile));
    }

    [Fact]
    public void A_projects_key_is_its_folder_where_compose_recorded_one()
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ContextPack.ProjectLabel] = "shop",
            ["com.docker.compose.project.working_dir"] = @"D:\work\shop",
        };
        var row = ContainerRow.From(new ContainerSummary { Id = "a", Names = ["/shop-api-1"], Labels = labels });

        Assert.Equal(@"D:\work\shop", SavePreference.KeyFor(row));
    }

    [Fact]
    public void The_read_order_names_every_source_first_wins()
    {
        Assert.Equal(6, SavePreference.ReadOrder.Count);
        Assert.StartsWith("Saved in FreeHarbor", SavePreference.ReadOrder[0], StringComparison.Ordinal);
    }

    [Fact]
    public void The_preference_survives_the_window_store()
    {
        var path = Path.Combine(Path.GetTempPath(), $"freeharbor-save-{Guid.NewGuid():N}.json");
        try
        {
            new WindowMemory
            {
                SaveDetailsTo = nameof(SaveDestination.ProjectFile),
                SaveDetailsToByProject = new(StringComparer.OrdinalIgnoreCase) { ["D:/blog"] = nameof(SaveDestination.ComposeFile) },
            }.Write(path);

            var read = WindowMemory.Read(path)!;
            Assert.Equal(nameof(SaveDestination.ProjectFile), read.SaveDetailsTo);
            Assert.Equal(nameof(SaveDestination.ComposeFile), read.SaveDetailsToByProject["D:/blog"]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
