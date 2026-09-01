import React from "react";
import {
  ComposedChart, BarChart, Bar, Line, XAxis, YAxis, Tooltip,
  CartesianGrid, ResponsiveContainer, Cell,
} from "recharts";
import { deriveMetrics, routeKey, days } from "./execMetrics.js";

/* Labels this dashboard adds on top of the app's own T map. ponytail: kept local so App.jsx's T needs no edits. */
const L = {
  en: {
    reserves: "Outstanding reserves", paidMTD: "Paid this month", lossRatio: "Loss ratio",
    severity: "Avg. open severity", cycle: "Avg. cycle time", days: "days",
    vsLast: "vs last month", ofPortfolio: "of reserves in top 5",
    paidVsReserve: "Paid vs. reserves (€k)", exposure: "Reserve exposure by type (€)",
    aging: "Open claims by age (€)", fraudExposure: "Reserve at risk by routing (€)",
    largest: "Largest open claims", approvals: "Awaiting your sign-off",
    workload: "Team workload (open claims / € held)", noApprovals: "Nothing waiting for sign-off.",
    approve: "Approve", reject: "Reject", oldest: "oldest", claimsN: "claims",
    sla: "Open 14+ days", unassigned: "Unassigned",
    ageBuckets: { "0-7": "0–7 d", "8-14": "8–14 d", "15-30": "15–30 d", "30+": "30+ d" },
  },
  nl: {
    reserves: "Uitstaande reserves", paidMTD: "Uitgekeerd deze maand", lossRatio: "Schaderatio",
    severity: "Gem. open schadelast", cycle: "Gem. doorlooptijd", days: "dagen",
    vsLast: "t.o.v. vorige maand", ofPortfolio: "van reserves in top 5",
    paidVsReserve: "Uitkeringen vs. reserves (€k)", exposure: "Reserveblootstelling per type (€)",
    aging: "Open schades naar ouderdom (€)", fraudExposure: "Reserve met risico per routering (€)",
    largest: "Grootste open schades", approvals: "Wacht op jouw akkoord",
    workload: "Teambelasting (open schades / € onderhanden)", noApprovals: "Niets wacht op akkoord.",
    approve: "Akkoord", reject: "Afwijzen", oldest: "oudste", claimsN: "schades",
    sla: "14+ dagen open", unassigned: "Niet toegewezen",
    ageBuckets: { "0-7": "0–7 d", "8-14": "8–14 d", "15-30": "15–30 d", "30+": "30+ d" },
  },
};

const INK = "#16232e", ACCENT = "#d95d0f", LINE = "#e3e1da";
const ROUTE_COLOR = { auto: "#2e7d5b", manual: "#a05e00", human: "#c23b2e" };
const TYPE_COLORS = ["#16232e", "#d95d0f", "#2b5d8a", "#2e7d5b", "#8a8478"];

export default function ExecDashboard({
  claims,
  role = "cfo",
  lang = "en",
  t = (k) => k,
  onOpen = () => {},
  onApprove = () => {},
  handlers = [],
  today = "2026-08-16",
  premiumEarned = 531000,
  priorReserves = null,
  paidByMonth = [],
  reserveTrend = [],
}) {
  const l = L[lang] || L.en;
  const eur = (n) => "€ " + Math.round(n).toLocaleString(lang === "nl" ? "nl-NL" : "en-GB");
  const eurK = (n) => "€" + Math.round(n / 1000) + "k";
  const pct = (x) => (x * 100).toFixed(1) + "%";

  const m = deriveMetrics(claims, { today, premiumEarned, priorReserves, handlers });

  // Paid (€k) and reserve development share an x-axis; reserveTrend may be shorter than paidByMonth.
  const trend = paidByMonth.map((p) => {
    const r = reserveTrend.find((x) => x.m === p.m);
    return { m: p.m, paid: p.paid, reserves: r ? r.r : null };
  });

  const exposure = m.byExposure.map((e) => ({ ...e, name: t("t_" + e.type) }));
  const aging = m.aging.map((a) => ({ ...a, name: l.ageBuckets[a.bucket] }));

  const Tile = ({ label, value, sub, tone }) => (
    <div className="card stat">
      <div className="stat-label">{label}</div>
      <div className="stat-value">{value}</div>
      {sub && <div className={"stat-sub" + (tone ? " x-" + tone : "")}>{sub}</div>}
    </div>
  );

  const money = (v) => eur(v);

  return (
    <>
      <div className="stats">
        <Tile
          label={l.reserves}
          value={eur(m.reserves)}
          sub={m.reserveDelta == null ? `${m.openCount} ${l.claimsN}`
            : `${m.reserveDelta >= 0 ? "▲" : "▼"} ${eur(Math.abs(m.reserveDelta))} ${l.vsLast}`}
          tone={m.reserveDelta == null ? null : m.reserveDelta > 0 ? "bad" : "ok"}
        />
        <Tile label={l.paidMTD} value={eur(m.paidMTD)} />
        <Tile label={l.lossRatio} value={pct(m.lossRatio)} sub={`${pct(m.top5Share)} ${l.ofPortfolio}`} />
        <Tile label={l.severity} value={eur(m.avgSeverity)} />
        <Tile label={l.cycle} value={m.avgCycle.toFixed(1)} sub={l.days} />
      </div>

      <div className="grid-3c">
        <div className="card chart x-wide">
          <div className="card-title">{l.paidVsReserve}</div>
          <ResponsiveContainer width="100%" height={220}>
            <ComposedChart data={trend} margin={{ top: 8, right: 8, left: -18, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke={LINE} vertical={false} />
              <XAxis dataKey="m" tickLine={false} axisLine={false} fontSize={11} />
              <YAxis tickLine={false} axisLine={false} fontSize={11} />
              <Tooltip cursor={{ fill: "#16232e0d" }} />
              <Bar dataKey="paid" name={l.paidMTD} fill={INK} radius={[3, 3, 0, 0]} />
              <Line type="monotone" dataKey="reserves" name={l.reserves} stroke={ACCENT}
                strokeWidth={2.5} dot={{ r: 3 }} connectNulls />
            </ComposedChart>
          </ResponsiveContainer>
        </div>

        <div className="card chart">
          <div className="card-title">{l.exposure}</div>
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={exposure} layout="vertical" margin={{ top: 4, right: 12, left: 8, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke={LINE} horizontal={false} />
              <XAxis type="number" tickFormatter={eurK} tickLine={false} axisLine={false} fontSize={11} />
              <YAxis type="category" dataKey="name" width={80} tickLine={false} axisLine={false} fontSize={11} />
              <Tooltip formatter={money} cursor={{ fill: "#16232e0d" }} />
              <Bar dataKey="value" name={l.reserves} radius={[0, 3, 3, 0]}>
                {exposure.map((e, i) => <Cell key={e.type} fill={TYPE_COLORS[i % TYPE_COLORS.length]} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="grid-3c">
        <div className="card chart">
          <div className="card-title">{l.aging}</div>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={aging} margin={{ top: 8, right: 8, left: -8, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke={LINE} vertical={false} />
              <XAxis dataKey="name" tickLine={false} axisLine={false} fontSize={11} />
              <YAxis tickFormatter={eurK} tickLine={false} axisLine={false} fontSize={11} />
              <Tooltip formatter={money} cursor={{ fill: "#16232e0d" }} />
              <Bar dataKey="value" name={l.reserves} radius={[3, 3, 0, 0]}>
                {aging.map((a, i) => <Cell key={a.bucket} fill={i >= 2 ? "#c23b2e" : INK} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
          <div className="legend">
            {aging.map((a) => <span key={a.bucket} className="leg">{a.name} · {a.count}</span>)}
          </div>
        </div>

        <div className="card chart">
          <div className="card-title">{l.fraudExposure}</div>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={m.fraudExposure.map((f) => ({ ...f, name: t("route_" + f.route) }))}
              margin={{ top: 8, right: 8, left: -8, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke={LINE} vertical={false} />
              <XAxis dataKey="name" tickLine={false} axisLine={false} fontSize={11} />
              <YAxis tickFormatter={eurK} tickLine={false} axisLine={false} fontSize={11} />
              <Tooltip formatter={money} cursor={{ fill: "#16232e0d" }} />
              <Bar dataKey="value" name={l.reserves} radius={[3, 3, 0, 0]}>
                {m.fraudExposure.map((f) => <Cell key={f.route} fill={ROUTE_COLOR[f.route]} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
          <div className="legend">
            {m.fraudExposure.map((f) => (
              <span key={f.route} className="leg">
                <span className="leg-dot" style={{ background: ROUTE_COLOR[f.route] }} />
                {t("route_" + f.route)} · {f.count}
              </span>
            ))}
          </div>
        </div>

        <div className="card">
          <div className="card-title">{l.workload}</div>
          {m.handlers.map((h) => (
            <div key={h.id} className="wl">
              <span className="wl-n">{h.name}</span>
              <span className="wl-bar">
                <span className="wl-fill" style={{ width: (m.reserves ? (h.value / m.reserves) * 100 : 0) + "%" }} />
              </span>
              <span className="wl-c mono">{h.count}</span>
              <span className="x-amt mono">{eurK(h.value)}</span>
              <span className="cell-sub">{h.oldest} d</span>
            </div>
          ))}
          <div className="x-foot hint">
            {l.sla}: <b>{m.sla.length}</b> · {l.unassigned}: <b>{m.unassigned.length}</b>
          </div>
        </div>
      </div>

      {role === "manager" && (
        <div className="card">
          <div className="card-title">{l.approvals} · {eur(m.approvalsValue)}</div>
          {m.approvals.length === 0 && <div className="empty">{l.noApprovals}</div>}
          {m.approvals.map((c) => (
            <div key={c.id} className="appr">
              <div className="appr-info" onClick={() => onOpen(c.id)} role="button" tabIndex={0}
                onKeyDown={(e) => e.key === "Enter" && onOpen(c.id)}>
                <div>
                  <div className="cell-name">{c.claimant} · {t("t_" + c.type)}</div>
                  <div className="cell-sub">
                    {c.id} · {days(c.opened, today)} d · <span style={{ color: ROUTE_COLOR[routeKey(c.fraud)] }}>
                      {t("route_" + routeKey(c.fraud))}</span>
                  </div>
                </div>
              </div>
              <div className="appr-right">
                <span className="appr-amt mono">{eur(c.reserve)}</span>
                <button className="btn primary" onClick={() => onApprove(c.id, true)}>{l.approve}</button>
                <button className="btn danger" onClick={() => onApprove(c.id, false)}>{l.reject}</button>
              </div>
            </div>
          ))}
        </div>
      )}

      <div className="card table-wrap">
        <div className="card-title">{l.largest}</div>
        <table className="tbl">
          <thead><tr>
            <th>{t("claim")}</th><th>{t("claimant")}</th><th>{t("type")}</th>
            <th>{t("statusL")}</th><th>{t("fraudCol")}</th>
            <th className="num">{t("reserveL")}</th><th className="num">{l.days}</th>
          </tr></thead>
          <tbody>
            {m.largest.map((c) => (
              <tr key={c.id} onClick={() => onOpen(c.id)} tabIndex={0}
                onKeyDown={(e) => e.key === "Enter" && onOpen(c.id)}>
                <td className="mono">{c.id}</td>
                <td><div className="cell-name">{c.claimant}</div><div className="cell-sub">{c.vehicle}</div></td>
                <td>{t("t_" + c.type)}</td>
                <td>{t("s_" + c.status)}</td>
                <td style={{ color: ROUTE_COLOR[routeKey(c.fraud)] }}>{c.fraud} · {t("route_" + routeKey(c.fraud))}</td>
                <td className="num mono">{eur(c.reserve)}</td>
                <td className={"num mono" + (days(c.opened, today) >= 14 ? " late" : "")}>{days(c.opened, today)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

/* Append to the app's CSS string — everything else reuses existing classes. */
export const EXEC_CSS = `
.x-ok{color:#2e7d5b} .x-bad{color:#c23b2e}
.x-wide{grid-column:span 2}
.x-amt{width:52px; text-align:right; font-size:12px; color:var(--ink2)}
.x-foot{margin-top:12px; border-top:1px solid var(--line); padding-top:10px}
@media (max-width:940px){ .x-wide{grid-column:span 1} }
`;
