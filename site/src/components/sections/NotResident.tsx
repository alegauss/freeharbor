import { notResident } from "../../lib/site-content";
import { SiteRich as Rich } from "@viglet/viglet-design-system/site";

export function NotResident() {
  return (
    <section style={{ paddingTop: "20px" }}>
      <div className="vg-site-wrap reveal">
        <div className="banner">
          <div className="lock">{notResident.icon}</div>
          <h2>{notResident.heading}</h2>
          {notResident.body.map((runs, i) => (
            <p key={i}>
              <Rich runs={runs} />
            </p>
          ))}
        </div>
      </div>
    </section>
  );
}
