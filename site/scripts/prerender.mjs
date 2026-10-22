// The prerender and the social card, both through the design system's build helpers (DD285).
//
// prerenderSite renders every route into the client build's index.html, patching the <head>
// by replace-or-throw so a drifted template fails the build rather than shipping a page under
// another route's title (§DD41, S4/S8), and writes the Markdown twin beside each page (S5),
// manifest.json, sitemap.xml and robots.txt (§DD84) — all from the route table routes.tsx has
// already asserted against the component map. The sitemap's lastmod is the last git commit
// under the authored tree, never the build clock.
//
// renderOgCard rasterises the card og-card.mjs fills to dist/og.png at 1200x630 (§DD49).
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { prerenderSite, renderOgCard } from "@viglet/viglet-design-system/vite";

import {
  render,
  renderStatic,
  ROUTE_META,
  canonicalUrl,
  outputDir,
  OG_IMAGE,
} from "../dist-server/entry-server.js";
import { htmlToMarkdown } from "./markdown.mjs";
import { CARD_FONT, cardSvg } from "./og-card.mjs";

const siteDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const distDir = join(siteDir, "dist");

const card = await renderOgCard({ svg: cardSvg(), out: join(distDir, "og.png"), fontFamily: CARD_FONT });
console.log(`og-image: dist/og.png  ${card.width}x${card.height}  (${(card.bytes / 1024).toFixed(0)} kB)`);

const written = prerenderSite({
  distDir,
  routes: ROUTE_META,
  render,
  canonicalUrl,
  outputDir,
  ogImage: OG_IMAGE,
  // The Markdown twin, converted from the same component tree as clean static HTML.
  twin: (path) => htmlToMarkdown(renderStatic(path)),
  manifest: { name: "FreeWilly", base: "/freewilly/", llms: "llms.txt" },
  git: { cwd: siteDir },
  log: console.log,
});

console.log(`prerender: ${written.length} route(s) + twins, manifest.json, sitemap.xml and robots.txt written to dist/`);
