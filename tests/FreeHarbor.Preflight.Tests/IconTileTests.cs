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
    public void Every_bundled_name_draws_a_concept_the_font_has()
    {
        // The resolver promises a picture for each name in Bundled; this is where that promise is
        // kept. A concept misspelt in ImageIcons would draw a blank tile.
        Assert.All(ContainerDetails.Bundled, name =>
        {
            Assert.True(IconCatalogue.Contains(ContainerDetails.ImageIcons[name]), name);
            Assert.NotEqual("", IconTile.Glyph(name));
        });
    }

    [Fact]
    public void Every_suggested_icon_is_one_the_font_has()
    {
        // DD347: a suggestion the catalogue does not hold would be an empty button.
        Assert.All(EditDetailsWindow.SuggestedIcons, name => Assert.True(IconCatalogue.Contains(name), name));
        Assert.Equal(EditDetailsWindow.SuggestedIcons.Count, EditDetailsWindow.SuggestedIcons.Distinct().Count());
    }

    [Fact]
    public void The_catalogue_holds_thousands_of_concepts_and_draws_those_above_the_basic_plane()
    {
        // DD347. brain sits at U+F0798, outside the 16-bit range, and a char cast would cut it.
        Assert.True(IconCatalogue.Names.Count > 2000);
        Assert.Equal(char.ConvertFromUtf32(0xF0798), IconCatalogue.GlyphOf("brain"));
        Assert.Null(IconCatalogue.GlyphOf("no-such-icon"));
    }

    [Fact]
    public void A_search_puts_the_exact_name_first_and_matches_every_word()
    {
        var found = IconCatalogue.Search("database", 50);
        Assert.Equal("database", found[0]);
        Assert.All(found, name => Assert.Contains("database", name, StringComparison.Ordinal));

        var bag = IconCatalogue.Search("Shopping Bag", 10);
        Assert.Equal("shopping-bag", bag[0]);
        Assert.Empty(IconCatalogue.Search("  ", 10));
    }

    [Fact]
    public void A_label_can_name_any_concept_the_font_has_and_not_a_typo()
    {
        // DD347: freeharbor.icon=cloud is a picture now; before it fell through to the letter.
        var cloud = ContainerRow.From(Container("example/app:1", "api-1", new() { ["freeharbor.icon"] = "cloud" }));
        var typo = ContainerRow.From(Container("example/app:1", "api-1", new() { ["freeharbor.icon"] = "clowd" }));

        Assert.Equal(IconCatalogue.GlyphOf("cloud"), cloud.TileGlyph);
        Assert.Equal("", typo.TileGlyph);
        Assert.Equal("A", typo.TileLetter);
    }

    [Fact]
    public void An_image_named_like_a_concept_does_not_take_its_icon()
    {
        // Only the image names in ImageIcons resolve by themselves: an image called "key" is not one.
        Assert.Null(ContainerDetails.BundledFor("example/key:1"));
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

        Assert.Equal(IconCatalogue.GlyphOf("database"), row.TileGlyph);
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
            (dir, _) => string.Equals(dir, folder, StringComparison.Ordinal) ? [file] : []);
        var header = ContainerRow.ProjectHeader("shop", [child], collapsed: false);

        // The file beats the label: it is read on every refresh, where a label needs a recreate.
        Assert.Equal("Back office", child.Title);
        Assert.Equal("Storefront", header.Title);
        Assert.Equal("Shop API and the Postgres behind it", header.Description);
    }
}
