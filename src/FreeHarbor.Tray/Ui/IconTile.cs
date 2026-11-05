using FreeHarbor.Core.Details;

namespace FreeHarbor.Tray.Ui;

/// <summary>
/// What an icon tile draws for a resolved icon (DD294): a glyph, a letter, or a picture from disk.
/// </summary>
/// <remarks>
/// <b>Glyphs of what a thing is, not logos.</b> A bundled icon is a Segoe MDL2 Assets glyph for the
/// kind of service the image is (a cylinder for a database, a globe for a web server), because the
/// marks of Postgres or nginx are their owners' to ship, and the font is already the one the row's
/// chevron is drawn in. The glyphs were picked from a rendered sheet of the font, not from a table
/// of code points, since a wrong code point draws a box and nothing else says so.
/// </remarks>
internal static class IconTile
{
    /// <summary>The font the bundled glyphs are drawn in.</summary>
    public const string GlyphFont = "Segoe MDL2 Assets";

    private const string Database = "";
    private const string Layers = "";
    private const string Globe = "";
    private const string Mail = "";
    private const string Code = "";
    private const string Chart = "";
    private const string Report = "";
    private const string Send = "";
    private const string Drive = "";

    /// <summary>The glyph for each name <see cref="ContainerDetails.Bundled"/> promises.</summary>
    /// <remarks>A test holds the two sets equal, so a name added there without a glyph here fails.</remarks>
    public static readonly IReadOnlyDictionary<string, string> Glyphs =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["elasticsearch"] = Database,
            ["mariadb"] = Database,
            ["mongo"] = Database,
            ["mysql"] = Database,
            ["postgres"] = Database,
            ["redis"] = Layers,
            ["nginx"] = Globe,
            ["traefik"] = Globe,
            ["wordpress"] = Globe,
            ["mailpit"] = Mail,
            ["node"] = Code,
            ["python"] = Code,
            ["grafana"] = Chart,
            ["prometheus"] = Report,
            ["rabbitmq"] = Send,
            ["minio"] = Drive,
        };

    // Pictures already read, by path. The list is rebuilt on every event, and reading a file once per
    // rebuild per row would put disk reads on the UI thread's hottest path.
    private static readonly Dictionary<string, System.Windows.Media.ImageSource?> Pictures =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The glyph a resolved icon draws, or <see langword="null"/> where it draws something else.</summary>
    /// <param name="icon">The resolved icon.</param>
    /// <returns>The glyph.</returns>
    public static string? GlyphFor(DetailIcon icon) =>
        icon.Kind == IconKind.Bundled && Glyphs.TryGetValue(icon.Value, out var glyph) ? glyph : null;

    /// <summary>The picture a file icon draws, or <see langword="null"/> where there is none to read.</summary>
    /// <param name="icon">The resolved icon.</param>
    /// <returns>The picture, decoded and frozen, or nothing; a tile with nothing draws its letter.</returns>
    /// <remarks>
    /// A file that is missing or is not a picture costs the picture and not the row: the letter is
    /// drawn instead, and the failure is remembered so it is not retried on every rebuild. SVG is
    /// one of those, because WPF decodes bitmaps only; a PNG or an ICO is the file to point at.
    /// </remarks>
    public static System.Windows.Media.ImageSource? PictureFor(DetailIcon icon)
    {
        if (icon.Kind != IconKind.File)
        {
            return null;
        }

        lock (Pictures)
        {
            if (Pictures.TryGetValue(icon.Value, out var known))
            {
                return known;
            }

            System.Windows.Media.ImageSource? picture = null;
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 128;
                bitmap.UriSource = new Uri(icon.Value, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                picture = bitmap;
            }
            catch (Exception failure) when (failure is System.IO.IOException or UriFormatException
                or NotSupportedException or UnauthorizedAccessException or ArgumentException
                or InvalidOperationException)
            {
            }

            Pictures[icon.Value] = picture;
            return picture;
        }
    }

    /// <summary>The letter a tile draws where it has no glyph.</summary>
    /// <param name="icon">The resolved icon.</param>
    /// <param name="title">The resolved title, whose first letter a file icon falls back to.</param>
    /// <returns>One character.</returns>
    public static string LetterFor(DetailIcon icon, string title) => icon.Kind switch
    {
        IconKind.Monogram => icon.Value,
        _ => title.FirstOrDefault(char.IsLetterOrDigit) is var letter && letter != default
            ? char.ToUpperInvariant(letter).ToString()
            : "?",
    };
}
