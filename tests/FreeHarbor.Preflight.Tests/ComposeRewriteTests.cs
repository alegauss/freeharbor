using FreeHarbor.Core.Details;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>Writing the <c>x-freeharbor</c> block into somebody's compose file (DD296).</summary>
public sealed class ComposeRewriteTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), $"freeharbor-rewrite-{Guid.NewGuid():N}");

    public ComposeRewriteTests() => Directory.CreateDirectory(_folder);

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

    private static ProjectFileDetails Storefront() => new(
        new DetailFields("Storefront", "Shop: the API #1", null),
        new Dictionary<string, DetailFields> { ["web"] = new("Public site", null, "nginx") });

    [Fact]
    public void A_block_is_replaced_and_every_other_byte_stays_where_it_was()
    {
        var text = string.Join("\r\n",
            "# the team's compose file",
            "name: shop",
            "x-freeharbor:   # ours",
            "  title: Old",
            "",
            "  services:",
            "    web:",
            "      title: Old site",
            "",
            "services:",
            "  web:",
            "    image: nginx:1.27   # pinned",
            "");

        var (rewritten, before, after) = ComposeExtension.Rewrite(text, Storefront(), _folder);

        Assert.Contains("  title: Old", before, StringComparison.Ordinal);
        Assert.StartsWith("x-freeharbor:", after, StringComparison.Ordinal);
        Assert.StartsWith("# the team's compose file\r\nname: shop\r\nx-freeharbor:\r\n", rewritten, StringComparison.Ordinal);
        Assert.EndsWith("\r\n\r\nservices:\r\n  web:\r\n    image: nginx:1.27   # pinned\r\n", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("Old", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\r", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_without_a_block_gets_one_at_the_end()
    {
        var (rewritten, before, _) = ComposeExtension.Rewrite("services:\n  web:\n    image: nginx\n\n", Storefront(), _folder);

        Assert.Equal("", before);
        Assert.StartsWith("services:\n  web:\n    image: nginx\n\nx-freeharbor:\n  title: \"Storefront\"\n", rewritten, StringComparison.Ordinal);
        Assert.EndsWith("\n", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_that_only_starts_with_the_name_is_not_the_block()
    {
        var (_, before, _) = ComposeExtension.Rewrite("x-freeharbor-legacy:\n  a: b\n", Storefront(), _folder);

        Assert.Equal("", before);
    }

    [Fact]
    public void The_write_goes_to_the_last_file_that_has_a_block_or_else_the_first()
    {
        var main = Path.Combine(_folder, "compose.yaml");
        var overrides = Path.Combine(_folder, "compose.override.yaml");
        File.WriteAllText(main, "services: {}\n");
        File.WriteAllText(overrides, "services: {}\n");

        Assert.Equal(main, ComposeExtension.TargetFile([main, overrides]));

        File.WriteAllText(overrides, "x-freeharbor:\n  title: Over\n");
        Assert.Equal(overrides, ComposeExtension.TargetFile([main, overrides]));
    }

    [Fact]
    public void What_is_written_is_what_compose_answers_back()
    {
        // The block goes through compose's own reader on the way back, so this is the check that the
        // writer and that reader agree; Parse here takes exactly the JSON compose produced for it.
        var (rewritten, _, _) = ComposeExtension.Rewrite("services: {}\n", Storefront(), _folder);
        var json = """
            {"services":{},"x-freeharbor":{"title":"Storefront","description":"Shop: the API #1","services":{"web":{"title":"Public site","icon":"nginx"}}}}
            """;

        Assert.Contains("description: \"Shop: the API #1\"", rewritten, StringComparison.Ordinal);
        var read = ComposeExtension.Parse(json, _folder);
        Assert.NotNull(read);
        Assert.Equal(Storefront().Project, read.Project);
        Assert.Equal(Storefront().Services["web"], read.Services["web"]);
    }
}
