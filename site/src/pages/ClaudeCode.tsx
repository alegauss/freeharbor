import { claudeCode as cc, friction } from "../lib/site-content";
import { ceiling, measured, shippedCount, surface, thousands } from "../lib/surface";
import { SiteCopyButton, SiteRich as Rich, SiteSectionHead } from "@viglet/viglet-design-system/site";
import { Nav, Footer } from "../components/Chrome";

type Verb = { k: string; v: string; d: string };

function VerbList({ heading, verbs }: { heading: string; verbs: Verb[] }) {
  return (
    <div className="verbs-col reveal">
      <h3 className="verbs-head">{heading}</h3>
      <div className="verbs">
        {verbs.map((verb) => {
          // No shipped/designed mark (DD90): every row here is a verb the registry
          // dispatches, so the mark had one value and said it fifteen times. The claim it
          // used to carry is now carried by the list itself, and the assertion that keeps
          // that true is in surface.test.mjs rather than in a badge a reader has to read.
          const cap = ceiling(verb.k);
          return (
            <div className="verb verb-shipped" key={verb.k}>
              {/* a <p>, so the twin keeps the verb and its ceiling on one line */}
              <p className="verb-head">
                <code className="verb-name">{verb.v}</code>{" "}
                {cap !== null && <span className="verb-cap">≤ {cap} tok</span>}
              </p>
              <span className="verb-desc">{verb.d}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/**
 * One friction: what the command costs today, and the verb that replaces it.
 *
 * Both figures are generated. `shape` keys into the measured baseline and throws on a shape
 * the benchmark does not measure; `verb` keys into the budget file's ceilings and the
 * registry's own list. So a row cannot outlive the thing it is about (S1, S2).
 */
function Friction({ item }: { item: (typeof friction.items)[number] }) {
  const cost = "shape" in item.today && item.today.shape ? measured(item.today.shape) : null;
  const cap = "verb" in item.here && item.here.verb ? ceiling(item.here.verb) : null;

  return (
    <article className="fr reveal">
      <h3 className="fr-t">{item.t}</h3>
      {/* The label, the command and the cost are <p>s carrying inline children rather than
          divs, because the twin generator renders a paragraph as one line and recurses a div
          into one line per child — which shattered "1,906 tok measured" into two (S5). */}
      <div className="fr-pair">
        <div className="fr-side fr-today">
          <p className="fr-label">{friction.todayLabel}</p>
          <p className="fr-cmd">
            <code>{item.today.cmd}</code>
          </p>
          {cost !== null && (
            <p className="fr-cost">
              <b>{thousands(cost)}</b> tok measured
            </p>
          )}
          <p className="fr-body">
            <Rich runs={item.today.body} />
          </p>
        </div>
        {/* decorative: the twin reads the two labelled sides, so the glyph carries nothing */}
        <div className="fr-arrow" aria-hidden="true" data-twin="omit">
          →
        </div>
        <div className="fr-side fr-here">
          {/* No task id and no shipped mark (DD90): the id addressed this repository's
              backlog and the mark restated what the verb's presence already says. The
              ceiling below is the claim that matters, and a build fails on it. */}
          <p className="fr-label">{friction.hereLabel}</p>
          <p className="fr-cmd">
            <code>{item.here.cmd}</code>
          </p>
          {cap !== null && (
            <p className="fr-cost">
              <b>≤ {cap}</b> tok ceiling
            </p>
          )}
          <p className="fr-body">
            <Rich runs={item.here.body} />
          </p>
        </div>
      </div>
    </article>
  );
}

export function ClaudeCode() {
  const { baseline, target } = surface;
  return (
    <>
      <Nav />
      <header className="page-hero" id="top">
        <div className="vg-site-wrap">
          <div className="vg-site-eyebrow">{cc.eyebrow}</div>
          <h1>{cc.heading}</h1>
          <p className="sub">
            <Rich runs={cc.intro} />
          </p>
          <p className="page-status">
            <b>{shippedCount()}</b> {cc.statusLead} <Rich runs={cc.status} />
          </p>
        </div>
      </header>

      <section id="friction">
        <div className="vg-site-wrap">
          <div className="reveal">
            <SiteSectionHead eyebrow={friction.eyebrow} title={friction.heading}>
              <Rich runs={friction.intro} />
            </SiteSectionHead>
          </div>

          {/* Every figure here is read from agent-budget.json at build time (S2). A <p> for
              the same reason as the rows below: the twin keeps it as one legible line. */}
          <p className="fr-baseline reveal">
            <span className="fr-baseline-lead">{friction.baselineLead}</span>{" "}
            <span className="fr-baseline-task">{baseline.task}</span>{" "}
            <span className="fr-baseline-fig">
              <b>{thousands(baseline.tokens)}</b> est. tokens
            </span>{" "}
            <span className="fr-baseline-fig">
              <b>{baseline.calls}</b> calls
            </span>{" "}
            <span className="fr-baseline-fig target">
              target <b>≤ {thousands(target.tokens)}</b> over <b>{target.calls}</b>
            </span>
          </p>
          <p className="fr-baseline-note reveal">
            <Rich runs={friction.baselineNote} />
          </p>

          <div className="frictions">
            {friction.items.map((item) => (
              <Friction item={item} key={item.t} />
            ))}
          </div>

          <p className="fr-footer reveal">
            <Rich runs={friction.footer} />
          </p>
        </div>
      </section>

      <section>
        <div className="vg-site-wrap narrow">
          <div className="allowlist-head reveal">
            <SiteSectionHead title={cc.allowlistHeading} />
          </div>
          <p className="allowlist-lead reveal">{cc.allowlistLead}</p>
          <div className="codeblock copy reveal" style={{ maxWidth: "520px", margin: "0 auto" }}>
            <code>{cc.allowlistLine}</code>
            <SiteCopyButton
              text={cc.allowlistLine}
              label="Copy the allowlist line"
              copyLabel="Copy"
              copiedLabel="Copied!"
            />
          </div>
          <p className="allowlist-note reveal">
            <Rich runs={cc.allowlistNote} />
          </p>
        </div>
      </section>

      <section>
        <div className="vg-site-wrap">
          <div className="verbs-split">
            <VerbList heading={cc.readHeading} verbs={cc.read} />
            <VerbList heading={cc.doHeading} verbs={cc.do} />
          </div>
        </div>
      </section>

      <section>
        <div className="vg-site-wrap narrow">
          <div className="reveal">
            <SiteSectionHead eyebrow="Discovery" title={cc.pluginHeading}>
              <Rich runs={cc.pluginBody} />
            </SiteSectionHead>
          </div>
        </div>
      </section>

      <section>
        <div className="vg-site-wrap">
          <div className="reveal">
            <SiteSectionHead eyebrow="Scope" title={cc.refusesHeading}>
              <Rich runs={cc.refusesLead} />
            </SiteSectionHead>
          </div>
          <div className="refuses reveal">
            {cc.refuses.map((r) => (
              <div className="refuse" key={r.t}>
                <h4>
                  <em>✗</em> {r.t}
                </h4>
                <p>{r.b}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <Footer />
    </>
  );
}
