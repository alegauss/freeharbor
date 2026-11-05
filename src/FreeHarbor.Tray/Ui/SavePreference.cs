namespace FreeHarbor.Tray.Ui;

/// <summary>Where an edit to names, descriptions and icons is saved (DD301).</summary>
public enum SaveDestination
{
    /// <summary>In FreeHarbor, on this machine only: the project folder is never touched.</summary>
    FreeHarbor,

    /// <summary>In a <c>.freeharbor</c> folder beside the compose file, shared through git.</summary>
    ProjectFile,

    /// <summary>In the compose file's own <c>x-freeharbor</c> block.</summary>
    ComposeFile,
}

/// <summary>
/// The user's choice of where edits are saved, and the projects that depart from it (DD301).
/// </summary>
/// <param name="Default">Where an edit goes unless its project says otherwise.</param>
/// <param name="Projects">
/// The projects whose edit dialog was set to somewhere else, by <see cref="KeyFor"/>: the folder
/// compose ran in where there is one, so two projects of one name in two folders are two entries.
/// </param>
/// <remarks>
/// A choice made once rather than a question asked on every save: the dialog opens on it, and a
/// project whose Save to is changed there remembers that, which is the list Settings shows.
/// </remarks>
public sealed record SavePreference(SaveDestination Default, IReadOnlyDictionary<string, SaveDestination> Projects)
{
    /// <summary>What a machine with nothing chosen does: save in FreeHarbor.</summary>
    public static SavePreference None { get; } =
        new(SaveDestination.FreeHarbor, new Dictionary<string, SaveDestination>(StringComparer.OrdinalIgnoreCase));

    /// <summary>The key a project's own choice is kept under: its folder, else its name.</summary>
    /// <param name="row">A project's header or one of its containers.</param>
    /// <returns>The key, or <see langword="null"/> for a container outside compose, which has no choice.</returns>
    public static string? KeyFor(ContainerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Project is null ? null : row.WorkingDir ?? row.Project;
    }

    /// <summary>Where the dialog opens: the project's own choice, else the default, else FreeHarbor.</summary>
    /// <param name="key">From <see cref="KeyFor"/>.</param>
    /// <param name="available">What this project can be saved to.</param>
    /// <returns>A destination that is available.</returns>
    /// <remarks>
    /// Falls back rather than refusing: a default of the compose file means nothing to a project
    /// whose compose file this machine cannot open, and FreeHarbor is always there.
    /// </remarks>
    public SaveDestination For(string? key, IReadOnlyCollection<SaveDestination> available)
    {
        ArgumentNullException.ThrowIfNull(available);

        if (key is not null && Projects.TryGetValue(key, out var own) && available.Contains(own))
        {
            return own;
        }

        return available.Contains(Default) ? Default : SaveDestination.FreeHarbor;
    }

    /// <summary>Record what a project's dialog saved to: kept where it departs from the default, dropped where it does not.</summary>
    /// <param name="key">From <see cref="KeyFor"/>; nothing changes for a standalone container.</param>
    /// <param name="chosen">Where the save went.</param>
    /// <returns>The preference after it.</returns>
    public SavePreference Chose(string? key, SaveDestination chosen)
    {
        if (key is null)
        {
            return this;
        }

        var projects = new Dictionary<string, SaveDestination>(Projects, StringComparer.OrdinalIgnoreCase);
        if (chosen == Default)
        {
            _ = projects.Remove(key);
        }
        else
        {
            projects[key] = chosen;
        }

        return this with { Projects = projects };
    }

    /// <summary>Make a project follow the default again.</summary>
    /// <param name="key">The project's key.</param>
    /// <returns>The preference after it.</returns>
    public SavePreference Follow(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var projects = new Dictionary<string, SaveDestination>(Projects, StringComparer.OrdinalIgnoreCase);
        _ = projects.Remove(key);
        return this with { Projects = projects };
    }

    /// <summary>What a destination is called on the Settings page and in the dialog.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>Its name.</returns>
    public static string Name(SaveDestination destination) => destination switch
    {
        SaveDestination.ProjectFile => ".freeharbor (the whole team)",
        SaveDestination.ComposeFile => "the compose file (x-freeharbor)",
        _ => "FreeHarbor (only you)",
    };

    /// <summary>
    /// The order details are read in, first wins, as the Settings page states it so the user can
    /// predict which value shows.
    /// </summary>
    public static IReadOnlyList<string> ReadOrder { get; } =
    [
        "Saved in FreeHarbor on this machine",
        "The project's .freeharbor/project.yaml",
        "The compose file's x-freeharbor block",
        "The service's own labels (freeharbor.*, then homepage.*)",
        "The image's labels",
        "The fallback: the service or container name, and a monogram",
    ];
}
