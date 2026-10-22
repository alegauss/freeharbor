import { engine } from "../../lib/site-content";
import { SiteRich as Rich, SiteSectionHead } from "@viglet/viglet-design-system/site";

export function Engine() {
  return (
    <section id="engine">
      <div className="vg-site-wrap">
        <div className="reveal">
          <SiteSectionHead eyebrow={engine.eyebrow} title={engine.heading}>
            <Rich runs={engine.intro} />
          </SiteSectionHead>
        </div>
        <div className="steps reveal">
          {engine.steps.map((step) => (
            <div className="step" key={step.title}>
              <div className={step.ask ? "n ask" : "n"}>{step.n}</div>
              <h4>{step.title}</h4>
              <p>
                <Rich runs={step.body} />
              </p>
            </div>
          ))}
        </div>
        <div className="term reveal">
          <div className="bar">
            <i />
            <i />
            <i />
            <span>{engine.helpTitle}</span>
          </div>
          <pre>
            {engine.help}
            {"\n\n"}
            <span className="c">{engine.helpNote}</span>
          </pre>
        </div>
      </div>
    </section>
  );
}
