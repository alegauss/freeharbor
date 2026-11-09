# DD294 to DD301: the containers page, redesigned

How the containers page, the detail page, the edit dialog and Settings are laid out. The
roadmap sections say what each task does and why; this file says what each screen looks
like, so a task can be built without the design board. Where the two disagree, the
roadmap's behaviour wins and this file is corrected.

The screens were explored on a design board
(https://claude.ai/artifact/VgKD48AfuNnC1AyduJJExu, private to its owner). Each section
below links the image of its board, exported into
[`DD294-containers-redesign/`](DD294-containers-redesign/). The images are a record of the
board as it was approved and are not regenerated; a change to a screen is made here, in
text.
The board is an HTML mockup and the window is WPF, so it shows composition and hierarchy,
not pixels. Section 9 lists where this file deliberately departs from the board.

Everything here sits under the window's existing law: [DD34](DD34-window-constitution.md),
`Palette.cs` for every colour, and claude-tray as the formatting reference.

---

## 1. The shared vocabulary

These elements appear on more than one screen. Each is built once and reused.

| Element | What it is | Built from |
|---|---|---|
| **Icon tile** | A rounded square holding the resolved icon in white, on the resolved colour. 40 px in the list, 44 px on a card, 64 px on the detail page, 24 px in a rail. Radius about a quarter of the side. | New. The colour comes from a set of tile hues declared in `Palette.cs` (L1), never a hex in markup. |
| **Monogram** | The tile with the first letter of the title instead of an icon, on a hue hashed from the name. | The tile, with text. |
| **State chip** | The DD36 chip, plus a shape before the word: a filled circle for running, a hollow square for stopped, a triangle for a failure (L8). | The existing chip in `ContainersPage.xaml` and `RowStyle.cs`. |
| **Project chip** | The same chip on a group, saying `n of m running`. Running when all run, stopped when none do, failure when any exited with a non-zero code. | The chip, fed by the existing `ProjectCount`. |
| **Open button** | Opens the primary link. Filled with the Fluent accent (`AccentButtonStyle`), disabled rather than hidden when nothing runs. | Fluent button. The board's blue is a stand-in for the accent, not a new colour. |
| **Link chip** | A secondary link: label, then host in tertiary text. A non-HTTP port is a dashed chip that copies the address. | The existing `PortLink` style, widened to carry a label. |
| **Verbs** | Logs, one primary verb, and `⋯`, all always drawn. Shell, Restart, Remove and (new) Edit details and Details live in `⋯`. | The existing row verbs. |
| **Failure line** | The daemon's own sentence under whatever caused it, in `Palette.Danger`. | The existing `Failure` line. |
| **Resolved details** | Title, description, icon, colour and links for a project, a service or a container, resolved in the DD294 and DD295 order. | New, one class. Every screen reads it; none resolves on its own. |

Where a title is missing it falls back to the service or container name, so a title is
never blank. Where a description is missing nothing is drawn, except on a standalone
container, which shows `No details yet · Add icon and description`.

As built (DD325):

- A port chip reads the host port, `:8080`, with its protocol only where it is not TCP
  (`:5353/udp`), in `Palette.Mono` on the accent at low opacity. The mapping,
  `8080->80/tcp`, is the tooltip, and the text filter still matches it.
- A port only exposed keeps the daemon's `6379/tcp`: it has no host port to show.
- Every Open holds the external-link glyph and the link's name verbatim (`Site`,
  `Console`, `Inbox`), or `Open` where the link is a port. One template, `OpenContent`,
  draws it in the list, the cards and the detail page.
- A stopped container's chips are drawn at half opacity.

---

## 2. The list (DD297)

The default view, and the one that holds forty containers. Columns stay aligned with the
headings, and the test that pairs them still passes.

Board: [list.png](DD294-containers-redesign/list.png). The row it draws in its hover state,
with Logs and Restart as icons, is overruled by section 9.1.

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│ [▢]  Storefront   (● 4 of 4 running)                  [Open site] [Stop] [⋯]        │  group header
│      Shop API, admin panel and the Postgres behind them                              │
├─────────────────────────────────────────────────────────────────────────────────────┤
│  ●  Public site        nginx:1.27       (● running)  [:8080]      Up 2 hours  Logs Stop ⋯ │
│     Hot reload on                                                                    │
│  ●  admin              node:22-alpine   (● running)  [:8081/admin] Up 2 hours  Logs Stop ⋯ │
│  ●  PostgreSQL         postgres:17      (● running)  [:5432 ⧉]    Up 2 hours  Logs Stop ⋯ │
└─────────────────────────────────────────────────────────────────────────────────────┘

┌─ Standalone containers ─────────────────────────────────────────────────────────────┐
│ [▢] Mail catcher   mailpit · catches outgoing mail   axllent/mailpit  (● running) [Inbox] Logs Stop ⋯ │
│ [N] nervous_tesla  No details yet · Add icon and description           (● running)        Logs Stop ⋯ │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

- **Group header.** Chevron, icon tile, title (a link to the detail page), project chip,
  description underneath in secondary text. On the right: Open (primary link), Start or
  Stop for the whole project, `⋯`. A click anywhere else on the strip folds the group, as
  DD127 made it.
- **Row.** State shape, title over description, image, state chip, link chips, status,
  verbs. The title is the resolved one; the service name moves to the tooltip.
- **Standalone block.** Last, under its own heading, with no group verbs. Each row has its
  own icon tile and its own Open.
- **Fixed:** column alignment, verbs always drawn, failure line under the row.
- **Free:** spacing, how a long description truncates, whether the description row
  collapses when empty (it should).

As built (DD297):

- The project chip sits in the STATE column, not beside the title, so the column a
  reader scans for trouble covers projects too. A half-up project, like a paused
  container, takes the in-between tone and a half circle.
- A project's Open follows the first primary link among its services, ordered by name, so
  a re-sort never moves it. The header's fold covers NAME to STATUS and leaves PORTS to
  Open.
- "Does not answer HTTP" is a list of well-known ports matched on the port inside the
  container (`PortLink.NotHttp`), not a probe, because the list makes no call per refresh.
- The Standalone heading appears only when a project sits above it. A machine with no
  compose project is the flat list it always was.

As built (DD324):

- Each project, and the standalone block, is one card drawn a row at a time: the first
  row has the top corners, the last the bottom ones and a 12 px gap, and each row's top
  stroke is the divider above it. The list's items lose the Fluent item chrome so the rows
  touch, and the card carries the hover. No project, no card.
- A header's name cell spans NAME to STATUS and its cells sit at the top, so the
  description runs under the title and the chip in STATE stays beside the title.
- A service inside a project leads with its state shape in the engine dot's colours; the
  tile belongs to the header and to standalone rows.
- The standalone block opens with a strip: a box glyph tile, the heading, `n of m
  running` and one line saying what the block is.
- The card's right edge meets the page edge and is lost there with the filter box's; the
  page's right inset is DD328's.

## 3. A large project (DD298)

What changes when a project has many services or many links. Under five services and
three links nothing here applies.

Board: [large-project.png](DD294-containers-redesign/large-project.png), the card on the
left with its link menu open and the list on the right.

```
group header:  [▢] Platform (▲ 18 of 20 running)        [Gateway ▸ | 10 ▾] [Admin] [Grafana] [Stop] [⋯]

               ┌ Filter 10 links ─────────────┐
               │ PINNED                        │
               │  Gateway      gateway   :8000 │
               │  Admin        admin     :8001 │
               │ ALL                           │
               │  API docs     gateway   :8000/docs │
               │  ...                          │
               └───────────────────────────────┘

tier filter:   (All 20) (Running 18) (Problems 2)
tiers:         ▸ Edge            2 containers · gateway, admin           (all running)
               ▸ APIs           10 containers · catalog, cart, orders…    (all running)
               ▾ Workers         4 containers                             (2 problems)
                   ▲ payments-worker   Exited (1) 2 minutes ago            Logs Start ⋯
                   ▲ notifications     Restarting (1) 10 seconds ago       Logs Start ⋯
                   ● search-indexer                                        Logs Stop  ⋯
               ▸ Data and tools  4 containers · postgres, redis, queue…   (all running)
```

- **Links.** The `freeharbor.primary` link (else the first) is a split button: Open on
  one side, a count and a menu on the other. Up to two pinned links sit beside it. The
  menu has a filter box once it lists more than five.
- **Tiers.** One per `freeharbor.group` value, in first-seen order. A tier where
  everything runs is folded behind a one-line summary; a tier with a problem opens. No
  label means one tier, and the plain list.
- **On a card** the services become a health bar (one block per container, problems last
  and drawn as triangles), the failure lines, and `Show all 20`, which opens the detail
  page.
- **Fixed:** a healthy tier starts folded; a problem is never folded away.

As built (DD298):

- A link is pinned by its service's `freeharbor.pinned` label, naming up to two of its
  links by name, comma-separated.
- From three links, a header and a card show Open, the pinned links and an `n ▾` menu of
  every link under Pinned and All, with a filter box from six.
- Tiers come in first-seen order by container name, never by the sort. A service with no
  group in a grouped project goes in a last tier named Other. What is remembered is the
  healthy tiers somebody opened, so a tier that goes bad opens on its own.
- No per-project tier filter. The page's state chips (DD300) narrow every project over the
  rows in hand already, and a second filter would be two controls writing one state.
- From five services, a card draws one shape per container with problems last, lists only
  the services that need attention, and Show all opens the detail page.

## 4. The detail page (DD299)

Board: [detail.png](DD294-containers-redesign/detail.png).

```
┌ rail ────────────────┐ ┌──────────────────────────────────────────────────────────────┐
│ [Filter          ]   │ │ [▢▢]  Search lab  (▲ 2 of 3 running)   [Open console] [Restart] [Stop] [⋯] │
│ COMPOSE PROJECTS     │ │       Search engine with a sample index and its indexer         │
│ [▢] Storefront    ●  │ │       compose project search-lab · compose.yaml · up 40 minutes │
│ [▢] Search lab    ▲  │ │                                                                 │
│ [▢] Team wiki     □  │ │ ▲ indexer: Exited (143) 3 minutes ago            [Start indexer]│
│ STANDALONE           │ │                                                                 │
│ [▢] Mail catcher  ●  │ │ ENDPOINTS                                                        │
│ [N] nervous_tesla ●  │ │ ┌ Console · engine ┐ ┌ API · engine ┐ ┌ Database · db ┐         │
│                      │ │ │ localhost:2700/… │ │ …            │ │ localhost:3306│         │
│                      │ │ │ [Open] [Copy]    │ │ [Open][Copy] │ │ [Copy]        │         │
│                      │ │ └──────────────────┘ └──────────────┘ └───────────────┘         │
│                      │ │ SERVICES                     RECENT OUTPUT · indexer  Open log │
│                      │ │ ● engine  search:3.1  running │ 12:41:15 INFO loading batch…  │
│                      │ │ ▲ indexer indexer:3.1 exited  │ 12:41:21 received SIGTERM…    │
│                      │ │ ● db      mariadb:11  running │ (stderr in Palette.Danger)    │
└──────────────────────┘ └──────────────────────────────────────────────────────────────┘
```

- **Opened from:** the title link in a list group header or standalone row, `Details` in
  any `⋯`, a click on a card outside its buttons, and `Show all` on a large card. Back
  returns to the view and scroll position it came from.
- **Rail:** compose projects, then standalone containers, each with a tile and a state
  shape. Selecting one swaps the page without going back through the list.
- **Recent output** tails the failing service, or the first service when none failed.
- **Fixed:** the failure line repeats the daemon's sentence; Endpoints lists every link,
  including those folded into a menu elsewhere.
- **Free:** whether memory and CPU appear in Services (only when the engine reports them).

As built (DD299):

- The page lies over the list in the Containers destination, and the list stays built
  underneath, so Back finds it at its scroll position. It opens from `Details` in any
  `⋯`, from a header's or standalone container's title, and from the rail.
- Memory and CPU are not shown. The list response carries neither, and a stats stream
  per service is a call this page does not make.
- Recent output is read once, not followed (`tail` 200, newest 14 lines kept). Open log
  is the followed view.
- `FreeHarbor.exe --capture-window <png> containers --fixture --detail=<project>`
  photographs the page from the fixture. `<project>` is the compose name (`sample` in the
  fixture), not the title, and a name that opens no page is refused (DD323).

## 5. The cards view and the switch (DD300)

Board: [cards.png](DD294-containers-redesign/cards.png), exported with the Running filter
on, so it shows one project card and the standalone section. The wireframe below shows the
unfiltered page.

```
[Containers]   3 projects · 3 standalone · 9 of 14 running     (All 5)(Running 2)(Stopped 2)(Problems 1)
                                                               [Filter…] [Cards | List]
┌──────────────────────────────┐ ┌──────────────────────────────┐ ┌──────────────────────────────┐
│ [▢] Storefront (● 4 of 4)  ⋯ │ │ [▢] Search lab (▲ 2 of 3)  ⋯ │ │ [▢] Team wiki (□ 0 of 3)   ⋯ │
│ Shop API, admin panel and…   │ │ Search engine with a sample… │ │ WordPress with its database… │
│                              │ │ ▲ indexer: Exited (143) …    │ │                              │
│ [Open site localhost:8080]   │ │ [Console localhost:2700]     │ │ [Open site] (disabled)       │
│ [Admin] [db :5432 ⧉]         │ │                              │ │                              │
│ ● web    nginx:1.27   :8080  │ │ ● engine …                   │ │ □ wordpress …                │
│ ● db     postgres:17  :5432  │ │ ▲ indexer …                  │ │ □ db …                       │
│ Up 2 hours   Logs Restart Stop│ │ Up 40 min    Logs Restart Stop│ │ Exited 20 h  Logs Restart Start│
└──────────────────────────────┘ └──────────────────────────────┘ └──────────────────────────────┘
Standalone containers
┌ [▢] Mail catcher (● running)  axllent/mailpit · :8025 · Up 3 days   [Inbox] [Stop] [⋯] ┐
└ [N] nervous_tesla             No details yet · Add icon and description     [Stop] [⋯] ┘
```

- A card is a `GroupCard` (DD34 section 3: card fill, card stroke, 8 px radius). The grid
  fills the width with cards of at least 400 px.
- A stopped project dims its links, never the card, so its Start stays readable.
- The switch, the filter chips, the text filter and the sort are shared by both views and
  remembered with the window's placement (DD39).
- **Fixed:** the primary verb is the accent button only when it is Start.

As built (DD300):

- The switch and the chips sit before the filter box, which keeps the right edge it always
  held. A capture showed a button placed past it clipped by the page's edge.
- A card's footer is the one verb, without Restart beside it: DD36 keeps a row to one
  verb, and the project's page is a click away.
- Chips narrow the rows before the text filter and the sort. Stopped is everything not up,
  failures included; Problems is the subset that failed or is stuck in between. The
  counts line counts every container, not the chip's narrowing.
- The view and the chip are remembered with the window (DD39). The text filter and the
  sort carry across a switch, not across a restart.
- Open never follows a port that does not speak HTTP, even where the resolver made a link
  of it.
- `--capture-window <png> containers --fixture --cards` photographs the cards. A capture
  always draws All, whatever this account chose.

As built (DD326, the card):

- The header has the list header's `⋯` at its top right. Both open one menu,
  `RowOverflow`, so they cannot offer different verbs.
- A meta line, `compose · <project> · n services`, sits under the description.
- Open writes where it goes after its name (`Site  localhost:8080`). Links a browser can
  open are outlined buttons with the glyph, name and host. Copy-only addresses are dashed
  chips, `service :port`, on a line of their own. On a project with a link menu only the
  pinned links are drawn and the rest stay behind the menu (DD298).
- Under a rule, each service is a row: its shape and state word in its state's colour, its
  title and image, and its first port in mono.
- The footer has the first running service's status on the left, or the first service's
  where none runs, and the one verb on the right.

As built (DD326, the page head):

- A `Containers` title, then the counts line, then the state chips on a line of their own.
  On one line, the title, the counts and four pills ran under the switch at the default
  width, which is the overlap DD320 removed.
- A chip is a pill: its word, then its count in tertiary text. The chosen one carries the
  link chips' accent tint and stroke.
- Cards and List are one segmented control in one outline, with the chosen half filled.
  The filter box keeps the right edge; moving it into the shell's strip is DD35's
  business.

## 6. The edit details dialog (DD296)

A dialog owned by the main window, about 980 px wide.

Board: [edit-project.png](DD294-containers-redesign/edit-project.png) for a compose project
(exported with Save to set to the compose file), and
[edit-container.png](DD294-containers-redesign/edit-container.png) for a standalone
container, the `.freeharbor` folder Save writes, and the one-time compose diff.

```
┌───────────────────────────────────────────────────────────────────────────────────┐
│ Edit details · compose project storefront                                       ✕ │
│ 🗀 C:\src\storefront\compose.yaml + compose.override.yaml  [Copy path] [Open folder] │
├──────────────────────┬────────────────────────────────────────────────────────────┤
│ PROJECT              │ EDITING (Service) storefront / db                          │
│ [▢] Storefront     • │ container storefront-db-1  image postgres:17  id 3f2a9c1b7e04 │
│     storefront · 4   │ Saved under storefront / db, so it survives a recreate.    │
│ SERVICES             │                                                            │
│ [▢] Public site      │ PREVIEW  [▢]  PostgreSQL                                   │
│     web · storefr…-1 │               No description                               │
│ [▢] PostgreSQL     ◂ │ Icon     bundled icon for postgres                         │
│     db · storefr…-1  │ [▢][▢][▢][▢]…[Aa][Choose file…]                             │
│                      │ Colour   ○ ○ ○ ○ ○ ○ ○ ○                                    │
│                      │ Title    from image label org.opencontainers.image.title   │
│                      │ [PostgreSQL                                          ]     │
│                      │ Description  none                                0 / 120   │
│                      │ [                                                    ]     │
├──────────────────────┴────────────────────────────────────────────────────────────┤
│ 💾 Saves in FreeHarbor on this machine.            Save to [FreeHarbor (default) ▾] │
│    Default from Settings · only you see these details             [Cancel] [Save] │
└───────────────────────────────────────────────────────────────────────────────────┘
```

- **Identity, always.** The header names the project and its compose files. The Editing
  strip names the target, the key it is saved under, and the container, image and short
  id. A rail item shows the title over `service · container`.
- **Each field says its source** (x-freeharbor, a label, the image, a fallback, or
  `edited here`) and has Reset to source.
- **Save asks nothing.** The footer says where it will write. `Save to` changes the
  destination for this project only, and the change is listed in Settings. Writing into a
  compose file shows its diff once per project, with `Don't ask again`.
- **A standalone container** has one target, no rail and no `Save to`: it always saves in
  FreeHarbor. When docker made its name up, a warning says the details will not follow a
  new `docker run`, with a link that copies the `--label` flags.
- **Fixed:** the identity block, the source per field, no confirmation on Save.

## 7. Settings (DD301)

A destination in the nav strip, before About, built as a page (L2).

Board: [settings.png](DD294-containers-redesign/settings.png).

```
Settings

Where edits to names, descriptions and icons are saved
Applies to compose projects. A standalone container is always saved in FreeHarbor.
 (•) In FreeHarbor                          default
     Only on this machine and only for you. Nothing in the project folder changes.
 ( ) In a .freeharbor folder in the project  shared through git
     A file beside the compose file, which it overrides. Commit it and the team sees it.
     <project>\.freeharbor\project.yaml · <project>\.freeharbor\icons\
 ( ) In the compose file                    edits your file
     Only the x-freeharbor block. The first save in each project shows the diff once.

Projects that save somewhere else
 storefront   C:\src\storefront\compose.yaml        .freeharbor in the project   ✕
 search-lab   C:\src\search-lab\docker-compose.yml  The compose file             ✕

Read order
 1. Saved in FreeHarbor  2. .freeharbor/project.yaml  3. x-freeharbor
 4. Service labels (freeharbor.*, then homepage.*)  5. Image labels  6. Bundled icon or monogram
```

The page is a single column, about 920 px wide, centred. Each choice is a radio card: the
selected one carries the accent border.

As built (DD301):

- The page is a left-aligned column at most 720 px wide, like About, and each choice is a
  plain radio button with its explanation under it, not a radio card.
- A project's own choice is kept under the folder compose ran in, else its name. The edit
  dialog opens on it, then on the default, then on FreeHarbor where the project cannot use
  either. A save to the default forgets the project's departure, so the list holds only
  projects that differ. Choosing a new default drops the projects that now match it.
- The preference lives in the window's memory (DD39). With `--fixture` a capture reads a
  throwaway file instead, so a picture never carries this account's project folders, and
  it draws at the window's own default size.
- The snapshot renderer moved out of the shell into `Ui/WindowSnapshot.cs` to make room
  for the destination: rendering a window is capture, not chrome.

## 8. The files the details come from (DD294, DD295)

Board: [details-sources.png](DD294-containers-redesign/details-sources.png), the read
order with a labelled compose file (its text is in Portuguese, written for the review).

`.freeharbor/project.yaml`, in the folder compose records as `working_dir`:

```yaml
title: Storefront
description: Shop API, admin panel and the Postgres behind them
icon: icons/storefront.svg        # relative to .freeharbor/, or a bundled icon name
services:
  admin:
    title: Back office
    icon: icons/backoffice.png
  db:
    description: Orders and customers
```

The `x-freeharbor` block in a compose file takes the same keys. Service labels are
`freeharbor.title`, `.description`, `.icon`, `.url`, `.url.<name>`, `.primary` and
`.group`. An icon is a bundled name or a local file; a URL is never fetched.

## 9. Where this departs from the board

1. **No verbs on hover.** Board B reveals Logs and Restart on hover. The row keeps DD36's
   always-drawn Logs, one verb and `⋯`, as the comment in `ContainersPage.xaml` records:
   a verb nobody can see is a verb nobody finds.
2. **The header strip still folds.** The board treats a click on a group as opening the
   detail page. In the list that click already folds the project (DD127), so the detail
   page opens from the title link and from `Details` in `⋯`. Only a card opens on a plain
   click.
3. **Ports were already links.** DD7 drew them as links; DD297 restyles them as chips, it
   does not introduce them.
4. **Colours are tokens.** The board's hex values stand in for the Fluent accent, the
   `Palette` state colours and the new tile hues.

## 10. What the fixture must carry

DD38's `SampleMachine` is how every screen here is captured without a daemon (L6), so it
grows with the work: a project with `x-freeharbor` details, a service with `freeharbor.*`
labels, one with only an image label, one with nothing, a standalone container with a
docker-made name, and a project of twenty services in four `freeharbor.group` tiers with
two failures. Each task adds the part its screen needs.

As built (DD329):

- Four projects. Storefront (`sample`, four services, all running), Search lab
  (`sample-search`, its indexer exited 143), Team wiki (`sample-wiki`, stopped, every exit
  clean) and Platform (`sample-platform`, twenty services in Edge, APIs, Workers and Data
  and tools, two failed, ten named links with Admin and Grafana pinned). Standalone: Mail
  catcher with its own Inbox link, `sample-nervous_tesla` with nothing to resolve, and the
  DD167 container on a gone image.
- Storefront reads both project files: its `x-freeharbor` block names the project and the
  back office, and its `.freeharbor` file renames only the worker. The other three carry
  the `.freeharbor` file alone.
- Tiles are bundled glyphs, chosen from the names the build carries, so Storefront draws a
  globe and Search lab a cylinder where the board draws a bag and a magnifier.
- Storefront keeps the compose name `sample`, so `--detail=sample` still opens it.
