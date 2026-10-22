import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { siteThemeScript } from "@viglet/viglet-design-system/site";
import { THEME_KEY } from "./src/lib/site-content.ts";

// GitHub Pages derives this from the repository name, so it is not a preference — the site
// is served at https://alegauss.github.io/freewilly/ and every canonical, asset path and
// sitemap entry carries the prefix. (§6, DD59)
export const BASE = "/freewilly/";

// The pre-paint theme script comes from the design system rather than being typed into
// index.html, so the page and the toggle cannot disagree about the rule (DD285). Replace-or-
// throw, like the prerender: a template that lost its anchor would ship a flash of the wrong
// theme.
function themeScript(): Plugin {
  const anchor = "<!--theme-script-->";
  return {
    name: "theme-script",
    transformIndexHtml(html) {
      if (!html.includes(anchor)) throw new Error(`index.html lost its ${anchor} anchor`);
      return html.replace(anchor, () => `<script>${siteThemeScript(THEME_KEY)}</script>`);
    },
  };
}

export default defineConfig({
  base: BASE,
  plugins: [react(), tailwindcss(), themeScript()],
  build: {
    // docs/ is roadkeep's, never a web root — the site builds to its own dist/. (§6)
    outDir: "dist",
    emptyOutDir: true,
  },
});
