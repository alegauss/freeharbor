using System.Globalization;

namespace FreeHarbor.Core.Details;

/// <summary>
/// Every icon this build can draw by name (DD347): the concepts of Fluent UI System Icons.
/// </summary>
/// <remarks>
/// <para>The font itself is the window's (the tray carries the TTF); what lives here is the list of
/// names and where each sits in that font, so a label, a project file or the edit dialog can name
/// <c>database</c> or <c>shopping-bag</c> and the core can tell a name it can draw from a typo
/// without referencing WPF.</para>
///
/// <para>One entry per concept, in the 24 px design where the font has one (the size the tiles are
/// drawn closest to) and otherwise the nearest it has. Names are the font's own with hyphens, the
/// spelling every other icon set and Homepage's labels already use. The list is generated from the
/// font's JSON map, <c>FluentSystemIcons-Regular.json</c>, at the version the TTF was taken from.</para>
/// </remarks>
public static class IconCatalogue
{
    /// <summary>The release of Fluent UI System Icons the font and this list were taken from.</summary>
    public const string Version = "1.1.345";

    private static readonly Lazy<IReadOnlyDictionary<string, int>> Codes = new(Load);

    /// <summary>Every name, in order.</summary>
    public static IReadOnlyList<string> Names => [.. Codes.Value.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Whether the font has a concept by this name.</summary>
    /// <param name="name">The name, such as <c>database</c>.</param>
    /// <returns>Whether it can be drawn.</returns>
    public static bool Contains(string name) => Codes.Value.ContainsKey(name);

    /// <summary>The text that draws a concept in the icon font, or <see langword="null"/> for an unknown name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>One code point, as a string: some sit above U+FFFF and take two UTF-16 units.</returns>
    public static string? GlyphOf(string name) =>
        Codes.Value.TryGetValue(name, out var code) ? char.ConvertFromUtf32(code) : null;

    /// <summary>The names that contain every word of a query, best match first.</summary>
    /// <param name="query">What was typed: words separated by spaces or hyphens, any case.</param>
    /// <param name="limit">How many to return.</param>
    /// <returns>Exact name first, then names that start with the query, then the rest, each alphabetically.</returns>
    public static IReadOnlyList<string> Search(string query, int limit)
    {
        ArgumentNullException.ThrowIfNull(query);
        var words = query.ToLowerInvariant()
            .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [];
        }

        var joined = string.Join('-', words);
        return [.. Codes.Value.Keys
            .Where(name => words.All(word => name.Contains(word, StringComparison.Ordinal)))
            .OrderBy(name => name == joined ? 0 : name.StartsWith(joined, StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(name => name.Length)
            .ThenBy(name => name, StringComparer.Ordinal)
            .Take(limit)];
    }

    private static Dictionary<string, int> Load()
    {
        using var stream = typeof(IconCatalogue).Assembly
            .GetManifestResourceStream("FreeHarbor.Core.Details.fluent-icons.txt")
            ?? throw new InvalidOperationException("The icon catalogue is not embedded in this build.");
        using var reader = new StreamReader(stream);

        var codes = new Dictionary<string, int>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space > 0)
            {
                codes[line[..space]] = int.Parse(
                    line.AsSpan(space + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
        }

        return codes;
    }
}
