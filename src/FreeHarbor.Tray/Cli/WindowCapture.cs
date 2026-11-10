using System.IO;
using FreeHarbor.Core.Api;
using FreeHarbor.Core.Fixtures;
using FreeHarbor.Core.Engine;

namespace FreeHarbor.Tray.Cli;

/// <summary>
/// Photographs the window by rendering it, off-screen, where nothing else can be in the frame.
/// </summary>
/// <remarks>
/// DD22. A window used to be verified by copying the pixels on screen inside its rectangle, which
/// reads whatever is actually there rather than the window: twice that was somebody else's content —
/// an editor holding a credential and a messaging app holding an appointment — and both reached a
/// transcript, which deleting the file afterwards does not undo.
///
/// This cannot do that. A <c>RenderTargetBitmap</c> over the window's own visual tree has no access to
/// anything outside it, and the window is shown at <c>-32000</c> so it is never composited onto a
/// desktop at all. That also means this works with no interactive desktop present, which a screen copy
/// does not — measured while shipping DD21, where a copy of the notification area came back as a
/// single flat colour on a locked session.
///
/// The screen copy is kept for the one thing a render cannot see: a popup is its own top-level window
/// and is not in this window's visual tree. That path lives in <c>scripts\Capture-Window.ps1</c>, with
/// the overlap check that decides whether it is safe to photograph anything at all.
/// </remarks>
internal static class WindowCapture
{
    private const int Ok = 0;
    private const int Failed = 1;
    private const int Usage = 2;

    /// <summary>How long to let the window settle before rendering it.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);

    /// <summary>What asks for the fixture instead of the live engine (DD38).</summary>
    public const string FixtureFlag = "--fixture";

    /// <summary>What opens the containers page's detail page on a project before the picture (DD299).</summary>
    public const string DetailFlag = "--detail=";

    /// <summary>
    /// What draws this machine's own engine as running and photographs its real containers (DD340),
    /// where a capture with neither flag draws the stopped page.
    /// </summary>
    public const string LiveFlag = "--live";

    /// <summary>What draws the containers page as cards rather than as its list (DD300).</summary>
    public const string CardsFlag = "--cards";

    /// <summary>
    /// What photographs the edit details dialog instead of the main window (DD330), on a project or
    /// one of its services: <c>--edit=shop</c> or <c>--edit=shop/api</c>.
    /// </summary>
    public const string EditFlag = "--edit=";

    /// <summary>Render the window to a PNG.</summary>
    /// <param name="args">The output path, a tab header, and <c>--fixture</c> or <c>--live</c>, all optional but the path.</param>
    /// <returns>The process exit code.</returns>
    internal static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            return Complain(
                "takes an output path and optionally a tab, "
                + $"e.g. {CommandLine.ExecutableName} {CommandLine.CaptureWindowVerb} window.png Images");
        }

        var fixture = args.Contains(FixtureFlag, StringComparer.Ordinal);
        args = [.. args.Where(a => !string.Equals(a, FixtureFlag, StringComparison.Ordinal))];

        var live = args.Contains(LiveFlag, StringComparer.Ordinal);
        args = [.. args.Where(a => !string.Equals(a, LiveFlag, StringComparison.Ordinal))];
        if (live && fixture)
        {
            return Complain($"{LiveFlag} and {FixtureFlag} name two different machines; pass one");
        }

        // DD299: the detail page has no tab of its own, so a capture names the project it is open on.
        var detail = args.FirstOrDefault(a => a.StartsWith(DetailFlag, StringComparison.Ordinal))?[DetailFlag.Length..];
        args = [.. args.Where(a => !a.StartsWith(DetailFlag, StringComparison.Ordinal))];

        // DD300: the containers page's view is named, never inherited from this account's own choice.
        var cards = args.Contains(CardsFlag, StringComparer.Ordinal);
        args = [.. args.Where(a => !string.Equals(a, CardsFlag, StringComparison.Ordinal))];

        // DD330: the edit dialog is its own window, so it is named the way the detail page is.
        var edit = args.FirstOrDefault(a => a.StartsWith(EditFlag, StringComparison.Ordinal))?[EditFlag.Length..];
        args = [.. args.Where(a => !a.StartsWith(EditFlag, StringComparison.Ordinal))];

        if (args.Length > 2)
        {
            // Naming the argument, not just the arity: a refusal that does not say what it failed to
            // understand leaves the caller to guess which of their words was wrong.
            return Complain(
                $"unexpected argument {args[2]}: this takes an output path and optionally a tab");
        }

        // A path that looks like a flag is a caller who forgot the path, and writing a file called
        // "--json" is how that mistake becomes silent. Borrowed from claude-tray, where exactly this
        // produced a file named after the flag next door.
        if (args[0].StartsWith('-'))
        {
            return Complain($"{args[0]} is not an output path");
        }

        var outPath = Path.GetFullPath(args[0]);
        var tab = args.Length == 2 ? args[1] : null;

        // The same application the tray makes, made the same way: a capture that resolved different
        // chrome would be a picture of something nobody runs (DD34).
        var app = Ui.Theme.Apply();

        // With no engine answering, the window renders its own empty state — a picture of a real
        // thing, and the only picture this verb could take before DD38. --fixture is the other half:
        // a known machine, so the rows, the ports, the sizes and the states are the same on every
        // machine and in CI. The window is handed one or the other and never learns which.
        IEngineClient api = fixture ? new SampleMachine() : new DockerApi();

        // DD340: --live asks before drawing, so an engine that does not answer is refused by name
        // rather than photographed as a running engine with nothing in it.
        if (live)
        {
            try
            {
                _ = api.VersionAsync().GetAwaiter().GetResult();
            }
            catch (DockerApiException refused)
            {
                app.Shutdown();
                return Complain($"{LiveFlag} found no engine answering: {refused.Message}");
            }
        }

        // The builds page reads its own history rather than the Engine API, so it needs its own
        // fixture or --fixture would photograph a page that shells out to the real Buildx and shows
        // whatever this machine happened to build (DD126, L6). Null is the live one.
        FreeHarbor.Core.Builds.IBuildHistory? builds =
            fixture ? new FreeHarbor.Core.Fixtures.SampleBuilds() : null;

        // And the engine page is the same problem three times over (DD165, DD197, DD199, L6). The
        // live journal is whatever this machine's engine did that afternoon under a path naming the
        // account it was taken on; the readings are this laptop's disk, free space and error count;
        // and the buttons beside them start something that terminates a distribution. The fixture's
        // journal and readings are the same everywhere and its work refuses — a capture is rendered
        // off-screen where nothing can click, and the seam is what makes that true by construction
        // rather than by the window happening not to be reachable. Null is the live set.
        EngineSeams? engine = fixture
            ? new EngineSeams(
                new FreeHarbor.Core.Fixtures.SampleJournal(),
                new FreeHarbor.Core.Fixtures.SampleMachineReport(),
                new FreeHarbor.Core.Fixtures.SampleFilesystemWork(),
                new NoInterlude())
            : null;

        // Before the window exists, because the water starts drifting the moment it is told the
        // engine is up and this verb's fixture says exactly that (DD69, DD70). A picture that caught a
        // random phase would make every capture differ from the last, which is the one property
        // this whole verb is for.
        Ui.Motion.Still = true;

        // DD301: and the window's memory, which now holds this account's project folders (where their
        // edits are saved). A fixture capture reads a file nobody wrote, so Settings draws the defaults.
        var windowState = fixture
            ? Path.Combine(Path.GetTempPath(), $"freeharbor-capture-window-{Guid.NewGuid():N}.json")
            : null;

        var window = new Ui.MainWindow(
            api, () => fixture || live ? EngineState.Running : EngineState.Stopped, () => { },
            builds, engine, windowState)
        {
            WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
            // Off the desktop entirely. Not merely unfocused: there is no screen region for anything
            // else to be composited into.
            Left = -32000,
            Top = -32000,
            // Off-screen there is no system backdrop to follow, so ThemeMode="System" would leave the
            // Fluent brushes unresolved and render light text on an unpainted surface.
            ThemeMode = System.Windows.ThemeMode.Dark,
        };

        // DD295: the sample project names a folder no capturing machine has, so its project file
        // comes from the fixture too. Set before the first refresh, which is the first read of it.
        if (fixture)
        {
            window.Containers.ProjectFiles = FreeHarbor.Core.Fixtures.SampleMachine.ProjectFiles;

            // DD296: and nothing saved, or the picture would carry whatever this account edited.
            window.Containers.Saved = new FreeHarbor.Core.Details.DetailStore(
                Path.Combine(Path.GetTempPath(), $"freeharbor-capture-{Guid.NewGuid():N}.json"));
        }

        // Unhooked first, so a capture cannot write the view it was told to draw over the one the
        // person chose, for the reason WindowRecall refuses to save a placement no screen can reach.
        window.Containers.ViewChosen = null;
        window.Containers.ChipChosen = null;
        window.Containers.UseChip(Ui.StateChip.All);
        window.Containers.UseCards(cards);

        if (detail is { Length: > 0 })
        {
            window.Containers.OpenDetail(detail);
        }

        var code = Failed;
        window.Show();

        if (tab is not null && !window.ShowTab(tab))
        {
            // Refused, not defaulted, and before any file exists. A name this window does not have
            // would otherwise render Containers into the file somebody asked to hold Images and report
            // success about it.
            Console.Error.WriteLine(
                $"{CommandLine.ExecutableName}: this window has no {tab} tab. It has: "
                + string.Join(", ", window.TabNames));
            app.Shutdown();
            return Usage;
        }

        // The same read the tray does when it opens the window, so the picture is of the window a user
        // gets rather than of one that was never asked to draw anything. With no engine this fails and
        // the window shows its own empty state, which is the honest thing to photograph — without it
        // the header row came back blank, seen by looking at the first capture.
        // DD340: the dispatcher's context is installed by Application.Run, which has not run yet, so
        // without this a read that really waits on the pipe resumes on a pool thread and the redraw
        // throws there, unobserved, leaving the empty page. The fixture answers synchronously and
        // never needed it, which is why this went unseen until a capture read a real engine.
        SynchronizationContext.SetSynchronizationContext(
            new System.Windows.Threading.DispatcherSynchronizationContext(window.Dispatcher));
        _ = window.RefreshAsync();

        var settle = new System.Windows.Threading.DispatcherTimer { Interval = Settle };
        settle.Tick += (_, _) =>
        {
            settle.Stop();

            // A --detail that named no project draws the list, and a picture of the list filed as the
            // detail page is how a capture of the wrong screen went unnoticed: the compose name is
            // asked for, and the title (which is what the page shows) silently matched nothing.
            if (detail is { Length: > 0 } && !window.Containers.DetailShowing)
            {
                Console.Error.WriteLine(
                    $"{CommandLine.ExecutableName}: no compose project is named {detail}, so there is no "
                    + "detail page to capture. Name the project as compose does, not by its title");
                code = Usage;
                app.Shutdown();
                return;
            }

            // DD330: the dialog is opened only now, once the rows it is built from are in, shown
            // off-screen as the window is, and photographed after a settle of its own. A name that
            // opens nothing is refused rather than photographing the main window under its name.
            if (edit is { Length: > 0 })
            {
                var dialog = window.Containers.EditorFor(edit);
                if (dialog is null)
                {
                    Console.Error.WriteLine(
                        $"{CommandLine.ExecutableName}: nothing is named {edit}, so there is no edit dialog to "
                        + "capture. Name a compose project, or a project and a service as project/service");
                    code = Usage;
                    app.Shutdown();
                    return;
                }

                dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                dialog.Left = -32000;
                dialog.Top = -32000;
                dialog.ThemeMode = System.Windows.ThemeMode.Dark;
                dialog.Show();

                var opened = new System.Windows.Threading.DispatcherTimer { Interval = Settle };
                opened.Tick += (_, _) =>
                {
                    opened.Stop();
                    code = Save(dialog, outPath, $"the edit dialog on {edit}");
                    app.Shutdown();
                };
                opened.Start();
                return;
            }

            try
            {
                var (width, height) = Ui.WindowSnapshot.Save(window, outPath);
                Console.Out.WriteLine(
                    $"wrote {outPath}: {width}x{height}, "
                    + $"{tab ?? window.TabNames.FirstOrDefault() ?? "the window"} rendered off-screen");
                code = Ok;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException or InvalidOperationException
                or ArgumentException or NotSupportedException)
            {
                Console.Error.WriteLine($"{CommandLine.ExecutableName}: {exception.Message}");
                code = Failed;
            }
            finally
            {
                app.Shutdown();
            }
        };
        settle.Start();

        app.Run();

        // The fixture holds nothing to release; the real client holds a pipe and a pool.
        (api as IDisposable)?.Dispose();
        return code;
    }

    /// <summary>Render one window to the path and say what was written.</summary>
    private static int Save(System.Windows.Window window, string outPath, string what)
    {
        try
        {
            var (width, height) = Ui.WindowSnapshot.Save(window, outPath);
            Console.Out.WriteLine($"wrote {outPath}: {width}x{height}, {what} rendered off-screen");
            return Ok;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or InvalidOperationException
            or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"{CommandLine.ExecutableName}: {exception.Message}");
            return Failed;
        }
    }

    private static int Complain(string problem)
    {
        Console.Error.WriteLine(
            $"{CommandLine.ExecutableName} {CommandLine.CaptureWindowVerb}: {problem}");
        return Usage;
    }
}
