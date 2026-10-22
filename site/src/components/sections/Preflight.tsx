import { preflight } from "../../lib/site-content";
import { preflightTerminal } from "../../lib/diagrams";
import { SiteRich as Rich, SiteSectionHead } from "@viglet/viglet-design-system/site";

export function Preflight() {
  return (
    <section id="preflight">
      <div className="vg-site-wrap">
        <div className="reveal">
          <SiteSectionHead eyebrow={preflight.eyebrow} title={preflight.heading}>
            <Rich runs={preflight.intro} />
          </SiteSectionHead>
        </div>
        <div className="reveal">
          <div className="term">
            <div className="bar">
              <i />
              <i />
              <i />
              <span>{preflight.terminalTitle}</span>
            </div>
            <pre
              // eslint-disable-next-line react/no-danger
              dangerouslySetInnerHTML={{ __html: preflightTerminal }}
            />
          </div>
        </div>
        <ul className="feat-list two reveal" style={{ marginTop: "34px" }}>
          {/* The rows and the notes about them render as one list and are two arrays, so the
              row count the heading states is a number a test can hold the page to (DD159). */}
          {[...preflight.rows, ...preflight.notes].map(([lead, rest]) => (
            <li key={lead}>
              <span className="chk">✓</span>
              <span>
                <b>{lead}</b>
                {rest}
              </span>
            </li>
          ))}
        </ul>
        <p
          style={{
            textAlign: "center",
            color: "var(--vg-muted-foreground)",
            fontSize: ".9rem",
            marginTop: "30px",
          }}
        >
          <Rich runs={preflight.note} />
        </p>
      </div>
    </section>
  );
}
