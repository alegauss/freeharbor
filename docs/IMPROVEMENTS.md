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
client note.

### §DD305 The error-mode test races its neighbours

Seen on 5 November 2026: the full suite went red once on
`TrayTests.A_child_Windows_refuses_to_start_cannot_hold_the_teardown_on_a_modal_box`,
then passed alone and on the next full run, with no change between.

The test calls `HardErrorBox.Suppress()` twice and asserts that the second answer keeps
every flag the first one had. The error mode is one process-wide word, shared with the
WinForms and WPF runtimes and with every test running in parallel, and its own comment
says another writer may add a flag in between. A writer can also clear one: anything
that calls `SetErrorMode` with a value of its own, rather than adding to the current
one, makes the second answer lose a bit the first had, and the assertion fails on code
that is fine.

Fix: run the error-mode tests in a collection of their own, the way `ConsoleCollection`
serialises the tests that swap the console, and find which test or runtime path sets the
mode outright. If that path is in this product, it is a real defect, since a cleared
`SEM_FAILCRITICALERRORS` brings the modal box back. Done when the full suite has run ten
times in a row without this test failing.

### §DD308 Stream order inverted once on CI

CI run 42024365310 (commit bef1139, 5 November 2026) failed one test:
`ConsoleDecodeTests.A_real_tool_that_writes_to_one_stream_and_then_the_other_comes_back_in_that_order`,
with stderr's "banner" arriving after stdout's "summary". The same test passed on CI for
670a14b an hour earlier, bef1139 touches nothing in `ProcessOutput`, and it passes on
every local run.

`ProcessOutput.PumpAsync` reads each pipe with `ReadAsync` and orders pieces by when the
read's continuation runs. A read or a continuation delayed past the test's two-second
`ping` pause would invert the order. Flooding this machine's thread pool with 512
blocking work items did not reproduce it, so pool starvation is not confirmed as the
cause.

If it recurs: record the runner's load and the two pieces' arrival times in the failure
message, and if it is the pump, read each stream on a dedicated thread
(`TaskCreationOptions.LongRunning`) so the order stops depending on the pool. Done when
the cause is shown, not when a rerun goes green.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

### §DD297 The enriched list

The list stays the default view, because it is the one that holds forty containers and
still obeys L4 and L5 of the DD34 constitution. It changes in what each element says,
not in its structure.

A compose project's header (DD106) gains the resolved icon, title and description, the
running-of-total chip, a primary Open button for the project's primary link, and Start
or Stop as one verb, with the rest in the overflow. Open is disabled, not hidden, while
the project is stopped.

A row shows the state chip with its shape (L8), the service's title over its
description, and keeps the port links DD7 drew, now as tinted chips. A port that does
not answer HTTP copies its address instead of opening a browser. The row keeps DD36's
Logs, one verb and an always-drawn overflow; nothing is revealed on hover.

Containers started without compose are gathered in a final Standalone containers block.
Its header carries no group verbs, because each row is its own unit with its own Open
and Start or Stop. A container with no metadata shows its monogram and an "Add icon and
description" link to the edit screen.

The header columns still match the row columns, and a test still says so. Layout:
docs/specs/DD294-containers-redesign.md section 2.

### §DD298 A large compose project

The enriched list and the cards both assume a project of a handful of services. A real
platform project runs twenty containers and publishes ten URLs, and the same layout then
shows a wall of identical green rows and a row of ten buttons.

Links: the link marked `freeharbor.primary` (otherwise the first) is a split button,
with Open on one side and a count with a menu on the other. Up to two links marked
pinned sit beside it as buttons. The menu lists every link under Pinned and All, with a
filter box once there are more than five.

Services in the list: a `freeharbor.group` label splits the project into tiers (Edge,
APIs, Workers, Data). A tier where everything runs stays folded behind a one-line
summary and an all running chip, and a tier with a problem opens. The group's own filter
(All, Running, Problems) narrows over the rows in hand, as L5 requires. Without the
label there is one tier and the behaviour is the plain list.

Services in a card: a health bar with one block per container, problems drawn last and
in their own shape, then only the services that need attention, and Show all, which
opens the detail page.

Fold state is remembered per project, like the fold DD112 narrows. Layout:
docs/specs/DD294-containers-redesign.md section 3.

### §DD299 The project detail page

The list and the cards answer "what is running and how do I open it". They cannot answer
"why did this stop" or "what does this project expose" without growing into something
else. Those answers belong on a page of their own.

It opens from a project's or container's title link, Details in any overflow, or a click
on a card; in the list the header strip still folds (DD127). Back returns to the view
and scroll position it came from. Layout: docs/specs/DD294-containers-redesign.md
section 4.

The page carries: the resolved icon, title and description, the compose project name and
file, and the project's verbs (Open primary, Restart, Stop, overflow with Edit details).
A problem line repeats the daemon's own sentence for each service that exited badly,
with a per-service Start. An Endpoints grid holds every link, each with Open and Copy,
non-HTTP ports offering Copy alone. A Services table shows state, image and, where the
engine reports it, memory and CPU. A Recent output pane tails the failing service's log,
its stderr in the danger colour, with a link to the full log window.

A rail on the left lists compose projects and standalone containers apart, so moving
between details does not go back through the list. A standalone container gets the same
page with one service. The page is drawn from the fixture, so a capture can show it
without a daemon (L6).

### §DD300 The cards view and the view switch

Most machines run a handful of projects, and there the list spends its density on
nothing. The cards view trades density for recognition: one card per project, read at a
glance by icon and description, opened in one click.

A card carries the icon tile, title, state chip and description, then a problem line in
the daemon's words when a service failed, then the links (the primary one filled in the
accent colour, others outlined, non-HTTP ports as dashed copy chips), then the services
with shape and state, then Logs, Restart and one verb. A stopped project dims its links,
not its card. Standalone containers follow in a section of compact one-line cards, and a
container with no metadata shows its monogram and the link to Edit details.

A Cards and List switch sits beside the filter box. Both views are the same rows in
hand: the filter chips (All, Running, Stopped, Problems), the text filter and the sort
apply to both, and switching keeps them. The chosen view is remembered with the window's
placement and tab (DD39), so the window reopens the way it was left.

Cards land after the enriched list on purpose. The list is the default and the one that
scales, and the cards reuse everything it introduces. Layout:
docs/specs/DD294-containers-redesign.md section 5.

### §DD301 The settings destination

The edit screen saves to one of three places, and that choice is the user's to make once
rather than a question asked on every save. The window has no place for a preference
today: its destinations are Containers, Images, Volumes, Builds, Engine and About. A
Settings destination joins the nav strip before About, built as a page like the others
(L2).

Its first section is "Where edits to names, descriptions and icons are saved", three
choices with what each means and where it writes:

1. In FreeHarbor, the default: personal, this machine only, the project folder untouched.
2. In a `.freeharbor` folder in the project: its own file beside the compose file, which it overrides, shared through git.
3. In the compose file: only the `x-freeharbor` block, the first save in each project showing the diff once.

A note says standalone containers always save in FreeHarbor, because they have no
project folder.

The second section lists the projects whose Save to was changed in their edit dialog,
each with its compose path and destination, and a control to make it follow the default
again.

The third section states the read order, so the user can predict which value wins: saved
in FreeHarbor, `.freeharbor`, `x-freeharbor`, service labels, image labels, the
fallback.

Settings persist in the DD39 store and the page draws from the fixture (L6). Layout:
docs/specs/DD294-containers-redesign.md section 7.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD283 Measuring the winget install before promising it

winget-pkgs validates a new package by installing it silently on a Microsoft VM.
FreeHarbor's PrepareToInstall refuses with exit 7 when the preflight blocks, and WSL2 or
hardware virtualization is very likely off on that VM. That refusal is DD130's decision
and is right for a person, so it is not loosened on a guess.

The measurement comes first: enable LocalManifestFiles, run `winget install --manifest
build\winget` in Windows Sandbox, which has no WSL2, and in the test guest, which has
it. If the Sandbox run exits 7 with the systemNotSupported message, the first PR says so
and asks for manual validation. The README and the site name `winget install
alegauss.FreeHarbor` only after the PR merges, because an install line that finds
nothing is worse than none.

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

From the portfolio review of 30/10/2026 (visibility, `estrategia/portfolio.md`). The
installer is not signed, so SmartScreen warns on every first download, which is the
wrong first impression on exactly the corporate laptop this tool is for. The decision
for all of the author's Windows apps is the Microsoft Store, which signs the MSIX
package itself, instead of buying a code signing certificate. claude-tray tested the
path first (visibility VS58) and is listed as 9P27BHWNT56D since its T412.

The restrictions FreeHarbor has of its own are now separate lines: whether WSL
registration works from inside a package (DD309, the gate), the package build (DD310),
packaged behaviour and updates (DD311), the command line (DD312), start with Windows
(DD313), the build-details link (DD314), the engine download against policy 10.2.2
(DD315), the first-run page that replaces the wizard (DD316), and removing the engine
without an uninstaller (DD317).

What stays here is the submission: the listing in en-US and pt-BR, the store art,
claude-tray's store-submit.ps1 ported, and certification notes that justify
runFullTrust, any unvirtualizedResources, and the engine download. The README and the
site's download section name the Store beside winget and the installer, which both stay.

If DD309 shows MSIX cannot host the WSL registration, the answer is a Store listing that
points to the existing installer, or no Store at all, and the reason is recorded here.

Done when FreeHarbor is in the Store with its install still working without admin, or
the reason it cannot be is recorded here and in the product sheet.

### §DD309 DD309 — Measuring WSL registration from inside a package

The gate for the whole Store path (DD292). A full-trust app in an MSIX package has its
writes to a new folder under AppData redirected to the package's LocalCache, and its
HKCU writes kept in a private hive. FreeHarbor's root is %LOCALAPPDATA%\FreeHarbor and
distro is the BasePath WSL registers the disk at. WslService runs outside the package
and sees the real filesystem, so the app and the service may disagree about where the
disk is.

The test: a minimal package, self-signed and sideloaded, holding the current
FreeHarbor.exe, run once with virtualization on and once with
desktop6:FileSystemWriteVirtualization and RegistryWriteVirtualization disabled under
rescap:unvirtualizedResources. In each mode, run --provision, then record:

- the BasePath under HKCU\...\Lxss and where the vhdx really sits;
- whether the tray reports the engine, starts it and reaches the docker_engine pipe;
- whether --compact and --fsck find the disk;
- what is left after Remove-AppxPackage: files, the Lxss entry, the Run values.

Done when the table is written here and the mode the package will use is chosen. If
neither mode keeps the app and WSL on one disk, DD292 records the fallback (a listing
that points to the existing installer) and DD309 onward are retired.

### §DD310 Building the MSIX package

Ported from claude-tray's build\build-msix.cmd, build-msix.ps1 and
build\msix\AppxManifest.xml, which passed certification (claude-tray T409 and T412).

- Reserve the name FreeHarbor in Partner Center under the Alegauss account; Identity Name and Publisher are copied from the reservation exactly, or the upload is refused.
- {VERSION} is filled from <Version> in Directory.Build.props with a fourth part of 0, which the Store requires.
- The payload is the Release publish of FreeHarbor.exe, the shim's docker.exe, and build\agent.
- Tiles and StoreLogo are drawn from build\icon.svg by the script that already draws the .ico.
- Resources en-us and pt-br, matching the installer's languages; MinVersion 10.0.19041, as installer.iss requires for WSL2; runFullTrust; and the virtualization mode DD309 chose.
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

### §DD315 Downloading the engine versus shipping it in the package

The installer's engine task downloads about 250 MB at install time: the distribution's
root filesystem, dockerd, the Windows docker CLI, and the compose and buildx plugins.
Each is pinned and its checksum verified by EngineProvisioner, and --provision does the
same work later. Microsoft Store policy 10.2.2 says an app must not change its described
functionality by downloading code after certification. A container engine that downloads
separate, pinned programs is the kind of case the certification notes are written to
argue, not a clear pass.

Two outcomes, each with its cost:

- **Keep the download.** The listing description names the download and its size, and the certification notes explain what is fetched, from where, and how it is pinned. Engine updates stay independent of package updates. Cheapest to build; the risk is a rejection after the rest of the work is done.
- **Ship the binaries in the package.** The provision copies from the package folder instead of downloading, the package grows by roughly the same 250 MB, and every engine bump becomes a package submission. FreeHarbor then redistributes those binaries inside its own package, so the NOTICE DD13 wrote and the attribution list (Licensing\Attribution.cs) are checked against exactly what the package carries.

Read the current policy text and the published guidance on runtimes and package
managers, and record which applies. Submit first with the download if the reading allows
it.

Done when the chosen outcome and the policy text it rests on are written here.

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
What is left depends on what DD309 measured: with virtualization off, the whole root
stays under %LOCALAPPDATA%\FreeHarbor; with it on, Windows deletes LocalCache but WSL
keeps a registered distribution whose disk is gone, which wsl --list then shows broken.

Three parts:

- **A verb.** freeharbor --remove-engine stops the engine host, terminates and unregisters the distribution (terminate first, per DD209), and deletes distro, downloads, cli and cli-plugins, keeping settings.json and details.json unless --all is given. It prints what it removed and how much space came back. The Inno uninstaller calls the same code, so there is one removal and not two.
- **A place in the window.** Settings gets Remove engine, with a confirmation that states the disk size and that every image, container and volume goes with it.
- **The leftover after the fact.** The preflight gains a row for a registered distribution named freeharbor whose BasePath no longer exists, with the unregister command as its remedy, so a reinstall after a bare package removal can clean up.

The Store listing and the site's uninstall note say to run Remove engine before
uninstalling.

Done when a sideload install, an engine provisioned, Remove engine, then
Remove-AppxPackage leaves no Lxss entry and no root.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)

### §DD291 DD291 — Opening on the corporate laptop without admin

From the portfolio review of 30/10/2026 (visibility, `estrategia/portfolio.md` and
`produtos/freewilly.md`). The first answer anyone gives to a free Docker Desktop
alternative is Rancher Desktop or Podman Desktop: free, open source, multi-platform,
backed by SUSE and Red Hat. The README and the site open on the licence ("no headcount
threshold, no revenue threshold"), which those two also satisfy, and mention "per-user
install, no admin prompt" only as a small note under the download link.

What separates FreeHarbor is who can install it: a developer on a managed corporate
laptop, with no admin rights, whose company crossed Docker Desktop's limits. The
README's first paragraph and the site's hero should open on that reader and that
situation, with the licence as the reason the company allows it, and say plainly the one
exception (enabling the WSL feature may need elevation once). A short, honest comparison
row against Rancher and Podman (admin needed or not, own distribution, no resident
service) answers the comment before it is written.

Checked on 5 November 2026, and the premise does not hold. Podman 6 installs per-user by
default, Podman Desktop has a current-user option, and `podman machine` serves
`docker_engine`. Rancher Desktop installs per-user too, its privileged service aside.
All need WSL enabled once. Which differentiator leads is a person's call.

Done when the first screen of the README and of the site names the corporate laptop
without admin, and the comparison with Rancher and Podman is on the page.

### §DD304 Correcting the rival cells on the compare page

Found while working DD291 on 5 November 2026. The `/compare` matrix in
`site/src/lib/site-content.ts` scores three rival cells against FreeHarbor that current
sources contradict:

- **Per-user install, no admin prompt.** Rancher Desktop is ✗, but its Windows installer
  offers a current-user install; only its privileged service (which binds ports beyond
  127.0.0.1) needs admin. That is `~`. Podman Desktop is `~`, but it has a current-user
  option that needs no admin, and Podman 6's MSI is per-user by default. That is ✓.
- **Standard docker_engine pipe, no DOCKER_HOST.** Podman is ✗, but `podman machine`
  claims `npipe:////./pipe/docker_engine` whenever nothing else holds it, and only falls
  back to its own pipe on a conflict. That is ✓.

Every one of these still needs WSL enabled once by an administrator, which is
FreeHarbor's own caveat as well, so that is a footnote under the row and not a cell.

`winsFooter` then names the per-user install and the standard pipe as the axis only this
one wins, and after the correction it does not win them alone. Rewrite it to state what
the corrected rows leave: nothing resident from boot (against Rancher), the read/do
agent split, and Compose and Buildx pinned by digest. Do not invent a new claim to
replace the lost ones.

Done when the three cells and the footer match the sources, and the build's own page
tests pass.

## Block I — The site on the design system

### §DD285 The site on the design system's shared template

The site is on the design system's site layer, pinned at 2026.3.22. Chrome.tsx draws the
nav and the footer, the landing hero and the feature index are SiteHero and
SiteFeatureIndex, and every section head is SiteSectionHead. The local Nav, Footer,
Hero, FeatureIndex, CopyButton, Rich, ThemeToggle, lib/theme.ts and scripts/og-image.mjs
are gone. The build calls prerenderSite and renderOgCard; og-card.mjs keeps filling the
hand-drawn card, because its measured pill (DD219) is not something ogCardSvg draws.
Analytics loads only after consent, as the hub's AG1.

The tone is `navy`, not the `cool` this section first named. Cool turned out to be the
preset's neutral grey with an orange brand, and navy, added later by VDS221 and the
hub's tone, is the blue ground the page already stood on. Its brand is re-keyed to the
azure pair.

What is left is the census: the site is not in the design system's consumers.json. Its
entries measure as ./site, ./site.css, ./tokens.css and ./vite, with tone navy. Adding a
consumer there rewords that repository's README and census tests, so it is that
repository's change, the way VDS222 registered the pilot.

Done when:

- The site appears in the design system's consumers.json.
