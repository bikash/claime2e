// Boxora — single-file bilingual (EN/NL) motor-insurance claims workspace demo.
// Self-contained: react + recharts only. All data in memory; RDW/CIS/OCR/payments simulated.
import { useState, useRef } from "react";
import {
  BarChart, Bar, PieChart, Pie, Cell, LineChart, Line,
  XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid,
} from "recharts";

const TODAY = "2026-08-16";

const CSS = `
@import url('https://fonts.googleapis.com/css2?family=Archivo:wght@700;800&family=IBM+Plex+Sans:wght@400;500;600&family=IBM+Plex+Mono:wght@400;600&display=swap');
:root{--ink:#16232e;--paper:#f3f2ec;--line:#e3e1da;--accent:#d95d0f;--green:#2e7d5b;--red:#c23b2e;--amber:#b58a1f;--muted:#6b7680;}
*{box-sizing:border-box;margin:0;padding:0;}
.sd{font-family:'IBM Plex Sans',sans-serif;background:var(--paper);color:var(--ink);min-height:100vh;font-size:14px;}
.sd h1,.sd h2,.sd h3,.sd h4{font-family:'Archivo',sans-serif;}
.sd h3{font-size:15px;margin-bottom:10px;} .sd h4{font-size:13px;margin-bottom:6px;}
.mono{font-family:'IBM Plex Mono',monospace;}
.sd button{font-family:inherit;cursor:pointer;}
.sd input,.sd select,.sd textarea{font-family:inherit;font-size:13px;padding:8px 10px;border:1px solid var(--line);border-radius:8px;background:#fff;color:var(--ink);outline:none;width:100%;}
.sd input:focus,.sd select:focus,.sd textarea:focus{border-color:var(--accent);}
.sd textarea{resize:vertical;min-height:80px;}
.lbl{display:block;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:.05em;margin:10px 0 4px;}
.btn{display:inline-flex;align-items:center;gap:6px;border:1px solid var(--line);background:#fff;color:var(--ink);border-radius:8px;padding:8px 14px;font-weight:600;font-size:13px;}
.btn:hover{border-color:var(--ink);}
.btn-p{background:var(--accent);color:#fff;border-color:var(--accent);} .btn-p:hover{border-color:var(--accent);filter:brightness(1.06);}
.btn-g{background:var(--green);color:#fff;border-color:var(--green);}
.btn-d{background:var(--red);color:#fff;border-color:var(--red);}
.btn-sm{padding:4px 10px;font-size:12px;border-radius:7px;}
.btn:disabled{opacity:.5;cursor:default;}
.card{background:#fff;border:1px solid var(--line);border-radius:12px;padding:16px;margin-bottom:16px;}
.shell{display:flex;min-height:100vh;}
.side{width:216px;background:var(--ink);color:#fff;flex-shrink:0;display:flex;flex-direction:column;gap:4px;padding:16px 10px;position:sticky;top:0;height:100vh;}
.side .brandrow{display:flex;align-items:center;gap:10px;padding:4px 8px 16px;}
.side .brand{font-family:'Archivo';font-weight:800;font-size:16px;letter-spacing:.02em;}
.nav-i{display:flex;align-items:center;gap:10px;padding:9px 12px;border-radius:8px;color:#aeb9c2;font-weight:500;background:none;border:none;font-size:13.5px;text-align:left;width:100%;}
.nav-i:hover{color:#fff;background:rgba(255,255,255,.06);}
.nav-i.on{background:var(--accent);color:#fff;}
.side .userbox{margin-top:auto;padding:10px 12px;border-top:1px solid rgba(255,255,255,.12);font-size:12.5px;color:#c8d1d8;}
.main{flex:1;min-width:0;}
.topbar{position:sticky;top:0;z-index:20;background:rgba(243,242,236,.93);backdrop-filter:blur(6px);border-bottom:1px solid var(--line);padding:12px 24px;display:flex;align-items:center;gap:12px;}
.pagetitle{font-family:'Archivo';font-size:20px;font-weight:800;flex:1;}
.content{max-width:1240px;margin:0 auto;padding:20px 24px 70px;}
.langsw{display:flex;border:1px solid var(--line);border-radius:999px;overflow:hidden;background:#fff;flex-shrink:0;}
.langsw button{border:none;background:none;padding:5px 13px;font-weight:700;font-size:12px;color:var(--muted);}
.langsw .on{background:var(--ink);color:#fff;}
.tbl{width:100%;border-collapse:collapse;font-size:13px;}
.tbl th{text-align:left;font-size:11px;text-transform:uppercase;letter-spacing:.05em;color:var(--muted);padding:8px 10px;border-bottom:1px solid var(--line);white-space:nowrap;}
.tbl td{padding:10px;border-bottom:1px solid #edece5;vertical-align:middle;}
.tbl tr.click:hover{background:#faf9f4;cursor:pointer;}
.pill{display:inline-block;padding:3px 9px;border-radius:999px;font-size:11.5px;font-weight:600;border:1px solid;white-space:nowrap;}
.plate{display:inline-flex;align-items:stretch;background:#f5b301;border:1.5px solid #2b2b2b;border-radius:5px;overflow:hidden;font-family:'Archivo',sans-serif;font-weight:800;letter-spacing:.6px;color:#111;line-height:1;}
.plate .eu{background:#0a3a8f;color:#fff;font-size:7px;padding:2px 3px;display:flex;align-items:flex-end;font-weight:700;}
.plate .reg{padding:4px 8px;font-size:12.5px;display:flex;align-items:center;}
.plate.big .reg{font-size:20px;padding:6px 13px;}
.plate.big .eu{font-size:9px;padding:3px 5px;}
.meter{width:64px;height:6px;background:#e8e6df;border-radius:3px;overflow:hidden;display:inline-block;vertical-align:middle;}
.meter i{display:block;height:100%;}
.stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:14px;margin-bottom:16px;}
.stat{background:#fff;border:1px solid var(--line);border-radius:12px;padding:14px 16px;}
.stat .k{font-size:11.5px;color:var(--muted);font-weight:600;text-transform:uppercase;letter-spacing:.04em;}
.stat .v{font-family:'Archivo';font-size:26px;font-weight:800;margin-top:4px;}
.tabs{display:flex;gap:2px;border-bottom:2px solid var(--line);margin:14px 0;flex-wrap:wrap;}
.tabs button{border:none;background:none;padding:9px 14px;font-weight:600;font-size:13px;color:var(--muted);border-bottom:2px solid transparent;margin-bottom:-2px;}
.tabs .on{color:var(--accent);border-bottom-color:var(--accent);}
.toasts{position:fixed;bottom:84px;right:18px;z-index:99;display:flex;flex-direction:column;gap:8px;}
.toast{background:var(--ink);color:#fff;padding:11px 16px;border-radius:10px;font-size:13px;max-width:360px;box-shadow:0 6px 20px rgba(0,0,0,.25);}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:16px;align-items:start;}
.row{display:flex;gap:10px;align-items:center;flex-wrap:wrap;}
.spin{display:inline-block;width:13px;height:13px;border:2px solid #ccc;border-top-color:var(--accent);border-radius:50%;animation:sdsp .8s linear infinite;vertical-align:middle;flex-shrink:0;}
@keyframes sdsp{to{transform:rotate(360deg)}}
.login{min-height:100vh;display:flex;flex-direction:column;align-items:center;justify-content:center;padding:24px;}
.rolegrid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:14px;max-width:1020px;width:100%;margin-top:26px;}
.rolecard{background:#fff;border:1px solid var(--line);border-radius:12px;padding:18px;display:flex;flex-direction:column;gap:8px;}
.avatar{width:42px;height:42px;border-radius:50%;background:var(--ink);color:#fff;display:flex;align-items:center;justify-content:center;font-family:'Archivo';font-weight:800;font-size:15px;}
.chat{display:flex;flex-direction:column;gap:8px;max-height:340px;overflow-y:auto;margin-bottom:10px;}
.bub{padding:9px 12px;border-radius:10px;font-size:13px;max-width:85%;white-space:pre-wrap;}
.bub.q{background:var(--ink);color:#fff;align-self:flex-end;}
.bub.a{background:#f0efe8;align-self:flex-start;}
.tl{list-style:none;} .tl li{position:relative;padding:0 0 16px 20px;border-left:2px solid var(--line);margin-left:6px;}
.tl li:before{content:'';position:absolute;left:-6px;top:2px;width:10px;height:10px;border-radius:50%;background:var(--accent);border:2px solid #fff;}
.muted{color:var(--muted);} .small{font-size:12px;} .b{font-weight:700;}
.wlbar{height:8px;background:#e8e6df;border-radius:4px;overflow:hidden;flex:1;} .wlbar i{display:block;height:100%;background:var(--accent);}
.studio-grid{display:grid;grid-template-columns:1fr 340px;gap:16px;align-items:start;}
.bench{position:sticky;top:70px;}
.steprow{display:flex;align-items:center;gap:10px;padding:8px 10px;border:1px solid var(--line);border-radius:8px;margin-bottom:6px;background:#fff;}
.chip{display:inline-block;background:#f0efe8;border:1px solid var(--line);border-radius:999px;padding:2px 9px;font-size:11px;font-weight:600;margin:2px 3px 2px 0;}
.overdue{color:var(--red);font-weight:700;}
.aiN{font-size:11.5px;color:var(--muted);font-style:italic;margin-top:8px;}
.dash2{display:grid;grid-template-columns:2fr 1fr;gap:16px;align-items:start;}
.claimwrap{display:grid;grid-template-columns:300px minmax(0,1fr);gap:16px;align-items:start;}
.airail{position:sticky;top:70px;}
.aicard{background:#f0f6f2;border:1px solid #cfe3d8;border-radius:12px;padding:16px;margin-bottom:16px;}
.aicard.warn{background:#fbf6e9;border-color:#e6d7ad;} .aicard.stop{background:#fbeeec;border-color:#e8c7c2;}
.eyebrow{font-size:11px;font-weight:700;letter-spacing:.09em;text-transform:uppercase;color:var(--muted);}
.sect{font-size:12.5px;text-transform:uppercase;letter-spacing:.07em;}
.stat .sub{font-size:11.5px;color:var(--muted);margin-top:6px;line-height:1.35;}
.flbl{display:block;font-size:13px;font-weight:700;margin:16px 0 8px;}
.optrow{display:flex;gap:24px;flex-wrap:wrap;}
.optrow label{display:flex;align-items:center;gap:7px;font-size:13px;font-weight:500;}
.optrow input{width:16px;flex-shrink:0;}
.kv{display:flex;justify-content:space-between;align-items:baseline;gap:10px;padding:7px 0;border-bottom:1px solid #e6e4dc;font-size:12.5px;}
.kv:last-child{border-bottom:none;}
.rowitem{display:flex;align-items:center;gap:12px;padding:11px 0;border-bottom:1px solid #edece5;}
.rowitem:last-child{border-bottom:none;}
.num{display:inline-flex;align-items:center;justify-content:center;width:19px;height:19px;border-radius:5px;background:#e8e6df;font-size:11px;font-weight:700;flex-shrink:0;}
.polrow{display:flex;align-items:center;gap:12px;flex-wrap:wrap;border-top:1px solid var(--line);margin-top:14px;padding-top:14px;}
.fab{position:fixed;right:18px;bottom:18px;z-index:80;width:52px;height:52px;border-radius:50%;background:var(--accent);color:#fff;border:none;display:flex;align-items:center;justify-content:center;box-shadow:0 6px 18px rgba(0,0,0,.22);font-size:20px;}
.fab:hover{filter:brightness(1.07);}
.cbox{position:fixed;right:18px;bottom:82px;z-index:80;width:370px;max-width:calc(100vw - 36px);background:#fff;border:1px solid var(--line);border-radius:14px;box-shadow:0 14px 36px rgba(0,0,0,.18);padding:14px;}
.cbox .chat{max-height:280px;}
.lnk{background:none;border:none;padding:0;color:var(--accent);font-weight:600;text-decoration:underline;font-family:'IBM Plex Mono',monospace;font-size:13px;}
@media(max-width:940px){
.shell{flex-direction:column;}
.side{width:100%;height:auto;position:static;flex-direction:row;align-items:center;overflow-x:auto;padding:8px 10px;}
.side .brandrow{padding:4px 10px;} .side .userbox{margin:0 0 0 auto;border:none;white-space:nowrap;}
.nav-i{width:auto;white-space:nowrap;}
.grid2,.studio-grid,.dash2,.claimwrap{grid-template-columns:1fr;} .bench,.airail{position:static;}
}
`;

const STATUS_COLOR = {
  new: "#8a63d2", assessment: "#2f6fb2", awaitingDocs: "#b58a1f",
  pendingApproval: "#d95d0f", approved: "#2e7d5b", paid: "#1e6f50", rejected: "#c23b2e",
};
const TYPE_COLOR = { collision: "#2f6fb2", theft: "#c23b2e", glass: "#2e7d5b", vandalism: "#8a63d2", storm: "#b58a1f" };
const routeOf = f => (f < 25 ? "auto" : f <= 60 ? "manual" : "human");
// Routing thresholds — used by both the assessment save path and the AI rail.
const MGR_RESERVE = 10000, MAX_AUTO_FRAUD = 60;
const ROUTE_COLOR = { auto: "#2e7d5b", manual: "#b58a1f", human: "#c23b2e" };
const meterColor = f => (f < 25 ? "#2e7d5b" : f <= 60 ? "#b58a1f" : "#c23b2e");
const isOpen = c => c.status !== "paid" && c.status !== "rejected";
const eur = (n, lang) => "€ " + Math.round(n).toLocaleString(lang === "nl" ? "nl-NL" : "en-GB");
const daysOpen = c => Math.round((new Date(TODAY) - new Date(c.opened)) / 86400000);

// ── i18n ────────────────────────────────────────────────────────────
const T = {
  en: {
    tagline: "AI-powered motor claims workspace for the Dutch market",
    nav_dashboard: "Dashboard", nav_claims: "Claims", nav_policies: "Policies", nav_tasks: "Tasks",
    nav_studio: "Agent studio", nav_audit: "Audit log", nav_orgs: "Organizations", nav_users: "Users",
    nav_legal: "Legal & compliance", nav_finance: "Finance", nav_platform: "Platform",
    st_new: "New", st_assessment: "Assessment", st_awaitingDocs: "Awaiting docs",
    st_pendingApproval: "Pending approval", st_approved: "Approved", st_paid: "Paid", st_rejected: "Rejected",
    ty_collision: "Collision", ty_theft: "Theft", ty_glass: "Glass", ty_vandalism: "Vandalism", ty_storm: "Storm",
    rt_auto: "Auto-approve", rt_manual: "Manual review", rt_human: "Human required",
    search: "Search", save: "Save", send: "Send", back: "Back", status: "Status", type: "Type",
    coverage: "Coverage", reserve: "Reserve", handler: "Handler", due: "Due", claimant: "Claimant",
    vehicle: "Vehicle", city: "City", actions: "Actions", policy: "Policy", premium: "Premium",
    ownRisk: "Own risk", bm: "No-claim years", renewal: "Renewal", holder: "Policyholder",
    active: "Active", paused: "Paused", unassigned: "Unassigned", assignee: "Assignee",
    logout: "Log out", riskRouting: "Risk / routing", myOnly: "My claims only",
    newClaim: "＋ New claim (FNOL)", simulate: "⚡ Simulate incoming claim",
    signoff: "Reserves above €10,000 need your sign-off.", approve: "Approve", reject: "Reject",
    executePayment: "Execute payment", description: "Description", timeline: "Timeline",
    noPolicy: "⚠ No active policy found for this license plate.",
    logAudit: "Every assessment, approval, payment, email and AI action is recorded here — the EU AI Act record-keeping trail.",
    exportJson: "Export JSON", runPipeline: "Run pipeline", addTask: "Add task",
  },
  nl: {
    tagline: "AI-gedreven werkomgeving voor motorrijtuigschades op de Nederlandse markt",
    nav_dashboard: "Dashboard", nav_claims: "Schades", nav_policies: "Polissen", nav_tasks: "Taken",
    nav_studio: "Agent-studio", nav_audit: "Auditlog", nav_orgs: "Organisaties", nav_users: "Gebruikers",
    nav_legal: "Juridisch & compliance", nav_finance: "Financiën", nav_platform: "Platform",
    st_new: "Nieuw", st_assessment: "Beoordeling", st_awaitingDocs: "Wacht op documenten",
    st_pendingApproval: "Wacht op akkoord", st_approved: "Akkoord", st_paid: "Uitbetaald", st_rejected: "Afgewezen",
    ty_collision: "Aanrijding", ty_theft: "Diefstal", ty_glass: "Ruitschade", ty_vandalism: "Vandalisme", ty_storm: "Storm",
    rt_auto: "Automatisch akkoord", rt_manual: "Handmatige controle", rt_human: "Mens vereist",
    search: "Zoeken", save: "Opslaan", send: "Versturen", back: "Terug", status: "Status", type: "Type",
    coverage: "Dekking", reserve: "Reserve", handler: "Behandelaar", due: "Deadline", claimant: "Verzekerde",
    vehicle: "Voertuig", city: "Plaats", actions: "Acties", policy: "Polis", premium: "Premie",
    ownRisk: "Eigen risico", bm: "Schadevrije jaren", renewal: "Prolongatie", holder: "Verzekeringnemer",
    active: "Actief", paused: "Gepauzeerd", unassigned: "Niet toegewezen", assignee: "Toegewezen aan",
    logout: "Uitloggen", riskRouting: "Risico / routering", myOnly: "Alleen mijn schades",
    newClaim: "＋ Nieuwe schade (FNOL)", simulate: "⚡ Simuleer inkomende schade",
    signoff: "Reserves boven €10.000 vereisen uw akkoord.", approve: "Akkoord", reject: "Afwijzen",
    executePayment: "Betaling uitvoeren", description: "Omschrijving", timeline: "Tijdlijn",
    noPolicy: "⚠ Geen actieve polis gevonden voor dit kenteken.",
    logAudit: "Elke beoordeling, goedkeuring, betaling, e-mail en AI-actie wordt hier vastgelegd — het registratiespoor voor de EU AI Act.",
    exportJson: "Exporteer JSON", runPipeline: "Pipeline uitvoeren", addTask: "Taak toevoegen",
  },
};

// ── AI plumbing ─────────────────────────────────────────────────────
async function askClaude(system, userMsg) {
  const res = await fetch("https://api.anthropic.com/v1/messages", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      model: "claude-sonnet-4-6",
      max_tokens: 1000,
      system,
      messages: [{ role: "user", content: userMsg }],
    }),
  });
  const data = await res.json();
  return (data.content || []).filter(c => c.type === "text").map(c => c.text).join("\n");
}

async function askClaudeJSON(system, userMsg, fallback) {
  try {
    const raw = await askClaude(
      system + "\nRespond ONLY with valid JSON, no markdown fences, no preamble.",
      userMsg
    );
    const clean = raw.replace(/```json/gi, "").replace(/```/g, "").trim();
    return JSON.parse(clean);
  } catch {
    return fallback;
  }
}

// Mock RDW registry: deterministic vehicle from plate hash.
const RDW_DB = [
  "Volkswagen Polo 1.0 TSI (2022)", "Toyota Corolla Hybrid (2021)", "Kia Niro EV (2023)",
  "Volvo V60 T4 (2020)", "Skoda Fabia (2019)", "Ford Focus Wagon (2021)",
  "Hyundai Kona Electric (2022)", "Renault Captur (2020)", "Peugeot 308 SW (2023)", "Opel Astra (2021)",
];
const vehicleFromPlate = p => {
  let h = 0;
  for (const ch of (p || "X").toUpperCase()) h = (h * 31 + ch.charCodeAt(0)) % 997;
  return RDW_DB[h % RDW_DB.length];
};

// ── tiny presentational components (stateless, module-level: no focus issues) ──
// Brand mark: "Dossier B" — a B whose open bowl is an intake box with an
// orange claim card dropped in. Ink tile keeps it legible on any background.
function Mark({ size = 24 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 64 64" aria-hidden="true">
      <rect width="64" height="64" rx="14" fill="#16232e" />
      <path fill="#fff" fillRule="evenodd" d="M13 10H24V18H21V25H35Q41 25 41 21.5Q41 18 35 18H33V10H39Q49 10 49 20V24Q49 33 40 33Q51 33 51 43V46Q51 56 41 56H13ZM21 41H36Q43 41 43 44.5Q43 48 36 48H21Z" />
      <rect x="25.5" y="3" width="6" height="22" rx="1" fill="#d95d0f" />
    </svg>
  );
}

function Plate({ reg, big }) {
  return (
    <span className={"plate" + (big ? " big" : "")}>
      <span className="eu">NL</span>
      <span className="reg">{reg}</span>
    </span>
  );
}
function Pill({ label, color }) {
  return (
    <span className="pill" style={{ background: color + "1a", borderColor: color, color }}>
      {label}
    </span>
  );
}
function Meter({ v }) {
  return (
    <span className="row" style={{ gap: 6, display: "inline-flex", flexWrap: "nowrap" }}>
      <span className="meter"><i style={{ width: Math.min(100, v) + "%", background: meterColor(v) }} /></span>
      <span className="mono small b" style={{ color: meterColor(v) }}>{v}</span>
    </span>
  );
}
function Icon({ name }) {
  const P = {
    dash: "M3 3h8v8H3zM13 3h8v5h-8zM13 10h8v11h-8zM3 13h8v8H3z",
    claims: "M6 2h9l5 5v15H6zM14 2v6h6M9 13h8M9 17h8",
    policies: "M12 2l8 4v6c0 5-3.5 8.5-8 10-4.5-1.5-8-5-8-10V6z",
    tasks: "M4 6h2v2H4zM9 7h11M4 11h2v2H4zM9 12h11M4 16h2v2H4zM9 17h11",
    studio: "M12 2v4M5 8h14a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2v-8a2 2 0 012-2zM8 13h.01M16 13h.01M9 17h6",
    audit: "M4 4h16v16H4zM8 9h8M8 13h8M8 17h5",
    orgs: "M3 21h18M5 21V7l7-4 7 4v14M9 9h.01M9 13h.01M15 9h.01M15 13h.01M11 21v-4h2v4",
    users: "M17 21v-2a4 4 0 00-4-4H7a4 4 0 00-4 4v2M10 11a4 4 0 100-8 4 4 0 000 8zM21 21v-2a4 4 0 00-3-3.87M16 3.13A4 4 0 0116 11",
    legal: "M12 3v18M5 7l7-4 7 4M5 7l-3 7a4 4 0 006 0zM19 7l-3 7a4 4 0 006 0zM8 21h8",
  };
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
      strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      <path d={P[name] || P.dash} />
    </svg>
  );
}

// ── agent prompt templates ──────────────────────────────────────────
const TOOLS = ["Policy lookup", "RDW vehicle registry", "CIS fraud database", "Repair network Schadegarant", "Email & letters"];

const TMPL = {
  summariser: {
    name: "Claim summariser",
    prompt: "You are a claims summarisation assistant for a Dutch motor insurer. Given a claim file (JSON), produce a crisp, factual summary for a schadebehandelaar: what happened, who is involved, the vehicle and coverage, the current status, financial exposure (reserve, own risk), and anything unusual. Never speculate beyond the file; explicitly mark missing information as missing. Use professional insurance vocabulary (dekking, eigen risico, aangifte). Keep it under 150 words unless asked otherwise.",
  },
  intake: {
    name: "FNOL intake",
    prompt: "You are the FNOL (first notification of loss) intake agent for a Dutch motor insurer. Collect and confirm: kenteken (Dutch license plate), date and location of the incident, a short description, and whether anyone was injured. If there are injuries of any kind, stop and escalate to a human handler immediately. Verify the plate against the RDW vehicle registry and the policy register. Classify the claim as collision, theft, glass, vandalism or storm. Be warm, clear and efficient; ask one question at a time; never promise coverage.",
  },
  fraud: {
    name: "Fraud screener",
    prompt: "You are a fraud screening assistant for a Dutch motor insurer, working under the Verbond van Verzekeraars fraud protocol. Analyse the claim file for indicators such as: policy age vs. loss date, tracker or key anomalies, night-time single-vehicle incidents, CIS database signals, inconsistent statements, prior claims history, and unusually high reserves for the damage described. Return a risk score from 0 to 100 and list each indicator with a short justification. You flag risk for human investigation — you never accuse anyone and never assert that fraud occurred.",
  },
  comms: {
    name: "Customer comms",
    prompt: "You write customer communications for a Dutch motor insurer. Write at B1 reading level in the claimant's language: short sentences, no jargon, explain any necessary insurance term in plain words. Be empathetic and concrete about what happens next and what the claimant must do. Never promise coverage, payout amounts or timelines that are not confirmed in the claim file. Sign off as the Boxora claims team. Return a subject line and a body.",
  },
  reserve: {
    name: "Reserve advisor",
    prompt: "You are a reserve advisory assistant for a Dutch motor insurer. Using Dutch repair-cost benchmarks (Schadegarant network rates, ANWB/BOVAG price levels, typical parts and labour costs, total-loss thresholds by vehicle age and value), propose a low / expected / high reserve range in euros for the claim, with a short rationale referencing the damage description and vehicle. Your figures are advisory only — the schadebehandelaar decides. Be realistic, not conservative-by-default.",
  },
};

// ── seed data ───────────────────────────────────────────────────────
const HANDLERS = [
  { id: "h1", name: "Sanne de Vries" },
  { id: "h2", name: "Tom Willems" },
  { id: "h3", name: "Fatima el Amrani" },
  { id: "h4", name: "Pieter Bos" },
];

const CLAIMS_SEED = [
  {
    id: "CLM-2026-0101", plate: "XR-482-J", claimant: "J. van Dijk", vehicle: "Volkswagen Golf 1.5 TSI (2021)",
    type: "collision", coverage: "Allrisk (Casco)", status: "assessment", reserve: 3200, paid: 0,
    handler: "h1", fraud: 12, opened: "2026-08-04", due: "2026-08-20", city: "Utrecht",
    desc: "Rear-ended at a traffic light on the Biltstraat; bumper and tailgate damage. Counterparty admitted fault on the European accident statement.",
    timeline: [
      { d: "2026-08-04", who: "System", ev: "FNOL received via app · RDW vehicle verification passed" },
      { d: "2026-08-05", who: "Sanne de Vries", ev: "Claim assigned; damage photos requested" },
      { d: "2026-08-07", who: "System", ev: "Repair estimate received via Schadegarant network partner" },
    ],
  },
  {
    id: "CLM-2026-0102", plate: "ND-318-B", claimant: "R. Kaya", vehicle: "BMW X3 xDrive30e (2023)",
    type: "theft", coverage: "Allrisk (Casco)", status: "pendingApproval", reserve: 28500, paid: 0,
    handler: "h2", fraud: 61, opened: "2026-08-02", due: "2026-08-18", city: "Rotterdam",
    desc: "Vehicle reported stolen from public parking near Zuidplein. Both keys in claimant's possession; factory tracker was offline for 48 hours before the reported theft.",
    cis: ["Policy started 3 months before loss", "Tracker signal gap 48h before theft", "Prior theft claim (2024) at previous insurer"],
    timeline: [
      { d: "2026-08-02", who: "System", ev: "FNOL received via phone · RDW vehicle verification passed" },
      { d: "2026-08-03", who: "Tom Willems", ev: "Police report (aangifte) received and verified" },
      { d: "2026-08-05", who: "System", ev: "CIS database check returned 3 network signals" },
      { d: "2026-08-06", who: "Tom Willems", ev: "Reserve set to €28,500 · routed for manager approval" },
    ],
  },
  {
    id: "CLM-2026-0103", plate: "GK-115-P", claimant: "M. Visser", vehicle: "Skoda Octavia Combi (2020)",
    type: "glass", coverage: "WA + Beperkt Casco", status: "assessment", reserve: 480, paid: 0,
    handler: "h1", fraud: 6, opened: "2026-08-11", due: "2026-08-25", city: "Amersfoort",
    desc: "Stone chip on the A28 grew into a crack across the windscreen. Replacement scheduled via a Schadegarant partner.",
    timeline: [
      { d: "2026-08-11", who: "System", ev: "FNOL received via web form · RDW vehicle verification passed" },
      { d: "2026-08-12", who: "Sanne de Vries", ev: "Replacement booked with Schadegarant repair network" },
    ],
  },
  {
    id: "CLM-2026-0104", plate: "TS-882-K", claimant: "A. de Boer", vehicle: "Tesla Model 3 Long Range (2022)",
    type: "vandalism", coverage: "Allrisk (Casco)", status: "awaitingDocs", reserve: 2100, paid: 0,
    handler: "h3", fraud: 18, opened: "2026-08-08", due: "2026-08-15", city: "Den Haag",
    desc: "Deep scratches along both doors and the bonnet, discovered in the morning. Police report (aangifte) filed but not yet received from the claimant.",
    timeline: [
      { d: "2026-08-08", who: "System", ev: "FNOL received via app · RDW vehicle verification passed" },
      { d: "2026-08-09", who: "Fatima el Amrani", ev: "Police report requested from claimant · status set to awaiting documents" },
    ],
  },
  {
    id: "CLM-2026-0105", plate: "HB-664-R", claimant: "K. Smits", vehicle: "Toyota Yaris Hybrid (2023)",
    type: "collision", coverage: "WA", status: "new", reserve: 1900, paid: 0,
    handler: null, fraud: 9, opened: "2026-08-14", due: "2026-08-28", city: "Eindhoven",
    desc: "Low-speed collision while reversing out of a parking bay at Winkelcentrum Woensel; damage to the counterparty's rear door.",
    timeline: [{ d: "2026-08-14", who: "System", ev: "FNOL received via app · RDW vehicle verification passed" }],
  },
  {
    id: "CLM-2026-0106", plate: "VD-292-L", claimant: "S. Jansen", vehicle: "Peugeot 208 (2021)",
    type: "vandalism", coverage: "WA + Beperkt Casco", status: "new", reserve: 850, paid: 0,
    handler: null, fraud: 14, opened: "2026-08-15", due: "2026-08-29", city: "Groningen",
    desc: "Side mirror kicked off and a scratch along the rear panel during a festival weekend. Aangifte filed with local police.",
    timeline: [{ d: "2026-08-15", who: "System", ev: "FNOL received via WhatsApp · RDW vehicle verification passed" }],
  },
  {
    id: "CLM-2026-0107", plate: "GL-903-F", claimant: "T. Mulder", vehicle: "Renault Clio (2019)",
    type: "glass", coverage: "WA + Beperkt Casco", status: "paid", reserve: 420, paid: 420,
    handler: "h4", fraud: 4, opened: "2026-07-19", due: "2026-08-02", city: "Zwolle", stp: true,
    desc: "Cracked windscreen after gravel impact on the N35. Straight-through processed and repaired by a Schadegarant partner.",
    timeline: [
      { d: "2026-07-19", who: "System", ev: "FNOL received via app · RDW vehicle verification passed" },
      { d: "2026-07-19", who: "System", ev: "STP rule matched (glass, low risk) · approved automatically" },
      { d: "2026-07-21", who: "System", ev: "€420 paid via SEPA" },
    ],
  },
  {
    id: "CLM-2026-0108", plate: "PC-548-N", claimant: "L. Hoekstra", vehicle: "Volvo XC40 (2021)",
    type: "storm", coverage: "Allrisk (Casco)", status: "paid", reserve: 5600, paid: 5600,
    handler: "h2", fraud: 11, opened: "2026-07-06", due: "2026-07-20", city: "Leeuwarden",
    desc: "Hail and falling-branch damage to roof and bonnet during the July storm front over Friesland.",
    timeline: [
      { d: "2026-07-06", who: "System", ev: "FNOL received via web form · KNMI storm data matched date and region" },
      { d: "2026-07-09", who: "Tom Willems", ev: "Assessment approved after Schadegarant estimate" },
      { d: "2026-07-15", who: "Mark Jansen", ev: "€5,600 paid via SEPA" },
    ],
  },
  {
    id: "CLM-2026-0109", plate: "NA-733-C", claimant: "D. Bakker", vehicle: "Audi A4 Avant (2020)",
    type: "collision", coverage: "Allrisk (Casco)", status: "pendingApproval", reserve: 7400, paid: 0,
    handler: "h3", fraud: 44, opened: "2026-07-30", due: "2026-08-13", city: "Tilburg",
    cis: ["Preferred bodyshop flagged in CIS network"],
    desc: "Single-vehicle accident at 03:40 on the N261: guardrail impact, no witnesses. Claimant states swerving for a deer.",
    timeline: [
      { d: "2026-07-30", who: "System", ev: "FNOL received via phone · RDW vehicle verification passed" },
      { d: "2026-08-01", who: "Fatima el Amrani", ev: "CIS check: claimant's preferred bodyshop is network-flagged" },
      { d: "2026-08-04", who: "Fatima el Amrani", ev: "Routed for manager review (night-time single-vehicle, score 44)" },
    ],
  },
  {
    id: "CLM-2026-0110", plate: "GB-407-T", claimant: "E. Vos", vehicle: "Kia Picanto (2022)",
    type: "glass", coverage: "Allrisk (Casco)", status: "new", reserve: 350, paid: 0,
    handler: null, fraud: 5, opened: "2026-08-15", due: "2026-08-29", city: "Nijmegen",
    desc: "Windscreen crack in the driver's field of vision after motorway debris impact on the A73.",
    timeline: [{ d: "2026-08-15", who: "System", ev: "FNOL received via app · RDW vehicle verification passed" }],
  },
];

const TASKS_SEED = [
  { id: "t1", claim: "CLM-2026-0101", title: "Review Schadegarant repair estimate", due: "2026-08-16", assignee: "h1", done: false },
  { id: "t2", claim: "CLM-2026-0104", title: "Chase missing police report (aangifte)", due: "2026-08-14", assignee: "h3", done: false },
  { id: "t3", claim: "CLM-2026-0102", title: "Call claimant about tracker signal gap", due: "2026-08-17", assignee: "h2", done: false },
  { id: "t4", claim: "CLM-2026-0108", title: "Verify KNMI storm data for loss date", due: "2026-07-08", assignee: "h2", done: true },
  { id: "t5", claim: "CLM-2026-0103", title: "Confirm glass replacement appointment", due: "2026-08-18", assignee: "h1", done: false },
];

const POLICIES_SEED = [
  { no: "POL-2023-10412", plate: "XR-482-J", holder: "J. van Dijk", vehicle: "Volkswagen Golf 1.5 TSI (2021)", coverage: "Allrisk (Casco)", premium: 62, ownRisk: 350, start: "2023-03-01", renewal: "2027-03-01", bm: 8, status: "active" },
  { no: "POL-2026-11873", plate: "ND-318-B", holder: "R. Kaya", vehicle: "BMW X3 xDrive30e (2023)", coverage: "Allrisk (Casco)", premium: 118, ownRisk: 500, start: "2026-05-01", renewal: "2027-05-01", bm: 2, status: "active" },
  { no: "POL-2021-10077", plate: "GK-115-P", holder: "M. Visser", vehicle: "Skoda Octavia Combi (2020)", coverage: "WA + Beperkt Casco", premium: 38, ownRisk: 150, start: "2021-06-15", renewal: "2027-06-15", bm: 11, status: "active" },
  { no: "POL-2022-10561", plate: "TS-882-K", holder: "A. de Boer", vehicle: "Tesla Model 3 Long Range (2022)", coverage: "Allrisk (Casco)", premium: 96, ownRisk: 500, start: "2022-09-01", renewal: "2026-09-01", bm: 5, status: "active" },
  { no: "POL-2024-10820", plate: "HB-664-R", holder: "K. Smits", vehicle: "Toyota Yaris Hybrid (2023)", coverage: "WA", premium: 29, ownRisk: 0, start: "2024-01-10", renewal: "2027-01-10", bm: 3, status: "active" },
  { no: "POL-2023-10655", plate: "VD-292-L", holder: "S. Jansen", vehicle: "Peugeot 208 (2021)", coverage: "WA + Beperkt Casco", premium: 41, ownRisk: 150, start: "2023-04-20", renewal: "2027-04-20", bm: 6, status: "active" },
  { no: "POL-2020-10233", plate: "GL-903-F", holder: "T. Mulder", vehicle: "Renault Clio (2019)", coverage: "WA + Beperkt Casco", premium: 33, ownRisk: 0, start: "2020-11-05", renewal: "2026-11-05", bm: 9, status: "active" },
  { no: "POL-2021-10388", plate: "PC-548-N", holder: "L. Hoekstra", vehicle: "Volvo XC40 (2021)", coverage: "Allrisk (Casco)", premium: 71, ownRisk: 350, start: "2021-08-12", renewal: "2026-08-12", bm: 12, status: "active" },
  { no: "POL-2022-10904", plate: "NA-733-C", holder: "D. Bakker", vehicle: "Audi A4 Avant (2020)", coverage: "Allrisk (Casco)", premium: 84, ownRisk: 350, start: "2022-02-28", renewal: "2027-02-28", bm: 4, status: "active" },
  { no: "POL-2025-11340", plate: "GB-407-T", holder: "E. Vos", vehicle: "Kia Picanto (2022)", coverage: "Allrisk (Casco)", premium: 47, ownRisk: 150, start: "2025-07-01", renewal: "2026-07-01", bm: 2, status: "active" },
  { no: "POL-2024-11002", plate: "GS-118-D", holder: "P. Brouwer", vehicle: "Ford Focus Wagon (2021)", coverage: "WA + Beperkt Casco", premium: 36, ownRisk: 0, start: "2024-05-14", renewal: "2027-05-14", bm: 7, status: "active" },
  { no: "POL-2025-11518", plate: "MB-777-S", holder: "O. Yildiz", vehicle: "Mercedes-Benz GLC 300e (2024)", coverage: "Allrisk (Casco)", premium: 132, ownRisk: 500, start: "2025-12-01", renewal: "2026-12-01", bm: 1, status: "active" },
  { no: "POL-2023-10719", plate: "KP-104-T", holder: "N. de Lange", vehicle: "Opel Astra (2021)", coverage: "WA", premium: 27, ownRisk: 0, start: "2023-10-01", renewal: "2026-10-01", bm: 5, status: "active" },
  { no: "POL-2021-10850", plate: "WS-330-H", holder: "F. Kuipers", vehicle: "Volvo V60 T4 (2020)", coverage: "WA + Beperkt Casco", premium: 44, ownRisk: 150, start: "2021-03-22", renewal: "2027-03-22", bm: 10, status: "active" },
  { no: "POL-2022-10099", plate: "SJ-201-X", holder: "W. de Groot", vehicle: "Opel Corsa (2018)", coverage: "WA", premium: 24, ownRisk: 0, start: "2022-01-15", renewal: "2026-01-15", bm: 6, status: "lapsed" },
];

const ORGS_SEED = [
  { id: "o1", name: "Hollands Glorie Verzekeringen", email: "ops@hollandsglorie.nl", plan: "Enterprise", country: "NL", status: "active" },
  { id: "o2", name: "Brabant Assuradeuren", email: "info@brabantassu.nl", plan: "Standard", country: "NL", status: "active" },
  { id: "o3", name: "BeNe Insurance Group", email: "hello@bene-insurance.be", plan: "Trial", country: "BE", status: "active" },
];

const PUSERS_SEED = [
  { id: "u1", name: "Sanne de Vries", email: "s.devries@hollandsglorie.nl", org: "o1", role: "Handler", status: "Active" },
  { id: "u2", name: "Mark Jansen", email: "m.jansen@hollandsglorie.nl", org: "o1", role: "Manager", status: "Active" },
  { id: "u3", name: "Els Bakker", email: "e.bakker@hollandsglorie.nl", org: "o1", role: "CFO", status: "Active" },
  { id: "u4", name: "Tom Willems", email: "t.willems@brabantassu.nl", org: "o2", role: "Handler", status: "Active" },
  { id: "u5", name: "Lars Vermeulen", email: "l.vermeulen@brabantassu.nl", org: "o2", role: "Manager", status: "Suspended" },
  { id: "u6", name: "Anouk Peeters", email: "a.peeters@bene-insurance.be", org: "o3", role: "Org admin", status: "Invited" },
];

const AGENTS_SEED = [
  { id: "a1", name: "FNOL Intake Agent", tmpl: "intake", lang: "both", tone: "friendly", tools: ["RDW vehicle registry", "Policy lookup"], prompt: TMPL.intake.prompt, active: true, trigger: "new", autonomy: "approval" },
  { id: "a2", name: "Fraud Screener", tmpl: "fraud", lang: "en", tone: "concise", tools: ["CIS fraud database", "Policy lookup"], prompt: TMPL.fraud.prompt, active: true, trigger: "new", autonomy: "suggest" },
  { id: "a3", name: "Claim Summariser", tmpl: "summariser", lang: "both", tone: "concise", tools: ["Policy lookup"], prompt: TMPL.summariser.prompt, active: true, trigger: "manual", autonomy: "suggest" },
  { id: "a4", name: "Customer Comms", tmpl: "comms", lang: "nl", tone: "friendly", tools: ["Email & letters"], prompt: TMPL.comms.prompt, active: true, trigger: "status", autonomy: "approval" },
  { id: "a5", name: "Reserve Advisor", tmpl: "reserve", lang: "en", tone: "formal", tools: ["Repair network Schadegarant"], prompt: TMPL.reserve.prompt, active: true, trigger: "manual", autonomy: "suggest" },
];

// Deployments: which organizations/users an agent is released to. Super admin
// manages these; an agent is usable only where an active deployment covers you.
// Seeded so the demo org (Hollands Glorie, o1) has the full suite — matching
// the app's behavior before deployments existed.
const DEPLOY_SEED = AGENTS_SEED.map((a, i) => ({
  id: "d" + (i + 1), agentId: a.id, orgId: "o1", users: "all",
  status: "active", by: "Priya Sharma", at: "2026-08-01",
}));

const PAID_MONTHS = [
  { m: "Jan", v: 284 }, { m: "Feb", v: 302 }, { m: "Mar", v: 271 }, { m: "Apr", v: 315 },
  { m: "May", v: 298 }, { m: "Jun", v: 322 }, { m: "Jul", v: 356 }, { m: "Aug", v: 341 },
];
const RESERVE_TREND = [
  { m: "Mar", v: 412 }, { m: "Apr", v: 398 }, { m: "May", v: 431 },
  { m: "Jun", v: 405 }, { m: "Jul", v: 388 }, { m: "Aug", v: 376 },
];

const SCENARIOS = [
  { plate: "GS-118-D", claimant: "P. Brouwer", type: "glass", coverage: "WA + Beperkt Casco", city: "Almere", reserve: 390, fraud: 8, channel: "App", desc: "Small windscreen crack from gravel on the A6; no injuries, vehicle driveable." },
  { plate: "MB-777-S", claimant: "O. Yildiz", type: "theft", coverage: "Allrisk (Casco)", city: "Amsterdam", reserve: 34000, fraud: 55, channel: "Phone", cis: ["Second key reported lost at FNOL", "Policy 8 months old"], desc: "Mercedes GLC stolen overnight from street parking in Amsterdam-West. Claimant can only return one key and reports the second key as lost." },
  { plate: "KP-104-T", claimant: "N. de Lange", type: "collision", coverage: "WA", city: "Breda", reserve: 2200, fraud: 15, channel: "Web form", desc: "Merged into another vehicle on the A16 near Breda; damage to counterparty's front wing. European accident statement completed." },
  { plate: "WS-330-H", claimant: "F. Kuipers", type: "storm", coverage: "WA + Beperkt Casco", city: "Haarlem", reserve: 1600, fraud: 10, channel: "App", desc: "Branch fell on the roof during KNMI code-orange storm; dents in roof and cracked rear window." },
];

const DEFAULT_CONSENT =
  "By submitting this claim you consent to the processing of your personal data under the GDPR (AVG) and the Dutch insurers' Code of Conduct for the Processing of Personal Data (Gedragscode Verwerking Persoonsgegevens Verzekeraars). Automated tools may support the handling of your claim; in line with GDPR Art. 22 you always have the right to human review of any automated decision. Data is shared with the CIS foundation database only where the fraud protocol of the Verbond van Verzekeraars applies.";

// Per-claim document seed: fnol/photos/estimate always tracked; police report for theft & vandalism.
function seedDocs(c) {
  const later = ["assessment", "pendingApproval", "approved", "paid"].includes(c.status);
  const d = [
    { key: "fnol", received: true },
    { key: "photos", received: c.status !== "new" },
    { key: "estimate", received: later && c.type !== "theft" },
  ];
  if (c.type === "theft" || c.type === "vandalism")
    d.push({ key: "police", received: c.id !== "CLM-2026-0104" && c.status !== "new" });
  return d;
}
const DOC_LABEL = {
  en: { fnol: "FNOL report", photos: "Damage photos", estimate: "Repair estimate", police: "Police report (aangifte)" },
  nl: { fnol: "FNOL-melding", photos: "Schadefoto's", estimate: "Reparatie-offerte", police: "Proces-verbaal (aangifte)" },
};

// ── e-mail templates (EN + NL) ──────────────────────────────────────
function emailTemplate(key, c, lang, ownRisk) {
  const nl = lang === "nl";
  const sign = nl ? "Met vriendelijke groet,\nBoxora schadeteam" : "Kind regards,\nBoxora claims team";
  const intro = nl ? `Beste ${c.claimant},` : `Dear ${c.claimant},`;
  switch (key) {
    case "ack": return {
      subject: nl ? `Ontvangstbevestiging schademelding ${c.id}` : `We received your claim ${c.id}`,
      body: `${intro}\n\n${nl
        ? `Wij hebben uw schademelding voor uw ${c.vehicle} (kenteken ${c.plate}) goed ontvangen. Uw dossiernummer is ${c.id}. Een schadebehandelaar bekijkt uw melding en neemt binnen 2 werkdagen contact met u op. U hoeft nu niets te doen.`
        : `We have received your claim for your ${c.vehicle} (license plate ${c.plate}). Your file number is ${c.id}. A claim handler will review your claim and contact you within 2 working days. You do not need to do anything right now.`}\n\n${sign}`,
    };
    case "docs": return {
      subject: nl ? `Documenten nodig voor dossier ${c.id}` : `Documents needed for claim ${c.id}`,
      body: `${intro}\n\n${nl
        ? `Om uw schade (${c.id}) verder te behandelen hebben wij nog documenten van u nodig, zoals foto's van de schade${c.type === "theft" || c.type === "vandalism" ? " en het proces-verbaal van de aangifte" : ""}. U kunt deze uploaden via de app of beantwoorden op deze e-mail. Zodra wij alles hebben, gaan wij direct verder met uw dossier.`
        : `To continue handling your claim (${c.id}) we still need some documents from you, such as photos of the damage${c.type === "theft" || c.type === "vandalism" ? " and the official police report" : ""}. You can upload them in the app or reply to this e-mail. As soon as we have everything, we will continue with your file right away.`}\n\n${sign}`,
    };
    case "payout": return {
      subject: nl ? `Uitkering goedgekeurd voor dossier ${c.id}` : `Payout approved for claim ${c.id}`,
      body: `${intro}\n\n${nl
        ? `Goed nieuws: uw schadeclaim ${c.id} is goedgekeurd. Wij keren binnen 5 werkdagen uit via SEPA-overboeking. Houd rekening met uw eigen risico van € ${ownRisk ?? 0}. Heeft u vragen, dan helpen wij u graag.`
        : `Good news: your claim ${c.id} has been approved. We will pay out within 5 working days by SEPA transfer. Please note your own risk (deductible) of € ${ownRisk ?? 0}. If you have any questions, we are happy to help.`}\n\n${sign}`,
    };
    case "repair": return {
      subject: nl ? `Reparatie-autorisatie ${c.id} · ${c.plate}` : `Repair authorisation ${c.id} · ${c.plate}`,
      body: `${nl ? "Beste Schadegarant-partner," : "Dear Schadegarant partner,"}\n\n${nl
        ? `Hierbij autoriseren wij de reparatie van de ${c.vehicle}, kenteken ${c.plate}, onder dossier ${c.id}. Gelieve conform de Schadegarant-afspraken te factureren en foto's van voor en na de reparatie aan het dossier toe te voegen.`
        : `We hereby authorise the repair of the ${c.vehicle}, license plate ${c.plate}, under file ${c.id}. Please invoice according to the Schadegarant agreements and add before/after photos to the file.`}\n\n${sign}`,
    };
    case "handover": return {
      subject: nl ? `Interne overdracht dossier ${c.id}` : `Internal handover of file ${c.id}`,
      body: `${nl ? "Beste collega," : "Hi team,"}\n\n${nl
        ? `Overdracht van dossier ${c.id} (${c.claimant}, ${c.vehicle}, ${c.plate}). Status: ${c.status}. Reserve: € ${c.reserve}. Bijzonderheden: ${c.desc}`
        : `Handing over file ${c.id} (${c.claimant}, ${c.vehicle}, ${c.plate}). Status: ${c.status}. Reserve: € ${c.reserve}. Notes: ${c.desc}`}\n\n${sign}`,
    };
    default: return { subject: "", body: "" };
  }
}

const ROLES = [
  { id: "h1", name: "Sanne de Vries", role: "handler", descEN: "Handles the daily werkvoorraad of motor claims.", descNL: "Behandelt de dagelijkse werkvoorraad motorschades." },
  { id: "m1", name: "Mark Jansen", role: "manager", descEN: "Approves large reserves and steers the team.", descNL: "Keurt grote reserves goed en stuurt het team aan." },
  { id: "c1", name: "Els Bakker", role: "cfo", descEN: "Watches reserves, payouts and the loss ratio.", descNL: "Bewaakt reserves, uitkeringen en de schaderatio." },
  { id: "p1", name: "Priya Sharma", role: "admin", descEN: "Runs the platform: organizations, users, compliance.", descNL: "Beheert het platform: organisaties, gebruikers, compliance." },
];
const ROLE_LABEL = {
  en: { handler: "Claim Handler", manager: "Claims Manager", cfo: "CFO", admin: "Super Admin" },
  nl: { handler: "Schadebehandelaar", manager: "Schademanager", cfo: "CFO", admin: "Super Admin" },
};
const NAV = {
  handler: [["dashboard", "nav_dashboard", "dash"], ["claims", "nav_claims", "claims"], ["policies", "nav_policies", "policies"], ["tasks", "nav_tasks", "tasks"], ["studio", "nav_studio", "studio"]],
  manager: [["dashboard", "nav_dashboard", "dash"], ["claims", "nav_claims", "claims"], ["policies", "nav_policies", "policies"], ["tasks", "nav_tasks", "tasks"], ["studio", "nav_studio", "studio"], ["audit", "nav_audit", "audit"]],
  cfo: [["dashboard", "nav_finance", "dash"], ["claims", "nav_claims", "claims"], ["policies", "nav_policies", "policies"], ["studio", "nav_studio", "studio"], ["audit", "nav_audit", "audit"]],
  admin: [["dashboard", "nav_platform", "dash"], ["orgs", "nav_orgs", "orgs"], ["users", "nav_users", "users"], ["studio", "nav_studio", "studio"], ["legal", "nav_legal", "legal"], ["audit", "nav_audit", "audit"]],
};

const PIPE_KEYS = ["intake", "coverage", "fraud", "reserve", "decision", "comms", "payment"];
const PIPE_AGENT = { intake: "intake", fraud: "fraud", reserve: "reserve", comms: "comms" }; // others = system rule

// ── App ─────────────────────────────────────────────────────────────
export default function App() {
  const [lang, setLang] = useState("en");
  const [user, setUser] = useState(null);
  const [page, setPage] = useState("dashboard");
  const [claims, setClaims] = useState(CLAIMS_SEED);
  const [tasks, setTasks] = useState(TASKS_SEED);
  const [policies, setPolicies] = useState(POLICIES_SEED);
  const [agents, setAgents] = useState(AGENTS_SEED);
  const [deployments, setDeployments] = useState(DEPLOY_SEED);
  const [depForm, setDepForm] = useState({ agentId: "a1", orgId: "o1", scope: "all", users: [] });
  const [orgs, setOrgs] = useState(ORGS_SEED);
  const [pUsers, setPUsers] = useState(PUSERS_SEED);
  const [audit, setAudit] = useState([]);
  const [toasts, setToasts] = useState([]);
  const [selClaim, setSelClaim] = useState(null);
  const [selPolicy, setSelPolicy] = useState(null);
  const [claimTab, setClaimTab] = useState("assessment");
  const [search, setSearch] = useState("");
  const [mineOnly, setMineOnly] = useState(false);
  const [polSearch, setPolSearch] = useState("");
  const [showFnol, setShowFnol] = useState(false);
  const emptyFnol = { channel: "App", plate: "", vehicle: "", claimant: "", type: "collision", coverage: "WA + Beperkt Casco", city: "", desc: "" };
  const [fnol, setFnol] = useState(emptyFnol);
  const [docs, setDocs] = useState(() => Object.fromEntries(CLAIMS_SEED.map(c => [c.id, seedDocs(c)])));
  const [drafts, setDrafts] = useState({});   // claimId -> {to,tmpl,elang,subject,body}
  const [sent, setSent] = useState({});       // claimId -> [{to,subject,d}]
  const [chats, setChats] = useState({});     // claimId -> [{q,a}]
  const [copilotQ, setCopilotQ] = useState("");
  const [bubble, setBubble] = useState(false);  // floating claim copilot
  const [aiOut, setAiOut] = useState({});     // claimId -> {summary, photo, fraud}
  const [busy, setBusy] = useState({});       // spinner flags
  const [assess, setAssess] = useState({});   // claimId -> assessment form
  // studio
  const [steps, setSteps] = useState(PIPE_KEYS.map(k => ({ key: k, on: true })));
  const [pipeClaim, setPipeClaim] = useState("");
  const [pipeMode, setPipeMode] = useState("approval");
  const [runLog, setRunLog] = useState(null); // {claimId, entries:[{key,status,note}]}
  const [benchAgent, setBenchAgent] = useState("a1");
  const [benchQ, setBenchQ] = useState("");
  const [benchLog, setBenchLog] = useState([]);
  const [agForm, setAgForm] = useState(null);
  const [evalRes, setEvalRes] = useState(null);
  // admin / misc
  const [orgForm, setOrgForm] = useState({ name: "", email: "", plan: "Trial", country: "NL" });
  const [userForm, setUserForm] = useState({ name: "", email: "", org: "o1", role: "Handler" });
  const [legal, setLegal] = useState({ retention: "7", dpia: "in_progress", cis: true, consent: DEFAULT_CONSENT });
  const [taskForm, setTaskForm] = useState({ title: "", claim: "", assignee: "" });
  const emptyPol = { plate: "", vehicle: "", holder: "", coverage: "WA + Beperkt Casco", premium: "45", ownRisk: 150, bm: "0" };
  const [polForm, setPolForm] = useState(emptyPol);
  const [showPolForm, setShowPolForm] = useState(false);
  const seq = useRef(110);

  const t = k => (T[lang] && T[lang][k]) || T.en[k] || k;
  const tt = (en, nl) => (lang === "nl" ? nl : en);
  const fmt = n => eur(n, lang);
  const handlerName = id => (HANDLERS.find(h => h.id === id) || {}).name || "—";
  const claimById = id => claims.find(c => c.id === id);
  const policyByPlate = plate => policies.find(p => p.plate === plate);
  const now = () => `${TODAY} ${new Date().toTimeString().slice(0, 8)}`;

  function toast(msg) {
    const id = Math.random().toString(36).slice(2);
    setToasts(x => [...x, { id, msg }]);
    setTimeout(() => setToasts(x => x.filter(y => y.id !== id)), 3500);
  }
  function logAudit(action, target, detail, actor) {
    const a = actor || (user ? user.name : "System");
    const role = actor ? "system" : user ? user.role : "system";
    setAudit(x => [{ t: now(), actor: a, role, action, target, detail }, ...x]);
  }
  function addTimeline(claimId, ev, who) {
    setClaims(cs => cs.map(c => c.id === claimId
      ? { ...c, timeline: [...c.timeline, { d: TODAY, who: who || (user ? user.name : "System"), ev }] } : c));
  }
  function updateClaim(id, patch) {
    setClaims(cs => cs.map(c => (c.id === id ? { ...c, ...patch } : c)));
  }
  function setB(key, v) { setBusy(b => ({ ...b, [key]: v })); }

  // ── shared render helpers ──
  function statusPill(s) { return <Pill label={t("st_" + s)} color={STATUS_COLOR[s]} />; }
  function typePill(ty) { return <Pill label={t("ty_" + ty)} color={TYPE_COLOR[ty]} />; }
  function routePill(f) { const r = routeOf(f); return <Pill label={t("rt_" + r)} color={ROUTE_COLOR[r]} />; }

  function openClaim(id) { setSelClaim(id); setClaimTab("assessment"); setPage("claims"); }
  function openPolicy(no) { setSelPolicy(no); setPage("policies"); }

  // ── login ──
  function renderLogin() {
    return (
      <div className="login">
        <div className="row" style={{ position: "absolute", top: 18, right: 18 }}>{renderLangSwitch()}</div>
        <div className="row" style={{ gap: 14 }}>
          <Mark size={44} />
          <div>
            <div style={{ fontFamily: "Archivo", fontWeight: 800, fontSize: 30 }}>Boxora</div>
            <div className="muted">{t("tagline")}</div>
          </div>
        </div>
        <div className="rolegrid">
          {ROLES.map(r => (
            <div key={r.id} className="rolecard">
              <div className="row">
                <div className="avatar">{r.name.split(" ").map(w => w[0]).slice(0, 2).join("")}</div>
                <div>
                  <div className="b">{ROLE_LABEL[lang][r.role]}</div>
                  <div className="small muted">{r.name}</div>
                </div>
              </div>
              <div className="small muted" style={{ minHeight: 34 }}>{lang === "nl" ? r.descNL : r.descEN}</div>
              <button className="btn btn-p" onClick={() => {
                setUser(r); setPage("dashboard"); setSelClaim(null); setSelPolicy(null);
                setMineOnly(r.role === "handler");
              }}>
                {tt("Continue as", "Ga verder als")} {r.name.split(" ")[0]} →
              </button>
            </div>
          ))}
        </div>
        <div className="small muted" style={{ marginTop: 26, maxWidth: 640, textAlign: "center" }}>
          {tt("Demo environment — all data is fictional, RDW / CIS / OCR / payments are simulated and compliance copy is demo content, not legal advice.",
              "Demo-omgeving — alle gegevens zijn fictief, RDW / CIS / OCR / betalingen zijn gesimuleerd en compliance-teksten zijn demomateriaal, geen juridisch advies.")}
        </div>
      </div>
    );
  }

  function renderLangSwitch() {
    return (
      <span className="langsw">
        <button className={lang === "en" ? "on" : ""} onClick={() => setLang("en")}>EN</button>
        <button className={lang === "nl" ? "on" : ""} onClick={() => setLang("nl")}>NL</button>
      </span>
    );
  }

  const PAGE_TITLE = {
    dashboard: user ? (user.role === "cfo" ? "nav_finance" : user.role === "admin" ? "nav_platform" : "nav_dashboard") : "nav_dashboard",
    claims: "nav_claims", policies: "nav_policies", tasks: "nav_tasks", studio: "nav_studio",
    audit: "nav_audit", orgs: "nav_orgs", users: "nav_users", legal: "nav_legal",
  };

  function renderShell(content) {
    return (
      <div className="shell">
        <aside className="side">
          <div className="brandrow"><Mark size={26} /><span className="brand">Boxora</span></div>
          <nav style={{ display: "flex", flexDirection: "inherit", gap: 4, flex: "0 1 auto" }}>
            {NAV[user.role].map(([p, key, icon]) => (
              <button key={p} className={"nav-i" + (page === p ? " on" : "")}
                onClick={() => { setPage(p); setSelClaim(null); setSelPolicy(null); }}>
                <Icon name={icon} />{t(key)}
              </button>
            ))}
          </nav>
          <div className="userbox">
            <div className="b" style={{ color: "#fff" }}>{user.name}</div>
            <div>{ROLE_LABEL[lang][user.role]}</div>
            <button className="btn btn-sm" style={{ marginTop: 8, background: "transparent", color: "#c8d1d8", borderColor: "rgba(255,255,255,.25)" }}
              onClick={() => { setUser(null); setPage("dashboard"); }}>
              {t("logout")}
            </button>
          </div>
        </aside>
        <div className="main">
          <div className="topbar">
            <div className="pagetitle">{t(PAGE_TITLE[page] || "nav_dashboard")}</div>
            {renderLangSwitch()}
          </div>
          <div className="content">{content}</div>
        </div>
        {renderBubble()}
      </div>
    );
  }

  // ── core actions ──
  const delay = ms => new Promise(r => setTimeout(r, ms));
  // Deployment gate: ops roles can only use agents the super admin released to
  // their organization (or to them personally). Admin bypasses — they manage it.
  const meAsPlatformUser = () => (user ? pUsers.find(u => u.name === user.name) : null);
  const myOrgId = () => (meAsPlatformUser() || { org: "o1" }).org; // demo ops personas belong to o1
  const isDeployed = agentId => {
    if (!user || user.role === "admin") return true;
    const me = meAsPlatformUser();
    const org = me ? me.org : "o1";
    return deployments.some(d => d.status === "active" && d.agentId === agentId && d.orgId === org &&
      (d.users === "all" || (me && d.users.includes(me.id))));
  };
  const agentFor = tmpl => agents.find(a => a.tmpl === tmpl && a.active && isDeployed(a.id));
  // Claim-tab AI gate: the template fallback only covers "no agent configured".
  // An active agent that simply isn't deployed to you must not run at all.
  const deployBlocked = tmpl => {
    if (agentFor(tmpl) || !agents.some(a => a.tmpl === tmpl && a.active)) return false;
    toast(tt("This agent is not deployed to your organization.", "Deze agent is niet uitgerold naar uw organisatie."));
    return true;
  };
  const langName = l => (l === "nl" ? "Dutch" : "English");
  const agentDirective = a =>
    `${a.prompt}\nTone: ${a.tone}. Respond in ${a.lang === "both" ? langName(lang) : langName(a.lang)}.` +
    `\nAvailable tools (simulated in this demo — say when you would use one): ${a.tools.join(", ") || "none"}.`;

  function createClaim(data) {
    seq.current += 1;
    const id = `CLM-2026-${String(seq.current + 1).padStart(4, "0")}`;
    const plate = data.plate.toUpperCase();
    const claim = {
      id, plate, claimant: data.claimant, vehicle: data.vehicle || vehicleFromPlate(plate),
      type: data.type, coverage: data.coverage, status: "new", reserve: data.reserve || 1200, paid: 0,
      handler: null, fraud: data.fraud ?? 10, opened: TODAY, due: "2026-08-30", city: data.city,
      desc: data.desc, ...(data.cis ? { cis: data.cis } : {}),
      timeline: [{ d: TODAY, who: "System", ev: `FNOL received via ${data.channel} · RDW vehicle verification passed` }],
    };
    setClaims(cs => [claim, ...cs]);
    setDocs(d => ({ ...d, [id]: seedDocs(claim) }));
    logAudit("fnol", id, `FNOL registered via ${data.channel} · ${plate} · ${data.claimant}`);
    toast(tt("Claim registered: ", "Schade geregistreerd: ") + id);
    if (agents.some(a => a.active && a.trigger === "new" && isDeployed(a.id))) {
      setPage("studio"); setSelClaim(null); setPipeClaim(id);
      toast(tt("Agent trigger 'on new claim' — pipeline starting…", "Agenttrigger 'bij nieuwe schade' — pipeline start…"));
      runPipeline(claim, pipeMode);
    }
    return claim;
  }

  async function runPipeline(claim, mode) {
    const order = steps.filter(s => s.on).map(s => s.key);
    let entries = order.map(k => ({ key: k, status: "queued", note: "" }));
    const upd = (k, patch) => {
      entries = entries.map(e => (e.key === k ? { ...e, ...patch } : e));
      setRunLog({ claimId: claim.id, mode, entries });
    };
    setRunLog({ claimId: claim.id, mode, entries });
    let c = { ...claim };
    let covFail = null;
    const suggest = mode === "suggest";
    for (const k of order) {
      upd(k, { status: "running" });
      const need = PIPE_AGENT[k];
      const ag = need ? agentFor(need) : null;
      if (need && !ag) {
        upd(k, { status: "skip", note: tt("⚠ skipped — no active agent for this template", "⚠ overgeslagen — geen actieve agent voor dit sjabloon") });
        continue;
      }
      if (k === "intake") {
        await delay(700);
        upd(k, { status: "done", note: `${c.plate} · ${c.claimant} · ${t("ty_" + c.type)} · RDW ✓` });
      } else if (k === "coverage") {
        await delay(500);
        const p = policyByPlate(c.plate);
        if (!p) covFail = tt("no policy found", "geen polis gevonden");
        else if (p.status !== "active") covFail = tt("policy is ", "polis is ") + p.status;
        else if (p.coverage === "WA" && c.type !== "collision") covFail = tt("WA does not cover ", "WA dekt geen ") + t("ty_" + c.type).toLowerCase();
        if (covFail) upd(k, { status: "fail", note: "✗ " + covFail + tt(" → human routing", " → routering naar mens") });
        else upd(k, { status: "done", note: `${p.no} · ${p.coverage} · ${t("ownRisk")} ${fmt(p.ownRisk)}` });
      } else if (k === "fraud") {
        const out = await askClaudeJSON(
          agentDirective(ag),
          `Screen this claim. Claim file JSON: ${JSON.stringify({ ...c, timeline: undefined })}. Policy: ${JSON.stringify(policyByPlate(c.plate) || null)}. Return JSON {"risk_score": number 0-100, "decision": string, "indicators": string[], "reasoning": string}.`,
          { risk_score: c.fraud, decision: "manual review", indicators: ["AI unavailable — kept existing score"], reasoning: "Fallback: model call failed." }
        );
        const score = Math.max(0, Math.min(100, Math.round(out.risk_score ?? c.fraud)));
        c.fraud = score;
        setAiOut(x => ({ ...x, [c.id]: { ...(x[c.id] || {}), fraud: { ...out, risk_score: score } } }));
        if (!suggest) {
          updateClaim(c.id, { fraud: score });
          addTimeline(c.id, `AI fraud screening: score ${score} (${routeOf(score)})`, "Fraud Screener");
        }
        logAudit("ai:fraud", c.id, `pipeline screening · score ${score} · ${routeOf(score)}`);
        upd(k, { status: "done", note: `score ${score} · ${t("rt_" + routeOf(score))}` });
      } else if (k === "reserve") {
        const out = await askClaudeJSON(
          agentDirective(ag),
          `Propose a reserve for this claim. Claim: ${JSON.stringify({ ...c, timeline: undefined })}. Return JSON {"low": number, "expected": number, "high": number, "rationale": string} in euros.`,
          { low: Math.round(c.reserve * 0.8), expected: c.reserve, high: Math.round(c.reserve * 1.3), rationale: "Fallback: model call failed." }
        );
        const exp = Math.max(0, Math.round(out.expected || c.reserve));
        c.reserve = exp;
        if (!suggest) updateClaim(c.id, { reserve: exp });
        logAudit("ai:reserve", c.id, `pipeline reserve proposal · expected ${fmt(exp)}`);
        upd(k, { status: "done", note: `${fmt(out.low || 0)} – ${fmt(exp)} – ${fmt(out.high || 0)}` });
      } else if (k === "decision") {
        await delay(400);
        let newStatus, note;
        if (c.type === "glass" && c.fraud < 25 && c.reserve <= 1500 && !covFail) {
          newStatus = "approved"; c.stp = true;
          note = "STP · " + t("st_approved");
        } else {
          const route = covFail ? "human" : routeOf(c.fraud);
          if (route === "auto" && c.reserve <= 10000) newStatus = mode === "auto" ? "approved" : "pendingApproval";
          else if (route === "manual") newStatus = "assessment";
          else newStatus = "pendingApproval";
          note = `${t("rt_" + route)} → ${t("st_" + newStatus)}` + (covFail ? ` (${covFail})` : "");
        }
        c.status = newStatus;
        if (!suggest) {
          updateClaim(c.id, { status: newStatus, ...(c.stp ? { stp: true } : {}) });
          addTimeline(c.id, `Pipeline decision: ${newStatus}${c.stp ? " (STP)" : ""}`, "Agent pipeline");
        } else note += " (" + tt("suggested", "voorstel") + ")";
        upd(k, { status: "done", note });
      } else if (k === "comms") {
        const aLang = ag.lang === "both" ? lang : ag.lang;
        const fb = emailTemplate("ack", c, aLang, (policyByPlate(c.plate) || {}).ownRisk);
        const out = await askClaudeJSON(
          agentDirective(ag),
          `Draft an e-mail to the claimant explaining the current status of their claim. Claim: ${JSON.stringify({ ...c, timeline: undefined })}. Current status: ${c.status}. Return JSON {"subject": string, "body": string}.`,
          fb
        );
        setDrafts(d => ({ ...d, [c.id]: { to: "claimant", tmpl: "blank", elang: aLang, subject: out.subject || fb.subject, body: out.body || fb.body } }));
        logAudit("ai:comms", c.id, "pipeline customer e-mail drafted");
        upd(k, { status: "done", note: tt("e-mail drafted → Emails tab", "e-mail opgesteld → tabblad E-mails") });
      } else if (k === "payment") {
        await delay(400);
        if (c.status === "approved") {
          if (mode === "auto") {
            updateClaim(c.id, { status: "paid", paid: c.reserve });
            addTimeline(c.id, `${fmt(c.reserve)} paid via SEPA`, "System");
            logAudit("payment", c.id, `${fmt(c.reserve)} paid via SEPA (autonomous pipeline)`);
            c.status = "paid";
            upd(k, { status: "done", note: `${fmt(c.reserve)} · SEPA` });
          } else {
            upd(k, { status: "done", note: tt("ready — awaits manager approval", "gereed — wacht op akkoord manager") });
          }
        } else {
          upd(k, { status: "skip", note: tt("not approved — skipped", "niet akkoord — overgeslagen") });
        }
      }
    }
    logAudit("pipeline", claim.id, `run complete · mode ${mode} · final status ${c.status}`);
    toast(tt("Pipeline finished for ", "Pipeline afgerond voor ") + claim.id);
  }

  function payClaim(c) {
    if (user.role === "handler" && c.reserve > 5000) {
      toast(tt("Four-eyes rule: payments above €5,000 need a manager. Payment blocked.",
               "Vier-ogenprincipe: betalingen boven €5.000 vereisen een manager. Betaling geblokkeerd."));
      logAudit("payment_blocked", c.id, `four-eyes: handler attempted ${fmt(c.reserve)} payout`);
      return;
    }
    updateClaim(c.id, { status: "paid", paid: c.reserve });
    addTimeline(c.id, `${fmt(c.reserve)} paid via SEPA`);
    logAudit("payment", c.id, `${fmt(c.reserve)} paid via SEPA`);
    toast(tt("Payment executed: ", "Betaling uitgevoerd: ") + fmt(c.reserve));
  }

  function getAssess(c) {
    return assess[c.id] || {
      liability: 100, estimate: c.reserve, cov1: true, cov2: true, cov3: false,
      decision: "approve", notes: "",
    };
  }
  function saveAssessment(c, decision) {
    const a = { ...getAssess(c), ...(decision ? { decision } : {}) };
    let st, msg;
    if (a.decision === "approve") {
      st = c.reserve > MGR_RESERVE || c.fraud > MAX_AUTO_FRAUD ? "pendingApproval" : "approved";
      msg = st === "pendingApproval" ? tt("routed for manager approval", "doorgezet voor akkoord manager") : t("st_approved");
    } else if (a.decision === "docs") { st = "awaitingDocs"; msg = t("st_awaitingDocs"); }
    else { st = "rejected"; msg = t("st_rejected"); }
    updateClaim(c.id, { status: st, reserve: Number(a.estimate) || c.reserve });
    addTimeline(c.id, `Assessment saved: liability ${a.liability}% · estimate ${fmt(a.estimate)} · ${st}`);
    logAudit("assessment", c.id, `decision ${a.decision} → ${st} · estimate ${fmt(a.estimate)}`);
    toast(tt("Assessment saved — ", "Beoordeling opgeslagen — ") + msg);
  }

  async function runPhotoScan(c) {
    setB("photo", true);
    const out = await askClaudeJSON(
      `You are an AI damage-photo analysis assistant for a Dutch motor insurer. From the claim description, infer the likely damaged parts as if you had analysed the claim photos. Use realistic Dutch repair-cost levels. Respond in ${langName(lang)}.`,
      `Claim: ${JSON.stringify({ id: c.id, vehicle: c.vehicle, type: c.type, desc: c.desc })}. Return JSON {"parts":[{"part":string,"action":"repair"|"replace","cost":number}],"total":number,"verdict":"repairable"|"total_loss","confidence":number 0-1}.`,
      { parts: [{ part: "Assessment unavailable", action: "repair", cost: c.reserve }], total: c.reserve, verdict: "repairable", confidence: 0.3 }
    );
    setAiOut(x => ({ ...x, [c.id]: { ...(x[c.id] || {}), photo: out } }));
    logAudit("ai:photoscan", c.id, `verdict ${out.verdict} · total ${fmt(out.total || 0)}`);
    setB("photo", false);
  }

  async function runFraud(c) {
    if (deployBlocked("fraud")) return;
    setB("fraud", true);
    const ag = agentFor("fraud");
    const out = await askClaudeJSON(
      ag ? agentDirective(ag) : TMPL.fraud.prompt + ` Respond in ${langName(lang)}.`,
      `Screen this claim. Claim file: ${JSON.stringify({ ...c, timeline: undefined })}. Policy: ${JSON.stringify(policyByPlate(c.plate) || null)}. Return JSON {"risk_score": number 0-100, "decision": string, "indicators": string[], "reasoning": string}.`,
      { risk_score: c.fraud, decision: "manual review", indicators: ["AI unavailable — kept existing score"], reasoning: "Fallback: model call failed." }
    );
    const score = Math.max(0, Math.min(100, Math.round(out.risk_score ?? c.fraud)));
    setAiOut(x => ({ ...x, [c.id]: { ...(x[c.id] || {}), fraud: { ...out, risk_score: score } } }));
    updateClaim(c.id, { fraud: score });
    addTimeline(c.id, `AI fraud screening: score ${score} (${routeOf(score)})`, "Fraud Screener");
    logAudit("ai:fraud", c.id, `score ${score} · ${routeOf(score)}`);
    setB("fraud", false);
  }

  async function runSummary(c) {
    if (deployBlocked("summariser")) return;
    setB("summary", true);
    const ag = agentFor("summariser");
    const out = await askClaudeJSON(
      (ag ? agentDirective(ag) : TMPL.summariser.prompt) + ` Respond in ${langName(lang)}.`,
      `Summarise this claim file: ${JSON.stringify(c)}. Policy: ${JSON.stringify(policyByPlate(c.plate) || null)}. Return JSON {"summary": string, "risk_flags": string[], "recommendation": string, "next_steps": string[]}.`,
      { summary: tt("AI summary unavailable.", "AI-samenvatting niet beschikbaar."), risk_flags: [], recommendation: "—", next_steps: [] }
    );
    setAiOut(x => ({ ...x, [c.id]: { ...(x[c.id] || {}), summary: out } }));
    logAudit("ai:summary", c.id, "claim summary generated");
    setB("summary", false);
  }

  async function askCopilot(c) {
    const q = copilotQ.trim();
    if (!q) return;
    setB("copilot", true); setCopilotQ("");
    const hist = (chats[c.id] || []).slice(-4).map(m => `Q: ${m.q}\nA: ${m.a}`).join("\n");
    const a = await askClaude(
      `You are a claims copilot for a Dutch motor insurer. Answer ONLY from the claim file JSON provided; if the file does not contain the answer, say so plainly. Maximum 5 sentences. Respond in ${langName(lang)}.`,
      `Claim file: ${JSON.stringify(c)}\nPolicy: ${JSON.stringify(policyByPlate(c.plate) || null)}\nDocuments: ${JSON.stringify(docs[c.id] || [])}\nPrevious exchanges:\n${hist}\n\nQuestion: ${q}`
    ).catch(() => tt("The copilot is unavailable right now.", "De copiloot is nu niet beschikbaar."));
    setChats(x => ({ ...x, [c.id]: [...(x[c.id] || []), { q, a }] }));
    logAudit("ai:copilot", c.id, `question answered: "${q.slice(0, 60)}"`);
    setB("copilot", false);
  }

  const recipientAddr = (c, to) => ({
    claimant: c.claimant.toLowerCase().replace(/[^a-z]/g, "") + "@mail.nl",
    repair: "planning@schadegarant-partner.nl",
    internal: "team@schadedesk.nl",
    counterpart: "claims@tegenpartij-verzekeraar.nl",
  }[to]);

  function getDraft(c) {
    return drafts[c.id] || { to: "claimant", tmpl: "ack", elang: lang, subject: "", body: "" };
  }
  function setDraft(c, patch) {
    setDrafts(d => ({ ...d, [c.id]: { ...getDraft(c), ...patch } }));
  }
  function applyTemplate(c, key, elang) {
    const p = policyByPlate(c.plate);
    const e = emailTemplate(key, c, elang, p ? p.ownRisk : 0);
    setDraft(c, { tmpl: key, elang, subject: e.subject, body: e.body });
  }
  async function draftWithAI(c) {
    if (deployBlocked("comms")) return;
    setB("draft", true);
    const d = getDraft(c);
    const ag = agentFor("comms");
    const fb = emailTemplate(d.tmpl === "blank" ? "ack" : d.tmpl, c, d.elang, (policyByPlate(c.plate) || {}).ownRisk);
    const out = await askClaudeJSON(
      (ag ? ag.prompt : TMPL.comms.prompt) + `\nTone: ${ag ? ag.tone : "friendly"}. Write in ${langName(d.elang)}.`,
      (d.body
        ? `Improve this draft e-mail (keep the intent, make it clearer and B1-level). Subject: ${d.subject}\nBody:\n${d.body}\n`
        : `Draft an e-mail to the ${d.to} about this claim.\n`) +
      `Claim: ${JSON.stringify({ ...c, timeline: undefined })}. Return JSON {"subject": string, "body": string}.`,
      fb
    );
    setDraft(c, { subject: out.subject || fb.subject, body: out.body || fb.body });
    logAudit("ai:comms", c.id, "e-mail drafted with AI");
    setB("draft", false);
  }
  function sendEmail(c) {
    const d = getDraft(c);
    if (!d.subject && !d.body) { toast(tt("Nothing to send.", "Niets te versturen.")); return; }
    const addr = recipientAddr(c, d.to);
    setSent(s => ({ ...s, [c.id]: [...(s[c.id] || []), { to: addr, subject: d.subject, d: now() }] }));
    addTimeline(c.id, `E-mail sent to ${addr}: "${d.subject}"`);
    logAudit("email", c.id, `sent to ${addr} · "${d.subject}"`);
    toast(tt("E-mail sent (simulated) to ", "E-mail verstuurd (gesimuleerd) naar ") + addr);
  }

  function markDocReceived(c, key) {
    setDocs(ds => ({ ...ds, [c.id]: ds[c.id].map(d => (d.key === key ? { ...d, received: true } : d)) }));
    addTimeline(c.id, `Document received · OCR extracted key fields from ${DOC_LABEL.en[key]}`);
    logAudit("ocr", c.id, `${DOC_LABEL.en[key]} received · OCR extraction logged`);
    if (c.status === "awaitingDocs") {
      updateClaim(c.id, { status: "assessment" });
      toast(tt("Document received — claim moved back to assessment.", "Document ontvangen — schade terug naar beoordeling."));
    } else toast(tt("Document marked received.", "Document als ontvangen gemarkeerd."));
  }

  async function runBench() {
    const ag = agents.find(a => a.id === benchAgent && isDeployed(a.id));
    const q = benchQ.trim();
    if (!q) return;
    if (!ag) { toast(tt("This agent is no longer available to you.", "Deze agent is niet meer voor u beschikbaar.")); return; }
    setB("bench", true); setBenchQ("");
    const a = await askClaude(agentDirective(ag), q)
      .catch(() => tt("Agent call failed.", "Agent-aanroep mislukt."));
    setBenchLog(x => [{ agent: ag.name, q, a }, ...x]);
    logAudit("ai:bench", ag.name, `test bench scenario run`);
    setB("bench", false);
  }

  async function runEval() {
    const ag = agentFor("fraud");
    if (!ag) { toast(tt("Activate a Fraud Screener agent first.", "Activeer eerst een Fraud Screener-agent.")); return; }
    setB("eval", true);
    const ids = ["CLM-2026-0102", "CLM-2026-0107", "CLM-2026-0108"];
    const rows = [];
    for (const id of ids) {
      const c = claims.find(x => x.id === id);
      if (!c) continue;
      const { fraud: hidden, ...blind } = c; // original score hidden from the model
      const out = await askClaudeJSON(
        agentDirective(ag),
        `Screen this historical claim. Claim file: ${JSON.stringify({ ...blind, timeline: undefined })}. Return JSON {"risk_score": number 0-100, "decision": string, "indicators": string[], "reasoning": string}.`,
        { risk_score: 50, indicators: [], reasoning: "fallback" }
      );
      const pred = Math.max(0, Math.min(100, Math.round(out.risk_score ?? 50)));
      rows.push({ id, expected: routeOf(hidden), predicted: routeOf(pred), score: pred, recorded: hidden });
    }
    const hits = rows.filter(r => r.expected === r.predicted).length;
    setEvalRes({ rows, acc: rows.length ? Math.round((hits / rows.length) * 100) : 0 });
    logAudit("ai:eval", "Fraud Screener", `evaluation run · ${hits}/${rows.length} routing matches`);
    setB("eval", false);
  }

  // ── dashboards ──
  function stat(k, v, sub) {
    return (
      <div className="stat" key={k}>
        <div className="k">{k}</div><div className="v">{v}</div>
        {sub && <div className="sub">{sub}</div>}
      </div>
    );
  }

  function claimRowsTable(list, extra) {
    return (
      <table className="tbl">
        <thead><tr>
          <th>{t("nav_claims")}</th><th>{t("claimant")}</th><th>{t("status")}</th>
          <th>{t("reserve")}</th><th>{t("due")}</th>{extra ? <th>{t("actions")}</th> : null}
        </tr></thead>
        <tbody>
          {list.map(c => (
            <tr key={c.id} className="click" onClick={() => openClaim(c.id)}>
              <td><div className="row" style={{ gap: 8 }}><Plate reg={c.plate} /><span className="mono small">{c.id}</span></div></td>
              <td>{c.claimant}<div className="small muted">{c.vehicle}</div></td>
              <td>{statusPill(c.status)}</td>
              <td className="mono">{fmt(c.reserve)}</td>
              <td className={c.due < TODAY && isOpen(c) ? "overdue" : ""}>{c.due}</td>
              {extra ? <td onClick={e => e.stopPropagation()}>{extra(c)}</td> : null}
            </tr>
          ))}
          {!list.length && <tr><td colSpan={extra ? 6 : 5} className="muted">{tt("Nothing here.", "Niets te tonen.")}</td></tr>}
        </tbody>
      </table>
    );
  }

  function renderHandlerDash() {
    const mine = claims.filter(c => c.handler === user.id && isOpen(c));
    const myTasks = tasks.filter(x => x.assignee === user.id && !x.done);
    return (
      <>
        <div className="stats">
          {stat(tt("Open claims", "Open schades"), mine.length)}
          {stat(tt("Tasks due today", "Taken voor vandaag"), myTasks.filter(x => x.due <= TODAY).length)}
          {stat(tt("Awaiting docs", "Wacht op documenten"), mine.filter(c => c.status === "awaitingDocs").length)}
          {stat(tt("Avg cycle time", "Gem. doorlooptijd"), tt("11.4 days", "11,4 dagen"))}
        </div>
        <div className="card"><h3>{tt("My queue", "Mijn werkvoorraad")}</h3>{claimRowsTable(mine)}</div>
        <div className="card">
          <h3>{tt("Open tasks", "Open taken")}</h3>
          {myTasks.map(x => (
            <div key={x.id} className="row" style={{ padding: "6px 0", borderBottom: "1px solid #edece5" }}>
              <input type="checkbox" style={{ width: 16 }} checked={x.done} onChange={() => toggleTask(x)} />
              <span style={{ flex: 1 }}>{x.title}</span>
              <button className="btn btn-sm mono" onClick={() => openClaim(x.claim)}>{x.claim}</button>
              <span className={"small " + (x.due < TODAY ? "overdue" : "muted")}>{x.due}</span>
            </div>
          ))}
          {!myTasks.length && <div className="muted small">{tt("No open tasks.", "Geen open taken.")}</div>}
        </div>
      </>
    );
  }

  function renderManagerDash() {
    const open = claims.filter(isOpen);
    const unassigned = open.filter(c => !c.handler);
    const approvals = open.filter(c => c.status === "pendingApproval");
    const sla = open.filter(c => daysOpen(c) >= 14);
    const triage = open.filter(c => c.fraud >= 25).sort((a, b) => b.fraud - a.fraud);
    const none = <div className="muted small">{tt("Nothing here.", "Niets te tonen.")}</div>;
    return (
      <>
        <div className="stats">
          {stat(tt("Unassigned intake", "Niet-toegewezen instroom"), unassigned.length)}
          {stat(tt("Approvals", "Akkoorden"), approvals.length)}
          {stat(tt("Open claims", "Open schades"), open.length)}
          {stat(tt("SLA watchlist", "SLA-bewaking"), sla.length,
            tt("Open 14+ days without payout", "14+ dagen open zonder uitbetaling"))}
        </div>
        <div className="dash2">
          <div>
            <div className="card">
              <h3 className="sect">{tt("Unassigned intake", "Niet-toegewezen instroom")}</h3>
              <table className="tbl">
                <thead><tr>
                  <th>{t("nav_claims")}</th><th>{t("type")}</th>
                  <th style={{ textAlign: "right" }}>{t("reserve")}</th><th>{tt("Assign to…", "Wijs toe aan…")}</th>
                </tr></thead>
                <tbody>
                  {unassigned.map(c => (
                    <tr key={c.id} className="click" onClick={() => openClaim(c.id)}>
                      <td>
                        <div className="row" style={{ gap: 8, flexWrap: "nowrap" }}>
                          <Plate reg={c.plate} /><span className="mono small">{c.id.slice(-4)}</span>
                        </div>
                      </td>
                      <td>{t("ty_" + c.type)}</td>
                      <td className="mono" style={{ textAlign: "right" }}>{fmt(c.reserve)}</td>
                      <td onClick={e => e.stopPropagation()}>
                        <select value="" onChange={e => assignClaim(c, e.target.value)}>
                          <option value="" disabled>{tt("Assign to…", "Wijs toe aan…")}</option>
                          {HANDLERS.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
                        </select>
                      </td>
                    </tr>
                  ))}
                  {!unassigned.length && (
                    <tr><td colSpan={4} className="muted">{tt("Nothing here.", "Niets te tonen.")}</td></tr>
                  )}
                </tbody>
              </table>
            </div>
            <div className="card">
              <h3 className="sect">{tt("Approvals", "Akkoorden")}</h3>
              <div className="small muted">{t("signoff")}</div>
              {approvals.map(c => (
                <div key={c.id} className="rowitem click" style={{ cursor: "pointer" }} onClick={() => openClaim(c.id)}>
                  <Plate reg={c.plate} />
                  <div style={{ flex: 1, minWidth: 150 }}>
                    <div className="b">{c.claimant} · {t("ty_" + c.type)}</div>
                    <div className="small muted">
                      <span className="mono">{c.id}</span> · {handlerName(c.handler)} ·{" "}
                      {tt("fraud indicator", "fraude-indicator")} {c.fraud}
                    </div>
                  </div>
                  <span className="mono b" style={{ fontSize: 15 }}>{fmt(c.reserve)}</span>
                  <span className="row" style={{ gap: 6, flexWrap: "nowrap" }} onClick={e => e.stopPropagation()}>
                    <button className="btn btn-p" onClick={() => decideApproval(c, true)}>{t("approve")}</button>
                    <button className="btn" onClick={() => decideApproval(c, false)}>{t("reject")}</button>
                  </span>
                </div>
              ))}
              {!approvals.length && none}
            </div>
          </div>
          <div>
            <div className="card">
              <h3 className="sect">{tt("Team workload", "Teambelasting")}</h3>
              {HANDLERS.map(h => {
                const n = open.filter(c => c.handler === h.id).length;
                const max = Math.max(1, ...HANDLERS.map(x => open.filter(c => c.handler === x.id).length));
                return (
                  <div key={h.id} className="row" style={{ marginBottom: 8, flexWrap: "nowrap" }}>
                    <span style={{ width: 118 }} className="small">{h.name}</span>
                    <span className="wlbar"><i style={{ width: (n / max) * 100 + "%" }} /></span>
                    <span className="mono small b">{n}</span>
                  </div>
                );
              })}
            </div>
            <div className="card">
              <h3 className="sect">{tt("Fraud triage", "Fraudetriage")}</h3>
              {triage.map(c => (
                <div key={c.id} className="rowitem click" style={{ cursor: "pointer" }} onClick={() => openClaim(c.id)}>
                  <Plate reg={c.plate} />
                  <span style={{ flex: 1, minWidth: 90 }} className="small">{c.claimant} · {t("ty_" + c.type)}</span>
                  <Meter v={c.fraud} />
                  {routePill(c.fraud)}
                </div>
              ))}
              {!triage.length && (
                <div className="muted small">{tt("No elevated-risk claims.", "Geen schades met verhoogd risico.")}</div>
              )}
            </div>
            <div className="card">
              <h3 className="sect">{tt("SLA watchlist", "SLA-bewaking")}</h3>
              {sla.map(c => (
                <div key={c.id} className="rowitem click" style={{ cursor: "pointer" }} onClick={() => openClaim(c.id)}>
                  <Plate reg={c.plate} />
                  <span style={{ flex: 1, minWidth: 90 }} className="small">{c.claimant} · {t("st_" + c.status)}</span>
                  <span className="mono small overdue">{c.opened}</span>
                </div>
              ))}
              {!sla.length && none}
            </div>
          </div>
        </div>
      </>
    );
  }

  function renderCfoDash() {
    const open = claims.filter(isOpen);
    const outstanding = open.reduce((s, c) => s + c.reserve, 0);
    const byType = ["collision", "theft", "glass", "vandalism", "storm"]
      .map(ty => ({ name: t("ty_" + ty), value: open.filter(c => c.type === ty).length, color: TYPE_COLOR[ty] }))
      .filter(x => x.value > 0);
    const largest = [...open].sort((a, b) => b.reserve - a.reserve).slice(0, 5);
    return (
      <>
        <div className="stats">
          {stat(tt("Outstanding reserves", "Uitstaande reserves"), fmt(outstanding))}
          {stat(tt("Paid this month", "Uitgekeerd deze maand"), fmt(341000))}
          {stat(tt("Loss ratio", "Schaderatio"), lang === "nl" ? "64,2%" : "64.2%")}
          {stat(tt("Avg cycle", "Gem. doorlooptijd"), tt("11.4 days", "11,4 dagen"))}
          {stat(tt("STP settled", "STP afgehandeld"), claims.filter(c => c.stp && c.status === "paid").length)}
        </div>
        <div className="grid2">
          <div className="card">
            <h3>{tt("Paid per month (€k)", "Uitgekeerd per maand (€k)")}</h3>
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={PAID_MONTHS}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e3e1da" />
                <XAxis dataKey="m" tick={{ fontSize: 12 }} /><YAxis tick={{ fontSize: 12 }} />
                <Tooltip formatter={v => "€" + v + "k"} />
                <Bar dataKey="v" fill="#d95d0f" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
          <div className="card">
            <h3>{tt("Open claims by type", "Open schades per type")}</h3>
            <ResponsiveContainer width="100%" height={220}>
              <PieChart>
                <Pie data={byType} dataKey="value" nameKey="name" innerRadius={55} outerRadius={85} paddingAngle={2}>
                  {byType.map(x => <Cell key={x.name} fill={x.color} />)}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
            <div className="row" style={{ justifyContent: "center" }}>
              {byType.map(x => <span key={x.name} className="small row" style={{ gap: 4 }}>
                <span style={{ width: 9, height: 9, borderRadius: 2, background: x.color, display: "inline-block" }} />{x.name} ({x.value})
              </span>)}
            </div>
          </div>
        </div>
        <div className="card">
          <h3>{tt("Reserve trend (€k)", "Reservetrend (€k)")}</h3>
          <ResponsiveContainer width="100%" height={200}>
            <LineChart data={RESERVE_TREND}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e3e1da" />
              <XAxis dataKey="m" tick={{ fontSize: 12 }} /><YAxis tick={{ fontSize: 12 }} domain={["auto", "auto"]} />
              <Tooltip formatter={v => "€" + v + "k"} />
              <Line type="monotone" dataKey="v" stroke="#16232e" strokeWidth={2} dot={{ r: 3 }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
        <div className="card"><h3>{tt("Largest open claims", "Grootste open schades")}</h3>{claimRowsTable(largest)}</div>
      </>
    );
  }

  function renderPlatformDash() {
    return (
      <>
        <div className="stats">
          {stat(tt("Active organizations", "Actieve organisaties"), orgs.filter(o => o.status === "active").length)}
          {stat(tt("Active users", "Actieve gebruikers"), pUsers.filter(u => u.status === "Active").length)}
          {stat(tt("Claims in system", "Schades in systeem"), claims.length)}
          {stat(tt("AI actions logged", "Gelogde AI-acties"), audit.filter(a => a.action.startsWith("ai:")).length)}
          {stat(tt("Active deployments", "Actieve uitrollen"), deployments.filter(d => d.status === "active").length)}
        </div>
        <div className="card">
          <h3>{tt("Organization overview", "Organisatie-overzicht")}</h3>
          <table className="tbl">
            <thead><tr><th>{tt("Organization", "Organisatie")}</th><th>Plan</th><th>{tt("Country", "Land")}</th><th>{t("status")}</th><th>{tt("Users", "Gebruikers")}</th></tr></thead>
            <tbody>{orgs.map(o => (
              <tr key={o.id}>
                <td><span className="b">{o.name}</span><div className="small muted">{o.email}</div></td>
                <td>{o.plan}</td><td>{o.country}</td>
                <td><Pill label={o.status} color={o.status === "active" ? "#2e7d5b" : "#c23b2e"} /></td>
                <td className="mono">{pUsers.filter(u => u.org === o.id).length}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </>
    );
  }

  function assignClaim(c, hid) {
    updateClaim(c.id, { handler: hid, status: c.status === "new" ? "assessment" : c.status });
    addTimeline(c.id, `Assigned to ${handlerName(hid)}`);
    logAudit("assign", c.id, `assigned to ${handlerName(hid)}`);
    toast(tt("Assigned to ", "Toegewezen aan ") + handlerName(hid));
  }
  function decideApproval(c, ok) {
    updateClaim(c.id, { status: ok ? "approved" : "rejected" });
    addTimeline(c.id, ok ? `Reserve ${fmt(c.reserve)} approved by manager` : "Claim rejected by manager");
    logAudit(ok ? "approve" : "reject", c.id, `${ok ? "approved" : "rejected"} at ${fmt(c.reserve)}`);
    toast((ok ? t("approve") : t("reject")) + ": " + c.id);
  }
  function toggleTask(x) {
    setTasks(ts => ts.map(y => (y.id === x.id ? { ...y, done: !y.done } : y)));
  }

  // ── claims module ──
  function renderClaims() {
    if (selClaim) return renderClaimDetail(claimById(selClaim));
    const q = search.toLowerCase();
    const list = claims.filter(c =>
      (!mineOnly || c.handler === user.id) &&
      (!q || [c.id, c.plate, c.claimant, c.vehicle].some(s => s.toLowerCase().includes(q)))
    );
    return (
      <>
        <div className="row" style={{ marginBottom: 14 }}>
          <input style={{ maxWidth: 300 }} placeholder={t("search") + " (id / kenteken / " + t("claimant").toLowerCase() + ")"} value={search} onChange={e => setSearch(e.target.value)} />
          {user.role === "handler" && (
            <label className="row small" style={{ gap: 6 }}>
              <input type="checkbox" style={{ width: 16 }} checked={mineOnly} onChange={e => setMineOnly(e.target.checked)} />
              {t("myOnly")}
            </label>
          )}
          <span style={{ flex: 1 }} />
          <button className="btn btn-p" onClick={() => setShowFnol(v => !v)}>{t("newClaim")}</button>
          <button className="btn" onClick={simulateClaim}>{t("simulate")}</button>
        </div>
        {showFnol && renderFnolForm()}
        <div className="card" style={{ padding: 0, overflowX: "auto" }}>
          <table className="tbl">
            <thead><tr>
              <th>{tt("Claim", "Schade")}</th><th>{t("claimant")}</th><th>{t("type")}</th><th>{t("status")}</th>
              <th>{t("riskRouting")}</th><th>{t("reserve")}</th><th>{t("handler")}</th><th>{t("due")}</th>
            </tr></thead>
            <tbody>
              {list.map(c => (
                <tr key={c.id} className="click" onClick={() => openClaim(c.id)}>
                  <td><div className="row" style={{ gap: 8, flexWrap: "nowrap" }}><Plate reg={c.plate} /><span className="mono small">{c.id}</span></div></td>
                  <td>{c.claimant}<div className="small muted">{c.vehicle}</div></td>
                  <td>{typePill(c.type)}</td>
                  <td>{statusPill(c.status)}</td>
                  <td><div className="row" style={{ gap: 6, flexWrap: "nowrap" }}><Meter v={c.fraud} />{routePill(c.fraud)}</div></td>
                  <td className="mono">{fmt(c.reserve)}</td>
                  <td className="small">{c.handler ? handlerName(c.handler) : <span className="muted">{t("unassigned")}</span>}</td>
                  <td className={c.due < TODAY && isOpen(c) ? "overdue" : ""}>{c.due}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </>
    );
  }

  function renderFnolForm() {
    return (
      <div className="card">
        <h3>{tt("New claim — first notification of loss (FNOL)", "Nieuwe schade — eerste schademelding (FNOL)")}</h3>
        <div className="grid2">
          <div>
            <span className="lbl">{tt("Channel", "Kanaal")}</span>
            <select value={fnol.channel} onChange={e => setFnol({ ...fnol, channel: e.target.value })}>
              {["App", "Web form", "WhatsApp", "Phone"].map(x => <option key={x}>{x}</option>)}
            </select>
            <span className="lbl">{tt("License plate (kenteken)", "Kenteken")}</span>
            <div className="row" style={{ flexWrap: "nowrap" }}>
              <input className="mono" placeholder="XX-999-X" value={fnol.plate}
                onChange={e => setFnol({ ...fnol, plate: e.target.value.toUpperCase() })} />
              <button className="btn" onClick={() => {
                if (!fnol.plate) return;
                const p = policyByPlate(fnol.plate.toUpperCase());
                setFnol(f => ({ ...f, vehicle: p ? p.vehicle : vehicleFromPlate(f.plate), claimant: f.claimant || (p ? p.holder : "") }));
                toast("RDW ✓ " + (policyByPlate(fnol.plate.toUpperCase()) || { vehicle: vehicleFromPlate(fnol.plate) }).vehicle);
              }}>RDW</button>
            </div>
            {fnol.vehicle && <div className="small muted" style={{ marginTop: 4 }}>RDW: {fnol.vehicle}</div>}
            <span className="lbl">{t("claimant")}</span>
            <input value={fnol.claimant} onChange={e => setFnol({ ...fnol, claimant: e.target.value })} />
            <span className="lbl">{t("city")}</span>
            <input value={fnol.city} onChange={e => setFnol({ ...fnol, city: e.target.value })} />
          </div>
          <div>
            <span className="lbl">{t("type")}</span>
            <select value={fnol.type} onChange={e => setFnol({ ...fnol, type: e.target.value })}>
              {Object.keys(TYPE_COLOR).map(ty => <option key={ty} value={ty}>{t("ty_" + ty)}</option>)}
            </select>
            <span className="lbl">{t("coverage")}</span>
            <select value={fnol.coverage} onChange={e => setFnol({ ...fnol, coverage: e.target.value })}>
              {["WA", "WA + Beperkt Casco", "Allrisk (Casco)"].map(x => <option key={x}>{x}</option>)}
            </select>
            <span className="lbl">{t("description")}</span>
            <textarea value={fnol.desc} onChange={e => setFnol({ ...fnol, desc: e.target.value })} />
          </div>
        </div>
        <div className="small muted" style={{ margin: "12px 0", padding: 10, background: "#faf9f4", borderRadius: 8, border: "1px solid var(--line)" }}>
          {legal.consent}
        </div>
        <button className="btn btn-p" disabled={!fnol.plate || !fnol.claimant} onClick={() => {
          createClaim(fnol); setFnol(emptyFnol); setShowFnol(false);
        }}>{tt("Register claim", "Schade registreren")}</button>
      </div>
    );
  }

  function simulateClaim() {
    const s = SCENARIOS[Math.floor(Math.random() * SCENARIOS.length)];
    createClaim(s);
  }

  function renderClaimDetail(c) {
    if (!c) return null;
    const p = policyByPlate(c.plate);
    const tabs = [
      ["assessment", tt("Assessment", "Beoordeling")], ["fraud", tt("Fraud screening", "Fraudescreening")],
      ["summary", tt("AI summary", "AI-samenvatting")], ["copilot", "Copilot"],
      ["emails", tt("Emails", "E-mails")], ["timeline", t("timeline")], ["docs", tt("Documents", "Documenten")],
    ];
    return (
      <>
        <button className="btn btn-sm" style={{ marginBottom: 12 }} onClick={() => setSelClaim(null)}>
          ← {tt("Back to claims", "Terug naar schades")}
        </button>
        <div className="claimwrap">
          <div className="airail">{renderAiRail(c, p)}</div>
          <div>
            <div className="card">
              <div className="row" style={{ alignItems: "flex-start", justifyContent: "space-between" }}>
                <div className="row" style={{ gap: 16, flexWrap: "nowrap" }}>
                  <Plate reg={c.plate} big />
                  <div>
                    <div className="mono small muted">{c.id}</div>
                    <div style={{ fontFamily: "Archivo", fontWeight: 800, fontSize: 22, margin: "2px 0 5px" }}>{c.claimant}</div>
                    <div className="small muted">
                      {c.vehicle} · {c.city} · {tt("Opened", "Gemeld")} {c.opened} · {tt("Due", "Deadline")}{" "}
                      <span className={c.due < TODAY && isOpen(c) ? "overdue" : ""}>{c.due}</span>
                    </div>
                  </div>
                </div>
                <div style={{ textAlign: "right" }}>
                  <div className="row" style={{ justifyContent: "flex-end", gap: 6 }}>
                    {statusPill(c.status)}<Pill label={c.coverage} color="#16232e" />{typePill(c.type)}
                    {c.stp && <Pill label="STP" color="#2e7d5b" />}
                  </div>
                  <div className="mono" style={{ fontFamily: "Archivo", fontWeight: 800, fontSize: 26, margin: "10px 0 8px" }}>
                    {fmt(c.reserve)}
                  </div>
                  <div className="row" style={{ justifyContent: "flex-end", gap: 8, flexWrap: "nowrap" }}>
                    <span className="small muted b">{tt("Fraud indicator", "Fraude-indicator")}</span>
                    <Meter v={c.fraud} />{routePill(c.fraud)}
                  </div>
                </div>
              </div>
            </div>
            <div className="card">
              <h3 className="sect">{tt("What happened", "Wat is er gebeurd")}</h3>
              <p style={{ fontSize: 14, lineHeight: 1.6 }}>{c.desc}</p>
              {p ? (
                <div className="polrow">
                  <span className="eyebrow">{t("policy")}</span>
                  <button className="lnk" onClick={() => openPolicy(p.no)}>{p.no}</button>
                  <Pill label={p.coverage} color="#16232e" />
                  <span className="small muted">{t("ownRisk")}: {fmt(p.ownRisk)} · {t("bm")}: {p.bm}</span>
                  <Pill label={p.status === "active" ? t("active") : p.status}
                    color={p.status === "active" ? "#2e7d5b" : "#c23b2e"} />
                </div>
              ) : (
                <div className="polrow b" style={{ color: "var(--red)" }}>{t("noPolicy")}</div>
              )}
            </div>
            <div className="tabs">
              {tabs.map(([k, label]) => (
                <button key={k} className={claimTab === k ? "on" : ""} onClick={() => setClaimTab(k)}>{label}</button>
              ))}
            </div>
            {claimTab === "assessment" && renderTabAssessment(c)}
            {claimTab === "fraud" && renderTabFraud(c)}
            {claimTab === "summary" && renderTabSummary(c)}
            {claimTab === "copilot" && renderTabCopilot(c)}
            {claimTab === "emails" && renderTabEmails(c)}
            {claimTab === "timeline" && renderTabTimeline(c)}
            {claimTab === "docs" && renderTabDocs(c)}
          </div>
        </div>
      </>
    );
  }

  // AI recommendation rail. ponytail: the verdict is a rule over claim state, not a model
  // call — the summariser agent (AI summary tab) supplies next_steps once it has run.
  function renderAiRail(c, p) {
    const a = getAssess(c);
    const missing = (docs[c.id] || []).filter(d => !d.received);
    const myTasks = tasks.filter(x => x.claim === c.id);
    const verdict = c.fraud > MAX_AUTO_FRAUD
      ? { tone: " stop", color: "var(--red)", head: tt("Human review required", "Mens vereist"),
          why: tt("The fraud indicator is above the automatic-handling threshold.",
                  "De fraude-indicator ligt boven de drempel voor automatische afhandeling.") }
      : c.reserve > MGR_RESERVE
        ? { tone: " warn", color: "var(--amber)", head: tt("Manager approval", "Akkoord manager"),
            why: tt("The reserve exceeds €10,000, so this claim needs a manager sign-off.",
                    "De reserve overschrijdt €10.000, dus deze schade vereist akkoord van een manager.") }
        : { tone: "", color: "var(--green)", head: tt("Approve (STP)", "Goedkeuren (STP)"),
            why: missing.length
              ? tt(`Once ${missing.length} open item(s) are cleared this claim qualifies for automatic approval.`,
                   `Na afhandeling van ${missing.length} openstaand(e) punt(en) komt deze schade in aanmerking voor automatische goedkeuring.`)
              : tt("Coverage, policy and fraud checks all pass — no manual gates left.",
                   "Dekking, polis en fraudecontroles zijn akkoord — geen handmatige poorten meer.") };
    // ponytail: confidence is a display heuristic (risk + open items), not a model probability.
    const confidence = Math.max(40, 100 - c.fraud - missing.length * 8);
    const aiSteps = ((aiOut[c.id] || {}).summary || {}).next_steps;
    const derived = [];
    if (!c.handler) derived.push(tt("Assign a handler", "Wijs een behandelaar toe"));
    missing.forEach(d => derived.push(tt("Request ", "Vraag op: ") + DOC_LABEL[lang][d.key]));
    if (c.status === "pendingApproval") derived.push(tt("Manager sign-off on the reserve", "Akkoord manager op de reserve"));
    if (c.status === "approved") derived.push(tt("Execute the SEPA payment", "Voer de SEPA-betaling uit"));
    if (isOpen(c)) derived.push(tt("Confirm the assessment to close the file", "Bevestig de beoordeling om het dossier te sluiten"));
    const steps = (aiSteps && aiSteps.length ? aiSteps : derived).slice(0, 4);
    const primary =
      c.status === "approved" && user.role !== "cfo"
        ? { label: t("executePayment"), fn: () => payClaim(c) }
        : c.status === "pendingApproval" && (user.role === "manager" || user.role === "admin")
          ? { label: t("approve"), fn: () => decideApproval(c, true) }
          : isOpen(c) && user.role !== "cfo"
            ? { label: tt("Approve & settle", "Goedkeuren & afwikkelen"),
                fn: () => { setAssess(x => ({ ...x, [c.id]: { ...a, decision: "approve" } })); saveAssessment(c, "approve"); } }
            : null;
    return (
      <>
        <div className={"aicard" + verdict.tone}>
          <div className="eyebrow">{tt("AI recommendation", "AI-aanbeveling")}</div>
          <div style={{ fontFamily: "Archivo", fontWeight: 800, fontSize: 20, color: verdict.color, margin: "6px 0 8px" }}>
            {verdict.head}
          </div>
          <div className="small" style={{ lineHeight: 1.5 }}>{verdict.why}</div>
          <div className="row" style={{ gap: 8, marginTop: 12, flexWrap: "nowrap" }}>
            <span className="small muted b" style={{ whiteSpace: "nowrap" }}>{tt("Confidence", "Besluitvertrouwen")}</span>
            <span className="meter" style={{ flex: 1 }}><i style={{ width: confidence + "%", background: verdict.color }} /></span>
            <span className="mono small b">{confidence}%</span>
          </div>
        </div>
        <div className="card">
          <div className="eyebrow">{tt("Proposal", "Voorstel")}</div>
          <div style={{ marginTop: 8 }}>
            <div className="kv"><span>{tt("Liability", "Aansprakelijkheid")}</span><span className="mono b">{a.liability}%</span></div>
            <div className="kv"><span>{t("ownRisk")}</span><span className="mono b">{p ? fmt(p.ownRisk) : "—"}</span></div>
            <div className="kv"><span>{t("reserve")}</span><span className="mono b">{fmt(a.estimate || c.reserve)}</span></div>
            <div className="kv">
              <span>{tt("Fraud score", "Fraude-score")}</span>
              <span className="mono b" style={{ color: meterColor(c.fraud) }}>{c.fraud} / {t("rt_" + routeOf(c.fraud))}</span>
            </div>
          </div>
        </div>
        <div className="card">
          <div className="eyebrow">{tt("Open items", "Taken")}</div>
          <div style={{ marginTop: 8 }}>
            {missing.map(d => (
              <label key={d.key} className="row small" style={{ gap: 8, padding: "5px 0", flexWrap: "nowrap" }}>
                <input type="checkbox" style={{ width: 15 }} checked={false} onChange={() => markDocReceived(c, d.key)} />
                {DOC_LABEL[lang][d.key]}
              </label>
            ))}
            {myTasks.map(x => (
              <label key={x.id} className="row small" style={{ gap: 8, padding: "5px 0", flexWrap: "nowrap" }}>
                <input type="checkbox" style={{ width: 15 }} checked={x.done} onChange={() => toggleTask(x)} />
                <span style={{ textDecoration: x.done ? "line-through" : "none" }}>{x.title}</span>
              </label>
            ))}
            {!missing.length && !myTasks.length && (
              <div className="muted small">{tt("Nothing outstanding.", "Niets openstaand.")}</div>
            )}
          </div>
        </div>
        <div className="card">
          <div className="eyebrow">{tt("Next actions", "Volgende acties")}</div>
          <div style={{ marginTop: 8 }}>
            {steps.map((x, i) => (
              <div key={i} className="row small" style={{ gap: 9, padding: "5px 0", alignItems: "flex-start", flexWrap: "nowrap" }}>
                <span className="num">{i + 1}</span><span>{x}</span>
              </div>
            ))}
          </div>
          {primary && (
            <button className={"btn " + (verdict.tone ? "btn-p" : "btn-g")} style={{ width: "100%", justifyContent: "center", marginTop: 14 }}
              onClick={primary.fn}>
              {primary.label} · {fmt(c.reserve)}
            </button>
          )}
          <div className="aiN">{tt("AI suggestion — the handler decides. Every action is logged for the EU AI Act trail.",
            "AI-suggestie — de behandelaar beslist. Elke actie wordt vastgelegd voor het EU AI Act-spoor.")}</div>
        </div>
      </>
    );
  }

  // ── claim tabs ──
  function renderTabAssessment(c) {
    const a = getAssess(c);
    const up = patch => setAssess(x => ({ ...x, [c.id]: { ...a, ...patch } }));
    const photo = (aiOut[c.id] || {}).photo;
    const covLabels = [
      tt("Policy active at loss date", "Polis actief op schadedatum"),
      tt("Premium paid", "Premie betaald"),
      tt("Damage within cover", "Schade binnen dekking"),
    ];
    const decisions = [
      ["approve", tt("Approve for payment", "Goedkeuren voor betaling")],
      ["docs", tt("Request more documents", "Meer documenten opvragen")],
      ["reject", tt("Reject claim", "Schade afwijzen")],
    ];
    return (
      <>
        <div className="card">
          <div className="grid2">
            <div>
              <span className="flbl" style={{ marginTop: 0 }}>
                {tt("Liability of insured", "Aansprakelijkheid verzekerde")} — {a.liability}%
              </span>
              <input type="range" min="0" max="100" value={a.liability} onChange={e => up({ liability: Number(e.target.value) })} />
            </div>
            <div>
              <span className="flbl" style={{ marginTop: 0 }}>{tt("Damage estimate (€)", "Schatting schadebedrag (€)")}</span>
              <input type="number" className="mono" value={a.estimate} onChange={e => up({ estimate: e.target.value })} />
            </div>
          </div>
          <span className="flbl">{tt("Coverage checks", "Dekkingscontroles")}</span>
          <div className="optrow">
            {covLabels.map((lbl, i) => (
              <label key={i}>
                <input type="checkbox" checked={a["cov" + (i + 1)]} onChange={e => up({ ["cov" + (i + 1)]: e.target.checked })} />
                {lbl}
              </label>
            ))}
          </div>
          <span className="flbl">{tt("Decision", "Besluit")}</span>
          <div className="optrow">
            {decisions.map(([v, lbl]) => (
              <label key={v}>
                <input type="radio" checked={a.decision === v} onChange={() => up({ decision: v })} />
                {lbl}
              </label>
            ))}
          </div>
          <span className="flbl">{tt("Assessment notes", "Beoordelingsnotities")}</span>
          <textarea value={a.notes} onChange={e => up({ notes: e.target.value })} />
          <button className="btn btn-p" style={{ marginTop: 14 }} onClick={() => saveAssessment(c)}>
            {tt("Save assessment", "Beoordeling opslaan")}
          </button>
          <div className="aiN">{tt("Approvals route to a manager when the reserve exceeds €10,000 or the fraud score exceeds 60.",
            "Goedkeuringen gaan naar een manager bij een reserve boven €10.000 of een fraudescore boven 60.")}</div>
        </div>
        <div className="card">
          <h3 className="sect">{tt("AI photo damage scan", "AI-fotoschadescan")}</h3>
          <button className="btn" disabled={busy.photo} onClick={() => runPhotoScan(c)}>
            {busy.photo ? <span className="spin" /> : "📷"} {tt("Analyse claim photos", "Analyseer schadefoto's")}
          </button>
          {photo && (
            <div style={{ marginTop: 12 }}>
              <table className="tbl">
                <thead><tr><th>{tt("Part", "Onderdeel")}</th><th>{tt("Action", "Actie")}</th><th>{tt("Cost", "Kosten")}</th></tr></thead>
                <tbody>
                  {(photo.parts || []).map((x, i) => (
                    <tr key={i}><td>{x.part}</td>
                      <td><Pill label={x.action} color={x.action === "replace" ? "#c23b2e" : "#2e7d5b"} /></td>
                      <td className="mono">{fmt(x.cost || 0)}</td></tr>
                  ))}
                  <tr><td className="b">{tt("Total", "Totaal")}</td><td /><td className="mono b">{fmt(photo.total || 0)}</td></tr>
                </tbody>
              </table>
              <div className="row" style={{ marginTop: 10 }}>
                <Pill label={photo.verdict === "total_loss" ? tt("Total loss", "Total loss") : tt("Repairable", "Herstelbaar")}
                  color={photo.verdict === "total_loss" ? "#c23b2e" : "#2e7d5b"} />
                <span className="small muted">{tt("Confidence", "Zekerheid")}: {Math.round((photo.confidence || 0) * 100)}%</span>
                <button className="btn btn-sm" onClick={() => { setAssess(x => ({ ...x, [c.id]: { ...a, estimate: photo.total } })); toast(tt("Estimate applied.", "Schatting overgenomen.")); }}>
                  {tt("Apply to damage estimate", "Overnemen als schadebedrag")}
                </button>
              </div>
            </div>
          )}
          <div className="aiN">{tt("AI output — verify against the actual photos before relying on it.", "AI-resultaat — controleer aan de hand van de echte foto's voordat u erop vertrouwt.")}</div>
        </div>
      </>
    );
  }

  function renderTabFraud(c) {
    const out = (aiOut[c.id] || {}).fraud;
    return (
      <div className="grid2">
        <div className="card">
          <h3>{tt("Fraud risk", "Frauderisico")}</h3>
          <div className="row" style={{ gap: 18 }}>
            <div style={{ fontFamily: "Archivo", fontWeight: 800, fontSize: 44, color: meterColor(c.fraud) }}>{c.fraud}</div>
            <div>
              {routePill(c.fraud)}
              <div className="small muted" style={{ marginTop: 6 }}>0–24 {t("rt_auto").toLowerCase()} · 25–60 {t("rt_manual").toLowerCase()} · 61+ {t("rt_human").toLowerCase()}</div>
            </div>
          </div>
          <div className="meter" style={{ width: "100%", height: 10, margin: "14px 0" }}>
            <i style={{ width: c.fraud + "%", background: meterColor(c.fraud) }} />
          </div>
          {c.cis && (
            <>
              <span className="lbl">{tt("CIS network signals", "CIS-netwerksignalen")}</span>
              <div>{c.cis.map((s, i) => <span key={i} className="chip">⚠ {s}</span>)}</div>
            </>
          )}
          <button className="btn btn-p" style={{ marginTop: 14 }} disabled={busy.fraud} onClick={() => runFraud(c)}>
            {busy.fraud ? <span className="spin" /> : "🔎"} {tt("Run AI fraud screening", "Start AI-fraudescreening")}
          </button>
          <div className="aiN">{tt("Screening flags risk for human investigation — it never proves fraud.",
            "Screening signaleert risico voor menselijk onderzoek — het bewijst nooit fraude.")}</div>
        </div>
        <div className="card">
          <h3>{tt("Screening result", "Screeningsresultaat")}</h3>
          {out ? (
            <>
              <div className="row"><span className="b">{tt("Score", "Score")}:</span><Meter v={out.risk_score} /><Pill label={out.decision} color={meterColor(out.risk_score)} /></div>
              <span className="lbl">{tt("Indicators", "Indicatoren")}</span>
              <div>{(out.indicators || []).map((s, i) => <span key={i} className="chip">{s}</span>)}</div>
              <span className="lbl">{tt("Reasoning", "Onderbouwing")}</span>
              <p className="small" style={{ lineHeight: 1.55, whiteSpace: "pre-wrap" }}>{out.reasoning}</p>
            </>
          ) : <div className="muted small">{tt("No screening run yet in this session.", "Nog geen screening uitgevoerd in deze sessie.")}</div>}
        </div>
      </div>
    );
  }

  function renderTabSummary(c) {
    const out = (aiOut[c.id] || {}).summary;
    return (
      <div className="card">
        <div className="row" style={{ justifyContent: "space-between" }}>
          <h3>{tt("AI summary", "AI-samenvatting")}</h3>
          <button className="btn" disabled={busy.summary} onClick={() => runSummary(c)}>
            {busy.summary ? <span className="spin" /> : "✨"} {out ? tt("Regenerate", "Opnieuw genereren") : tt("Generate", "Genereren")}
          </button>
        </div>
        {out ? (
          <>
            <p style={{ fontSize: 13.5, lineHeight: 1.6, margin: "8px 0" }}>{out.summary}</p>
            {!!(out.risk_flags || []).length && <>
              <span className="lbl">{tt("Risk flags", "Risicosignalen")}</span>
              <div>{out.risk_flags.map((s, i) => <span key={i} className="chip">⚑ {s}</span>)}</div>
            </>}
            <span className="lbl">{tt("Recommendation", "Aanbeveling")}</span>
            <p className="small b">{out.recommendation}</p>
            <span className="lbl">{tt("Next steps", "Vervolgstappen")}</span>
            <ol style={{ paddingLeft: 20, fontSize: 13 }}>{(out.next_steps || []).map((s, i) => <li key={i} style={{ padding: "2px 0" }}>{s}</li>)}</ol>
          </>
        ) : <div className="muted small">{tt("Generate a summary of the claim file.", "Genereer een samenvatting van het dossier.")}</div>}
        <div className="aiN">{tt("AI-generated — verify before external use.", "AI-gegenereerd — controleer vóór extern gebruik.")}</div>
      </div>
    );
  }

  function renderTabCopilot(c) {
    const msgs = chats[c.id] || [];
    return (
      <div className="card">
        <h3>{tt("Claim copilot", "Dossier-copiloot")}</h3>
        <div className="chat">
          {msgs.map((m, i) => (
            <div key={i} style={{ display: "contents" }}>
              <div className="bub q">{m.q}</div>
              <div className="bub a">{m.a}</div>
            </div>
          ))}
          {!msgs.length && <div className="muted small">{tt("Ask anything about this claim file — answers are grounded strictly in the file.",
            "Stel elke vraag over dit dossier — antwoorden komen uitsluitend uit het dossier.")}</div>}
          {busy.copilot && <div className="bub a"><span className="spin" /></div>}
        </div>
        <div className="row" style={{ flexWrap: "nowrap" }}>
          <input placeholder={tt("e.g. Is the own risk already applied?", "bijv. Is het eigen risico al toegepast?")}
            value={copilotQ} onChange={e => setCopilotQ(e.target.value)}
            onKeyDown={e => e.key === "Enter" && askCopilot(c)} />
          <button className="btn btn-p" disabled={busy.copilot} onClick={() => askCopilot(c)}>{t("send")}</button>
        </div>
      </div>
    );
  }

  // ponytail: same askCopilot as the Copilot tab — the bubble only exists while a
  // claim is open, so "context" is just the claim the user is already looking at.
  function renderBubble() {
    const c = selClaim && page === "claims" ? claimById(selClaim) : null;
    if (!c) return null;
    const msgs = chats[c.id] || [];
    return (
      <>
        {bubble && (
          <div className="cbox">
            <div className="row" style={{ flexWrap: "nowrap", marginBottom: 10 }}>
              <Plate reg={c.plate} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div className="b small">{tt("Claim copilot", "Dossier-copiloot")}</div>
                <div className="mono small muted">{c.id}</div>
              </div>
              <button className="btn btn-sm" onClick={() => setBubble(false)}>✕</button>
            </div>
            <div className="chat">
              {msgs.map((m, i) => (
                <div key={i} style={{ display: "contents" }}>
                  <div className="bub q">{m.q}</div>
                  <div className="bub a">{m.a}</div>
                </div>
              ))}
              {!msgs.length && (
                <div className="muted small">{tt("Ask anything about this claim file — answers are grounded strictly in the file.",
                  "Stel elke vraag over dit dossier — antwoorden komen uitsluitend uit het dossier.")}</div>
              )}
              {busy.copilot && <div className="bub a"><span className="spin" /></div>}
            </div>
            <div className="row" style={{ flexWrap: "nowrap" }}>
              <input autoFocus placeholder={tt("Ask about this claim…", "Vraag iets over dit dossier…")}
                value={copilotQ} onChange={e => setCopilotQ(e.target.value)}
                onKeyDown={e => e.key === "Enter" && askCopilot(c)} />
              <button className="btn btn-p" disabled={busy.copilot} onClick={() => askCopilot(c)}>{t("send")}</button>
            </div>
          </div>
        )}
        <button className="fab" title={tt("Claim copilot", "Dossier-copiloot")} onClick={() => setBubble(v => !v)}>
          {bubble ? "✕" : "✨"}
        </button>
      </>
    );
  }

  function renderTabTimeline(c) {
    const items = [...c.timeline].reverse();
    return (
      <div className="card">
        <h3>{t("timeline")}</h3>
        <ul className="tl">
          {items.map((e, i) => (
            <li key={i}>
              <div className="small muted mono">{e.d} · {e.who}</div>
              <div style={{ fontSize: 13.5 }}>{e.ev}</div>
            </li>
          ))}
        </ul>
      </div>
    );
  }

  function renderTabDocs(c) {
    const list = docs[c.id] || [];
    return (
      <div className="card">
        <h3>{tt("Documents", "Documenten")}</h3>
        <table className="tbl">
          <thead><tr><th>{tt("Document", "Document")}</th><th>{t("status")}</th><th>{t("actions")}</th></tr></thead>
          <tbody>
            {list.map(d => (
              <tr key={d.key}>
                <td>{DOC_LABEL[lang][d.key]}</td>
                <td><Pill label={d.received ? tt("Received", "Ontvangen") : tt("Missing", "Ontbreekt")} color={d.received ? "#2e7d5b" : "#c23b2e"} /></td>
                <td>{!d.received && (
                  <button className="btn btn-sm" onClick={() => markDocReceived(c, d.key)}>
                    {tt("Mark received (OCR)", "Markeer ontvangen (OCR)")}
                  </button>
                )}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  function renderTabEmails(c) {
    const d = getDraft(c);
    const sentList = sent[c.id] || [];
    const recips = [
      ["claimant", t("claimant")], ["repair", tt("Repair shop", "Herstelbedrijf")],
      ["internal", tt("Internal team", "Intern team")], ["counterpart", tt("Counterpart insurer", "Verzekeraar tegenpartij")],
    ];
    const tmpls = [
      ["ack", tt("Acknowledge", "Ontvangstbevestiging")], ["docs", tt("Request documents", "Documenten opvragen")],
      ["payout", tt("Payout approved", "Uitkering goedgekeurd")], ["repair", tt("Repair authorisation", "Reparatie-autorisatie")],
      ["handover", tt("Internal handover", "Interne overdracht")], ["blank", tt("Blank", "Leeg")],
    ];
    return (
      <div className="grid2">
        <div className="card">
          <h3>{tt("Compose e-mail", "E-mail opstellen")}</h3>
          <span className="lbl">{tt("Recipient", "Ontvanger")}</span>
          <select value={d.to} onChange={e => setDraft(c, { to: e.target.value })}>
            {recips.map(([v, lbl]) => <option key={v} value={v}>{lbl} — {recipientAddr(c, v)}</option>)}
          </select>
          <div className="grid2" style={{ gap: 10 }}>
            <div>
              <span className="lbl">{tt("Template", "Sjabloon")}</span>
              <select value={d.tmpl} onChange={e => applyTemplate(c, e.target.value, d.elang)}>
                {tmpls.map(([v, lbl]) => <option key={v} value={v}>{lbl}</option>)}
              </select>
            </div>
            <div>
              <span className="lbl">{tt("E-mail language", "Taal e-mail")}</span>
              <select value={d.elang} onChange={e => (d.tmpl !== "blank" ? applyTemplate(c, d.tmpl, e.target.value) : setDraft(c, { elang: e.target.value }))}>
                <option value="en">English</option><option value="nl">Nederlands</option>
              </select>
            </div>
          </div>
          <span className="lbl">{tt("Subject", "Onderwerp")}</span>
          <input value={d.subject} onChange={e => setDraft(c, { subject: e.target.value })} />
          <span className="lbl">{tt("Body", "Bericht")}</span>
          <textarea style={{ minHeight: 180 }} value={d.body} onChange={e => setDraft(c, { body: e.target.value })} />
          <div className="row" style={{ marginTop: 12 }}>
            <button className="btn" disabled={busy.draft} onClick={() => draftWithAI(c)}>
              {busy.draft ? <span className="spin" /> : "✨"} {tt("Draft with AI", "Opstellen met AI")}
            </button>
            <button className="btn btn-p" onClick={() => sendEmail(c)}>{t("send")}</button>
          </div>
          <div className="aiN">{tt("Sending is simulated in this demo — no real e-mail leaves the system.",
            "Versturen is gesimuleerd in deze demo — er verlaat geen echte e-mail het systeem.")}</div>
        </div>
        <div className="card">
          <h3>{tt("Sent", "Verzonden")}</h3>
          {sentList.length ? (
            <table className="tbl">
              <thead><tr><th>{tt("To", "Aan")}</th><th>{tt("Subject", "Onderwerp")}</th><th>{tt("When", "Wanneer")}</th></tr></thead>
              <tbody>{sentList.map((m, i) => (
                <tr key={i}><td className="mono small">{m.to}</td><td className="small">{m.subject}</td><td className="mono small">{m.d}</td></tr>
              ))}</tbody>
            </table>
          ) : <div className="muted small">{tt("No e-mails sent for this claim yet.", "Nog geen e-mails verzonden voor deze schade.")}</div>}
        </div>
      </div>
    );
  }

  // ── tasks ──
  function renderTasks() {
    const list = user.role === "manager" ? tasks : tasks.filter(x => x.assignee === user.id);
    return (
      <>
        <div className="card">
          <h3>{t("nav_tasks")}</h3>
          <table className="tbl">
            <thead><tr><th /><th>{tt("Task", "Taak")}</th><th>{tt("Claim", "Schade")}</th><th>{t("assignee")}</th><th>{t("due")}</th></tr></thead>
            <tbody>{list.map(x => (
              <tr key={x.id} style={x.done ? { opacity: 0.5 } : {}}>
                <td><input type="checkbox" style={{ width: 16 }} checked={x.done} onChange={() => toggleTask(x)} /></td>
                <td style={x.done ? { textDecoration: "line-through" } : {}}>{x.title}</td>
                <td><button className="btn btn-sm mono" onClick={() => openClaim(x.claim)}>{x.claim}</button></td>
                <td className="small">{handlerName(x.assignee)}</td>
                <td className={!x.done && x.due < TODAY ? "overdue" : ""}>{x.due}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
        <div className="card">
          <h3>{t("addTask")}</h3>
          <div className="row">
            <input style={{ flex: 2, minWidth: 180 }} placeholder={tt("Task title", "Titel taak")} value={taskForm.title}
              onChange={e => setTaskForm({ ...taskForm, title: e.target.value })} />
            <select style={{ flex: 1, minWidth: 150 }} value={taskForm.claim} onChange={e => setTaskForm({ ...taskForm, claim: e.target.value })}>
              <option value="">{tt("Claim…", "Schade…")}</option>
              {claims.filter(isOpen).map(c => <option key={c.id} value={c.id}>{c.id}</option>)}
            </select>
            {user.role === "manager" && (
              <select style={{ flex: 1, minWidth: 140 }} value={taskForm.assignee} onChange={e => setTaskForm({ ...taskForm, assignee: e.target.value })}>
                <option value="">{t("assignee")}…</option>
                {HANDLERS.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
              </select>
            )}
            <button className="btn btn-p" disabled={!taskForm.title || !taskForm.claim} onClick={() => {
              const id = "t" + Math.random().toString(36).slice(2, 7);
              setTasks(ts => [...ts, { id, claim: taskForm.claim, title: taskForm.title, due: "2026-08-23", assignee: taskForm.assignee || user.id, done: false }]);
              setTaskForm({ title: "", claim: "", assignee: "" });
              toast(tt("Task added.", "Taak toegevoegd."));
            }}>{t("addTask")}</button>
          </div>
        </div>
      </>
    );
  }

  // ── policies ──
  function renderPolicies() {
    if (selPolicy) return renderPolicyDetail(policies.find(p => p.no === selPolicy));
    const readOnly = user.role === "cfo";
    const q = polSearch.toLowerCase();
    const list = policies.filter(p => !q || [p.no, p.plate, p.holder, p.vehicle].some(s => s.toLowerCase().includes(q)));
    const act = policies.filter(p => p.status === "active");
    const gwp = act.reduce((s, p) => s + p.premium * 12, 0);
    return (
      <>
        <div className="stats">
          {stat(tt("Active policies", "Actieve polissen"), act.length)}
          {stat(tt("Annual GWP", "Jaarlijkse bruto premie"), fmt(gwp))}
          {stat(tt("Avg premium / month", "Gem. premie / maand"), fmt(act.length ? gwp / 12 / act.length : 0))}
        </div>
        <div className="row" style={{ marginBottom: 14 }}>
          <input style={{ maxWidth: 300 }} placeholder={t("search")} value={polSearch} onChange={e => setPolSearch(e.target.value)} />
          <span style={{ flex: 1 }} />
          {!readOnly && <button className="btn btn-p" onClick={() => setShowPolForm(v => !v)}>{tt("＋ New policy", "＋ Nieuwe polis")}</button>}
        </div>
        {showPolForm && !readOnly && renderPolicyForm()}
        <div className="card" style={{ padding: 0, overflowX: "auto" }}>
          <table className="tbl">
            <thead><tr>
              <th>{t("policy")}</th><th>{t("holder")}</th><th>{t("coverage")}</th><th>{t("premium")}</th>
              <th>{t("ownRisk")}</th><th>{t("bm")}</th><th>{t("renewal")}</th><th>{t("status")}</th>
            </tr></thead>
            <tbody>{list.map(p => (
              <tr key={p.no} className="click" onClick={() => setSelPolicy(p.no)}>
                <td><div className="row" style={{ gap: 8, flexWrap: "nowrap" }}><Plate reg={p.plate} /><span className="mono small">{p.no}</span></div></td>
                <td>{p.holder}<div className="small muted">{p.vehicle}</div></td>
                <td className="small">{p.coverage}</td>
                <td className="mono">{fmt(p.premium)}/m</td>
                <td className="mono">{fmt(p.ownRisk)}</td>
                <td className="mono">{p.bm}</td>
                <td>{p.renewal}</td>
                <td><Pill label={p.status} color={p.status === "active" ? "#2e7d5b" : p.status === "suspended" ? "#b58a1f" : "#c23b2e"} /></td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </>
    );
  }

  function renderPolicyForm() {
    return (
      <div className="card">
        <h3>{tt("New policy", "Nieuwe polis")}</h3>
        <div className="row">
          <div style={{ flex: 1, minWidth: 150 }}>
            <span className="lbl">{tt("Kenteken", "Kenteken")}</span>
            <div className="row" style={{ flexWrap: "nowrap" }}>
              <input className="mono" value={polForm.plate} onChange={e => setPolForm({ ...polForm, plate: e.target.value.toUpperCase() })} />
              <button className="btn" onClick={() => setPolForm(f => ({ ...f, vehicle: vehicleFromPlate(f.plate) }))}>RDW</button>
            </div>
            {polForm.vehicle && <div className="small muted">{polForm.vehicle}</div>}
          </div>
          <div style={{ flex: 1, minWidth: 150 }}><span className="lbl">{t("holder")}</span>
            <input value={polForm.holder} onChange={e => setPolForm({ ...polForm, holder: e.target.value })} /></div>
          <div style={{ flex: 1, minWidth: 170 }}><span className="lbl">{t("coverage")}</span>
            <select value={polForm.coverage} onChange={e => setPolForm({ ...polForm, coverage: e.target.value })}>
              {["WA", "WA + Beperkt Casco", "Allrisk (Casco)"].map(x => <option key={x}>{x}</option>)}
            </select></div>
          <div style={{ width: 110 }}><span className="lbl">{t("premium")} €/m</span>
            <input type="number" className="mono" value={polForm.premium} onChange={e => setPolForm({ ...polForm, premium: e.target.value })} /></div>
          <div style={{ width: 110 }}><span className="lbl">{t("ownRisk")}</span>
            <select value={polForm.ownRisk} onChange={e => setPolForm({ ...polForm, ownRisk: Number(e.target.value) })}>
              {[0, 150, 350, 500].map(x => <option key={x} value={x}>€ {x}</option>)}
            </select></div>
          <div style={{ width: 90 }}><span className="lbl">{t("bm")}</span>
            <input type="number" className="mono" value={polForm.bm} onChange={e => setPolForm({ ...polForm, bm: e.target.value })} /></div>
        </div>
        <button className="btn btn-p" style={{ marginTop: 12 }} disabled={!polForm.plate || !polForm.holder} onClick={() => {
          const no = `POL-2026-${String(11900 + policies.length)}`;
          const p = { no, plate: polForm.plate, holder: polForm.holder, vehicle: polForm.vehicle || vehicleFromPlate(polForm.plate), coverage: polForm.coverage, premium: Number(polForm.premium) || 0, ownRisk: polForm.ownRisk, start: TODAY, renewal: "2027-08-16", bm: Number(polForm.bm) || 0, status: "active" };
          setPolicies(ps => [p, ...ps]);
          logAudit("policy", no, `policy created for ${p.plate} · ${p.coverage}`);
          setPolForm(emptyPol); setShowPolForm(false);
          toast(tt("Policy created: ", "Polis aangemaakt: ") + no);
        }}>{t("save")}</button>
      </div>
    );
  }

  function renderPolicyDetail(p) {
    if (!p) return null;
    const linked = claims.filter(c => c.plate === p.plate);
    const readOnly = user.role === "cfo";
    return (
      <>
        <button className="btn btn-sm" style={{ marginBottom: 12 }} onClick={() => setSelPolicy(null)}>← {t("back")}</button>
        <div className="card">
          <div className="row" style={{ justifyContent: "space-between", alignItems: "flex-start" }}>
            <div className="row" style={{ gap: 12 }}>
              <Plate reg={p.plate} big />
              <div>
                <div className="mono b" style={{ fontSize: 16 }}>{p.no}</div>
                <div className="b">{p.holder}</div>
                <div className="small muted">{p.vehicle}</div>
              </div>
            </div>
            <div style={{ textAlign: "right" }}>
              <span className="lbl">{t("status")}</span>
              {readOnly ? <Pill label={p.status} color={p.status === "active" ? "#2e7d5b" : "#c23b2e"} /> : (
                <select style={{ width: 150 }} value={p.status} onChange={e => {
                  setPolicies(ps => ps.map(x => (x.no === p.no ? { ...x, status: e.target.value } : x)));
                  logAudit("policy", p.no, `status changed to ${e.target.value}`);
                  toast(tt("Policy status: ", "Polisstatus: ") + e.target.value);
                }}>
                  {["active", "suspended", "lapsed", "cancelled"].map(s => <option key={s} value={s}>{s}</option>)}
                </select>
              )}
            </div>
          </div>
          <div className="row" style={{ marginTop: 14, gap: 24 }}>
            <span className="small"><span className="muted">{t("coverage")}:</span> <span className="b">{p.coverage}</span></span>
            <span className="small"><span className="muted">{t("premium")}:</span> <span className="mono b">{fmt(p.premium)}/m</span></span>
            <span className="small"><span className="muted">{t("ownRisk")}:</span> <span className="mono b">{fmt(p.ownRisk)}</span></span>
            <span className="small"><span className="muted">{t("bm")}:</span> <span className="mono b">{p.bm}</span></span>
            <span className="small"><span className="muted">{tt("Start", "Ingang")}:</span> {p.start}</span>
            <span className="small"><span className="muted">{t("renewal")}:</span> {p.renewal}</span>
          </div>
        </div>
        <div className="card">
          <h3>{tt("Linked claims", "Gekoppelde schades")}</h3>
          {claimRowsTable(linked)}
        </div>
      </>
    );
  }

  // ── agent studio ──
  const STEP_LABEL = k => ({
    intake: tt("FNOL intake & data check", "FNOL-intake & datacontrole"),
    coverage: tt("Coverage check", "Dekkingscontrole"),
    fraud: tt("Fraud screening", "Fraudescreening"),
    reserve: tt("Reserve proposal", "Reservevoorstel"),
    decision: tt("Decision & routing", "Besluit & routering"),
    comms: tt("Customer communication", "Klantcommunicatie"),
    payment: tt("Payment & settlement", "Betaling & afwikkeling"),
  }[k]);

  function moveStep(i, dir) {
    setSteps(ss => {
      const j = i + dir;
      if (j < 0 || j >= ss.length) return ss;
      const next = [...ss];
      [next[i], next[j]] = [next[j], next[i]];
      return next;
    });
  }

  function renderStudio() {
    if (user.role === "admin") return renderAdminStudio();
    const openClaims = claims.filter(isOpen);
    const visAgents = agents.filter(a => isDeployed(a.id));
    const stepIcon = s => s === "running" ? <span className="spin" /> :
      s === "done" ? "✅" : s === "fail" ? "🟥" : s === "skip" ? "⏭" : "•";
    return (
      <div className="studio-grid">
        <div>
          <div className="card">
            <h3>{tt("End-to-end claims pipeline", "End-to-end schadepipeline")}</h3>
            <div className="small muted" style={{ marginBottom: 10 }}>
              {tt("Orchestrated agents run the claims process from FNOL to settlement. Pause an agent and its step is skipped.",
                  "Georkestreerde agents draaien het schadeproces van FNOL tot afwikkeling. Pauzeer een agent en zijn stap wordt overgeslagen.")}
            </div>
            <div className="row" style={{ marginBottom: 12 }}>
              <select style={{ maxWidth: 260 }} value={pipeClaim} onChange={e => setPipeClaim(e.target.value)}>
                <option value="">{tt("Choose an open claim…", "Kies een open schade…")}</option>
                {openClaims.map(c => <option key={c.id} value={c.id}>{c.id} · {c.plate} · {t("ty_" + c.type)}</option>)}
              </select>
              <select style={{ maxWidth: 230 }} value={pipeMode} onChange={e => setPipeMode(e.target.value)}>
                <option value="suggest">{tt("Suggest only", "Alleen voorstellen")}</option>
                <option value="approval">{tt("Act with approval", "Handelen met akkoord")}</option>
                <option value="auto">{tt("Fully autonomous", "Volledig autonoom")}</option>
              </select>
              <button className="btn btn-p" disabled={!pipeClaim} onClick={() => runPipeline(claimById(pipeClaim), pipeMode)}>
                ▶ {t("runPipeline")}
              </button>
            </div>
            {steps.map((s, i) => {
              const need = PIPE_AGENT[s.key];
              const ag = need ? agentFor(need) : null;
              const undeployed = need && !ag && agents.some(a => a.tmpl === need && a.active);
              return (
                <div key={s.key} className="steprow" style={{ opacity: s.on ? 1 : 0.45 }}>
                  <span className="mono small muted">{i + 1}</span>
                  <div style={{ flex: 1 }}>
                    <div className="b" style={{ fontSize: 13 }}>{STEP_LABEL(s.key)}</div>
                    <div className="small muted">
                      {need ? (ag ? "🤖 " + ag.name
                        : undeployed ? "⚠ " + tt("not deployed to your organization", "niet uitgerold naar uw organisatie")
                        : "⚠ " + tt("no active agent", "geen actieve agent")) : tt("System rule", "Systeemregel")}
                    </div>
                  </div>
                  <button className="btn btn-sm" onClick={() => moveStep(i, -1)}>↑</button>
                  <button className="btn btn-sm" onClick={() => moveStep(i, 1)}>↓</button>
                  <button className="btn btn-sm" onClick={() => setSteps(ss => ss.map(x => x.key === s.key ? { ...x, on: !x.on } : x))}>
                    {s.on ? tt("On", "Aan") : tt("Off", "Uit")}
                  </button>
                </div>
              );
            })}
            {runLog && (
              <div style={{ marginTop: 14, background: "#faf9f4", border: "1px solid var(--line)", borderRadius: 10, padding: 12 }}>
                <div className="b small" style={{ marginBottom: 8 }}>
                  {tt("Run log", "Uitvoeringslog")} — <span className="mono">{runLog.claimId}</span>
                </div>
                {runLog.entries.map(e => (
                  <div key={e.key} className="row small" style={{ padding: "4px 0", flexWrap: "nowrap", alignItems: "flex-start" }}>
                    <span style={{ width: 22 }}>{stepIcon(e.status)}</span>
                    <span style={{ width: 190, flexShrink: 0 }} className="b">{STEP_LABEL(e.key)}</span>
                    <span className="muted">{e.note}</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="card">
            <div className="row" style={{ justifyContent: "space-between" }}>
              <h3>{tt("Agents", "Agents")}</h3>
              <button className="btn btn-p btn-sm" onClick={() => setAgForm(agForm ? null : {
                name: "", tmpl: "summariser", lang: "both", tone: "concise", tools: [],
                prompt: TMPL.summariser.prompt, trigger: "manual", autonomy: "suggest",
              })}>{tt("＋ New agent", "＋ Nieuwe agent")}</button>
            </div>
            {agForm && renderAgentForm()}
            {visAgents.length === 0 && (
              <div className="small muted" style={{ marginTop: 10 }}>
                {tt("No agents are deployed to your organization. Ask your platform admin.",
                    "Er zijn geen agents uitgerold naar uw organisatie. Vraag het uw platformbeheerder.")}
              </div>
            )}
            <div className="grid2" style={{ marginTop: 10 }}>
              {visAgents.map(a => (
                <div key={a.id} className="card" style={{ marginBottom: 0 }}>
                  <div className="row" style={{ justifyContent: "space-between" }}>
                    <span className="b">{a.name}</span>
                    <button className="btn btn-sm" style={a.active ? { background: "var(--green)", color: "#fff", borderColor: "var(--green)" } : {}}
                      onClick={() => {
                        setAgents(x => x.map(y => (y.id === a.id ? { ...y, active: !y.active } : y)));
                        logAudit("agent", a.name, a.active ? "paused" : "activated");
                      }}>
                      {a.active ? t("active") : t("paused")}
                    </button>
                  </div>
                  <div className="small muted" style={{ margin: "4px 0 8px" }}>
                    {TMPL[a.tmpl].name} · {a.lang === "both" ? "EN+NL" : a.lang.toUpperCase()} · {a.tone}
                  </div>
                  <div className="small">
                    <span className="chip">⏱ {a.trigger === "new" ? tt("on new claim", "bij nieuwe schade") : a.trigger === "docs" ? tt("on documents", "bij documenten") : a.trigger === "status" ? tt("on status change", "bij statuswijziging") : tt("manual", "handmatig")}</span>
                    <span className="chip">🎚 {a.autonomy === "auto" ? tt("autonomous", "autonoom") : a.autonomy === "approval" ? tt("with approval", "met akkoord") : tt("suggest only", "alleen voorstellen")}</span>
                  </div>
                  <div style={{ marginTop: 6 }}>{a.tools.map(x => <span key={x} className="chip">🔧 {x}</span>)}</div>
                </div>
              ))}
            </div>
          </div>

          <div className="card">
            <div className="row" style={{ justifyContent: "space-between" }}>
              <h3>{tt("Agent evaluation harness", "Agent-evaluatieharnas")}</h3>
              <button className="btn" disabled={busy.eval} onClick={runEval}>
                {busy.eval ? <span className="spin" /> : "🧪"} {tt("Run evaluation", "Start evaluatie")}
              </button>
            </div>
            <div className="small muted" style={{ marginBottom: 8 }}>
              {tt("Replays 3 settled/known claims through the Fraud Screener (original score hidden) and compares predicted vs recorded routing — the gate for promoting agent autonomy.",
                  "Speelt 3 afgehandelde/bekende schades opnieuw af door de Fraud Screener (originele score verborgen) en vergelijkt voorspelde met vastgelegde routering — de poort voor het verhogen van agent-autonomie.")}
            </div>
            {evalRes && (
              <>
                <table className="tbl">
                  <thead><tr><th>{tt("Claim", "Schade")}</th><th>{tt("Expected", "Verwacht")}</th><th>{tt("Predicted", "Voorspeld")}</th><th>{tt("Score", "Score")}</th><th /></tr></thead>
                  <tbody>{evalRes.rows.map(r => (
                    <tr key={r.id}>
                      <td className="mono small">{r.id}</td>
                      <td><Pill label={t("rt_" + r.expected)} color={ROUTE_COLOR[r.expected]} /></td>
                      <td><Pill label={t("rt_" + r.predicted)} color={ROUTE_COLOR[r.predicted]} /></td>
                      <td><Meter v={r.score} /></td>
                      <td>{r.expected === r.predicted
                        ? <span style={{ color: "var(--green)" }} className="b">✓ {tt("hit", "raak")}</span>
                        : <span style={{ color: "var(--red)" }} className="b">✗ {tt("miss", "mis")}</span>}</td>
                    </tr>
                  ))}</tbody>
                </table>
                <div className="b" style={{ marginTop: 10, fontSize: 15 }}>
                  {tt("Routing accuracy", "Routeringsnauwkeurigheid")}: <span style={{ color: evalRes.acc >= 67 ? "var(--green)" : "var(--red)" }}>{evalRes.acc}%</span>
                </div>
              </>
            )}
          </div>
        </div>

        <div className="bench">
          <div className="card">
            <h3>{tt("Test bench", "Testbank")}</h3>
            <span className="lbl">{tt("Agent", "Agent")}</span>
            <select value={benchAgent} onChange={e => setBenchAgent(e.target.value)}>
              {visAgents.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
            </select>
            <span className="lbl">{tt("Scenario", "Scenario")}</span>
            <textarea placeholder={tt("e.g. Claimant calls: hit a parked car, no injuries, plate XR-482-J…",
              "bijv. Verzekerde belt: geparkeerde auto geraakt, geen letsel, kenteken XR-482-J…")}
              value={benchQ} onChange={e => setBenchQ(e.target.value)} />
            <button className="btn btn-p" style={{ marginTop: 10 }} disabled={busy.bench || !benchQ.trim()} onClick={runBench}>
              {busy.bench ? <span className="spin" /> : "▶"} {tt("Send scenario", "Scenario versturen")}
            </button>
            <div style={{ marginTop: 12, maxHeight: 420, overflowY: "auto" }}>
              {benchLog.map((m, i) => (
                <div key={i} style={{ borderTop: "1px solid var(--line)", padding: "8px 0" }}>
                  <div className="small b">🤖 {m.agent}</div>
                  <div className="small muted" style={{ margin: "4px 0" }}>{m.q}</div>
                  <div className="small" style={{ whiteSpace: "pre-wrap" }}>{m.a}</div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ── super-admin studio: agent deployment ──
  function deployAgent() {
    const users = depForm.scope === "all" ? "all" : depForm.users;
    if (users !== "all" && users.length === 0)
      return toast(tt("Pick at least one user.", "Kies minstens één gebruiker."));
    const agName = (agents.find(a => a.id === depForm.agentId) || {}).name || depForm.agentId;
    const orgName = (orgs.find(o => o.id === depForm.orgId) || {}).name || depForm.orgId;
    const scopeTxt = users === "all" ? "entire organization"
      : users.map(id => (pUsers.find(u => u.id === id) || {}).name).join(", ");
    setDeployments(ds => [
      // one deployment per agent+org: redeploying replaces the previous grant
      ...ds.filter(d => !(d.agentId === depForm.agentId && d.orgId === depForm.orgId)),
      { id: "d" + Math.random().toString(36).slice(2, 7), agentId: depForm.agentId,
        orgId: depForm.orgId, users, status: "active", by: user.name, at: TODAY },
    ]);
    logAudit("deploy", agName, `deployed to ${orgName} (${scopeTxt})`);
    setDepForm(f => ({ ...f, users: [] }));
    toast(tt("Agent deployed.", "Agent uitgerold."));
  }

  function renderAdminStudio() {
    const orgUsers = pUsers.filter(u => u.org === depForm.orgId);
    const agName = id => (agents.find(a => a.id === id) || {}).name || id;
    const orgName = id => (orgs.find(o => o.id === id) || {}).name || id;
    const scopeLabel = d => d.users === "all"
      ? tt("Entire organization", "Hele organisatie")
      : d.users.map(id => (pUsers.find(u => u.id === id) || {}).name || id).join(", ");
    return (
      <>
        <div className="card">
          <h3>{tt("Agent deployment", "Agent-uitrol")}</h3>
          <div className="small muted" style={{ marginBottom: 10 }}>
            {tt("Release agents to organizations, org-wide or to selected users. An agent only works in a workspace an active deployment covers — pausing or revoking one takes it away immediately.",
                "Rol agents uit naar organisaties, organisatiebreed of naar geselecteerde gebruikers. Een agent werkt alleen in een werkplek die door een actieve uitrol wordt gedekt — pauzeren of intrekken haalt hem direct weg.")}
          </div>
          <div className="row">
            <div style={{ flex: 1, minWidth: 180 }}><span className="lbl">{tt("Agent", "Agent")}</span>
              <select value={depForm.agentId} onChange={e => setDepForm(f => ({ ...f, agentId: e.target.value }))}>
                {agents.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
              </select></div>
            <div style={{ flex: 1, minWidth: 200 }}><span className="lbl">{tt("Organization", "Organisatie")}</span>
              <select value={depForm.orgId} onChange={e => setDepForm(f => ({ ...f, orgId: e.target.value, users: [] }))}>
                {orgs.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
              </select></div>
            <div style={{ width: 200 }}><span className="lbl">{tt("Access", "Toegang")}</span>
              <select value={depForm.scope} onChange={e => setDepForm(f => ({ ...f, scope: e.target.value }))}>
                <option value="all">{tt("Entire organization", "Hele organisatie")}</option>
                <option value="users">{tt("Selected users", "Geselecteerde gebruikers")}</option>
              </select></div>
          </div>
          {depForm.scope === "users" && (
            <div className="row" style={{ marginTop: 8 }}>
              {orgUsers.length === 0 && <span className="small muted">{tt("This organization has no users yet.", "Deze organisatie heeft nog geen gebruikers.")}</span>}
              {orgUsers.map(u => (
                <label key={u.id} className="row small" style={{ gap: 5 }}>
                  <input type="checkbox" style={{ width: 15 }} checked={depForm.users.includes(u.id)}
                    onChange={e => setDepForm(f => ({ ...f, users: e.target.checked ? [...f.users, u.id] : f.users.filter(x => x !== u.id) }))} />
                  {u.name} <span className="muted">({u.role})</span>
                </label>
              ))}
            </div>
          )}
          <button className="btn btn-p" style={{ marginTop: 10 }} onClick={deployAgent}>
            🚀 {tt("Deploy agent", "Agent uitrollen")}
          </button>
        </div>

        <div className="card">
          <h3>{tt("Active deployments", "Actieve uitrollen")}</h3>
          {deployments.length === 0
            ? <div className="small muted">{tt("Nothing deployed yet.", "Nog niets uitgerold.")}</div>
            : <table className="tbl">
                <thead><tr><th>{tt("Agent", "Agent")}</th><th>{tt("Organization", "Organisatie")}</th><th>{tt("Access", "Toegang")}</th><th>{tt("By", "Door")}</th><th>{t("status")}</th><th /></tr></thead>
                <tbody>{deployments.map(d => (
                  <tr key={d.id}>
                    <td className="b">{agName(d.agentId)}</td>
                    <td>{orgName(d.orgId)}</td>
                    <td className="small">{scopeLabel(d)}</td>
                    <td className="small muted">{d.by} · {d.at}</td>
                    <td><Pill label={d.status === "active" ? t("active") : t("paused")} color={d.status === "active" ? "#2e7d5b" : "#b58a1f"} /></td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <button className="btn btn-sm" onClick={() => {
                        setDeployments(ds => ds.map(x => x.id === d.id ? { ...x, status: x.status === "active" ? "paused" : "active" } : x));
                        logAudit("deploy", agName(d.agentId), `${d.status === "active" ? "paused" : "resumed"} for ${orgName(d.orgId)}`);
                      }}>{d.status === "active" ? tt("Pause", "Pauzeer") : tt("Resume", "Hervat")}</button>{" "}
                      <button className="btn btn-sm" onClick={() => {
                        setDeployments(ds => ds.filter(x => x.id !== d.id));
                        logAudit("deploy", agName(d.agentId), `revoked for ${orgName(d.orgId)}`);
                        toast(tt("Deployment revoked.", "Uitrol ingetrokken."));
                      }}>{tt("Revoke", "Intrekken")}</button>
                    </td>
                  </tr>
                ))}</tbody>
              </table>}
        </div>

        <div className="card">
          <div className="row" style={{ justifyContent: "space-between" }}>
            <h3>{tt("Agent catalog", "Agent-catalogus")}</h3>
            <button className="btn btn-p btn-sm" onClick={() => setAgForm(agForm ? null : {
              name: "", tmpl: "summariser", lang: "both", tone: "concise", tools: [],
              prompt: TMPL.summariser.prompt, trigger: "manual", autonomy: "suggest",
            })}>{tt("＋ New agent", "＋ Nieuwe agent")}</button>
          </div>
          {agForm && renderAgentForm()}
          <div className="grid2" style={{ marginTop: 10 }}>
            {agents.map(a => (
              <div key={a.id} className="card" style={{ marginBottom: 0 }}>
                <div className="row" style={{ justifyContent: "space-between" }}>
                  <span className="b">{a.name}</span>
                  <button className="btn btn-sm" style={a.active ? { background: "var(--green)", color: "#fff", borderColor: "var(--green)" } : {}}
                    onClick={() => {
                      setAgents(x => x.map(y => (y.id === a.id ? { ...y, active: !y.active } : y)));
                      logAudit("agent", a.name, a.active ? "paused platform-wide" : "activated platform-wide");
                    }}>
                    {a.active ? t("active") : t("paused")}
                  </button>
                </div>
                <div className="small muted" style={{ margin: "4px 0 8px" }}>
                  {TMPL[a.tmpl].name} · {a.lang === "both" ? "EN+NL" : a.lang.toUpperCase()} · {a.tone}
                </div>
                <div className="small">
                  {deployments.filter(d => d.agentId === a.id && d.status === "active").map(d => (
                    <span key={d.id} className="chip">🏢 {orgName(d.orgId)}{d.users !== "all" ? ` (${d.users.length})` : ""}</span>
                  ))}
                  {!deployments.some(d => d.agentId === a.id && d.status === "active") &&
                    <span className="chip">⚠ {tt("not deployed", "niet uitgerold")}</span>}
                </div>
              </div>
            ))}
          </div>
        </div>
      </>
    );
  }

  function renderAgentForm() {
    const f = agForm;
    const up = patch => setAgForm({ ...f, ...patch });
    return (
      <div style={{ background: "#faf9f4", border: "1px solid var(--line)", borderRadius: 10, padding: 14, marginTop: 10 }}>
        <div className="row">
          <div style={{ flex: 1, minWidth: 160 }}><span className="lbl">{tt("Name", "Naam")}</span>
            <input value={f.name} onChange={e => up({ name: e.target.value })} /></div>
          <div style={{ flex: 1, minWidth: 160 }}><span className="lbl">{tt("Template", "Sjabloon")}</span>
            <select value={f.tmpl} onChange={e => up({ tmpl: e.target.value, prompt: TMPL[e.target.value].prompt })}>
              {Object.entries(TMPL).map(([k, v]) => <option key={k} value={k}>{v.name}</option>)}
            </select></div>
          <div style={{ width: 110 }}><span className="lbl">{tt("Language", "Taal")}</span>
            <select value={f.lang} onChange={e => up({ lang: e.target.value })}>
              <option value="en">EN</option><option value="nl">NL</option><option value="both">EN+NL</option>
            </select></div>
          <div style={{ width: 130 }}><span className="lbl">{tt("Tone", "Toon")}</span>
            <select value={f.tone} onChange={e => up({ tone: e.target.value })}>
              {["concise", "formal", "friendly"].map(x => <option key={x}>{x}</option>)}
            </select></div>
          <div style={{ width: 170 }}><span className="lbl">Trigger</span>
            <select value={f.trigger} onChange={e => up({ trigger: e.target.value })}>
              <option value="manual">{tt("manual", "handmatig")}</option>
              <option value="new">{tt("on new claim", "bij nieuwe schade")}</option>
              <option value="docs">{tt("on documents", "bij documenten")}</option>
              <option value="status">{tt("on status change", "bij statuswijziging")}</option>
            </select></div>
          <div style={{ width: 170 }}><span className="lbl">{tt("Autonomy", "Autonomie")}</span>
            <select value={f.autonomy} onChange={e => up({ autonomy: e.target.value })}>
              <option value="suggest">{tt("suggest only", "alleen voorstellen")}</option>
              <option value="approval">{tt("with approval", "met akkoord")}</option>
              <option value="auto">{tt("autonomous", "autonoom")}</option>
            </select></div>
        </div>
        <span className="lbl">Tools</span>
        <div className="row">
          {TOOLS.map(x => (
            <label key={x} className="row small" style={{ gap: 5 }}>
              <input type="checkbox" style={{ width: 15 }} checked={f.tools.includes(x)}
                onChange={e => up({ tools: e.target.checked ? [...f.tools, x] : f.tools.filter(y => y !== x) })} />
              {x}
            </label>
          ))}
        </div>
        <span className="lbl">{tt("System prompt", "Systeemprompt")}</span>
        <textarea style={{ minHeight: 130 }} value={f.prompt} onChange={e => up({ prompt: e.target.value })} />
        <div className="row" style={{ marginTop: 10 }}>
          <button className="btn btn-p" disabled={!f.name} onClick={() => {
            const id = "a" + Math.random().toString(36).slice(2, 7);
            setAgents(x => [...x, { ...f, id, active: true }]);
            logAudit("agent", f.name, `agent created (${TMPL[f.tmpl].name} · trigger ${f.trigger} · ${f.autonomy})`);
            if (user.role !== "admin") {
              // An ops user must be able to use what they just built: release it
              // to their own organization; wider rollout stays with the admin.
              setDeployments(ds => [...ds, { id: "d" + Math.random().toString(36).slice(2, 7),
                agentId: id, orgId: myOrgId(), users: "all", status: "active", by: user.name, at: TODAY }]);
              logAudit("deploy", f.name, `auto-deployed to ${(orgs.find(o => o.id === myOrgId()) || {}).name || myOrgId()} (entire organization)`);
            }
            setAgForm(null);
            toast(tt("Agent created.", "Agent aangemaakt."));
          }}>{t("save")}</button>
          <button className="btn" onClick={() => setAgForm(null)}>{tt("Cancel", "Annuleren")}</button>
        </div>
      </div>
    );
  }

  // ── audit log ──
  function downloadJSON(filename, obj) {
    const blob = new Blob([JSON.stringify(obj, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url; a.download = filename; a.click();
    URL.revokeObjectURL(url);
  }

  function renderAudit() {
    return (
      <div className="card">
        <div className="row" style={{ justifyContent: "space-between" }}>
          <h3>{t("nav_audit")}</h3>
          <button className="btn" onClick={() => { downloadJSON("schadedesk-audit-log.json", audit); toast(tt("Audit log exported.", "Auditlog geëxporteerd.")); }}>
            ⬇ {t("exportJson")}
          </button>
        </div>
        <div className="small muted" style={{ marginBottom: 10 }}>{t("logAudit")}</div>
        <div style={{ overflowX: "auto" }}>
          <table className="tbl">
            <thead><tr><th>{tt("Time", "Tijd")}</th><th>{tt("Actor", "Actor")}</th><th>{tt("Action", "Actie")}</th><th>{tt("Target", "Doel")}</th><th>{tt("Detail", "Detail")}</th></tr></thead>
            <tbody>
              {audit.map((a, i) => (
                <tr key={i}>
                  <td className="mono small" style={{ whiteSpace: "nowrap" }}>{a.t}</td>
                  <td className="small">{a.actor}<div className="muted" style={{ fontSize: 11 }}>{a.role}</div></td>
                  <td><Pill label={a.action} color={a.action.startsWith("ai:") ? "#d95d0f" : "#16232e"} /></td>
                  <td className="mono small">{a.target}</td>
                  <td className="small">{a.detail}</td>
                </tr>
              ))}
              {!audit.length && <tr><td colSpan={5} className="muted small">{tt("No events logged yet this session.", "Nog geen gebeurtenissen gelogd deze sessie.")}</td></tr>}
            </tbody>
          </table>
        </div>
      </div>
    );
  }

  // ── super admin ──
  function renderOrgs() {
    return (
      <>
        <div className="card">
          <h3>{tt("Create organization", "Organisatie aanmaken")}</h3>
          <div className="row">
            <input style={{ flex: 2, minWidth: 180 }} placeholder={tt("Organization name", "Naam organisatie")} value={orgForm.name} onChange={e => setOrgForm({ ...orgForm, name: e.target.value })} />
            <input style={{ flex: 2, minWidth: 180 }} placeholder={tt("Contact e-mail", "Contact-e-mail")} value={orgForm.email} onChange={e => setOrgForm({ ...orgForm, email: e.target.value })} />
            <select style={{ width: 130 }} value={orgForm.plan} onChange={e => setOrgForm({ ...orgForm, plan: e.target.value })}>
              {["Trial", "Standard", "Enterprise"].map(x => <option key={x}>{x}</option>)}
            </select>
            <select style={{ width: 90 }} value={orgForm.country} onChange={e => setOrgForm({ ...orgForm, country: e.target.value })}>
              {["NL", "BE", "DE", "UK"].map(x => <option key={x}>{x}</option>)}
            </select>
            <button className="btn btn-p" disabled={!orgForm.name || !orgForm.email} onClick={() => {
              const id = "o" + Math.random().toString(36).slice(2, 7);
              setOrgs(x => [...x, { id, ...orgForm, status: "active" }]);
              logAudit("org", orgForm.name, `organization created · ${orgForm.plan} · ${orgForm.country}`);
              setOrgForm({ name: "", email: "", plan: "Trial", country: "NL" });
              toast(tt("Organization created.", "Organisatie aangemaakt."));
            }}>{tt("Create", "Aanmaken")}</button>
          </div>
          <div className="aiN">{tt("New organizations start on a Trial plan by default.", "Nieuwe organisaties starten standaard met een Trial-abonnement.")}</div>
        </div>
        <div className="card">
          <h3>{t("nav_orgs")}</h3>
          <table className="tbl">
            <thead><tr><th>{tt("Organization", "Organisatie")}</th><th>Plan</th><th>{tt("Country", "Land")}</th><th>{t("status")}</th><th>{t("actions")}</th></tr></thead>
            <tbody>{orgs.map(o => (
              <tr key={o.id}>
                <td><span className="b">{o.name}</span><div className="small muted">{o.email}</div></td>
                <td>{o.plan}</td><td>{o.country}</td>
                <td><Pill label={o.status} color={o.status === "active" ? "#2e7d5b" : "#c23b2e"} /></td>
                <td><button className="btn btn-sm" onClick={() => {
                  const ns = o.status === "active" ? "suspended" : "active";
                  setOrgs(x => x.map(y => (y.id === o.id ? { ...y, status: ns } : y)));
                  logAudit("org", o.name, `organization ${ns}`);
                }}>{o.status === "active" ? tt("Suspend", "Schorsen") : tt("Activate", "Activeren")}</button></td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </>
    );
  }

  function renderUsers() {
    const orgName = id => (orgs.find(o => o.id === id) || {}).name || "—";
    return (
      <>
        <div className="card">
          <h3>{tt("Invite user", "Gebruiker uitnodigen")}</h3>
          <div className="row">
            <input style={{ flex: 1, minWidth: 150 }} placeholder={tt("Name", "Naam")} value={userForm.name} onChange={e => setUserForm({ ...userForm, name: e.target.value })} />
            <input style={{ flex: 1, minWidth: 180 }} placeholder="E-mail" value={userForm.email} onChange={e => setUserForm({ ...userForm, email: e.target.value })} />
            <select style={{ width: 210 }} value={userForm.org} onChange={e => setUserForm({ ...userForm, org: e.target.value })}>
              {orgs.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
            </select>
            <select style={{ width: 130 }} value={userForm.role} onChange={e => setUserForm({ ...userForm, role: e.target.value })}>
              {["Handler", "Manager", "CFO", "Org admin"].map(x => <option key={x}>{x}</option>)}
            </select>
            <button className="btn btn-p" disabled={!userForm.name || !userForm.email} onClick={() => {
              const id = "u" + Math.random().toString(36).slice(2, 7);
              setPUsers(x => [...x, { id, ...userForm, status: "Invited" }]);
              logAudit("user", userForm.email, `user invited as ${userForm.role} @ ${orgName(userForm.org)}`);
              setUserForm({ name: "", email: "", org: orgs[0] ? orgs[0].id : "o1", role: "Handler" });
              toast(tt("Invitation sent (simulated).", "Uitnodiging verstuurd (gesimuleerd)."));
            }}>{tt("Invite", "Uitnodigen")}</button>
          </div>
        </div>
        <div className="card">
          <h3>{t("nav_users")}</h3>
          <table className="tbl">
            <thead><tr><th>{tt("User", "Gebruiker")}</th><th>{tt("Organization", "Organisatie")}</th><th>{tt("Role", "Rol")}</th><th>{t("status")}</th><th>{t("actions")}</th></tr></thead>
            <tbody>{pUsers.map(u => (
              <tr key={u.id}>
                <td><span className="b">{u.name}</span><div className="small muted">{u.email}</div></td>
                <td className="small">{orgName(u.org)}</td>
                <td><select style={{ width: 130 }} value={u.role} onChange={e => {
                  setPUsers(x => x.map(y => (y.id === u.id ? { ...y, role: e.target.value } : y)));
                  logAudit("user", u.email, `role changed to ${e.target.value}`);
                }}>{["Handler", "Manager", "CFO", "Org admin"].map(x => <option key={x}>{x}</option>)}</select></td>
                <td><Pill label={u.status} color={u.status === "Active" ? "#2e7d5b" : u.status === "Invited" ? "#b58a1f" : "#c23b2e"} /></td>
                <td className="row" style={{ gap: 6 }}>
                  <button className="btn btn-sm" onClick={() => {
                    const ns = u.status === "Suspended" ? "Active" : "Suspended";
                    setPUsers(x => x.map(y => (y.id === u.id ? { ...y, status: ns } : y)));
                    logAudit("user", u.email, `user ${ns.toLowerCase()}`);
                  }}>{u.status === "Suspended" ? tt("Activate", "Activeren") : tt("Suspend", "Schorsen")}</button>
                  {u.status === "Invited" && <button className="btn btn-sm" onClick={() => {
                    logAudit("user", u.email, "invitation re-sent");
                    toast(tt("Invite re-sent (simulated).", "Uitnodiging opnieuw verstuurd (gesimuleerd)."));
                  }}>{tt("Resend invite", "Opnieuw uitnodigen")}</button>}
                </td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </>
    );
  }

  function renderLegal() {
    const controls = [
      [tt("Human oversight", "Menselijk toezicht"), tt("Four-eyes payout limit and manager approval queue", "Vier-ogen betaallimiet en manager-akkoordwachtrij")],
      [tt("Record-keeping", "Registratieplicht"), tt("Immutable audit trail of every AI and human action", "Onveranderlijk auditspoor van elke AI- en menselijke actie")],
      [tt("Accuracy monitoring", "Nauwkeurigheidsbewaking"), tt("Agent evaluation harness with routing accuracy", "Agent-evaluatieharnas met routeringsnauwkeurigheid")],
      [tt("Transparency", "Transparantie"), tt("AI actions labelled and highlighted in the audit log", "AI-acties gelabeld en gemarkeerd in het auditlog")],
    ];
    return (
      <>
        <div className="grid2">
          <div className="card">
            <h3>EU AI Act</h3>
            <p className="small" style={{ lineHeight: 1.55, marginBottom: 10 }}>
              {tt("AI systems used for risk assessment and pricing in insurance are classified as high-risk under the EU AI Act. Boxora implements the corresponding controls:",
                  "AI-systemen voor risicobeoordeling en premiestelling in verzekeringen gelden als hoog-risico onder de EU AI Act. Boxora implementeert de bijbehorende beheersmaatregelen:")}
            </p>
            {controls.map(([name, feat], i) => (
              <div key={i} className="row" style={{ padding: "6px 0", borderBottom: "1px solid #edece5", flexWrap: "nowrap" }}>
                <span style={{ color: "var(--green)" }} className="b">✓</span>
                <span style={{ flex: 1 }}><span className="b small">{name}</span>
                  <div className="small muted">{feat}</div></span>
                <Pill label={tt("Implemented", "Geïmplementeerd")} color="#2e7d5b" />
              </div>
            ))}
          </div>
          <div className="card">
            <h3>GDPR / AVG</h3>
            <span className="lbl">{tt("Data retention", "Bewaartermijn")}</span>
            <select value={legal.retention} onChange={e => { setLegal({ ...legal, retention: e.target.value }); logAudit("legal", "retention", `set to ${e.target.value} years`); }}>
              {["5", "7", "10"].map(x => <option key={x} value={x}>{x} {tt("years", "jaar")}</option>)}
            </select>
            <span className="lbl">DPIA</span>
            <div className="row">
              <Pill label={legal.dpia === "done" ? tt("Completed", "Afgerond") : tt("In progress", "In uitvoering")} color={legal.dpia === "done" ? "#2e7d5b" : "#b58a1f"} />
              {legal.dpia !== "done" && <button className="btn btn-sm" onClick={() => { setLegal({ ...legal, dpia: "done" }); logAudit("legal", "DPIA", "marked completed"); toast("DPIA ✓"); }}>
                {tt("Mark completed", "Markeer afgerond")}</button>}
            </div>
            <span className="lbl">{tt("Art. 15 data subject access", "Art. 15 inzagerecht")}</span>
            <button className="btn" onClick={() => {
              const who = "R. Kaya";
              downloadJSON("data-subject-export-r-kaya.json", {
                exportedAt: TODAY, dataSubject: who, legalBasis: "GDPR Art. 15",
                claims: claims.filter(c => c.claimant === who),
                policies: policies.filter(p => p.holder === who),
              });
              logAudit("legal", who, "Art. 15 data subject export downloaded");
              toast(tt("Data subject export downloaded.", "Inzage-export gedownload."));
            }}>⬇ {tt("Export data of R. Kaya", "Exporteer gegevens R. Kaya")}</button>
          </div>
        </div>
        <div className="grid2">
          <div className="card">
            <h3>{tt("Fraud protocol (Verbond van Verzekeraars)", "Fraudeprotocol (Verbond van Verzekeraars)")}</h3>
            <label className="row small" style={{ gap: 8 }}>
              <input type="checkbox" style={{ width: 16 }} checked={legal.cis} onChange={e => {
                setLegal({ ...legal, cis: e.target.checked });
                logAudit("legal", "CIS reporting", e.target.checked ? "enabled" : "disabled");
              }} />
              {tt("Report confirmed fraud signals to the CIS foundation database", "Meld bevestigde fraudesignalen aan de CIS-databank")}
            </label>
          </div>
          <div className="card">
            <h3>{tt("FNOL consent notice", "FNOL-toestemmingstekst")}</h3>
            <div className="small muted" style={{ marginBottom: 6 }}>
              {tt("This exact text is shown on the FNOL registration form.", "Deze tekst wordt letterlijk getoond op het FNOL-formulier.")}
            </div>
            <textarea style={{ minHeight: 140 }} value={legal.consent} onChange={e => setLegal({ ...legal, consent: e.target.value })} />
            <button className="btn btn-sm" style={{ marginTop: 8 }} onClick={() => { logAudit("legal", "consent notice", "FNOL consent text updated"); toast(tt("Consent text saved.", "Toestemmingstekst opgeslagen.")); }}>{t("save")}</button>
          </div>
        </div>
        <div className="small muted">{tt("All compliance copy in this demo is illustrative content, not legal advice.",
          "Alle compliance-teksten in deze demo zijn illustratief en geen juridisch advies.")}</div>
      </>
    );
  }

  // ── root render ──
  function renderPage() {
    if (page === "claims") return renderClaims();
    if (page === "policies") return renderPolicies();
    if (page === "tasks") return renderTasks();
    if (page === "studio") return renderStudio();
    if (page === "audit") return renderAudit();
    if (page === "orgs") return renderOrgs();
    if (page === "users") return renderUsers();
    if (page === "legal") return renderLegal();
    if (user.role === "manager") return renderManagerDash();
    if (user.role === "cfo") return renderCfoDash();
    if (user.role === "admin") return renderPlatformDash();
    return renderHandlerDash();
  }

  return (
    <div className="sd">
      <style>{CSS}</style>
      {user ? renderShell(renderPage()) : renderLogin()}
      <div className="toasts">
        {toasts.map(x => <div key={x.id} className="toast">{x.msg}</div>)}
      </div>
    </div>
  );
}
