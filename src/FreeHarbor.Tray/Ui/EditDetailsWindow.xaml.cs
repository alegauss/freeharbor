using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using FreeHarbor.Core.Details;

namespace FreeHarbor.Tray.Ui;

/// <summary>
/// Edits a container's title, description and icon, and saves them in FreeHarbor (DD296).
/// </summary>
/// <remarks>
/// <b>What Save writes is the difference, not the form.</b> The dialog is opened with two answers:
/// what the row shows now, and what it would show with nothing saved here. A field is stored only
/// where it differs from the second, so opening the dialog and pressing Save stores nothing, and a
/// label changed later still reaches every field nobody edited. Reset puts that second answer back.
///
/// <para><b>One target in this version.</b> The store is the one place an edit can always land.
/// Writing into <c>.freeharbor/project.yaml</c> or a compose file's <c>x-freeharbor</c> block is the
/// Save to choice the roadmap keeps open, so the footer says plainly where this one writes.</para>
/// </remarks>
internal partial class EditDetailsWindow : Window
{
    /// <summary>The name shape docker gives a container nobody named: an adjective and a surname.</summary>
    private static readonly Regex Invented = new("^[a-z]+_[a-z]+$", RegexOptions.CultureInvariant);

    private readonly ContainerRow _row;
    private readonly ResolvedDetails _beneath;
    private readonly DetailStore _store;
    private readonly int _hue;

    /// <summary>Open the dialog on one row.</summary>
    /// <param name="row">The row, as the list draws it now.</param>
    /// <param name="beneath">What the row would show with nothing saved in FreeHarbor.</param>
    /// <param name="store">Where Save writes.</param>
    internal EditDetailsWindow(ContainerRow row, ResolvedDetails beneath, DetailStore store)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(beneath);
        ArgumentNullException.ThrowIfNull(store);
        InitializeComponent();
        _row = row;
        _beneath = beneath;
        _store = store;
        _hue = row.Details?.Hue ?? beneath.Hue;

        var shown = row.Details ?? beneath;
        Fill(shown);
        Refresh();
    }

    /// <summary>Whether Save reached the disk; false after a save that only holds until the window closes.</summary>
    internal bool Saved { get; private set; }

    /// <summary>What the dialog would store for these fields, which is what Save writes.</summary>
    /// <param name="beneath">What the sources under the store say.</param>
    /// <param name="title">The title box.</param>
    /// <param name="description">The description box.</param>
    /// <param name="icon">The chosen icon: a bundled name, a path, or null for automatic.</param>
    /// <returns>The fields to store; empty where nothing differs from the sources.</returns>
    internal static DetailFields Difference(
        ResolvedDetails beneath, string title, string description, string? icon)
    {
        ArgumentNullException.ThrowIfNull(beneath);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);

        var keptTitle = title.Trim();
        var keptDescription = description.Trim();
        return new DetailFields(
            keptTitle.Length == 0 || keptTitle == beneath.Title ? null : keptTitle,
            keptDescription.Length == 0 || keptDescription == (beneath.Description ?? "")
                ? null
                : keptDescription,
            icon);
    }

    private void Fill(ResolvedDetails shown)
    {
        var project = _row.Project;
        Heading.Text = project is null
            ? $"Edit details · container {_row.Name}"
            : $"Edit details · compose project {project}";

        var files = (_row.ConfigFiles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);
        Files.Text = files.Length == 0
            ? ""
            : files[0] + string.Concat(files.Skip(1).Select(file => " + " + Path.GetFileName(file)));
        FilesRow.Visibility = files.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        Editing.Text = _row.Service is { } service && project is not null
            ? $"EDITING  service {project} / {service}"
            : $"EDITING  container {_row.Name}";
        Identity.Text =
            $"container {_row.Name}   image {_row.Image}   id {_row.Id[..Math.Min(12, _row.Id.Length)]}";
        KeyNote.Text = project is not null && _row.Service is not null
            ? $"Saved under {project} / {_row.Service}, so it survives a recreate."
            : Invented.IsMatch(_row.Name)
                ? $"Docker made the name {_row.Name} up, so these details stay with it and will not "
                  + "follow a new docker run. Give the container a --name, or label it "
                  + "with --label freeharbor.title=…"
                : $"Saved under the name {_row.Name}.";

        // Automatic first, which is "whatever the sources say", then every bundled icon, then a file
        // if one is saved. A choice of what is drawn and never a free text box, so a typo cannot
        // become a blank tile.
        IconBox.Items.Add(new IconChoice($"Automatic ({_beneath.IconSource})", null));
        foreach (var name in ContainerDetails.Bundled.Order(StringComparer.Ordinal))
        {
            IconBox.Items.Add(new IconChoice(name, name));
        }

        var savedIcon = _row.DetailKey is { } key ? _store.For(key)?.Icon : null;
        if (savedIcon is not null && Path.IsPathFullyQualified(savedIcon))
        {
            IconBox.Items.Add(new IconChoice(Path.GetFileName(savedIcon), savedIcon));
        }

        IconBox.SelectedItem = IconBox.Items.Cast<IconChoice>().FirstOrDefault(choice => choice.Value == savedIcon)
            ?? IconBox.Items[0];

        TitleBox.Text = shown.Title;
        DescriptionBox.Text = shown.Description ?? "";
    }

    private void Refresh()
    {
        if (!IsInitialized || IconBox.SelectedItem is not IconChoice choice)
        {
            return;
        }

        var title = TitleBox.Text.Trim().Length == 0 ? _beneath.Title : TitleBox.Text.Trim();
        var description = DescriptionBox.Text.Trim();

        TitleSource.Text = TitleBox.Text.Trim() == _beneath.Title || TitleBox.Text.Trim().Length == 0
            ? $"from {_beneath.TitleSource}"
            : ContainerDetails.EditedHere;
        DescriptionSource.Text = description == (_beneath.Description ?? "") || description.Length == 0
            ? $"from {_beneath.DescriptionSource}"
            : ContainerDetails.EditedHere;
        IconSource.Text = choice.Value is null ? $"from {_beneath.IconSource}" : ContainerDetails.EditedHere;
        Counter.Text = $"{DescriptionBox.Text.Length} / {DescriptionBox.MaxLength}";

        var icon = choice.Value switch
        {
            null => _beneath.Icon,
            var path when Path.IsPathFullyQualified(path) => new DetailIcon(IconKind.File, path),
            var name => new DetailIcon(IconKind.Bundled, name),
        };
        var glyph = IconTile.GlyphFor(icon);
        var picture = IconTile.PictureFor(icon);
        Tile.Background = Palette.TileBrushes[_hue % Palette.TileBrushes.Count];
        TileGlyph.Text = glyph ?? "";
        TilePicture.Source = picture;
        TileLetter.Text = glyph is null && picture is null ? IconTile.LetterFor(icon, title) : "";

        PreviewTitle.Text = title;
        PreviewDescription.Text = description.Length == 0 ? _beneath.Description ?? "" : description;
    }

    private void FieldChanged(object sender, RoutedEventArgs e) => Refresh();

    private void ResetTitle(object sender, RoutedEventArgs e) => TitleBox.Text = _beneath.Title;

    private void ResetDescription(object sender, RoutedEventArgs e) =>
        DescriptionBox.Text = _beneath.Description ?? "";

    private void ResetIcon(object sender, RoutedEventArgs e) => IconBox.SelectedIndex = 0;

    private void ChooseFile(object sender, RoutedEventArgs e)
    {
        // Bitmaps only, because that is what the tile can decode (IconTile.PictureFor).
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose an icon",
            Filter = "Pictures (*.png;*.ico;*.jpg;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.bmp",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var choice = new IconChoice(Path.GetFileName(dialog.FileName), dialog.FileName);
        _ = IconBox.Items.Add(choice);
        IconBox.SelectedItem = choice;
    }

    private void CopyPath(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(_row.ConfigFiles ?? "");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another process holds the clipboard; the path is still on screen to select.
        }
    }

    private void OpenFolder(object sender, RoutedEventArgs e)
    {
        var folder = _row.WorkingDir;
        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            // Named and not inherited (DD261): Explorer outlives this dialog, and the directory it
            // would hold is the install's own.
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"")
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.SystemDirectory,
            });
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        if (_row.DetailKey is not { } key || IconBox.SelectedItem is not IconChoice choice)
        {
            return;
        }

        Saved = _store.Save(key, Difference(_beneath, TitleBox.Text, DescriptionBox.Text, choice.Value));
        DialogResult = true;
    }

    /// <summary>One entry in the icon list.</summary>
    /// <param name="Label">What the list shows.</param>
    /// <param name="Value">What is stored: a bundled name, a path, or null for automatic.</param>
    private sealed record IconChoice(string Label, string? Value)
    {
        public override string ToString() => Label;
    }
}
