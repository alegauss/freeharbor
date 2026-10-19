# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

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

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)

## Block I — The site on the design system

### §DD285 The site on the design system's shared template

site/ carries its own copy of the template the eight React product sites share:
components/Nav.tsx and Footer.tsx, sections/Hero.tsx, FeatureIndex.tsx and NonGoals.tsx,
pages/Feature.tsx, and ui/Ad.tsx, CopyButton.tsx, RawSvg.tsx, Rich.tsx and
ThemeToggle.tsx, with lib/ads.ts and lib/theme.ts behind them. scripts/prerender.mjs and
scripts/og-image.mjs are the build's copies of the same two steps. The tokens live in
src/index.css, with --azure, --azure-bright and --azure-deep as the brand constants.

The move follows claude-tray's pilot, T389. The page takes `data-tone="cool"` because
its ground is already cool: #f5f9fc in light and #08131b in dark, both on the blue side.
The accent comes from projects.json, `46,155,214` with `#67C3F2` as the bright step,
which is the --azure pair the page draws today. Shared components come from `./site`;
HeroSession.tsx, Waves.tsx, the product sections and the Claude Code and Compare pages
stay local. The build calls the `./vite` prerender and OG-card helpers in place of the
two scripts, and prerender.test.mjs then tests what the helper writes. The Google tag in
index.html loads only after consent, as the hub's AG1.

Done when:

- site/src holds none of the files `./site` provides.
- The prerendered HTML of every route holds the same text as before.
- Light and dark screenshots of each route are compared before and after, and the commit
  lists every difference.
- The site appears in the design system's consumers.json.
