using FreeHarbor.Core.Api;
using FreeHarbor.Core.Details;
using FreeHarbor.Tray.Ui;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>The icon tile and the title a container row draws (DD294).</summary>
public sealed class IconTileTests
{
    private static ContainerSummary Container(string image, string name, Dictionary<string, string>? labels = null) =>
        new() { Id = name, Image = image, Names = ["/" + name], State = "running", Labels = labels };

    [Fact]
    public void Every_bundled_name_has_a_glyph_and_every_glyph_a_bundled_name()
    {
        // The resolver promises a picture for each name in Bundled; this is where that promise is
        // kept. A name added on one side alone would draw a blank tile.
        Assert.Equal(
            ContainerDetails.Bundled.Order(StringComparer.Ordinal),
            IconTile.Glyphs.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_hue_the_resolver_can_answer_has_a_brush()
    {
        Assert.Equal(ContainerDetails.HueCount, Palette.TileBrushes.Count);
        Assert.All(Palette.TileBrushes, brush => Assert.True(brush.IsFrozen));
        Assert.True(Palette.TileInk.IsFrozen);
    }

    [Fact]
    public void A_bundled_image_draws_its_glyph_and_no_letter()
    {
        var row = ContainerRow.From(Container("postgres:17", "shop-db-1"));

        Assert.Equal(IconTile.Glyphs["postgres"], row.TileGlyph);
        Assert.Equal("", row.TileLetter);
    }

    [Fact]
    public void An_unknown_image_draws_the_titles_letter()
    {
        var row = ContainerRow.From(Container("example/app:1", "nervous_tesla"));

        Assert.Equal("", row.TileGlyph);
        Assert.Equal("N", row.TileLetter);
    }

    [Fact]
    public void A_missing_icon_file_draws_the_letter_and_not_a_broken_picture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.png");
        var row = ContainerRow.From(Container("example/app:1", "shop-api-1",
            new() { ["freeharbor.icon"] = path, ["freeharbor.title"] = "Shop API" }));

        Assert.Null(row.TilePicture);
        Assert.Equal("S", row.TileLetter);
    }

    [Fact]
    public void The_row_shows_the_resolved_title_and_keeps_the_name_in_its_tooltip()
    {
        var row = ContainerRow.From(Container("example/app:1", "shop-api-1", new()
        {
            ["freeharbor.title"] = "Shop API",
            ["freeharbor.description"] = "Orders, carts and checkout",
        }));

        Assert.Equal("Shop API", row.Title);
        Assert.True(row.HasDescription);
        Assert.Contains("shop-api-1", row.NameEvidence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_with_no_file_is_headed_by_its_compose_name_on_a_monogram()
    {
        var child = ContainerRow.From(Container("example/app:1", "shop-api-1",
            new() { ["com.docker.compose.project"] = "shop" }));
        var header = ContainerRow.ProjectHeader("shop", [child], collapsed: false);

        Assert.True(header.HasTile);
        Assert.Equal("shop", header.Title);
        Assert.Equal("S", header.TileLetter);
        Assert.Equal("shop", header.NameEvidence);
    }

    [Fact]
    public void A_project_file_titles_the_header_and_renames_its_service()
    {
        var folder = @"C:\work\shop";
        var file = ProjectFile.Parse(
            """
            title: Storefront
            description: Shop API and the Postgres behind it
            services:
              api:
                title: Back office
            """,
            folder);
        var child = ContainerRow.From(
            Container("example/app:1", "shop-api-1", new()
            {
                ["com.docker.compose.project"] = "shop",
                ["com.docker.compose.service"] = "api",
                [ProjectFile.WorkingDirLabel] = folder,
                ["freeharbor.title"] = "Label title",
            }),
            dir => string.Equals(dir, folder, StringComparison.Ordinal) ? file : null);
        var header = ContainerRow.ProjectHeader("shop", [child], collapsed: false);

        // The file beats the label: it is read on every refresh, where a label needs a recreate.
        Assert.Equal("Back office", child.Title);
        Assert.Equal("Storefront", header.Title);
        Assert.Equal("Shop API and the Postgres behind it", header.Description);
    }
}
