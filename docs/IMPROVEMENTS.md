# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD288 Admitting clients below the engine API floor

Met in Turing (viglet/turing 2026.3, its T1108). The FreeWilly engine (Docker 29.7.2)
refuses any client below API 1.40. Testcontainers' bundled docker-java opens with 1.32,
so on this machine every Testcontainers suite reads Docker as absent and skips its
container tests, until each project pins a newer version itself (Turing added
`docker-java.properties` with `api.version=1.44`). The same holds for any older tool or
SDK that pins its API version.

This is Moby's own floor, not a FreeWilly defect, and the engine stays unmodified (the
non-goal against forking it). But dockerd takes its minimum from the
`DOCKER_MIN_API_VERSION` environment variable, which the process that starts the engine
inside the `freewilly` distribution can set without changing the engine.

What FreeWilly should do: offer the minimum API version as an engine setting, default
unchanged (Moby's own), that sets `DOCKER_MIN_API_VERSION` when the engine starts, with
the preflight-style explanation of what it admits and why one would lower it (old
Testcontainers, old SDKs). `read health` then shows the effective minimum (see the
companion line on printing it).

Done when lowering the setting to 1.24 lets a client asking for API 1.32 list
containers, and leaving it alone keeps today's floor.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD283 Measuring the winget install before promising it

winget-pkgs validates a new package by installing it silently on a Microsoft VM.
FreeWilly's PrepareToInstall refuses with exit 7 when the preflight blocks, and WSL2 or
hardware virtualization is very likely off on that VM. That refusal is DD130's decision
and is right for a person, so it is not loosened on a guess.

The measurement comes first: enable LocalManifestFiles, run `winget install --manifest
build\winget` in Windows Sandbox, which has no WSL2, and in the test guest, which has
it. If the Sandbox run exits 7 with the systemNotSupported message, the first PR says so
and asks for manual validation. The README and the site name `winget install
alegauss.FreeWilly` only after the PR merges, because an install line that finds nothing
is worse than none.

### §DD290 DD290 — Seeing the winget submission through to a working install

From the portfolio review of 30/10/2026 (visibility, `produtos/freewilly.md`). DD282
shipped the submission path, and the first pull request to winget-pkgs is now open,
waiting for the moderators. Until it merges, `winget show alegauss.FreeWilly` finds
nothing, and the README and the site rightly do not mention winget. The work is to
follow that pull request to its end, not to build anything new.

While it waits: answer the moderators' review comments, and if the validation VM fails
the install (the risk DD283 set aside, since the preflight exits 7 without WSL2), record
what it reported and fix the manifest or the installer's silent path. Once merged:
confirm `winget install alegauss.FreeWilly` installs the current release on a clean
machine, then add `winget install alegauss.FreeWilly` to the README's download line and
to the site, and check that the next release's automatic submission (DD282) produces an
update pull request rather than a new package.

Done when `winget show alegauss.FreeWilly` lists the latest release and the README and
the site name the command.

### §DD292 DD292 — Distributing through the Microsoft Store

From the portfolio review of 30/10/2026 (visibility, `estrategia/portfolio.md`). The
installer is not signed, so SmartScreen warns on every first download, which is the
wrong first impression on exactly the corporate laptop this tool is for. The decision
for all of the author's Windows apps is the Microsoft Store, which signs the MSIX
package itself, instead of buying a code signing certificate. claude-tray tests the path
first (visibility VS58); this line waits for what that test learns.

FreeWilly has restrictions of its own to check against the Store's rules: it enables or
relies on the WSL2 feature, which can require elevation; it creates and registers its
own WSL distribution and downloads the engine from upstream at install time; and its
per-user, no-admin install is the selling point, so the package must keep it. If MSIX
cannot host the preflight and the WSL registration, the answer may be a Store listing
that points to the existing installer, or no Store at all; the outcome is written down
either way.

Done when FreeWilly is in the Store with its install still working without admin, or the
reason it cannot be is recorded here and in the product sheet.

## Block G — The agent surface (an agent operates this, and pays in tokens)

### §DD289 The engine minimum API version is never shown

Met in Turing (viglet/turing 2026.3, its T1108), running Testcontainers integration
tests (`mvn verify`, Testcontainers 2.0.5) against the FreeWilly engine (Docker 29.7.2,
API 1.55, minimum API 1.40).

Testcontainers' bundled docker-java opens with API version 1.32. The engine answers `400
client version 1.32 is too old. Minimum supported API version is 1.40`, Testcontainers
concludes Docker is unavailable, and every `@Testcontainers(disabledWithoutDocker =
true)` test is reported as skipped. The build ends green, and nothing on the machine
says why.

`freewilly read health` and `read context` print the engine's API version (`engine API
version 1.55`, `api=1.55`) but not its minimum. `DockerApi` already reads
`MinApiVersion` (the sample machine carries `1.24`), so the fact is in hand and only
unprinted. The one number that explains a refused client is the one an agent cannot see.

What FreeWilly should do:

- Print the minimum beside the version in `read health` and `read context`
  (`engine API version 1.55 (accepts 1.40 and later)`).
- Where the engine's own log records a client refused for its API version, say so in
  `read health` with the version it asked for, and the remedy (a newer client, or the
  client's `api.version` / `DOCKER_API_VERSION` setting).

Also observed, not yet understood: with `DOCKER_API_VERSION=1.44` set, Testcontainers'
detection still failed on about one run in two, then succeeded consistently once the
version came from a `docker-java.properties` file. Worth checking against DD142 (every
client failing together in bursts) before assuming a client-side cause.

Workaround in Turing: `turing-app/src/test/resources/docker-java.properties` with
`api.version=1.44`.

## Block H — The public surface (the site a reader and an agent both read)

### §DD291 DD291 — Opening on the corporate laptop without admin

From the portfolio review of 30/10/2026 (visibility, `estrategia/portfolio.md` and
`produtos/freewilly.md`). The first answer anyone gives to a free Docker Desktop
alternative is Rancher Desktop or Podman Desktop: free, open source, multi-platform,
backed by SUSE and Red Hat. The README and the site open on the licence ("no headcount
threshold, no revenue threshold"), which those two also satisfy, and mention "per-user
install, no admin prompt" only as a small note under the download link.

What separates FreeWilly is who can install it: a developer on a managed corporate
laptop, with no admin rights, whose company crossed Docker Desktop's limits. The
README's first paragraph and the site's hero should open on that reader and that
situation, with the licence as the reason the company allows it, and say plainly the one
exception (enabling the WSL feature may need elevation once). A short, honest comparison
row against Rancher and Podman (admin needed or not, own distribution, no resident
service) answers the comment before it is written.

Done when the first screen of the README and of the site names the corporate laptop
without admin, and the comparison with Rancher and Podman is on the page.

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
