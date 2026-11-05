# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 💭 **DD303** (deps: —) **Testcontainers detects the engine on about one run in two even with an API version the engine accepts** — Whether the misses are docker-java's or the pipe's DD142 bursts decides who fixes it, and nobody has set the runs against the journal. → §DD303

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

- 📋 **DD296** (deps: DD294 ✅, DD295 ✅) **A container from someone else's compose file or a docker run cannot be given a title, icon or link** — An Edit details screen that saves title, description, icon and links where Settings says: FreeHarbor's store by default, or the project's .freeharbor or compose. → §DD296
- 📋 **DD297** (deps: DD294 ✅) **A project header names the project and a count, with no icon, no description and no way to open what it serves** — A group header carries icon, description, an Open button and Start/Stop; port links become chips; standalone containers sit in their own block, one verb per row. → §DD297
- 📋 **DD298** (deps: DD297) **A project of twenty containers and ten links fills the page, and its two broken services sit among eighteen healthy ones** — One primary link and two pinned ones are buttons, the rest a filterable menu; services fold into freeharbor.group tiers, and a healthy tier stays folded. → §DD298
- 📋 **DD299** (deps: DD294 ✅) **Nothing shows one project whole: its endpoints, every service, and why the one that stopped stopped** — Clicking a project or a standalone container opens a detail page with its endpoints, every service, recent output and the daemon's words for a failure. → §DD299
- 📋 **DD300** (deps: DD294 ✅, DD297) **The containers page has one dense layout, so a user with five projects reads a table built for forty** — A Cards view shows each project as a card with icon, description, links and its services, beside List behind a switch; the choice, filter and sort persist. → §DD300
- 📋 **DD301** (deps: DD295 ✅, DD296) **Nothing the user prefers can be set in the window, so where an edit is saved cannot be chosen at all** — A Settings destination chooses where edits are saved (FreeHarbor, .freeharbor or the compose file), lists the projects that differ and states the read order. → §DD301

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD290** (deps: winget-pkgs moderator approval) **winget install alegauss.FreeHarbor finds nothing while the first winget-pkgs pull request waits for moderators** — The terminal is where a developer looks for a Docker replacement, and the README cannot name the command until the package is live. → §DD290
- 📋 **DD292** (deps: visibility VS58) **Every first download meets a SmartScreen warning, because the installer is not signed** — The Store signs the MSIX package itself, and claude-tray tests that path first, so this follows what that test learns. → §DD292

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)

- 📋 **DD304** (deps: —) **The compare page scores Rancher and Podman lower than they are on per-user install and on the docker_engine pipe** — A comparison caught overclaiming loses the reader on every other row, and three cells say what current sources contradict. → §DD304

## Block I — The site on the design system

- ⏳ **DD285** (deps: viglet-design-system: VDS213–VDS215, claude-tray: T389) **The site keeps its own copy of the template that eight product sites share** — The site is not yet registered in the design system's consumers.json, which rewords that repo's README and census tests. → §DD285

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
