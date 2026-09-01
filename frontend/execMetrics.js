/* Pure derivations for the CFO / manager dashboard. No React, no recharts — testable in plain node. */

export const CLOSED = ["paid", "rejected"];
export const isOpen = (c) => !CLOSED.includes(c.status);

export const days = (from, to) => Math.round((new Date(to) - new Date(from)) / 86400000);
export const routeKey = (s) => (s > 60 ? "human" : s >= 25 ? "manual" : "auto");

/** Date a claim was settled = last timeline entry. ponytail: no settledOn field in the data. */
export const settledOn = (c) => (c.timeline && c.timeline.length ? c.timeline[c.timeline.length - 1].d : c.due);

const sum = (xs, f) => xs.reduce((s, x) => s + f(x), 0);
const group = (xs, key, val) =>
  Object.entries(xs.reduce((m, x) => ({ ...m, [key(x)]: (m[key(x)] || 0) + val(x) }), {}));

export const AGE_BUCKETS = [
  { key: "0-7", max: 7 },
  { key: "8-14", max: 14 },
  { key: "15-30", max: 30 },
  { key: "30+", max: Infinity },
];

export function deriveMetrics(claims, opts = {}) {
  const { today = "2026-08-16", premiumEarned = 531000, priorReserves = null, slaDays = 14 } = opts;
  const month = today.slice(0, 7);

  const open = claims.filter(isOpen);
  const reserves = sum(open, (c) => c.reserve);
  const settledThisMonth = claims.filter((c) => c.status === "paid" && settledOn(c).slice(0, 7) === month);
  const paidMTD = sum(settledThisMonth, (c) => c.paid);

  const cycles = claims.filter((c) => !isOpen(c)).map((c) => days(c.opened, settledOn(c)));
  const avgCycle = cycles.length ? sum(cycles, (x) => x) / cycles.length : 0;

  const byExposure = group(open, (c) => c.type, (c) => c.reserve)
    .map(([type, value]) => ({ type, value }))
    .sort((a, b) => b.value - a.value);

  const aging = AGE_BUCKETS.map(({ key, max }, i) => {
    const min = i === 0 ? -Infinity : AGE_BUCKETS[i - 1].max;
    const rows = open.filter((c) => { const d = days(c.opened, today); return d > min && d <= max; });
    return { bucket: key, count: rows.length, value: sum(rows, (c) => c.reserve) };
  });

  const fraudExposure = ["auto", "manual", "human"].map((route) => {
    const rows = open.filter((c) => routeKey(c.fraud) === route);
    return { route, count: rows.length, value: sum(rows, (c) => c.reserve) };
  });

  const largest = [...open].sort((a, b) => b.reserve - a.reserve).slice(0, 5);
  const approvals = open.filter((c) => c.status === "pendingApproval");
  const sla = open.filter((c) => days(c.opened, today) >= slaDays);
  const unassigned = open.filter((c) => !c.handler);

  return {
    reserves,
    reserveDelta: priorReserves == null ? null : reserves - priorReserves,
    paidMTD,
    // ponytail: incurred/earned on the current month only — good enough for a demo, swap for actuarial ratio when real premium data lands.
    lossRatio: premiumEarned ? (paidMTD + reserves) / premiumEarned : 0,
    openCount: open.length,
    avgSeverity: open.length ? reserves / open.length : 0,
    avgCycle,
    byExposure,
    aging,
    fraudExposure,
    largest,
    approvals,
    approvalsValue: sum(approvals, (c) => c.reserve),
    sla,
    unassigned,
    /** share of reserves sitting in the 5 biggest claims — concentration risk */
    top5Share: reserves ? sum(largest, (c) => c.reserve) / reserves : 0,
    handlers: (opts.handlers || []).map((h) => {
      const mine = open.filter((c) => c.handler === h.id);
      return {
        ...h,
        count: mine.length,
        value: sum(mine, (c) => c.reserve),
        oldest: mine.length ? Math.max(...mine.map((c) => days(c.opened, today))) : 0,
      };
    }).sort((a, b) => b.value - a.value),
  };
}
