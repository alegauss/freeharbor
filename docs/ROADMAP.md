# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 📋 **DD321** (deps: —) **In a full test run the pipe relay once took no second connection for thirty seconds, then passed alone** — On 16 November a live relay ended its accept loop on IOException after six connections, so one closing client takes the pipe down. → §DD321

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

- 📋 **DD331** (deps: —) **Settings is plain radios in a 720 px column, where settings.png draws titled radio cards with tags and paths** — The owner chose the cards on 16 November, so Settings adopts the board and the spec's DD301 paragraph is rewritten with it. → §DD331

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD290** (deps: winget-pkgs moderator approval) **winget install alegauss.FreeHarbor finds nothing while the first winget-pkgs pull request waits for moderators** — The terminal is where a developer looks for a Docker replacement, and the README cannot name the command until the package is live. → §DD290
- 📋 **DD292** (deps: DD312 ✅, DD313 ✅, DD314, DD316, DD317, DD362 ⏸) **Every first download meets a SmartScreen warning, because the installer is not signed** — claude-tray reached the Store (T412), so what is left is the listing and the submission once the package installs and works without admin. → §DD292
- 📋 **DD314** (deps: DD310 ✅, DD311 ✅) **Packaged, the docker-desktop:// link buildx prints opens nothing, because the HKCU handler is never registered** — A uap:Protocol in the manifest registers the handler with the package and removes it on uninstall, which the Inno entry had to arrange by hand. → §DD314
- 📋 **DD316** (deps: DD311 ✅, DD315 ✅) **A Store install has no page before the first file, so a machine without WSL2 finds out only after installing** — MSIX runs no code at install time, so the preflight, the WSL2 turn-on, the engine download and the after-install notes move to the first launch. → §DD316
- 📋 **DD317** (deps: DD309 ✅) **Uninstalling the package leaves the freeharbor distribution and its multi-gigabyte disk registered with WSL** — MSIX runs no uninstall code, so removing the engine has to be a verb the user runs first, named in the app and in the Store listing. → §DD317

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)

## Block I — The site on the design system

## Non-goals

- **Feature parity with Docker Desktop** Kubernetes, the extensions marketplace and Dev
  Environments are most of that product and none of them is why anyone leaves it; the
  scope here is install, see, start, stop.
- **A fork of the engine** This drives upstream Moby unmodified. A fork would make every
  Docker answer on the internet subtly wrong for this tool's users, which is a worse tax
  than any licence.
- **macOS and Linux** The problem being solved is Windows-specific: Docker Desktop's
  terms plus WSL2 plumbing. Linux needs no GUI to install an engine, and macOS already
  has free alternatives.
- **Telemetry, accounts or a sign-in** Nothing here phones home and there is nothing to
  log into. A tool adopted to escape a licence check must not ship a different reason to
  be blocked by a corporate proxy.
- **A resident background service** The complaint this project answers is a desktop app
  holding gigabytes at every boot. Both the app and the engine run when asked, and
  autostart stays a setting the user turns on.
- **A model, prompts or API keys** FreeHarbor is the substrate an external agent drives,
  never a place intelligence lives: the caller already has a model, and hosting one
  would end the free, offline, no-account tool this is.
- **A second Docker CLI** The agent surface answers the joins the Engine API cannot
  make; what docker already answers well is not re-wrapped, so there is no build, no
  push and no registry credentials here.
- **Renumbering the DD task prefix** The rename stops at the product. Every id appears
  in a dependency, a section anchor, a shipped ledger entry and a pushed commit message,
  so a two-letter prefix change rewrites all of it to say exactly the same thing.
