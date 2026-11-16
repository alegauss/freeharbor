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

### §DD321 A relay that stops accepting under load

On 5 November 2026 `PipeSurvivalTests.A_healthy_relay_stumbles_over_nothing` failed once
in a full run: the test took 30 seconds and its second `ReachableAsync` returned false.
It passed three times alone in about 200 ms.

Ruled out the same day: the accept loop being starved (its own thread since DD142, and
its retry waits on a handle, not on the pool), and the client's timeout running out in
the pool's queue (a starved pool delays `ConnectAsync` but cannot fail it). The test now
prints the exception, the time the attempt took and the relay's `Figures`.

The answer came from outside the suite. On 16 November 2026 an installed 2.2.0 host, its
engine started by hand, wrote this to the journal five seconds after the engine first
answered:

    relay     stopped accepting: IOException: O pipe está sendo fechado.
    Starting  the daemon is running and no connection within 3s, first quiet poll,
              the relay accepted 6, holds 1 open and has stopped accepting

So the relay itself stops: the accept loop ends on an `IOException` ("the pipe is being
closed") after six connections, with the daemon still up. That is DD142's defect, not
the client's. The supervisor brought the engine back 34 seconds later.

What to build: the accept loop treats an `IOException` from a listener as the end of
that one listener, not of the loop, and puts another listener up (as it does after a
served connection), stopping only on its own cancellation. A test drives a listener that
throws `IOException` and asserts the next client still connects.

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

### §DD331 Settings against its board

DD301 shipped Settings as a left-aligned column at most 720 px wide, like About, with
plain radio buttons and their explanation under each. The spec records that as a
deliberate departure. `settings.png` draws something else, and an idea is filed rather
than a task because adopting it reverses a recorded decision:

1. A `Settings` page title, then each section heading in semibold at the body size.
2. Each choice a radio card: card fill and stroke, the chosen one with the accent stroke and an accent-tinted fill, a tag after the name in accent text (`default`, `shared through git`, `edits your file`), the explanation, and a mono line with where it writes (`%LOCALAPPDATA%\FreeHarbor\details`, `<project>\.freeharbor\project.yaml`, `<project>\compose.yaml · x-freeharbor:`).
3. Projects that save somewhere else as a bordered table: name and compose path in mono, the destination, and a ✕ to drop it.
4. The read order as a numbered list with its keys in mono, all six steps visible. Today the capture shows the heading and step 1 only before the fold.

If adopted, the spec's section 7 "As built" paragraph is rewritten in the same commit,
since it would no longer be true. The owner chose the cards on 16 November 2026.

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

### §DD310 Building the MSIX package

Ported from claude-tray's build\build-msix.cmd, build-msix.ps1 and
build\msix\AppxManifest.xml, which passed certification (claude-tray T409 and T412).

- Reserve the name FreeHarbor in Partner Center under the Alegauss account; Identity Name and Publisher are copied from the reservation exactly, or the upload is refused.
- {VERSION} is filled from <Version> in Directory.Build.props with a fourth part of 0, which the Store requires.
- The payload is the Release publish of FreeHarbor.exe, the shim's docker.exe, and build\agent.
- Tiles and StoreLogo are drawn from build\icon.svg by the script that already draws the .ico.
- Resources en-us and pt-br, matching the installer's languages; MinVersion 10.0.19041, as installer.iss requires for WSL2; runFullTrust; and file and registry write virtualization disabled under rescap:unvirtualizedResources, the one mode DD309 measured working (docs/specs/DD309-msix-virtualization.md).
- The output goes to dist\FreeHarbor.msix, plus a self-signed variant for sideloading tests.

This task carries the bare package and none of the manifest extensions: the command
aliases (DD312), the startup tasks (DD313) and the protocol handler (DD314) each come
with their own task. PackagingTests gains cases that hold the manifest version equal to
Directory.Build.props and check that both binaries are in the package.

Done when the package installs by sideload on a clean user, the tray starts from the
Start menu, and the existing Inno build is untouched.

### §DD311 Behaving as a packaged app

Ported from claude-tray's src\Tray\PackageIdentity.cs. Whether the process is packaged
is asked of the process at runtime through GetCurrentPackageFullName and is never set by
a build flag, so the exe the tests check is the exe both channels ship.

Two things change under a package.

- **Updates.** ReleaseCheck and ReleaseUpdate (DD154) stand down. The setup they download would install beside the package instead of replacing it, so the window's update notice and the menu entry go away, and the version row says updates come from the Microsoft Store. An unpackaged copy keeps its updater exactly as it is.
- **Activation.** The Store's Open button, the Start menu tile and an alias launch all start the package the same way the startup task does. Only the startup task is meant to be quiet; the others open the window, as a bare launch does today (DD80). The activation kind is read as claude-tray reads it, and a kind that cannot be read counts as the startup task, so a failure never puts a window in somebody's face at sign-in.

Paths that today derive from the install directory (the agent skill files,
freeharbor.cmd, the after-install text) are checked for assumptions that {app} is
%LOCALAPPDATA%\FreeHarbor, which under a package it is not.

Done when the pure decisions (stand down, open the window) are covered by tests and a
sideloaded package shows no update offer.

### §DD312 The command line from a package

The Inno setup's pathentry task does three things: it puts {app}\bin on the user's PATH,
it places the shim as docker.exe and freeharbor.cmd there, and it writes DOCKER_CONFIG
(DD124). Inside a package, the folder under WindowsApps cannot be launched from outside,
and HKCU writes may be virtualized.

- **freeharbor.** A uap5:AppExecutionAlias named freeharbor.exe on the tray's Application, so the allowlist entry Bash(freeharbor read:*) still matches (DD24). Measure whether ParentConsole still attaches when the windowed exe is launched through an alias. If it does not, the alias points at a console forwarder instead.
- **docker.** A second Application with Executable set to the shim, AppListEntry="none" so it never shows in Start, and its own alias docker.exe. The shim is console subsystem, which an alias launch keeps (DD141). Aliases land in %LOCALAPPDATA%\Microsoft\WindowsApps, late on PATH, so an earlier docker on PATH wins; the preflight's rival check already refuses that machine.
- **DOCKER_CONFIG.** claude-tray measured that a user-scope environment write from its package reached outside shells. Measure the same for DockerConfigEntry.Ensure. If it does not, the shim sets DOCKER_CONFIG for the child process when the variable is absent, which keeps compose and buildx found (DD73, DD74) with no registry write at all.

Done when, from a fresh terminal after a sideload install, docker compose version,
docker buildx version and freeharbor read status all answer.

### §DD313 Start with Windows from a package

Today there are two HKCU Run values (DD97): FreeHarbor, which the installer's checkbox
writes with --tray, and FreeHarbor Engine, which --autostart on writes and nothing
writes by default. A packaged app writes both into its private hive, where Explorer
never looks; claude-tray met exactly this (T409).

- **The tray.** A desktop:StartupTask with Enabled="true", as the installer's checkbox is ticked by default. Launched by it, the app stays in the tray, which is the activation rule DD311 sets.
- **The engine.** A second Application on the same exe, AppListEntry="none", with uap10:Parameters naming the verb the Run value carries today, and its own StartupTask with Enabled="false". Off by default is the product's rule, not a preference.
- **The verb.** Autostart reads and changes the state through the Windows.ApplicationModel.StartupTask API when packaged, and keeps the Run values when not. --autostart status reports Enabled, Disabled, DisabledByUser or DisabledByPolicy as they are. --autostart on can call RequestEnableAsync, but a task the user disabled in Settings cannot be re-enabled by the app, so the answer then names Settings > Apps > Startup. The WinRT call is declared by hand, as claude-tray's activation lookup is, rather than taking on the SDK projection.

Done when both tasks are visible in Settings > Apps > Startup after a sideload install,
the engine's is off, and --autostart on and off move it.

### §DD314 The build-details link from a package

Buildx ends every build with View build details: docker-desktop://dashboard/build/...,
hardcoded in the binary this project pins. DD126 made the link open the build in
FreeHarbor's window through a handler the installer writes under
HKCU\Software\Classes\docker-desktop. A packaged app's HKCU write lands in its private
hive, so on a Store install that link is dead again.

The manifest declares it instead: a uap:Extension of category windows.protocol with
Name="docker-desktop" on the tray's Application, the logo from the package, and the URL
passed to the exe. The handler today runs FreeHarbor.exe --open-build "%1"; under a
protocol activation the URL arrives through the activation arguments instead of the
command line. Read it there when packaged, through the same hand-declared WinRT lookup
DD311 uses for the activation kind. Then hand it to the code that already writes
open-build.txt and signals the running tray (DD126).

The trade-off DD126 recorded still holds: this is another vendor's scheme. A manifest
registration behaves better than the Inno one on that score, because Windows offers a
choice when two apps claim a scheme instead of the last writer silently winning, and the
registration leaves with the package.

Done when a build run from a terminal after a sideload install prints the link and
clicking it opens that build in the window.

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

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)

## Block I — The site on the design system
