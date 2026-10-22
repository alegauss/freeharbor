import { pipe } from "../../lib/site-content";
import { pipeDiagram } from "../../lib/diagrams";
import { SiteRich as Rich, SiteSectionHead } from "@viglet/viglet-design-system/site";
import { RawSvg } from "../ui/RawSvg";

export function Pipe() {
  return (
    <section id="pipe">
      <div className="vg-site-wrap">
        <div className="reveal">
          <SiteSectionHead eyebrow={pipe.eyebrow} title={<Rich runs={pipe.headingRuns} />}>
            <Rich runs={pipe.intro} />
          </SiteSectionHead>
        </div>
        <RawSvg className="shot-frame reveal" markup={pipeDiagram} />
        <div className="grid" style={{ marginTop: "34px" }}>
          {pipe.cards.map((card) => (
            <div className="card reveal" key={card.title}>
              <div className="ico">{card.icon}</div>
              <h3>{card.title}</h3>
              <p>
                <Rich runs={card.body} />
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
