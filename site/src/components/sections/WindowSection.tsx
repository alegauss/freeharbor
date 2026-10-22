import { windowSection } from "../../lib/site-content";
import { emptyStateDiagram, windowDiagram } from "../../lib/diagrams";
import { SiteRich as Rich, SiteSectionHead } from "@viglet/viglet-design-system/site";
import { RawSvg } from "../ui/RawSvg";

export function WindowSection() {
  return (
    <section id="window">
      <div className="vg-site-wrap">
        <div className="reveal">
          <SiteSectionHead eyebrow={windowSection.eyebrow} title={windowSection.heading}>
            <Rich runs={windowSection.intro} />
          </SiteSectionHead>
        </div>
        <figure className="shot-frame reveal" style={{ margin: 0 }}>
          <RawSvg markup={windowDiagram} />
          <figcaption>
            <Rich runs={windowSection.caption} />
          </figcaption>
        </figure>
        <div className="split rev reveal" style={{ marginTop: "48px" }}>
          <RawSvg className="shot-frame" markup={emptyStateDiagram} />
          <div className="split-txt">
            <div className="vg-site-eyebrow">{windowSection.detailsEyebrow}</div>
            <h2>{windowSection.detailsHeading}</h2>
            <ul className="feat-list">
              {windowSection.detailsList.map((runs, i) => (
                <li key={i}>
                  <span className="chk">✓</span>
                  <span>
                    <Rich runs={runs} />
                  </span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </section>
  );
}
