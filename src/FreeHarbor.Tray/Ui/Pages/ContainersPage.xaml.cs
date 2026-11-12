using System.Diagnostics;
using System.IO;
using System.Windows;
using FreeHarbor.Core.Api;
using FreeHarbor.Core.Engine;

namespace FreeHarbor.Tray.Ui.Pages;

/// <summary>
/// The containers list, and everything that is only about containers (DD35).
/// </summary>
/// <remarks>
/// It owns its header, its rows, the four verbs, the log windows it opens and the shell it launches.
/// What it does not own is the engine banner or the strip above it: the shell owns the chrome, and a
/// list owns its page. Before this, one window class held three of these, and DD12 and networks were
/// each about to add a fourth copy of the same stanza.
///
/// <para>Built the first time it is navigated to and then kept, collapsed, for the life of the window
/// — so a scroll position and a list already read survive switching away. That is claude-tray's
/// destination host, and it is why this is a <see cref="System.Windows.Controls.UserControl"/> rather
/// than a <c>TabItem</c>, which tears its content down on every switch.</para>
/// </remarks>
internal partial class ContainersPage : System.Windows.Controls.UserControl
{
    private readonly IEngineClient _api;
    private readonly Func<EngineState> _engineState;
    private readonly Action _startEngine;
    private readonly RowActivity _activity = new();
    private readonly Dictionary<string, LogWindow> _logs = new(StringComparer.Ordinal);

    /// <summary>Construct the page.</summary>
    /// <param name="api">The Engine API client.</param>
    /// <param name="engineState">What the engine is doing, asked at render time.</param>
    /// <param name="startEngine">What the empty state's button does.</param>
    internal ContainersPage(IEngineClient api, Func<EngineState> engineState, Action startEngine)
    {
        InitializeComponent();
        _api = api;
        ProjectFiles = (folder, files) =>
            [
                .. new[] { Core.Details.ProjectFile.For(folder), _extension.For(folder, files) }
                    .Where(details => details is not null)
                    .Select(details => details!),
            ];
        _extension.Changed += (_, _) => Dispatcher.InvokeAsync(() => RefreshAsync());
        _engineState = engineState;
        _startEngine = startEngine;
    }

    /// <summary>
    /// What a project's own file says, by the folder compose recorded (DD295).
    /// </summary>
    /// <remarks>
    /// Given the folder compose ran in and the compose files it read. By default
    /// <c>.freeharbor/project.yaml</c> over the compose file's <c>x-freeharbor</c> block, field by
    /// field. <c>--fixture</c> sets the sample's instead, because the folder the sample names exists
    /// on no machine that captures it. A property and not a constructor seam, so the shell that
    /// builds this page carries none of it.
    /// </remarks>
    internal Func<string?, string?, IReadOnlyList<Core.Details.ProjectFileDetails>> ProjectFiles { get; set; }

    /// <summary>
    /// What was saved in FreeHarbor on this machine (DD296), read before any file and written by the
    /// edit dialog. A property for the reason <see cref="ProjectFiles"/> is one.
    /// </summary>
    internal Core.Details.DetailStore Saved { get; set; } = new(new EnginePaths().Details);

    // The x-freeharbor reader (DD295). Compose takes half a second, so it reads in the background
    // and says when it has something new; the page redraws once then, instead of waiting on it.
    private readonly Core.Details.ComposeExtension _extension =
        new(new Core.Agent.BundledComposeCli());

    /// <summary>
    /// Re-read the list. Called when an event says the list changed, never on a timer and never
    /// from a refresh button: the stream is what makes this correct.
    /// </summary>
    /// <param name="settled">
    /// The container an event just arrived for, whose pending state that event confirms. This is
    /// what ends a wait — not the HTTP response, which only says the daemon took the call.
    /// </param>
    /// <returns>A task that completes when the window has been redrawn.</returns>
    internal async Task RefreshAsync(string? settled = null)
    {
        if (settled is not null)
        {
            _activity.Settled(settled);
        }

        var engine = _engineState();

        IReadOnlyList<ContainerRow> rows = [];
        if (engine is EngineState.Running)
        {
            try
            {
                var containers = await _api.ContainersAsync().ConfigureAwait(true);

                // Projected once, and the order is the whole of DD110: the prune used to reach for
                // `ContainerRow.From` to read each container's project label and then this line
                // built the same rows again three lines below, so every row was made twice per
                // refresh and half of them discarded after one property was read.
                // DD295: each project's own file is read here too, one stat per project when nothing
                // changed, so an edit to it shows on the next event with nothing recreated.
                var projected = containers
                    .Select(container => ContainerRow.From(container, ProjectFiles, Saved))
                    .ToList();

                // Before the dressing and never after, because `Dress` reads exactly the state this
                // is about to drop. The project headers' ids go in too (DD107): a header carries its
                // own count of refusals and the daemon has never heard of `compose:shop`, so pruning
                // against container ids alone would forget it on the very next event.
                _activity.Prune(
                [
                    .. projected.Select(row => row.Id),
                    .. projected
                        .Select(row => row.Project)
                        .Where(project => project is not null)
                        .Distinct(StringComparer.Ordinal)
                        .Select(project => ContainerRow.ProjectId(project!)),
                ]);

                // The page's other keyed set, narrowed here and nowhere else, for the reason above:
                // this is the one place the daemon has actually answered, so it is the only evidence
                // that a project is gone rather than unreachable (DD112).
                _collapsed = ContainerRow.FoldedStillThere(_collapsed, projected);
                var present = ContainerRow.FoldedStillThere(
                    _openTiers.Select(ContainerRow.ProjectOfTierKey).ToHashSet(StringComparer.Ordinal), projected);
                _ = _openTiers.RemoveWhere(key => !present.Contains(ContainerRow.ProjectOfTierKey(key)));

                // Resolved once for the whole render rather than per row: this list is rebuilt on
                // every engine event, and a FindResource per row is a dictionary walk per row.
                var style = RowStyle.For(this);
                rows = [.. projected.Select(_activity.Dress).Select(row => row.WithChip(style))];
            }
            catch (DockerApiException)
            {
                // The engine went away between the event and this call. The empty state below says
                // so, which is more use than a dialog about a race the user did not cause.
                //
                // Deliberately no prune on this path: there is no list, so nothing here is evidence
                // that a container went away — and pruning against nothing would forget every wait
                // and every failure in hand on the strength of one refused call.
                rows = [];
            }
        }

        _rows = rows;
        _answered = true;
        Show();
    }

    /// <summary>Whether a refresh has run, so an empty <see cref="_rows"/> means empty rather than not yet read.</summary>
    private bool _answered;

    /// <summary>What the daemon last returned, before the sort and the filter are applied.</summary>
    /// <remarks>
    /// Kept so a heading click and a keystroke redraw from these rather than asking the engine again:
    /// the answer is already here, and the question is about presentation.
    /// </remarks>
    private IReadOnlyList<ContainerRow> _rows = [];

    /// <summary>The list, reconciled rather than replaced, so a new row can say so (DD70).</summary>
    private LiveRows<ContainerRow>? _liveRows;

    private LiveRows<ContainerRow> _live =>
        _liveRows ??= new LiveRows<ContainerRow>(Containers, row => row.Id);

    /// <summary>Draw the rows in hand, shaped, and the detail page over them where one is open.</summary>
    private void Show()
    {
        ShowList();
        ShowCards();
        ShowDetail();
    }

    // ---- the cards view (DD300) ---------------------------------------------------------------------

    /// <summary>Whether the page shows its cards rather than its list.</summary>
    private bool _cards;

    /// <summary>What to tell when the view is chosen, so the window can remember it (DD39).</summary>
    internal Action<bool>? ViewChosen { get; set; }

    /// <summary>The narrowest a card is drawn, from the spec: the grid fills the width with cards this wide or wider.</summary>
    private const double CardWidth = 400;

    /// <summary>How many cards fit across, which the cards' UniformGrid binds to.</summary>
    public static readonly DependencyProperty CardColumnsProperty = DependencyProperty.Register(
        nameof(CardColumns), typeof(int), typeof(ContainersPage), new PropertyMetadata(1));

    /// <summary>How many cards fit across.</summary>
    public int CardColumns
    {
        get => (int)GetValue(CardColumnsProperty);
        set => SetValue(CardColumnsProperty, value);
    }

    // ---- the state chips (DD300) --------------------------------------------------------------------

    /// <summary>The state chip in force, which narrows both views before the text filter and the sort.</summary>
    private StateChip _chip = StateChip.All;

    /// <summary>What to tell when a chip is chosen, so the window can remember it (DD39).</summary>
    internal Action<StateChip>? ChipChosen { get; set; }

    /// <summary>The rows in hand that the chip in force keeps.</summary>
    private IReadOnlyList<ContainerRow> Chipped =>
        _chip is StateChip.All ? _rows : [.. _rows.Where(row => ContainerRow.Passes(row, _chip))];

    /// <summary>Hold a chip without telling anyone: a capture names the one it draws.</summary>
    /// <param name="chip">The chip.</param>
    internal void UseChip(StateChip chip) => _chip = chip;

    private void ChooseChip(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string name } && Enum.TryParse<StateChip>(name, out var chip)
            && chip != _chip)
        {
            _chip = chip;
            ChipChosen?.Invoke(chip);
            Show();
        }
    }

    /// <summary>Each chip with its count over every row in hand, the one in force filled, and the counts line.</summary>
    private void DrawChips()
    {
        foreach (var (button, chip) in new[]
                 {
                     (ChipAll, StateChip.All), (ChipRunning, StateChip.Running),
                     (ChipStopped, StateChip.Stopped), (ChipProblems, StateChip.Problems),
                 })
        {
            var count = _rows.Count(row => ContainerRow.Passes(row, chip));

            // DD326: the word, then the count dimmed, so the eye reads which chip before how many.
            var label = new System.Windows.Controls.TextBlock();
            label.Inlines.Add(new System.Windows.Documents.Run(chip.ToString()));
            label.Inlines.Add(new System.Windows.Documents.Run(
                "  " + count.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorTertiaryBrush"),
            });
            button.Content = label;
            button.Style = (Style)FindResource(chip == _chip ? "PillChosen" : "Pill");
        }

        Counts.Text = _rows.Count > 0 ? ContainerRow.Counts(_rows) : "";
    }

    /// <summary>The window's memory, which also holds where edits are saved (DD301); none in a test.</summary>
    private WindowRecall? _recall;

    /// <summary>
    /// Open on the view this window was left on, and write the choice down each time it changes (DD39).
    /// </summary>
    /// <param name="recall">The window's memory.</param>
    /// <remarks>
    /// Here and not in the shell, which owns the chrome and nothing else (DD35): the view is this
    /// page's, so the shell only hands over where it is remembered.
    /// </remarks>
    internal void RecallView(WindowRecall recall)
    {
        ArgumentNullException.ThrowIfNull(recall);
        _recall = recall;
        _chip = recall.ContainersChip;
        UseCards(recall.ContainersAsCards);
        ViewChosen = recall.RememberContainersView;
        ChipChosen = recall.RememberContainersChip;
    }

    /// <summary>Open on the cards or on the list, without telling anyone: this is the remembered choice arriving.</summary>
    /// <param name="cards">Whether to show the cards.</param>
    internal void UseCards(bool cards)
    {
        _cards = cards;
        Show();
    }

    private void ChooseCards(object sender, RoutedEventArgs e) => Choose(cards: true);

    private void ChooseList(object sender, RoutedEventArgs e) => Choose(cards: false);

    private void Choose(bool cards)
    {
        if (_cards == cards)
        {
            return;
        }

        _cards = cards;
        ViewChosen?.Invoke(cards);
        Show();
    }

    private void CardsPaneSized(object sender, SizeChangedEventArgs e) =>
        CardColumns = Math.Max(1, (int)(e.NewSize.Width / CardWidth));

    /// <summary>A click on a card outside its buttons opens the project's page (DD299, DD300).</summary>
    private void CardClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DetailView view })
        {
            _detailKey = view.Key;
            Show();
        }
    }

    /// <summary>A large card's Show all opens the project's page, where every service is (DD298).</summary>
    private void ShowAllServices(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DetailView view })
        {
            _detailKey = view.Key;
            Show();
        }
    }

    /// <summary>
    /// Draw the cards over the same shaped rows the list just drew, or put them away.
    /// </summary>
    private void ShowCards()
    {
        CardsSwitch.Style = (Style)FindResource(_cards ? "SegmentChosen" : "Segment");
        ListSwitch.Style = (Style)FindResource(_cards ? "Segment" : "SegmentChosen");

        // The list's own empty state still speaks for both: no rows is no rows in either view.
        if (!_cards || Empty.Visibility == Visibility.Visible)
        {
            CardsPane.Visibility = Visibility.Collapsed;
            return;
        }

        var (projects, standalone) = DetailView.Cards(Chipped, _shape);
        ProjectCards.ItemsSource = projects.Select(view => view with { Head = Dressed(view.Head) }).ToList();
        StandaloneCards.ItemsSource = standalone;
        StandaloneHeading.Visibility = projects.Count > 0 && standalone.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        CardsPane.Visibility = Visibility.Visible;
        Containers.Visibility = Visibility.Collapsed;
        HeaderRow.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Dress a header made outside the list the way the list dresses its own: its refusals by id, the
    /// wait its containers are under, and the theme's chip.
    /// </summary>
    private ContainerRow Dressed(ContainerRow head) =>
        head.IsProject
            ? ContainerRow.WithProjectWait(_activity.Dress(head), _rows).WithChip(RowStyle.For(this))
            : head;

    /// <summary>
    /// What the detail page is open on (DD299), or <see langword="null"/> while the list is showing.
    /// </summary>
    /// <remarks>
    /// A key and not a view: the view is rebuilt from the rows in hand on every redraw, so the page
    /// follows the engine's events the way the list does.
    /// </remarks>
    private string? _detailKey;

    /// <summary>Lay the detail page over the list, or take it away.</summary>
    private void ShowDetail()
    {
        var view = _detailKey is null ? null : DetailView.For(_detailKey, _rows);

        // What it was opened on went away (removed, or the engine stopped): back to the list, which
        // says what is there now, rather than a page about nothing. Only once the engine has
        // answered, though: a page asked for before the first refresh (a capture's --detail) has no
        // rows to find it in yet, and forgetting it then drew the list where the page was asked for.
        if (view is null)
        {
            if (_answered)
            {
                _detailKey = null;
            }

            _outputFor = null;
            Detail.Visibility = Visibility.Collapsed;
            Detail.DataContext = null;
            Toolbar.Visibility = Visibility.Visible;
            Summary.Visibility = Visibility.Visible;
            return;
        }

        // The header the page is about is made here as the list's are, so it is dressed the same way:
        // the theme's chip, its refusals by id, and the wait its containers are under.
        Detail.DataContext = view with { Head = Dressed(view.Head) };

        // DD328: the item the page is open on is marked, so the rail says where the reader is.
        ContainerRow Marked(ContainerRow row) =>
            string.Equals(DetailView.KeyFor(row), _detailKey, StringComparison.Ordinal)
                ? row with { IsRailSelected = true }
                : row;
        RailProjects.ItemsSource = DetailView.RailProjects(_rows, _railFilter).Select(Marked).ToList();
        RailStandalone.ItemsSource = DetailView.RailStandalone(_rows, _railFilter).Select(Marked).ToList();

        var output = view.OutputService;
        OutputOpenLog.Tag = output;
        OutputCaption.Text = output is null ? "RECENT OUTPUT" : $"RECENT OUTPUT · {output.Title}";
        if (!string.Equals(output?.Id, _outputFor, StringComparison.Ordinal))
        {
            _outputFor = output?.Id;
            Output.ItemsSource = null;
            if (output is not null)
            {
                _ = ReadOutputAsync(output.Id);
            }
        }

        Detail.Visibility = Visibility.Visible;
        Containers.Visibility = Visibility.Collapsed;
        HeaderRow.Visibility = Visibility.Collapsed;
        Empty.Visibility = Visibility.Collapsed;
        CardsPane.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Collapsed;
        Summary.Visibility = Visibility.Collapsed;
    }

    /// <summary>What the rail's box holds (DD299).</summary>
    private string? _railFilter;

    /// <summary>The container the Recent output pane last read, so a redraw does not read it again.</summary>
    private string? _outputFor;

    /// <summary>Narrow the rail as it is typed, over the rows in hand.</summary>
    private void RailFilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _railFilter = RailFilter.Text;
        ShowDetail();
    }

    /// <summary>
    /// Read the last lines a container wrote, once, into the Recent output pane (DD299).
    /// </summary>
    /// <remarks>
    /// Not followed: the pane is a glance at why something stopped, and a stream held open for every
    /// page somebody looks at is a reader nobody closes. Open log is the followed view. Asked whether
    /// the container has a TTY first, for <see cref="LogFrames"/>'s reason.
    /// </remarks>
    private async Task ReadOutputAsync(string id)
    {
        try
        {
            var container = await _api.InspectAsync(id).ConfigureAwait(true);
            await using var stream = await _api.LogsAsync(id, tail: 200, follow: false).ConfigureAwait(true);
            var frames = new LogFrames(stream, framed: !container.Config.Tty);
            var chunks = new List<LogChunk>();
            while (await frames.ReadAsync().ConfigureAwait(true) is { } chunk)
            {
                chunks.Add(chunk);
            }

            // The page may have moved on while this read: only the container still asked for draws.
            if (string.Equals(_outputFor, id, StringComparison.Ordinal))
            {
                Output.ItemsSource = DetailView.Tail(chunks, DetailView.OutputLines);
            }
        }
        catch (DockerApiException failure)
        {
            if (string.Equals(_outputFor, id, StringComparison.Ordinal))
            {
                Output.ItemsSource = new[] { new OutputLine(failure.Detail ?? failure.Message, IsError: true) };
            }
        }
    }

    /// <summary>Open the detail page on a project or a standalone container (DD299).</summary>
    private void ShowDetails(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContainerRow row })
        {
            _detailKey = DetailView.KeyFor(row);
            Show();
        }
    }

    /// <summary>Open the detail page on a compose project by name, drawn on the next redraw.</summary>
    /// <param name="project">The project's compose name.</param>
    internal void OpenDetail(string project) => _detailKey = ContainerRow.ProjectId(project);

    /// <summary>Whether the detail page is showing, so a capture can tell a name that opened nothing.</summary>
    internal bool DetailShowing => Detail.Visibility == Visibility.Visible;

    /// <summary>Back to the list, at the scroll position it was left at: it was never torn down.</summary>
    private void CloseDetails(object sender, RoutedEventArgs e)
    {
        _detailKey = null;
        Show();
    }

    /// <summary>Draw the rows in hand, shaped.</summary>
    private void ShowList()
    {
        // The headers are made by the grouping, so they are dressed after it rather than with the
        // containers: the count that came back refused is state about the header, and there is no
        // header until here (DD107). Its wait is not stored beside that count — it is read off the
        // containers, which end their own waits on the events that end them (DD108).
        DrawChips();
        var shown = ContainerRow.Grouped(Chipped, _shape, _collapsed, _openTiers);

        // DD297: a header's chip is drawn too, so it is dressed in the theme's brushes here, the one
        // place headers exist.
        var style = RowStyle.For(this);
        shown =
        [
            .. shown.Select(row => row.IsProject
                ? ContainerRow.WithProjectWait(_activity.Dress(row), _rows).WithChip(style)
                : row),
        ];

        _live.Show(shown);
        NameHeading.Content = ContainerRow.Columns.Name + _shape.GlyphFor(ContainerRow.Columns.Name);
        ImageHeading.Content = ContainerRow.Columns.Image + _shape.GlyphFor(ContainerRow.Columns.Image);
        StateHeading.Content = ContainerRow.Columns.State + _shape.GlyphFor(ContainerRow.Columns.State);
        StatusHeading.Content = ContainerRow.Columns.Status + _shape.GlyphFor(ContainerRow.Columns.Status);
        PortsHeading.Content = ContainerRow.Columns.Ports + _shape.GlyphFor(ContainerRow.Columns.Ports);

        var empty = shown.Count == 0;
        Containers.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        HeaderRow.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        Empty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

        if (!empty)
        {
            return;
        }

        // The third empty state. "No containers" and "nothing matched api" are different answers, and
        // only one of them is fixed by clearing a box.
        // DD300: emptied by a chip, which is cleared by choosing All rather than by clearing the box.
        if (_chip is not StateChip.All && _rows.Count > 0)
        {
            EmptyHeadline.Text = _chip switch
            {
                StateChip.Running => "Nothing is running",
                StateChip.Stopped => "Nothing is stopped",
                _ => "No problems",
            };
            EmptyDetail.Text = "Choose All to see every container.";
            EmptyStart.Visibility = Visibility.Collapsed;
            return;
        }

        if (_shape.EmptyBecauseFiltered("containers") is var (headline, detail) && _rows.Count > 0)
        {
            (EmptyHeadline.Text, EmptyDetail.Text) = (headline, detail);
            EmptyStart.Visibility = Visibility.Collapsed;
            return;
        }

        var state = EmptyState.For(_engineState());
        EmptyHeadline.Text = state.Headline;
        EmptyDetail.Text = state.Detail;
        EmptyStart.Visibility = state.OffersStart ? Visibility.Visible : Visibility.Collapsed;
    }


    /// <summary>
    /// The sort and the filter, held by the page rather than by the controls.
    /// </summary>
    /// <remarks>
    /// This is the part DD37 calls easy to get wrong. The list redraws on every engine event, so a
    /// shape that lived only in the ListView would be thrown away each time and snap back to its
    /// default while somebody was reading it.
    /// </remarks>
    private ListShape _shape = new(ContainerRow.DefaultColumn, Descending: false);

    /// <summary>
    /// The projects the user has folded away (DD106).
    /// </summary>
    /// <remarks>
    /// Beside <see cref="_shape"/> and for exactly its reason: DD37 says presentation cannot live in
    /// the ListView because the list is rebuilt on every engine event, and a collapse held there
    /// would spring open while somebody was reading it. Keyed by the project's name, which is the
    /// only handle a project has — and narrowed on every answered refresh, so a project's absence
    /// forgets its fold rather than holding it against the next one to use that name (DD112).
    /// </remarks>
    private HashSet<string> _collapsed = new(StringComparer.Ordinal);

    /// <summary>
    /// The healthy tiers somebody opened, by <see cref="ContainerRow.TierKey"/> (DD298).
    /// </summary>
    /// <remarks>
    /// Beside <see cref="_collapsed"/> for its reason, and narrowed with it: a project's absence
    /// forgets its tiers' folds as it forgets its own (DD112).
    /// </remarks>
    private readonly HashSet<string> _openTiers = new(StringComparer.Ordinal);

    /// <summary>Fold a project away, or open it again.</summary>
    /// <remarks>
    /// Redrawn from the rows in hand: which rows are hidden is presentation, and asking the daemon
    /// about it would be a round trip to answer a question this window already knows.
    /// </remarks>
    private void ToggleProject(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ContainerRow { IsProject: true, Project: { } project } row })
        {
            return;
        }

        // DD298: a tier's fold is held the other way round, as the tiers somebody opened, because a
        // healthy tier starts folded. A tier with a problem is never folded away, so its click does
        // nothing rather than hide the thing that needs looking at.
        if (row is { IsTier: true, Tier: { } tier })
        {
            if (row.Problems > 0)
            {
                return;
            }

            var key = ContainerRow.TierKey(project, tier);
            if (!_openTiers.Add(key))
            {
                _ = _openTiers.Remove(key);
            }

            Show();
            return;
        }

        if (!_collapsed.Add(project))
        {
            _ = _collapsed.Remove(project);
        }

        Show();
    }

    /// <summary>Re-sort on a heading click, and redraw from the rows already in hand.</summary>
    private void SortBy(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string column })
        {
            return;
        }

        // A size reads best biggest-first and a name does not, so a column starts at its own natural
        // direction rather than at whatever the last one happened to be.
        _shape = _shape.Toggled(column, descendsFirst: column is ContainerRow.Columns.Ports);
        Show();
    }

    /// <summary>Re-narrow as it is typed, over the rows in hand.</summary>
    private void FilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _shape = _shape.Narrowed((sender as System.Windows.Controls.TextBox)?.Text);
        Show();
    }

    private void OpenPort(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url } && url.Length > 0)
        {
            // The whole reason ports are links: making somebody retype localhost:8080 is a small
            // daily tax a GUI exists to remove.
            // The working directory is named and not inherited (DD261): what opens a link is the
            // user's browser, and it is not something to hand a lock on this window's directory.
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.SystemDirectory,
            })?.Dispose();
        }
    }

    /// <summary>
    /// Open a large project's menu of every link: Pinned, then All, with a filter box past five (DD298).
    /// </summary>
    /// <remarks>
    /// Built here on each click rather than bound in markup: its items are the filter's answer, and
    /// a menu rebuilt as it is typed into is code either way.
    /// </remarks>
    private void OpenLinkMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: ContainerRow { ProjectLinks: { Count: > 0 } links } } button)
        {
            return;
        }

        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = button,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
        };
        var box = new System.Windows.Controls.TextBox { MinWidth = 240, ToolTip = "Filter links" };
        var fixedItems = links.Count >= DetailView.LinkFilterFrom
            ? new object[] { new System.Windows.Controls.MenuItem { Header = box, StaysOpenOnClick = true } }
            : [];

        void Fill()
        {
            menu.Items.Clear();
            foreach (var item in fixedItems)
            {
                menu.Items.Add(item);
            }

            var shown = DetailView.Matching(links, box.Text);
            Section("PINNED", [.. shown.Where(link => link.IsPinned)]);
            Section("ALL", shown);
        }

        void Section(string caption, IReadOnlyList<Endpoint> section)
        {
            if (section.Count == 0)
            {
                return;
            }

            menu.Items.Add(new System.Windows.Controls.MenuItem { Header = caption, IsEnabled = false });
            foreach (var link in section)
            {
                var item = new System.Windows.Controls.MenuItem
                {
                    Header = $"{link.Label}  ·  {link.Service}  ·  {link.Address}",
                    Tag = link.Url ?? link.CopyText,
                    ToolTip = link.CanOpen ? link.Url : $"Copy {link.CopyText}",
                };
                item.Click += link.CanOpen ? OpenPort : CopyAddress;
                menu.Items.Add(item);
            }
        }

        box.TextChanged += (_, _) => Fill();
        Fill();
        button.ContextMenu = menu;
        menu.IsOpen = true;
        _ = box.Focus();
    }

    /// <summary>Copy the address of a port that does not speak HTTP (DD297).</summary>
    /// <remarks>
    /// A browser cannot open localhost:5432, and the client that can wants the address pasted in, so
    /// copying it is the click's one useful outcome.
    /// </remarks>
    private void CopyAddress(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string address } && address.Length > 0)
        {
            System.Windows.Clipboard.SetText(address);
        }
    }

    private void StartEngine(object sender, RoutedEventArgs e) => _startEngine();

    /// <summary>
    /// Open this container's log, or bring the window already open on it to the front.
    /// </summary>
    /// <remarks>
    /// One window per container, keyed by id. Pressing Logs twice on the same row opening a second
    /// window would mean two streams against one container and two buffers filling in parallel.
    /// </remarks>
    private void OpenLogs(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ContainerRow row })
        {
            return;
        }

        if (_logs.TryGetValue(row.Id, out var open))
        {
            open.Activate();
            return;
        }

        var shell = Window.GetWindow(this) as MainWindow;
        var window = new LogWindow(_api, row.Id, row.Name) { Owner = shell };

        // DD39: the child stays centred on its owner, which is right for a child — it is the size that
        // was being thrown away. A log read at half the screen came back at the markup's 900x600 every
        // time, and it is set before Show so there is no resize to watch.
        if (shell?.Recall.LogSize is { } size)
        {
            window.Width = size.Width;
            window.Height = size.Height;
        }

        _logs[row.Id] = window;
        window.Closing += (_, _) =>
            shell?.Recall.RememberLogSize(window.ActualWidth, window.ActualHeight);
        window.Closed += (_, _) => _logs.Remove(row.Id);
        window.Show();
    }

    /// <summary>
    /// Ask the container which shell it has, then hand the terminal the user already has a
    /// <c>docker exec</c> against it.
    /// </summary>
    /// <remarks>
    /// The probe is why the row goes pending first: two round trips to the daemon before any window
    /// appears, and a click that looks like it did nothing for that long is a click people press
    /// again. An image with no shell says so on the row rather than opening a terminal that closes.
    /// </remarks>
    private async void OpenShell(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ContainerRow row })
        {
            return;
        }

        _activity.Began(row.Id, ContainerVerb.Shell);
        Redress();
        try
        {
            var shell = await ContainerShell
                .FindAsync((command, token) => _api.RunInContainerAsync(row.Id, command, token))
                .ConfigureAwait(true);

            if (shell is null)
            {
                _activity.Failed(row.Id, ContainerShell.NoShellMessage(row.Name));
                return;
            }

            var launch = ContainerShell.LaunchFor(
                Terminals.Choose(File.Exists), new EnginePaths().DockerCli, row.Id, shell);

            // Named and not inherited (DD261). This is a terminal somebody is about to work in, so
            // it lives as long as they do, and its own directory is inside the container anyway.
            var start = new ProcessStartInfo(launch.FileName)
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.SystemDirectory,
            };
            foreach (var argument in launch.Arguments)
            {
                start.ArgumentList.Add(argument);
            }

            Process.Start(start)?.Dispose();
            _activity.Settled(row.Id);
        }
        catch (DockerApiException failure)
        {
            _activity.Failed(row.Id, failure.Detail ?? failure.Message);
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception
            or InvalidOperationException or IOException)
        {
            // The terminal is not where it was expected, or Windows refused to start it. The row is
            // where the click was, so the row is where this goes.
            _activity.Failed(row.Id, $"the terminal would not start: {failure.Message}");
        }
        finally
        {
            Redress();
        }
    }

    /// <summary>
    /// The one verb the row shows, which is Stop for a live container and Start for anything else.
    /// </summary>
    /// <remarks>
    /// Decided from the row rather than from two buttons with a visibility binding each: the row
    /// already knows which verb it is offering, and reading it in two places is how the two answers
    /// drift apart.
    /// </remarks>
    private void PrimaryAction(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContainerRow row })
        {
            Send(row, row.CanStop ? ContainerVerb.Stop : ContainerVerb.Start);
        }
    }

    /// <summary>
    /// Open the row's overflow, on a left click rather than only on a right one.
    /// </summary>
    /// <remarks>
    /// A context menu that only answers the right button is the discovery problem the section warns
    /// about wearing a different hat. Placed under the button so it reads as belonging to that row and
    /// not to wherever the pointer happened to be.
    /// </remarks>
    private void OpenOverflow(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            return;
        }

        // On the detail page the ⋯ belongs to the page itself, so Details would open what is already
        // open and seem to do nothing. It goes, and the separator under it with it.
        var leadsWithDetails = !DetailShowing;
        if (menu.Items.Count > 1 && menu.Items[0] is System.Windows.Controls.MenuItem { Header: "Details" } details
            && menu.Items[1] is System.Windows.Controls.Separator rule)
        {
            details.Visibility = rule.Visibility = leadsWithDetails ? Visibility.Visible : Visibility.Collapsed;
        }

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.DataContext = button.Tag;
        menu.IsOpen = true;
    }

    private void StartContainer(object sender, RoutedEventArgs e) =>
        Act(sender, ContainerVerb.Start);

    private void StopContainer(object sender, RoutedEventArgs e) =>
        Act(sender, ContainerVerb.Stop);

    private void RestartContainer(object sender, RoutedEventArgs e) =>
        Act(sender, ContainerVerb.Restart);

    /// <summary>Open the edit dialog on a row, and redraw with what it saved (DD296).</summary>
    private async void EditDetails(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ContainerRow { DetailsBeneath: not null } row })
        {
            return;
        }

        if (Editor(row).ShowDialog() == true)
        {
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// The edit dialog on a compose project, or on one of its services, by name (DD330), for a
    /// capture: <c>shop</c> or <c>shop/api</c>.
    /// </summary>
    /// <param name="target">The project's compose name, optionally a slash and a service.</param>
    /// <returns>The dialog, built and not shown, or <see langword="null"/> where nothing has that name.</returns>
    internal EditDetailsWindow? EditorFor(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var slash = target.IndexOf('/', StringComparison.Ordinal);
        var project = slash < 0 ? target : target[..slash];
        var service = slash < 0 ? null : target[(slash + 1)..];

        var children = _rows.Where(child => string.Equals(child.Project, project, StringComparison.Ordinal)).ToList();
        if (children.Count == 0)
        {
            return null;
        }

        var row = service is null
            ? ContainerRow.ProjectHeader(project, children, collapsed: false)
            : children.FirstOrDefault(child => string.Equals(child.Service, service, StringComparison.Ordinal));
        return row is { DetailsBeneath: not null } ? Editor(row) : null;
    }

    /// <summary>The edit dialog on a row, owned by this page's window, built and not shown.</summary>
    /// <param name="row">What was asked to be edited: a project's header or a container.</param>
    /// <returns>The dialog.</returns>
    private EditDetailsWindow Editor(ContainerRow row)
    {
        // The project and every one of its containers, folded or not, so the dialog's rail can name
        // the whole project in one sitting. A container outside compose is its only target.
        var children = row.Project is { } project
            ? _rows.Where(child => string.Equals(child.Project, project, StringComparison.Ordinal)).ToList()
            : [];
        var header = row.Project is { } named && children.Count > 0
            ? ContainerRow.ProjectHeader(named, children, collapsed: false)
            : null;
        IReadOnlyList<ContainerRow> targets = header is null ? [row] : [header, .. children];
        var initial = row.IsProject && header is not null
            ? header
            : targets.FirstOrDefault(target => string.Equals(target.Id, row.Id, StringComparison.Ordinal)) ?? row;

        return new EditDetailsWindow(
            targets, initial, Saved, _recall?.SavePreference, _recall is { } recall ? recall.RememberSavePreference : null)
        {
            Owner = Window.GetWindow(this),
        };
    }

    private void RemoveContainer(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ContainerRow row })
        {
            return;
        }

        if (ContainerAction.AsksBeforeRemoving(row))
        {
            var answer = System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                ContainerAction.RemovalPrompt(row),
                "Remove container",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,

                // The safe answer is the one Enter and Escape both land on.
                System.Windows.MessageBoxResult.No);

            if (answer is not System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }

        // force is what the dialog was about: without it the daemon answers 409 for a running
        // container and the row would say so after the user already agreed to the kill. On a project
        // it is decided per child rather than here — the fan-out asks each container whether it is
        // live, so a stopped one is not removed under a flag it never needed.
        Send(row, ContainerVerb.Remove, force: row.IsProject || row.IsLive);
    }

    private void Act(object sender, ContainerVerb verb)
    {
        if (sender is FrameworkElement { Tag: ContainerRow row })
        {
            Send(row, verb);
        }
    }

    private void Send(ContainerRow row, ContainerVerb verb, bool force = false)
    {
        if (row.IsProject)
        {
            SendProject(row, verb, force);
            return;
        }

        // Pending first, and without asking the daemon anything: this is the half-second between
        // the click and the engine's first word, and it is the half-second the row has to account
        // for.
        _activity.Began(row.Id, verb);
        Redress();
        _ = CallAsync(row.Id, verb, force);
    }

    /// <summary>
    /// Do one verb to every container of a project (DD107).
    /// </summary>
    /// <remarks>
    /// DD8's four verbs fanned across the children, and no new engine surface: <c>docker compose
    /// stop</c> wants the project's files and this window holds a container list, not a working
    /// directory. Every child id is already in hand.
    ///
    /// <para><b>In order</b>, which is the difference between this and a loop: what is depended on
    /// goes first on the way up and on a restart, and last on the way down, and
    /// <see cref="ComposeOrder"/> reads that off the label the containers already carry. Sequential
    /// for the same reason — a fan-out that issued all four calls at once would have computed an
    /// order and then not used it.</para>
    ///
    /// <para><b>The parent's wait ends when the calls do</b>, not when the containers are down. Each
    /// child keeps DD8's own rule and stays pending until its event confirms it; the header has no
    /// event of its own to wait for, and leaving it spinning until the last child settled would hang
    /// it forever on a container that will never emit one.</para>
    /// </remarks>
    private void SendProject(ContainerRow header, ContainerVerb verb, bool force)
    {
        // Only the children this verb has something to do to (DD208). A compose project almost
        // always holds one that exited on purpose — a migration, a one-shot — and asking that one to
        // stop gets a 304, which is the single answer the daemon gives with no event behind it: the
        // row would say `Stopping…` for the life of the window, and the header would borrow it.
        // Asked before the ordering rather than inside it, because which containers a verb reaches
        // and what order it reaches them in are two questions and only one is about depends_on.
        var children = _rows
            .Where(row => string.Equals(row.Project, header.Project, StringComparison.Ordinal))
            .Where(row => ContainerAction.Changes(verb, row))
            .ToList();

        // Which end wants the dependency first is the verb's question, and it is asked where the
        // ordering lives (DD114) — the condition used to be here and split on Start against
        // everything else, which put a restart on the wrong side.
        var ordered = ComposeOrder.For(verb, children);

        // The header's own last refusal count goes, because pressing the button is how somebody
        // says they have read it — but nothing marks the header pending here. Its wait is the
        // children's, and they are about to have one (DD108).
        _activity.Acknowledged(header.Id);
        foreach (var child in ordered)
        {
            _activity.Began(child.Id, verb);
        }

        Redress();
        _ = FanOutAsync(header.Id, ordered, verb, force);
    }

    private async Task FanOutAsync(
        string headerId, IReadOnlyList<ContainerRow> ordered, ContainerVerb verb, bool force)
    {
        var refused = 0;
        foreach (var child in ordered)
        {
            try
            {
                await ContainerAction
                    .InvokeAsync(_api, verb, child.Id, force && child.IsLive)
                    .ConfigureAwait(true);
            }
            catch (DockerApiException failure)
            {
                // On the child, in the engine's words — DD8's line is per row and stays there. Three
                // of four stopped is not the project's failure, it is one container's, and the
                // header repeating that container's sentence would say it twice and name neither.
                _activity.Failed(child.Id, failure.Detail ?? failure.Message);
                refused++;
            }
        }

        if (refused > 0)
        {
            // A count and a pointer, never a sentence borrowed from one child.
            var some = refused.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var all = ordered.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _activity.Failed(
                headerId,
                $"{some} of {all} were refused. The row that failed says why.");
        }

        Redress();
    }

    private async Task CallAsync(string id, ContainerVerb verb, bool force)
    {
        try
        {
            await ContainerAction.InvokeAsync(_api, verb, id, force).ConfigureAwait(true);
        }
        catch (DockerApiException failure)
        {
            // On the row, in the engine's words. Detail is those words alone; Message wraps them in
            // the endpoint, which a log wants and a row does not. A dialog would take the message
            // away from the place the user was looking when they pressed the button.
            _activity.Failed(id, failure.Detail ?? failure.Message);
            Redress();
        }
    }

    /// <summary>
    /// Redraw the rows already on screen against what is now known about them, with no call to the
    /// daemon. The list itself has not changed; what a row is waiting for has.
    /// </summary>
    private void Redress()
    {
        // Through the rows in hand and the same Show(), so a row going pending keeps the order and
        // the filter it was drawn under. Redressing the ListView's own items would drop both.
        _rows = [.. _rows.Select(_activity.Dress)];
        Show();
    }
}
