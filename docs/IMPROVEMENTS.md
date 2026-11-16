# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD303 Testcontainers detection misses with an accepted API version

Met in Turing (viglet/turing 2026.3, its T1108), carried over from DD289's design when
it shipped. With `DOCKER_API_VERSION=1.44` set, so the API floor was no longer the
cause, Testcontainers' Docker detection still failed on about one run in two, then
succeeded consistently once the version came from a `docker-java.properties` file
instead.

Not understood yet, and two readings fit. One is client-side: docker-java may read the
environment variable at a different point from the properties file, or not on every
path. The other is this engine: DD142 named bursts where every client fails together,
and the host journal on this machine shows the relay "stopped accepting" episodes that
DD142 counts. A detection probe that lands inside such a burst reads Docker as absent.

What to do first: reproduce with the properties file removed, and set the run's
timestamps against the host journal (`%LOCALAPPDATA%\FreeHarbor\engine.log`). Failures
that line up with refused pipe instances are FreeHarbor's and belong with DD142's
mechanism; failures on a quiet journal are docker-java's and go back to Turing as a
client note. DD321 is a full-run test failure on the same pipe, a second connection not
made in 30 seconds, whose cause is still open; if its message shows the relay stopped
accepting, it is this engine-side reading reproduced.

### §DD346 A flaky live compose test that says nothing

Seen on 11 November 2026 while DD345 ran the full suite:
`RelayExitCodeTests.A_compose_run_on_a_service_that_succeeds_is_reported_as_a_success`
failed once (1 of 1835), then passed three times when its class ran alone, with nothing
changed between the runs. It is a live-engine test: it serves the relay
(`LiveEngine.Served`) and runs `docker compose -p <name> run --rm -T probe` through it,
so its outcome depends on this machine's engine and on whatever else the suite has the
engine doing at the same moment.

The filtered output kept only the verdict, so the exit code and what compose printed are
not known yet. Start there: run the full suite with the class's `ran` output written to
the assertion message (`Assert.Equal(0, ran.ExitCode)` names neither stdout nor stderr),
and repeat until it fails again. The likely candidates are the ones DD305 and DD321
found for their own flakes: a resource another live test holds (the image pull, the
project network, the pipe's second connection) or a timeout that only bites under load.

Done when:

- A failure of this test names compose's own words, and the cause it names is fixed or
  the test waits for what it needs instead of racing it.

### §DD354 What a host that is done leaves behind

Measured on 16 November with WSL stuck: the host started at 07:02:28 wrote "this host is
done" at 07:04:01, but process 27008 was still running half an hour later. Under it were
a `wsl -d freeharbor … dockerd` launcher, a `wsl -d freeharbor … socat` relay channel,
and a `wsl --list --running --quiet` probe, each with the second wsl.exe the WindowsApps
alias spawns. The tray had its own hung `wsl --list --running --quiet` too. Every start
attempt on a stuck WSL adds another set, and they hold handles into a service that is
already short of them.

Two things to establish and fix. First, why the host process does not exit once it has
said it is done: a foreground wait on a child, or a relay dispose waiting on a channel
that never closes. Second, that a wsl.exe whose budget ran out is killed with its whole
tree (Process.Kill(entireProcessTree: true)), so the alias's child goes with it.

The relay's per-connection socat channel is a separate question, raised by the
WSAENOBUFS (0x80072747) that preceded the hang on 15 November; that is not this task,
but a count of what is left alive here is the evidence that question needs.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD290 DD290 — Seeing the winget submission through to a working install

From the portfolio review of 30/10/2026 (visibility, `produtos/freewilly.md`). DD282
shipped the submission path, and the first pull request to winget-pkgs is now open,
waiting for the moderators. Until it merges, `winget show alegauss.FreeHarbor` finds
nothing, and the README and the site rightly do not mention winget. The work is to
follow that pull request to its end, not to build anything new.

While it waits: answer the moderators' review comments, and if the validation VM fails
the install (the risk DD283 set aside, since the preflight exits 7 without WSL2), record
what it reported and fix the manifest or the installer's silent path. Once merged:
confirm `winget install alegauss.FreeHarbor` installs the current release on a clean
machine, then add `winget install alegauss.FreeHarbor` to the README's download line and
to the site, and check that the next release's automatic submission (DD282) produces an
update pull request rather than a new package.

Done when `winget show alegauss.FreeHarbor` lists the latest release and the README and
the site name the command.

### §DD292 DD292 — Distributing through the Microsoft Store

From the portfolio review of 30/10/2026 (visibility, `estrategia/portfolio.md`). An
unsigned installer meets SmartScreen on every first download, on exactly the corporate
laptop this tool is for. The author's Windows apps go to the Microsoft Store, which
signs the MSIX itself; claude-tray is listed there as 9P27BHWNT56D since its T412.

FreeHarbor's own restrictions are separate lines: WSL registration from inside a package
(DD309, the gate), the package build (DD310), packaged behaviour and updates (DD311),
the command line (DD312), start with Windows (DD313), the build-details link (DD314),
the first-run page that replaces the wizard (DD316), and removing the engine without an
uninstaller (DD317). The engine download stays (DD315): policy 10.2.2, as of version
7.20, forbids code that changes the described function, so the listing names it.

What stays here is the submission: the listing in en-US and pt-BR, the store art,
claude-tray's store-submit.ps1 ported, and certification notes that justify
runFullTrust, unvirtualizedResources (which DD309 showed the package needs), and the
engine download with its source, size and pinning. The README and the site's download
section name the Store beside winget and the installer, which both stay.

If DD309 shows MSIX cannot host the WSL registration, the fallback is policy 10.2.9's
listing by installer URL, which needs a signed, silent installer that downloads nothing
when run, so the engine moves to first launch; otherwise no Store.

Done when FreeHarbor is in the Store with its install working without admin, or the
reason it cannot be is recorded here.

### §DD313 Start with Windows from a package

The manifest half landed: a FreeHarborTray startup task, on, on the tray's Application,
and a FreeHarborEngine task, off, on an unlisted Engine Application that runs the same
exe with `--run` (build\msix\AppxManifest.xml). Measured from a sideload on 16 November
2026: the tray's task reads Enabled and the engine's Disabled. Under a package,
`--autostart` writes no Run value and names Settings > Apps > Startup.

Left: `--autostart on` and `off` under a package. Two ways, and the owner picks.

- **Settings.** `on` and `off` open `ms-settings:startupapps`, where the user flips
  "FreeHarbor engine". Small, and needs nothing below.
- **The API.** StartupTask answers per Application, not per package: from the tray's
  Application (the one the freeharbor.exe alias launches), `GetAsync("FreeHarborEngine")`
  fails and `GetForCurrentPackageAsync` lists only the tray's task; from the Engine
  Application it answers Disabled. So the verb must run as the Engine Application, through
  IApplicationActivationManager with arguments. Whether a parameterised Application
  receives extra activation arguments is unmeasured: activated with one, the process
  exited before WMI could read its command line. Then RequestEnableAsync and Disable,
  declared by hand as PackageIdentity's activation lookup is, with a task the user
  disabled in Settings named there rather than forced.

Done when `--autostart on` and `off` move the engine's task, or open the page that does,
from a sideload install.

### §DD316 The first launch carries what the setup wizard did

The Inno wizard does four things a package cannot: it runs --preflight before writing
anything (DD130), offers Turn it on for me with one elevated wsl.exe --install
--no-distribution (DD131, DD132), downloads the engine behind a checkbox (DD119), and
shows the after-install page about the tray overflow and the agent files (DD21, DD32). A
package runs no code until the user opens it.

So a packaged first launch, detected as no distribution registered and no settings.json,
opens the window on a first-run page built from the same pieces:

- the preflight report, from the same code --preflight runs, so there is still one opinion about the machine;
- when the WSL2 row blocks, the plain-words explanation, the command, and the button. WindowsElevation already elevates per step, and installing the app still asks for no administrator;
- after a restart, the tray's startup task brings the window back, replacing the wizard's RunOnce value;
- the engine download as an explicit button stating its size, or a copy step if DD315 ships the engine inside the package;
- the overflow note and the two agent setup commands, with paths valid under a package.

The promise changes and the listing says so: a machine that cannot host the engine now
gets a clean, removable package and this page, instead of nothing at all. The Inno
wizard keeps its own pages.

Done when a sideload install on a machine without WSL2 reaches a running engine through
this page alone.

### §DD317 Removing the engine without an uninstaller

The Inno uninstaller closes the tray, unregisters the freeharbor distribution by name
and offers to delete the root (DD14, DD121). Removing an MSIX package runs none of that.
DD309 measured what it leaves in the mode the package uses, with write virtualization
off: the distribution and its disk, the whole root under %LOCALAPPDATA%\FreeHarbor, and
a Run value naming an exe that no longer exists
(docs/specs/DD309-msix-virtualization.md).

Three parts:

- **A verb.** freeharbor --remove-engine stops the engine host, terminates and unregisters the distribution (terminate first, per DD209), and deletes distro, downloads, cli and cli-plugins, keeping settings.json and details.json unless --all is given. It prints what it removed and how much space came back. The Inno uninstaller calls the same code, so there is one removal and not two.
- **A place in the window.** Settings gets Remove engine, with a confirmation that states the disk size and that every image, container and volume goes with it.
- **The leftover after the fact.** The preflight gains a row for a registered distribution named freeharbor that no installed copy owns, or whose BasePath no longer exists, with the disk size and the unregister command as its remedy, so a reinstall after a bare package removal can clean up.

The Store listing and the site's uninstall note say to run Remove engine before
uninstalling.

Done when a sideload install, an engine provisioned, Remove engine, then
Remove-AppxPackage leaves no Lxss entry and no root.

### §DD362 The Store identity from the reservation

DD310 shipped the package with a provisional identity, `Alegauss.FreeHarbor` published
by `CN=FreeHarbor Sideload`, because FreeHarbor has no Partner Center reservation yet.
That is enough to build and sideload; the Store signs on submission and refuses an
upload whose Identity Name and Publisher differ from the reservation.

The owner reserves the name FreeHarbor under the Alegauss account, as claude-tray's was
(Store ID 9P27BHWNT56D, Publisher `CN=1ADDE401-...`). Then:

- `build\msix\AppxManifest.xml`: Identity Name and Publisher copied from Partner Center's
  Product identity page, exactly, and the comment that calls them provisional removed.
- `build-msix.ps1 -Sideload` keeps working: it signs with whatever subject the manifest's
  Publisher names, so a sideload build uses a self-signed certificate of that subject.
- PackagingTests gains a case that the Publisher is not the provisional one, so a Store
  build cannot go out with it again.

Done when `build-msix.cmd` produces a package whose identity matches the reservation,
which is what DD292's submission uploads.

## Block G — The agent surface (an agent operates this, and pays in tokens)

### §DD363 A cost test measured on one sample

On 16 November 2026, during DD317,
`AgentFollowTests.A_follow_costs_what_it_reads_rather_than_the_square_of_it` failed once
in a full `dotnet test` run and passed on its own straight after, and again in the next
full run. The code under test had not changed.

The test times one pass over 4,000 lines and one over 16,000 lines with a stopwatch, one
sample each, and asserts the ratio stays under 9. In a full run about 1,900 tests share
the machine, so one sample of a few milliseconds can be inflated by a pause in either
half, and the ratio moves with it.

What to build: take the minimum of several samples for each size (five is enough; the
minimum is the reading least disturbed by the rest of the suite) before forming the
ratio. Keep the bar at 9, which still separates linear (near 4) from quadratic (near
16). If that is still noisy, count the work instead of timing it: expose how many times
the follow splits or scans the buffer and assert that count grows linearly, which no
busy machine can disturb.

Done when the test passes in ten consecutive full runs.

## Block H — The public surface (the site a reader and an agent both read)

## Block I — The site on the design system
