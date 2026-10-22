import { tray } from "../../lib/site-content";
import { stateIcons, trayMenuDiagram } from "../../lib/diagrams";
import { SiteRich as Rich, SiteSectionHead } from "@viglet/viglet-design-system/site";
import { RawSvg } from "../ui/RawSvg";

export function Tray() {
  return (
    <section id="tray">
      <div className="vg-site-wrap">
        <div className="reveal">
          <SiteSectionHead eyebrow={tray.eyebrow} title={tray.heading}>
            <Rich runs={tray.intro} />
          </SiteSectionHead>
        </div>
        <div className="states reveal">
          {tray.states.map((state) => (
            <div className={`state ${state.kind}`} key={state.kind}>
              <RawSvg markup={stateIcons[state.kind]} />
              <h3>{state.title}</h3>
              <p>
                <Rich runs={state.body} />
              </p>
            </div>
          ))}
        </div>
        <div className="split reveal" style={{ marginTop: "52px" }}>
          <div className="split-txt">
            <div className="vg-site-eyebrow">{tray.splitEyebrow}</div>
            <h2>{tray.splitHeading}</h2>
            {/* One entry per item the menu shows, plus the one it hides — two arrays so the
                count the heading states is a number a test can hold the list to (DD160). */}
            <ul className="feat-list">
              {[...tray.splitList, tray.splitHidden].map((runs, i) => (
                <li key={i}>
                  <span className="chk">✓</span>
                  <span>
                    <Rich runs={runs} />
                  </span>
                </li>
              ))}
            </ul>
          </div>
          <RawSvg className="shot-frame" markup={trayMenuDiagram} />
        </div>
      </div>
    </section>
  );
}
