# Decisions

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

- ✅ **DD283** **Nobody knows whether winget's validation VM can install it, since the preflight exits 7 without WSL2** — A winget PR asks for manual validation instead of the installer skipping its preflight, because a silent install on a machine without WSL2 must still refuse.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)

## Block I — The site on the design system
