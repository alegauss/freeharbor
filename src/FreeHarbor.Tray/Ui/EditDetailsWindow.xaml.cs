using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using FreeHarbor.Core.Details;

namespace FreeHarbor.Tray.Ui;

/// <summary>
/// Edits the titles, descriptions and icons of a compose project and its services, or of one
/// container, and saves them where the footer says (DD296).
/// </summary>
/// <remarks>
/// <b>What Save writes is the difference, not the form.</b> Every target is opened with two answers:
/// what its row shows now, and what it would show with nothing saved here. A field is stored only
/// where it differs from the second, so opening the dialog and pressing Save stores nothing, and a
/// label changed later still reaches every field nobody edited. Reset puts that second answer back.
///
/// <para><b>One dialog for the whole project.</b> The rail lists the project and each of its
/// services. Moving along it keeps what was typed for the target being left, and Save writes every
/// target that changed, to one destination, in one write per file.</para>
///
/// <para><b>Three destinations.</b> The store is the one place an edit can always land. A compose
/// project whose folder this machine can open can also take it in <c>.freeharbor/project.yaml</c>,
/// which the whole team gets when it is committed. Saving there keeps every field of the file the
/// dialog did not change, and clears what the store held for the same key, because the store is read
/// first and would otherwise hide the save. The compose file's own <c>x-freeharbor</c> block is a
/// destination too, and that one is somebody's file: the change is shown once per project before it
/// is made, and only the block's own lines are touched.</para>
/// </remarks>
internal partial class EditDetailsWindow : Window
{
    /// <summary>The name shape docker gives a container nobody named: an adjective and a surname.</summary>
    private static readonly Regex Invented = new("^[a-z]+_[a-z]+$", RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<ContainerRow> _targets;
    private readonly DetailStore _store;
    private readonly Dictionary<string, Edit> _pending = new(StringComparer.Ordinal);
    private ContainerRow _row;
    private ResolvedDetails _beneath;
    private bool _loading;

    // DD347: the icon chosen for the target, kept apart from the list because the search replaces
    // the list's items, and a selection that lived only there would go with them.
    private string? _iconValue;

    // Set while ListIcons refills the buttons, so the selection events that causes are not a pick.
    private bool _listing;

    /// <summary>How many matches a search shows: enough to scan, few enough to draw at once.</summary>
    private const int IconSearchLimit = 120;

    /// <summary>
    /// What the picker offers before anything is typed (DD347): the kinds of thing a compose project
    /// usually runs or is for. A test holds every name to one the catalogue can draw.
    /// </summary>
    internal static readonly IReadOnlyList<string> SuggestedIcons =
    [
        "database", "database-stack", "server", "server-multiple", "globe", "cloud", "code",
        "storage", "mail-inbox", "chat", "search", "shopping-bag", "book", "chart-multiple",
        "data-histogram", "arrow-swap", "shield", "key", "people-team", "bot", "brain", "cube",
        "box", "settings", "wrench", "image", "video", "music-note-2", "games", "calendar",
        "money", "building", "heart-pulse", "rocket", "flash", "home",
    ];

    /// <summary>Open the dialog on one row, with the rows the rail lists beside it.</summary>
    /// <param name="targets">
    /// Every row the dialog can edit: a project's header and its containers, or the one container.
    /// Each must carry <see cref="ContainerRow.DetailsBeneath"/> and <see cref="ContainerRow.DetailKey"/>.
    /// </param>
    /// <param name="initial">The row the dialog opens on, which is one of the targets.</param>
    /// <param name="store">The store, read for what is saved and written by Save.</param>
    /// <param name="preference">Where edits are saved, which Save to opens on (DD301); FreeHarbor where none.</param>
    /// <param name="remember">Told what Save chose, so a project that departs from the default is remembered.</param>
    internal EditDetailsWindow(
        IReadOnlyList<ContainerRow> targets,
        ContainerRow initial,
        DetailStore store,
        SavePreference? preference = null,
        Action<SavePreference>? remember = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(store);
        _preference = preference ?? SavePreference.None;
        _remember = remember;
        InitializeComponent();
        _targets = [.. targets.Where(target => target.DetailKey is not null && target.DetailsBeneath is not null)];
        if (!_targets.Contains(initial))
        {
            _targets = [initial, .. _targets];
        }

        _store = store;
        _row = initial;
        _beneath = initial.DetailsBeneath ?? initial.Details!;

        FillShared(initial);
        // DD333: captioned the way the board captions it, the project and then its services.
        var firstService = true;
        foreach (var target in _targets)
        {
            var heading = target.IsProject ? "PROJECT" : firstService ? "SERVICES" : "";
            firstService &= target.IsProject;
            Rail.Items.Add(new RailItem(target, heading));
        }

        RailColumn.Width = _targets.Count > 1 ? new GridLength(240) : new GridLength(0);
        Rail.Visibility = _targets.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (_targets.Count > 1)
        {
            Width += 200;
        }

        Rail.SelectedIndex = _targets.ToList().IndexOf(initial);
        Load(initial);
    }

    private readonly SavePreference _preference;
    private readonly Action<SavePreference>? _remember;

    /// <summary>Whether Save reached the disk; false after a save that only holds until the window closes.</summary>
    internal bool Saved { get; private set; }

    /// <summary>What the dialog would store for these fields, which is what Save writes.</summary>
    /// <param name="beneath">What the sources under the store say.</param>
    /// <param name="title">The title box.</param>
    /// <param name="description">The description box.</param>
    /// <param name="icon">The chosen icon: a bundled name, a path, or null for automatic.</param>
    /// <param name="links">The links box, or null where the dialog never showed it.</param>
    /// <returns>The fields to store; empty where nothing differs from the sources.</returns>
    internal static DetailFields Difference(
        ResolvedDetails beneath, string title, string description, string? icon, string? links = null)
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
            icon,
            links is null ? null : ChangedLinks(beneath, links));
    }

    /// <summary>The links a box spells, one per line: a name and an address, or an address alone.</summary>
    /// <param name="text">The box.</param>
    /// <returns>The links whose address is an absolute http or https URL, in order; others are dropped.</returns>
    internal static IReadOnlyList<DetailLink> ParseLinks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var links = new List<DetailLink>();
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0
                || !Uri.TryCreate(parts[^1], UriKind.Absolute, out var url)
                || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            var name = parts.Length > 1 ? string.Join(' ', parts[..^1]) : url.Authority;
            links.Add(new DetailLink(name, parts[^1]));
        }

        return links;
    }

    /// <summary>The links as the box spells them.</summary>
    /// <param name="links">The links.</param>
    /// <returns>One line each.</returns>
    internal static string LinksText(IEnumerable<DetailLink> links) =>
        string.Join(Environment.NewLine, links.Where(link => link.Served).Select(link => $"{link.Name}  {link.Url}"));

    private static IReadOnlyList<DetailLink>? ChangedLinks(ResolvedDetails beneath, string links)
    {
        var typed = ParseLinks(links);
        var under = beneath.Links.Where(link => link.Served).Select(link => link with { Served = true });
        return typed.Count == 0 || typed.SequenceEqual(under) ? null : typed;
    }

    /// <summary>Whether a row's edit can go into its project's <c>.freeharbor</c> folder.</summary>
    /// <param name="row">The row.</param>
    /// <returns>True for a compose project or service whose folder exists here.</returns>
    internal static bool CanSaveToFile(ContainerRow row) =>
        row.Project is not null && (row.IsProject || row.Service is not null)
        && row.WorkingDir is { } folder && Path.IsPathFullyQualified(folder) && Directory.Exists(folder);

    /// <summary>What a save into the project file writes, given what the file holds now.</summary>
    /// <param name="existing">The fields the file holds for this target, or null.</param>
    /// <param name="beneath">What the sources under the store say.</param>
    /// <param name="title">The title box.</param>
    /// <param name="description">The description box.</param>
    /// <param name="icon">The chosen icon, or null for automatic.</param>
    /// <param name="links">The links box, or null where the dialog never showed it.</param>
    /// <returns>The fields to write: what changed, and the file's own value wherever nothing did.</returns>
    internal static DetailFields ForTheFile(
        DetailFields? existing, ResolvedDetails beneath, string title, string description, string? icon,
        string? links = null)
    {
        ArgumentNullException.ThrowIfNull(beneath);
        var changed = Difference(beneath, title, description, icon, links);
        var linksUnchanged = links is null
            || ParseLinks(links).SequenceEqual(beneath.Links.Where(link => link.Served));
        return new DetailFields(
            changed.Title ?? (title.Trim() == beneath.Title ? existing?.Title : null),
            changed.Description
                ?? (description.Trim() == (beneath.Description ?? "") ? existing?.Description : null),
            icon ?? existing?.Icon,
            changed.Links ?? (linksUnchanged ? existing?.Links : null));
    }

    /// <summary>A project file's details with every pending edit applied to it.</summary>
    /// <param name="file">What the file holds now.</param>
    /// <param name="edits">Each target that changed, with what was typed for it.</param>
    /// <returns>The details to write: the file's own wherever no edit reached.</returns>
    internal static ProjectFileDetails Applied(
        ProjectFileDetails file,
        IEnumerable<(ContainerRow Row, string Title, string Description, string? Icon, string? Links)> edits)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(edits);

        var project = file.Project;
        var services = new Dictionary<string, DetailFields>(file.Services, StringComparer.Ordinal);
        foreach (var (row, title, description, icon, links) in edits)
        {
            if (row.DetailsBeneath is not { } beneath)
            {
                continue;
            }

            if (row.IsProject)
            {
                // A project's Open is its primary service's (DD297), so it keeps no links of its own.
                project = ForTheFile(project, beneath, title, description, icon);
            }
            else if (row.Service is { } service)
            {
                services[service] = ForTheFile(
                    services.GetValueOrDefault(service), beneath, title, description, icon, links);
            }
        }

        return new ProjectFileDetails(project, services);
    }

    private void FillShared(ContainerRow row)
    {
        var project = row.Project;
        Heading.Text = project is null
            ? $"Edit details · container {row.Name}"
            : $"Edit details · compose project {project}";

        var files = (row.ConfigFiles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);
        // DD333: the project's own file in mono, then each file merged over it by name.
        Files.Text = files.Length == 0 ? "" : files[0];
        MoreFiles.Text = string.Concat(files.Skip(1).Select(file => " + " + Path.GetFileName(file)));
        FilesRow.Visibility = files.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        SaveTo.Items.Add(new SaveTarget(SaveDestination.FreeHarbor));
        if (CanSaveToFile(row))
        {
            SaveTo.Items.Add(new SaveTarget(SaveDestination.ProjectFile));
            if (ComposeFiles(row).Count > 0)
            {
                SaveTo.Items.Add(new SaveTarget(SaveDestination.ComposeFile));
            }
        }

        // DD301: opened on the project's own choice, else the one Settings names, where it is there.
        var targets = SaveTo.Items.OfType<SaveTarget>().ToList();
        var chosen = _preference.For(SavePreference.KeyFor(row), [.. targets.Select(target => target.Destination)]);
        SaveTo.SelectedIndex = targets.FindIndex(target => target.Destination == chosen);
        SaveTo.Visibility = SaveTo.Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        SaveToLabel.Visibility = SaveTo.Visibility;
    }

    /// <summary>Show one target, from what was typed for it if anything was, or from its row.</summary>
    private void Load(ContainerRow row)
    {
        _loading = true;
        _row = row;
        _beneath = row.DetailsBeneath ?? row.Details!;
        var project = row.Project;

        // DD333: what kind of thing it is as a chip, and what it is called in bold mono after it.
        (EditingKind.Text, EditingName.Text) = row.IsProject && project is not null
            ? ("Compose project", project)
            : row.Service is { } service && project is not null
                ? ("Service", $"{project} / {service}")
                : ("Container", row.Name);
        // DD333: as the board's strip spells it, the files by name, the folder, the size.
        Identity.Text = row.IsProject
            ? $"files {ComposeFileNames(row)}   working dir {row.WorkingDir ?? "not recorded"}   services {row.Total}"
            : $"container {row.Name}   image {row.Image}   id {row.Id[..Math.Min(12, row.Id.Length)]}";
        KeyNote.Text = row.IsProject
            ? $"Saved under {project}, so it survives a recreate."
            : project is not null && row.Service is not null
            ? $"Saved under {project} / {row.Service}, so it survives a recreate."
            : Invented.IsMatch(row.Name)
                ? $"Docker made the name {row.Name} up, so these details stay with it and will not "
                  + "follow a new docker run. Give the container a --name, or label it "
                  + "with --label freeharbor.title=…"
                : $"Saved under the name {row.Name}.";

        var pending = _pending.GetValueOrDefault(row.DetailKey!);
        _iconValue = pending is not null ? pending.Icon : _store.For(row.DetailKey!)?.Icon;
        IconSearch.Text = "";
        // Invariant, because the window's words are English: under pt-BR N0 printed "2.935".
        IconSearchHint.Text = string.Create(
            System.Globalization.CultureInfo.InvariantCulture, $"Search {IconCatalogue.Names.Count:N0} icons");
        ListIcons();

        var shown = row.Details ?? _beneath;
        TitleBox.Text = pending?.Title ?? shown.Title;
        DescriptionBox.Text = pending?.Description ?? shown.Description ?? "";
        LinksBox.Text = pending?.Links ?? LinksText(shown.Links);
        LinksBox.IsEnabled = !row.IsProject;
        _loading = false;
        Refresh();
    }

    /// <summary>Keep what is typed for the target on screen, so moving along the rail loses nothing.</summary>
    private void Stash()
    {
        if (_row.DetailKey is { } key)
        {
            _pending[key] = new Edit(
                _row, TitleBox.Text, DescriptionBox.Text, _iconValue, _row.IsProject ? null : LinksBox.Text);
        }
    }

    private void Refresh()
    {
        if (_loading || !IsInitialized)
        {
            return;
        }

        var title = TitleBox.Text.Trim().Length == 0 ? _beneath.Title : TitleBox.Text.Trim();
        var description = DescriptionBox.Text.Trim();

        TitleSource.Text = TitleBox.Text.Trim() == _beneath.Title || TitleBox.Text.Trim().Length == 0
            ? $"from {_beneath.TitleSource}"
            : ContainerDetails.EditedHere;
        DescriptionSource.Text = description == (_beneath.Description ?? "") || description.Length == 0
            ? _beneath.Description is null ? "none" : $"from {_beneath.DescriptionSource}"
            : ContainerDetails.EditedHere;
        IconSource.Text = _iconValue is null ? $"from {_beneath.IconSource}" : ContainerDetails.EditedHere;
        LinksSource.Text = _row.IsProject
            ? "a project opens its primary service's link"
            : ChangedLinks(_beneath, LinksBox.Text) is null
                ? _beneath.LinksSource == "none" ? "none" : $"from {_beneath.LinksSource}"
                : ContainerDetails.EditedHere;
        Counter.Text = $"{DescriptionBox.Text.Length} / {DescriptionBox.MaxLength}";

        var target = SaveTo.SelectedItem as SaveTarget;
        var compose = target is { ToCompose: true } ? ComposeExtension.TargetFile(ComposeFiles(_row)) : null;
        Lands.Text = compose is not null
            ? $"Saves in the {ComposeExtension.Key} block of {Path.GetFileName(compose)}."
            : target is { ToFile: true }
                ? $"Saves in {ProjectFile.Folder}/{ProjectFile.FileName} in the project folder."
                : "Saves in FreeHarbor on this machine.";
        Lands.ToolTip = compose
            ?? (target is { ToFile: true }
                ? Path.Combine(_row.WorkingDir ?? "", ProjectFile.Folder, ProjectFile.FileName)
                : null);
        LandsDetail.Text = compose is not null
            ? "Only that block changes; the rest of the file is left exactly as it is."
            : target is { ToFile: true }
                ? "Anyone with the project sees these details once the file is committed."
                : "Only you see these details, and nothing in the project changes.";

        var icon = _iconValue switch
        {
            null => _beneath.Icon,
            var path when Path.IsPathFullyQualified(path) => new DetailIcon(IconKind.File, path),
            var name => new DetailIcon(IconKind.Bundled, name),
        };
        var glyph = IconTile.GlyphFor(icon);
        var picture = IconTile.PictureFor(icon);
        Tile.Background = Palette.TileBrushes[(_row.Details?.Hue ?? _beneath.Hue) % Palette.TileBrushes.Count];
        TileGlyph.Text = glyph ?? "";
        TilePicture.Source = picture;
        TileLetter.Text = glyph is null && picture is null ? IconTile.LetterFor(icon, title) : "";

        PreviewTitle.Text = title;
        PreviewDescription.Text = description.Length == 0 ? _beneath.Description ?? "" : description;
    }

    private void FieldChanged(object sender, RoutedEventArgs e) => Refresh();

    /// <summary>
    /// Fill the icon buttons: Automatic, the chosen icon, then the suggestions or what the search found.
    /// </summary>
    /// <remarks>
    /// A choice of what is drawn and never a free text box, so a typo cannot become a blank tile. The
    /// chosen icon is pinned after Automatic whatever the search shows, so narrowing the list never
    /// drops the selection and Save never writes something nobody picked (DD347).
    /// </remarks>
    private void ListIcons()
    {
        _listing = true;
        try
        {
            var query = IconSearch.Text.Trim();
            var names = query.Length == 0 ? SuggestedIcons : IconCatalogue.Search(query, IconSearchLimit);

            IconBox.Items.Clear();
            IconBox.Items.Add(new IconChoice($"Automatic ({_beneath.IconSource})", null));
            if (_iconValue is not null && !names.Contains(_iconValue, StringComparer.Ordinal))
            {
                IconBox.Items.Add(IconChoice.For(_iconValue));
            }

            foreach (var name in names)
            {
                IconBox.Items.Add(IconChoice.For(name));
            }

            IconBox.SelectedItem = IconBox.Items.Cast<IconChoice>()
                .FirstOrDefault(choice => choice.Value == _iconValue) ?? IconBox.Items[0];
            IconFound.Text = query.Length == 0 ? ""
                : names.Count == 0 ? "nothing found"
                : names.Count == IconSearchLimit ? $"first {IconSearchLimit}"
                : $"{names.Count} found";
        }
        finally
        {
            _listing = false;
        }
    }

    private void IconSearchChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading && IsInitialized)
        {
            ListIcons();
        }
    }

    private void IconPicked(object sender, RoutedEventArgs e)
    {
        if (_listing || IconBox.SelectedItem is not IconChoice choice)
        {
            return;
        }

        _iconValue = choice.Value;
        Refresh();
    }

    private void RailChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || Rail.SelectedItem is not RailItem { Row: var next } || ReferenceEquals(next, _row))
        {
            return;
        }

        Stash();
        Load(next);
    }

    private void ResetTitle(object sender, RoutedEventArgs e) => TitleBox.Text = _beneath.Title;

    private void ResetDescription(object sender, RoutedEventArgs e) =>
        DescriptionBox.Text = _beneath.Description ?? "";

    private void ResetIcon(object sender, RoutedEventArgs e)
    {
        _iconValue = null;
        ListIcons();
        Refresh();
    }

    private void ResetLinks(object sender, RoutedEventArgs e) => LinksBox.Text = LinksText(_beneath.Links);

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

        _iconValue = dialog.FileName;
        ListIcons();
        Refresh();
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
            // Explorer refused; the folder is still named in the header to open by hand.
        }
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        Stash();

        // DD301: a project whose Save to departs from the default is remembered, and one set back to
        // it is forgotten, so Settings lists exactly the projects that differ.
        if (SaveTo.SelectedItem is SaveTarget target)
        {
            _remember?.Invoke(_preference.Chose(SavePreference.KeyFor(_row), target.Destination));
        }

        var edits = _pending.Values
            .Select(edit => (edit.Row, edit.Title, edit.Description, edit.Icon, edit.Links))
            .ToList();
        if (edits.Count == 0)
        {
            DialogResult = true;
            return;
        }

        var cleared = new DetailFields(null, null, null);
        if (SaveTo.SelectedItem is SaveTarget { ToCompose: true } && _row.WorkingDir is { } composeFolder)
        {
            if (!SaveToCompose(composeFolder, edits))
            {
                return;
            }

            // The store is read first, so what it held for these keys would hide the file.
            edits.ForEach(edit => _store.Save(edit.Row.DetailKey!, cleared));
        }
        else if (SaveTo.SelectedItem is SaveTarget { ToFile: true } && _row.WorkingDir is { } folder)
        {
            var file = ProjectFile.For(folder)
                ?? new ProjectFileDetails(cleared, new Dictionary<string, DetailFields>());
            Saved = ProjectFile.Write(folder, Applied(file, edits)) is not null;
            if (Saved)
            {
                edits.ForEach(edit => _store.Save(edit.Row.DetailKey!, cleared));
            }
        }
        else
        {
            Saved = edits.All(edit => edit.Row.DetailsBeneath is { } beneath
                && _store.Save(
                    edit.Row.DetailKey!, Difference(beneath, edit.Title, edit.Description, edit.Icon, edit.Links)));
        }

        DialogResult = true;
    }

    /// <summary>The compose files a row's project was brought up with, those this machine can open.</summary>
    private static List<string> ComposeFiles(ContainerRow row) =>
    [
        .. (row.ConfigFiles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(file => Path.IsPathFullyQualified(file) && File.Exists(file)),
    ];

    /// <summary>Write the edits into the compose file's block, after showing the change once per project.</summary>
    /// <param name="folder">The folder compose ran in.</param>
    /// <param name="edits">Each target that changed.</param>
    /// <returns>Whether it was written; false where the user said no or the file could not be written.</returns>
    private bool SaveToCompose(
        string folder,
        IEnumerable<(ContainerRow Row, string Title, string Description, string? Icon, string? Links)> edits)
    {
        var files = ComposeFiles(_row);
        var target = ComposeExtension.TargetFile(files);

        // Owned by this dialog, so a confirmation always appears above it (DD227).
        Window owner = this;

        // What the block says now, from compose itself, so a field this dialog did not change keeps
        // the value it had, however the author spelled it. Half a second, and only on Save.
        var current = new ComposeExtension(new Core.Agent.BundledComposeCli()).Read(folder, files)
            ?? new ProjectFileDetails(new DetailFields(null, null, null), new Dictionary<string, DetailFields>());

        try
        {
            var bytes = File.ReadAllBytes(target);
            var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var (text, before, after) = ComposeExtension.Rewrite(
                File.ReadAllText(target), Applied(current, edits), Path.GetDirectoryName(target)!);

            var paths = new Core.Engine.EnginePaths();
            var settings = Core.Settings.TraySettings.Read(paths.Settings);
            var name = _row.Project ?? "";
            if (!settings.ComposeWritesAllowed.Contains(name, StringComparer.Ordinal))
            {
                var answer = System.Windows.MessageBox.Show(
                    owner,
                    $"FreeHarbor will change {target}.\n\n"
                    + (before.Length == 0 ? "It adds this block at the end:\n\n" : $"This block:\n\n{before}\n\nbecomes:\n\n")
                    + after
                    + "\n\nNothing else in the file changes. This is asked once for this project.",
                    "Change the compose file?",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                if (answer != MessageBoxResult.OK)
                {
                    return false;
                }

                (settings with { ComposeWritesAllowed = [.. settings.ComposeWritesAllowed, name] })
                    .Write(paths.Settings);
            }

            File.WriteAllText(target, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: bom));
            Saved = true;
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(owner, $"{target} could not be written: {failure.Message}", "Edit details",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>A project's compose files by name, comma-separated, or a word saying there are none.</summary>
    private static string ComposeFileNames(ContainerRow row)
    {
        var names = (row.ConfigFiles ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFileName)
            .ToList();
        return names.Count == 0 ? "none recorded" : string.Join(", ", names);
    }

    /// <summary>What was typed for one target.</summary>
    private sealed record Edit(ContainerRow Row, string Title, string Description, string? Icon, string? Links);

    /// <summary>One entry in the rail: the target's title over what compose calls it.</summary>
    /// <param name="Row">The target.</param>
    /// <param name="Heading">
    /// The caption drawn above it (DD333): <c>PROJECT</c> over the project, <c>SERVICES</c> over the
    /// first service, and nothing over the rest.
    /// </param>
    private sealed record RailItem(ContainerRow Row, string Heading = "")
    {
        /// <summary>Under the title: the project and its size, or the service and its container.</summary>
        public string Caption => Row.IsProject
            ? $"{Row.Project} · {Row.Total}"
            : $"{Row.Service ?? Row.Name} · {Row.Name}";

        /// <summary>Whether a caption is drawn above it.</summary>
        public bool HasHeading => Heading.Length > 0;
    }

    /// <summary>One place Save can write, shown by the name Settings gives it (DD301).</summary>
    /// <param name="Destination">Which of the three it is.</param>
    private sealed record SaveTarget(SaveDestination Destination)
    {
        /// <summary>Whether it is a file in the project rather than the store.</summary>
        public bool ToFile => Destination is not SaveDestination.FreeHarbor;

        /// <summary>Whether that file is the compose file's block rather than <c>.freeharbor</c>.</summary>
        public bool ToCompose => Destination is SaveDestination.ComposeFile;

        public override string ToString() => SavePreference.Name(Destination);
    }

    /// <summary>One entry in the icon list.</summary>
    /// <param name="Label">What the list shows.</param>
    /// <param name="Value">What is stored: a bundled name, a path, or null for automatic.</param>
    private sealed record IconChoice(string Label, string? Value)
    {
        /// <summary>The button for a stored value: a file by its name, an icon by its name and concept.</summary>
        public static IconChoice For(string value)
        {
            if (Path.IsPathFullyQualified(value))
            {
                return new IconChoice(Path.GetFileName(value), value);
            }

            // An image's icon saved before DD347 says which concept it draws: "postgres (database)".
            var concept = ContainerDetails.ConceptOf(value);
            return new IconChoice(
                concept is not null && concept != value ? $"{value} ({concept.Replace('-', ' ')})" : value.Replace('-', ' '),
                value);
        }

        /// <summary>The glyph its button draws (DD333), or empty where it draws its words instead.</summary>
        public string Glyph => Value is not null && !Path.IsPathFullyQualified(Value) ? IconTile.Glyph(Value) : "";

        /// <summary>What a button with no glyph says: <c>Auto</c> for automatic, a file's name for a file.</summary>
        public string Words => Glyph.Length > 0 ? "" : Value is null ? "Auto" : Label;

        public override string ToString() => Label;
    }
}
