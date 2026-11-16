using System.Windows;

namespace FreeHarbor.Tray.Ui.Pages;

/// <summary>
/// What the user prefers: today, where edits to details are saved (DD301).
/// </summary>
/// <remarks>
/// It reads and writes the window's own memory (DD39), so a choice made here is the one the edit
/// dialog opens on next, and a project that saved somewhere else there shows up here.
/// </remarks>
internal partial class SettingsPage : System.Windows.Controls.UserControl
{
    private readonly WindowRecall _recall;
    private bool _drawing;

    /// <summary>Construct the page over the window's memory.</summary>
    /// <param name="recall">Where the preference is kept.</param>
    internal SettingsPage(WindowRecall recall)
    {
        ArgumentNullException.ThrowIfNull(recall);
        InitializeComponent();
        _recall = recall;
        ReadOrder.ItemsSource = SavePreference.ReadOrder
            .Select((source, index) => $"{(index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}. {source}")
            .ToList();
    }

    /// <summary>Draw what is remembered now: an edit dialog may have changed it since the last visit.</summary>
    internal void Refresh()
    {
        var preference = _recall.SavePreference;

        // The radio buttons are set from the store, and setting one raises Checked; this keeps that
        // from being read as the user choosing it and written straight back.
        _drawing = true;
        (preference.Default switch
        {
            SaveDestination.ProjectFile => ToProjectFile,
            SaveDestination.ComposeFile => ToComposeFile,
            _ => ToFreeHarbor,
        }).IsChecked = true;
        _drawing = false;

        var overrides = preference.Projects
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new KeyValuePair<string, string>(pair.Key, SavePreference.Name(pair.Value)))
            .ToList();
        Overrides.ItemsSource = overrides;
        NoOverrides.Visibility = overrides.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DefaultChosen(object sender, RoutedEventArgs e)
    {
        if (_drawing || sender is not FrameworkElement { Tag: string name }
            || !Enum.TryParse<SaveDestination>(name, out var destination))
        {
            return;
        }

        // A project that had chosen what is now the default no longer departs from it.
        var preference = _recall.SavePreference with { Default = destination };
        foreach (var same in preference.Projects.Where(pair => pair.Value == destination).Select(pair => pair.Key).ToList())
        {
            preference = preference.Follow(same);
        }

        _recall.RememberSavePreference(preference);
        Refresh();
    }

    /// <summary>Take the engine off this machine, once asked twice (DD317).</summary>
    /// <param name="sender">Unused.</param>
    /// <param name="e">Unused.</param>
    /// <remarks>
    /// The same stop and the same <see cref="Core.Engine.EngineRemoval"/> the verb runs, off the
    /// thread that draws. What it printed is shown under the button, because the space that came
    /// back and a folder WSL still held are both things the reader acts on.
    /// </remarks>
    private async void RemoveTheEngine(object sender, RoutedEventArgs e)
    {
        var paths = new Core.Engine.EnginePaths();
        var answer = System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            Core.Engine.EngineRemoval.Confirmation(Core.Engine.EngineRemoval.DiskBytes(paths)),
            "Remove the engine",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (answer is not System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        RemoveEngineButton.IsEnabled = false;
        RemovalSaid.Visibility = Visibility.Visible;
        RemovalSaid.Text = "Stopping the engine and removing it…";
        try
        {
            var removal = await Task.Run(() =>
            {
                Cli.EngineCommand.StopForRemoval();
                return new Core.Engine.EngineRemoval(new Core.Engine.Wsl(), paths).Remove(all: false);
            }).ConfigureAwait(true);

            RemovalSaid.Text = string.Join(Environment.NewLine, removal.Lines);
        }
        finally
        {
            RemoveEngineButton.IsEnabled = true;
        }
    }

    private void FollowDefault(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string key })
        {
            _recall.RememberSavePreference(_recall.SavePreference.Follow(key));
            Refresh();
        }
    }
}
