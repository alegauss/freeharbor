import {
  SiteButton,
  SiteFooter,
  SiteNav,
  SiteSponsor,
  SiteThemeToggle,
  type SiteBrand,
} from "@viglet/viglet-design-system/site";
import {
  THEME_KEY,
  download,
  footer,
  navLinks,
  parentUrl,
  releasesUrl,
  repoUrl,
  sponsor,
} from "../lib/site-content";
import { ConsentSettings } from "./Consent";
import { Ad } from "./ui/Ad";
import { Waves } from "./ui/Waves";

// The chrome every route shares, drawn by the design system's site layer (DD285). What is left
// here is this product's half of it: which links, which words, and the water the footer opens
// out of.

// A mark, not a tile: the orca is taller than it is wide and carries its own silhouette.
const brand: SiteBrand = { href: "/freewilly/", name: "FreeWilly", logo: "/freewilly/logo.svg", logoShape: "mark" };

const themeLabel = (next: string) => `Switch to ${next} theme`;

export function Nav() {
  return (
    <SiteNav
      brand={brand}
      parent={{ href: parentUrl, name: "alegauss", pre: "part of", title: "alegauss: small developer tools" }}
      links={navLinks}
      actions={
        <>
          {/* DD153: the primary button is the download now that there is one. GitHub keeps
              its place as the ghost beside it — the repository is what a reader checks
              before installing anything, and demoting it to a footer link would be the
              wrong half of this trade. */}
          <SiteButton href={releasesUrl}>{download.ctaShort}</SiteButton>
          <SiteButton variant="ghost" href={repoUrl}>
            ★ View on GitHub
          </SiteButton>
          <SiteThemeToggle storageKey={THEME_KEY} label={themeLabel} />
        </>
      }
    />
  );
}

export function Footer() {
  return (
    <SiteFooter
      brand={brand}
      links={footer.links}
      sponsor={
        <SiteSponsor
          declaration={sponsor}
          moreLabel="More at"
          note="Both Apache 2.0 and self-hostable."
        />
      }
      disclaimer={
        <>
          {footer.disclaimer} <ConsentSettings />
        </>
      }
      // DD186 — the house ad, above the footer rather than inside it: it belongs to the end
      // of the page's content, not to the chrome. Placed here because every route ends in
      // this component, so a page added later cannot forget it and no page can end up with two.
      before={<Ad slot="page-end" />}
    >
      <Waves className="waves--footer" />
    </SiteFooter>
  );
}
