import { SiteButton, SiteFeatureIndex, SiteHero, SiteRich as Rich } from "@viglet/viglet-design-system/site";
import { Nav, Footer } from "../components/Chrome";
import { HeroSession } from "../components/HeroSession";
import { Operator } from "../components/sections/Operator";
import { Why } from "../components/sections/Why";
import { Preflight } from "../components/sections/Preflight";
import { Engine } from "../components/sections/Engine";
import { Pipe } from "../components/sections/Pipe";
import { Tray } from "../components/sections/Tray";
import { WindowSection } from "../components/sections/WindowSection";
import { NotResident } from "../components/sections/NotResident";
import { NonGoals } from "../components/sections/NonGoals";
import { Download } from "../components/sections/Download";
import { Waves } from "../components/ui/Waves";
import { download, hero, heroSession, repoUrl } from "../lib/site-content";
import { features } from "../lib/features";

// The landing page. The section order is the argument, not a feature list (§5): why →
// preflight → engine → the pipe (the mechanism) → tray → window → nothing resident →
// non-goals → the download. DD44/DD45 reshape the opening once the hero session and the
// two-actor/laws sections land.
//
// The page ends on the download (DD153), and DD156 is why nothing follows it: the four
// commands from a clone used to be the last section, because for the whole life of this site
// they were the only way to run the thing. A release exists now, so a page that closed by
// asking for the .NET SDK was answering a question the section above it had already settled
// — and the reader it sent to a git clone was the one who had just decided to install. From
// source stays documented where a contributor looks for it, in CONTRIBUTING.md.
export function Landing() {
  return (
    <>
      <Nav />
      <SiteHero
        icon={{ src: "/freewilly/logo.svg", alt: "FreeWilly logo" }}
        badge={hero.badge}
        title={hero.titleLead}
        titleAccent={hero.titleAccent}
        sub={<Rich runs={hero.sub} />}
        // DD158: the hero's button scrolls to the download section rather than leaving for
        // GitHub. The question between a reader and an install is "what does this touch on
        // my machine", the section below answers it in four lines, and sending them straight
        // to a release page skipped the answer. The button inside that section is the one
        // that leaves.
        actions={
          <>
            <SiteButton href="#download">{download.cta}</SiteButton>
            <SiteButton variant="ghost" href={repoUrl}>
              ★ View on GitHub
            </SiteButton>
          </>
        }
      >
        {/* The session stands between the actions and the meta line, where the shared hero
            has no slot, so the tail of the hero is drawn here with the layer's own classes. */}
        <div className="vg-site-wrap">
          <div className="session-eyebrow">{heroSession.eyebrow}</div>
          <HeroSession />
          <div className="vg-site-hero-meta">
            {hero.meta.map((item) => (
              <span key={item}>{item}</span>
            ))}
          </div>
          <div className="vg-site-pills">
            {hero.pills.map((runs, i) => (
              <span className="vg-site-pill" key={i}>
                <Rich runs={runs} />
              </span>
            ))}
          </div>
        </div>
        <Waves />
      </SiteHero>
      <Operator />
      <Why />
      <Preflight />
      <Engine />
      <Pipe />
      <Tray />
      <WindowSection />
      {/* The home for the five depth pages (§DD48) — one card per record, so the list and the
          pages cannot disagree about what exists. */}
      <SiteFeatureIndex
        id="features"
        eyebrow="In depth"
        title="Five pages, one per pillar"
        description="Each section above has a page to link at, whether from a README, an issue, or a search result."
        goLabel="Read the page →"
        items={features.map((f) => ({
          href: `/freewilly/features/${f.slug}/`,
          heading: f.heading,
          description: f.description,
        }))}
      />
      <NotResident />
      <NonGoals />
      <Download />
      <Footer />
    </>
  );
}
