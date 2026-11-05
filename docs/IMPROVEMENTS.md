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
package itself, instead of buying a code signing certificate. claude-tray tests the path
first (visibility VS58); this line waits for what that test learns.

FreeHarbor has restrictions of its own to check against the Store's rules: it enables or
relies on the WSL2 feature, which can require elevation; it creates and registers its
own WSL distribution and downloads the engine from upstream at install time; and its
per-user, no-admin install is the selling point, so the package must keep it. If MSIX
cannot host the preflight and the WSL registration, the answer may be a Store listing
that points to the existing installer, or no Store at all; the outcome is written down
either way.

Done when FreeHarbor is in the Store with its install still working without admin, or
the reason it cannot be is recorded here and in the product sheet.

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
