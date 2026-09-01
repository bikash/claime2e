import assert from "node:assert/strict";
import { deriveMetrics, days, routeKey, settledOn } from "./execMetrics.js";

const c = (o) => ({ paid: 0, fraud: 10, handler: "a", timeline: [], ...o });

const claims = [
  c({ id: "1", status: "assessment", type: "collision", reserve: 5000, opened: "2026-08-12", fraud: 10 }),          // 4d open
  c({ id: "2", status: "pendingApproval", type: "theft", reserve: 28500, opened: "2026-07-28", fraud: 61 }),        // 19d open
  c({ id: "3", status: "awaitingDocs", type: "collision", reserve: 9200, opened: "2026-08-01", fraud: 30, handler: null }), // 15d
  c({ id: "4", status: "paid", type: "glass", reserve: 2100, paid: 2100, opened: "2026-07-06",
      timeline: [{ d: "2026-08-01" }] }),
  c({ id: "5", status: "rejected", type: "storm", reserve: 900, opened: "2026-06-01", timeline: [{ d: "2026-06-20" }] }),
];

const m = deriveMetrics(claims, {
  today: "2026-08-16", premiumEarned: 100000, priorReserves: 40000,
  handlers: [{ id: "a", name: "A" }, { id: "b", name: "B" }],
});

// helpers
assert.equal(days("2026-08-01", "2026-08-16"), 15);
assert.equal(routeKey(24), "auto");
assert.equal(routeKey(25), "manual");
assert.equal(routeKey(61), "human");
assert.equal(settledOn(claims[3]), "2026-08-01");

// closed claims excluded from exposure
assert.equal(m.openCount, 3);
assert.equal(m.reserves, 5000 + 28500 + 9200);
assert.equal(m.reserveDelta, 2700);

// paid MTD counts only claims settled in today's month
assert.equal(m.paidMTD, 2100);
assert.equal(m.lossRatio.toFixed(4), ((2100 + 42700) / 100000).toFixed(4));

// cycle time uses settlement date, not due date
assert.equal(m.avgCycle, (26 + 19) / 2);

// aging buckets partition the open book exactly once
assert.deepEqual(m.aging.map((b) => b.count), [1, 0, 2, 0]);
assert.equal(m.aging.reduce((s, b) => s + b.value, 0), m.reserves);

// fraud routing splits by €, not count
assert.deepEqual(m.fraudExposure, [
  { route: "auto", count: 1, value: 5000 },
  { route: "manual", count: 1, value: 9200 },
  { route: "human", count: 1, value: 28500 },
]);

// concentration + actionables
assert.equal(m.top5Share, 1);
assert.equal(m.approvalsValue, 28500);
assert.deepEqual(m.sla.map((x) => x.id), ["2", "3"]);
assert.deepEqual(m.unassigned.map((x) => x.id), ["3"]);
assert.deepEqual(m.byExposure[0], { type: "theft", value: 28500 });

// workload: unassigned claims belong to nobody
assert.deepEqual(m.handlers.map((h) => [h.id, h.count, h.value, h.oldest]), [["a", 2, 33500, 19], ["b", 0, 0, 0]]);

console.log("execMetrics: all checks passed");
