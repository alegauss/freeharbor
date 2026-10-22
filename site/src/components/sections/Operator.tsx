import { operator } from "../../lib/site-content";
import { SiteRich as Rich, SiteSectionHead } from "@viglet/viglet-design-system/site";

// §DD45 — the two actors, then the ten laws in the order an agent meets them.
export function Operator() {
  return (
    <section id="operator">
      <div className="vg-site-wrap">
        <div className="reveal">
          <SiteSectionHead eyebrow={operator.eyebrow} title={operator.heading}>
            <Rich runs={operator.intro} />
          </SiteSectionHead>
        </div>

        <div className="actors reveal">
          {operator.actors.map((actor) => (
            <div className={`actor actor-${actor.who.toLowerCase()}`} key={actor.who}>
              <div className="actor-head">
                <span className="actor-who">{actor.who}</span>
                <span className="actor-sub">{actor.sub}</span>
              </div>
              <div className="actor-iface">{actor.iface}</div>
              <div className="actor-job">{actor.job}</div>
            </div>
          ))}
        </div>
        <p className="actors-note reveal">
          <Rich runs={operator.actorsNote} />
        </p>

        <div className="reveal" style={{ marginTop: "72px" }}>
          <SiteSectionHead eyebrow={operator.lawsEyebrow} title={operator.lawsHeading}>
            <Rich runs={operator.lawsIntro} />
          </SiteSectionHead>
        </div>
        <div className="laws reveal">
          {operator.laws.map((law) => (
            <div className="law" key={law.id}>
              <span className="law-id">{law.id}</span>
              <div className="law-body">
                <h3>{law.title}</h3>
                <p>{law.body}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
