"""SQLite storage. Single file, plain sqlite3 — no ORM needed."""
import sqlite3
import json
import uuid
from pathlib import Path
from contextlib import contextmanager
from datetime import datetime

DB_PATH = Path(__file__).parent / "data" / "claims.db"

SCHEMA = """
CREATE TABLE IF NOT EXISTS claims (
    id TEXT PRIMARY KEY,
    claim_number TEXT UNIQUE,
    created_at TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'draft',
    policyholder_name TEXT,
    policy_number TEXT,
    license_plate TEXT,
    vin TEXT,
    loss_date TEXT,
    loss_location TEXT,
    description TEXT,
    third_party_involved INTEGER DEFAULT 0,
    injuries INTEGER DEFAULT 0,
    police_report_number TEXT,
    estimated_amount_eur REAL,
    extraction_confidence REAL,
    fraud_score REAL DEFAULT 0.0,
    damage_categories TEXT,
    summary TEXT,
    decision_outcome TEXT,
    decision_reasons TEXT,
    rules_trace TEXT,
    fraud_signals TEXT,
    assessment TEXT,
    assigned_handler_id TEXT
);

CREATE TABLE IF NOT EXISTS handlers (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    role TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS email_templates (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    audience TEXT NOT NULL,
    subject TEXT NOT NULL,
    body TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS activity (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    claim_id TEXT NOT NULL,
    actor_handler_id TEXT,
    kind TEXT NOT NULL,        -- note | assigned | delegated | email_saved | decision
    body TEXT,
    meta TEXT,                 -- JSON
    created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_activity_claim ON activity(claim_id);

CREATE TABLE IF NOT EXISTS documents (
    id TEXT PRIMARY KEY,
    claim_id TEXT NOT NULL,
    filename TEXT NOT NULL,
    filepath TEXT NOT NULL,
    doc_type TEXT,
    content_hash TEXT,
    perceptual_hash TEXT,
    extracted_json TEXT,
    created_at TEXT NOT NULL,
    FOREIGN KEY (claim_id) REFERENCES claims(id)
);

CREATE INDEX IF NOT EXISTS idx_docs_claim ON documents(claim_id);
CREATE INDEX IF NOT EXISTS idx_docs_phash ON documents(perceptual_hash);

CREATE TABLE IF NOT EXISTS usage (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    created_at TEXT NOT NULL,
    day TEXT NOT NULL,
    claim_id TEXT,
    operation TEXT NOT NULL,
    model TEXT NOT NULL,
    input_tokens INTEGER NOT NULL DEFAULT 0,
    output_tokens INTEGER NOT NULL DEFAULT 0,
    cost_usd REAL NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_usage_day ON usage(day);
CREATE INDEX IF NOT EXISTS idx_usage_claim ON usage(claim_id);
"""


DEFAULT_HANDLERS = [
    ("h_alex",  "Alex Terlouw",  "alex.terlouw@example.nl",  "senior_adjuster"),
    ("h_sam",   "Sam de Jong",   "sam.dejong@example.nl",    "adjuster"),
    ("h_lin",   "Lin Voormans",  "lin.voormans@example.nl",  "fraud_specialist"),
    ("h_mira",  "Mira Wortel",   "mira.wortel@example.nl",   "claim_handler"),
    ("h_dara",  "Dara Aksoy",    "dara.aksoy@example.nl",    "injury_department"),
    ("h_bas",   "Bas Kortenhof", "bas.kortenhof@example.nl", "liability_department"),
]


def handlers_by_role(role: str) -> list[dict]:
    with get_conn() as c:
        rows = c.execute(
            "SELECT * FROM handlers WHERE role = ? AND active = 1 ORDER BY name",
            (role,),
        ).fetchall()
        return [dict(r) for r in rows]

DEFAULT_EMAIL_TEMPLATES = [
    ("t_ack", "First-response acknowledgement", "customer",
     "We ontvingen uw schademelding {claim_number}",
     """Beste {policyholder_name},

Wij bevestigen ontvangst van uw schademelding met kenmerk {claim_number} voor voertuig {license_plate}.
Ons team beoordeelt uw dossier en neemt binnen 3 werkdagen contact op.

Kunt u — indien nog niet bijgevoegd — de volgende documenten aanleveren:
- foto's van de schade,
- reparatienota / offerte van de garage,
- kopie proces-verbaal (indien aanwezig).

Met vriendelijke groet,
{handler_name}
{handler_email}"""),

    ("t_docs", "Document request", "customer",
     "Aanvullende documenten nodig voor {claim_number}",
     """Beste {policyholder_name},

Voor de verdere behandeling van schadedossier {claim_number} hebben wij nog het volgende nodig:
- ontbrekende foto's van de schade,
- volledige reparatienota / offerte,
- (indien van toepassing) aanrijdingsformulier.

Reageert u binnen 14 dagen zodat wij het dossier vlot kunnen afronden.

Met vriendelijke groet,
{handler_name}"""),

    ("t_approve", "Approval notification", "customer",
     "Uw schadeclaim {claim_number} is goedgekeurd",
     """Beste {policyholder_name},

Uw schadeclaim {claim_number} is goedgekeurd. De uitkering wordt binnen 5 werkdagen overgemaakt op het bij ons bekende rekeningnummer.

Bij vragen: bel of mail ons met dossierkenmerk {claim_number}.

Met vriendelijke groet,
{handler_name}
{handler_email}"""),

    ("t_escalate_fraud", "Escalation to fraud team", "internal",
     "[FRAUD REVIEW] {claim_number} — {license_plate}",
     """Team fraude,

Dossier {claim_number} van {policyholder_name} (kenteken {license_plate}, verliesdatum {loss_date}) staat op manual review met fraud-score van niet-triviale hoogte.

Verzoek: beoordeel de fraud-signalen (recycled photo / EXIF / laat gemeld / lagen) en geef binnen 2 werkdagen advies.

Groet,
{handler_name}"""),

    ("t_delegate", "Internal delegation note", "internal",
     "Overdracht {claim_number}",
     """Hallo {handler_name},

Ik draag dossier {claim_number} ({policyholder_name} — {license_plate}) aan je over.

Reden: [vul kort in]

Status: {status}. Zie het claim-detailpagina voor volledige assessment, fraud-signalen en rules-audit.

Groet,
[Overdragende behandelaar]"""),
]


def init_db():
    DB_PATH.parent.mkdir(parents=True, exist_ok=True)
    with sqlite3.connect(DB_PATH) as conn:
        conn.executescript(SCHEMA)
    for hid, name, email, role in DEFAULT_HANDLERS:
        upsert_handler(hid, name, email, role)
    for tid, name, aud, subject, body in DEFAULT_EMAIL_TEMPLATES:
        upsert_email_template(tid, name, aud, subject, body)


@contextmanager
def get_conn():
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    try:
        yield conn
        conn.commit()
    finally:
        conn.close()


def new_id() -> str:
    return uuid.uuid4().hex[:12]


def now_iso() -> str:
    return datetime.utcnow().isoformat(timespec="seconds") + "Z"


def next_claim_number() -> str:
    with get_conn() as c:
        row = c.execute("SELECT COUNT(*) AS n FROM claims").fetchone()
        return f"NL-{datetime.utcnow().year}-{row['n'] + 1:05d}"


def create_claim(fnol: dict) -> str:
    cid = new_id()
    with get_conn() as c:
        c.execute(
            """INSERT INTO claims (id, claim_number, created_at, status,
               policyholder_name, policy_number, license_plate, vin,
               loss_date, loss_location, description,
               third_party_involved, injuries, police_report_number)
               VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
            (
                cid, next_claim_number(), now_iso(), "submitted",
                fnol.get("policyholder_name"),
                fnol.get("policy_number"),
                fnol.get("license_plate", "").upper().replace("-", "").strip(),
                fnol.get("vin", "").upper().strip(),
                fnol.get("loss_date"),
                fnol.get("loss_location"),
                fnol.get("description"),
                1 if fnol.get("third_party_involved") else 0,
                1 if fnol.get("injuries") else 0,
                fnol.get("police_report_number"),
            ),
        )
    return cid


def get_claim(cid: str) -> dict | None:
    with get_conn() as c:
        row = c.execute("SELECT * FROM claims WHERE id = ?", (cid,)).fetchone()
        return dict(row) if row else None


def list_claims() -> list[dict]:
    with get_conn() as c:
        rows = c.execute("SELECT * FROM claims ORDER BY created_at DESC").fetchall()
        return [dict(r) for r in rows]


def add_document(claim_id: str, filename: str, filepath: str, doc_type: str,
                 content_hash: str, perceptual_hash: str | None,
                 extracted: dict | None) -> str:
    did = new_id()
    with get_conn() as c:
        c.execute(
            """INSERT INTO documents (id, claim_id, filename, filepath, doc_type,
               content_hash, perceptual_hash, extracted_json, created_at)
               VALUES (?,?,?,?,?,?,?,?,?)""",
            (did, claim_id, filename, filepath, doc_type,
             content_hash, perceptual_hash,
             json.dumps(extracted) if extracted else None, now_iso()),
        )
    return did


def get_documents(claim_id: str) -> list[dict]:
    with get_conn() as c:
        rows = c.execute(
            "SELECT * FROM documents WHERE claim_id = ? ORDER BY created_at",
            (claim_id,),
        ).fetchall()
        out = []
        for r in rows:
            d = dict(r)
            if d.get("extracted_json"):
                try:
                    d["extracted"] = json.loads(d["extracted_json"])
                except Exception:
                    d["extracted"] = None
            out.append(d)
        return out


def find_duplicate_photo_claims(perceptual_hash: str, current_claim_id: str) -> list[str]:
    """Return claim_ids of prior claims sharing the same perceptual hash. Recycled-photo signal."""
    if not perceptual_hash:
        return []
    with get_conn() as c:
        rows = c.execute(
            """SELECT DISTINCT claim_id FROM documents
               WHERE perceptual_hash = ? AND claim_id != ?""",
            (perceptual_hash, current_claim_id),
        ).fetchall()
        return [r["claim_id"] for r in rows]


def update_claim_analysis(cid: str, *, estimated_amount_eur: float | None,
                          extraction_confidence: float | None,
                          fraud_score: float | None,
                          damage_categories: list | None,
                          summary: str | None,
                          status: str) -> None:
    with get_conn() as c:
        c.execute(
            """UPDATE claims SET
               estimated_amount_eur = COALESCE(?, estimated_amount_eur),
               extraction_confidence = COALESCE(?, extraction_confidence),
               fraud_score = COALESCE(?, fraud_score),
               damage_categories = COALESCE(?, damage_categories),
               summary = COALESCE(?, summary),
               status = ?
               WHERE id = ?""",
            (estimated_amount_eur, extraction_confidence, fraud_score,
             json.dumps(damage_categories) if damage_categories is not None else None,
             summary, status, cid),
        )


def record_decision(cid: str, outcome: str, reasons: list, rules_trace: dict) -> None:
    with get_conn() as c:
        c.execute(
            """UPDATE claims SET decision_outcome = ?, decision_reasons = ?,
               rules_trace = ?, status = 'decided' WHERE id = ?""",
            (outcome, json.dumps(reasons), json.dumps(rules_trace), cid),
        )


def record_fraud_and_assessment(cid: str, fraud_signals: list, assessment: dict) -> None:
    with get_conn() as c:
        c.execute(
            "UPDATE claims SET fraud_signals = ?, assessment = ? WHERE id = ?",
            (json.dumps(fraud_signals), json.dumps(assessment), cid),
        )


# --- handlers ----------------------------------------------------------------

def upsert_handler(hid: str, name: str, email: str, role: str) -> None:
    with get_conn() as c:
        c.execute(
            """INSERT INTO handlers (id, name, email, role, active)
               VALUES (?,?,?,?,1)
               ON CONFLICT(id) DO UPDATE SET
                 name=excluded.name, email=excluded.email, role=excluded.role""",
            (hid, name, email, role),
        )


def list_handlers(active_only: bool = True) -> list[dict]:
    with get_conn() as c:
        q = "SELECT * FROM handlers"
        if active_only:
            q += " WHERE active = 1"
        q += " ORDER BY role, name"
        return [dict(r) for r in c.execute(q).fetchall()]


def get_handler(hid: str) -> dict | None:
    with get_conn() as c:
        r = c.execute("SELECT * FROM handlers WHERE id = ?", (hid,)).fetchone()
        return dict(r) if r else None


def assign_claim(cid: str, handler_id: str | None) -> None:
    with get_conn() as c:
        c.execute("UPDATE claims SET assigned_handler_id = ? WHERE id = ?",
                  (handler_id, cid))


# --- email templates ---------------------------------------------------------

def upsert_email_template(tid: str, name: str, audience: str,
                          subject: str, body: str) -> None:
    with get_conn() as c:
        c.execute(
            """INSERT INTO email_templates (id, name, audience, subject, body)
               VALUES (?,?,?,?,?)
               ON CONFLICT(id) DO UPDATE SET
                 name=excluded.name, audience=excluded.audience,
                 subject=excluded.subject, body=excluded.body""",
            (tid, name, audience, subject, body),
        )


def list_email_templates() -> list[dict]:
    with get_conn() as c:
        return [dict(r) for r in c.execute(
            "SELECT * FROM email_templates ORDER BY audience, name").fetchall()]


def get_email_template(tid: str) -> dict | None:
    with get_conn() as c:
        r = c.execute("SELECT * FROM email_templates WHERE id = ?", (tid,)).fetchone()
        return dict(r) if r else None


# --- activity timeline -------------------------------------------------------

def add_activity(claim_id: str, kind: str, actor_handler_id: str | None,
                 body: str | None, meta: dict | None = None) -> int:
    with get_conn() as c:
        cur = c.execute(
            """INSERT INTO activity (claim_id, actor_handler_id, kind, body, meta, created_at)
               VALUES (?,?,?,?,?,?)""",
            (claim_id, actor_handler_id, kind, body,
             json.dumps(meta) if meta else None, now_iso()),
        )
        return cur.lastrowid


def list_activity(claim_id: str) -> list[dict]:
    with get_conn() as c:
        rows = c.execute(
            """SELECT a.*, h.name AS actor_name, h.role AS actor_role
               FROM activity a
               LEFT JOIN handlers h ON h.id = a.actor_handler_id
               WHERE a.claim_id = ?
               ORDER BY a.created_at DESC""", (claim_id,)).fetchall()
        out = []
        for r in rows:
            d = dict(r)
            if d.get("meta"):
                try:
                    d["meta_parsed"] = json.loads(d["meta"])
                except Exception:
                    d["meta_parsed"] = {}
            else:
                d["meta_parsed"] = {}
            out.append(d)
        return out


def record_usage(claim_id: str | None, operation: str, model: str,
                 input_tokens: int, output_tokens: int, cost_usd: float) -> None:
    ts = now_iso()
    day = ts[:10]
    with get_conn() as c:
        c.execute(
            """INSERT INTO usage
               (created_at, day, claim_id, operation, model,
                input_tokens, output_tokens, cost_usd)
               VALUES (?,?,?,?,?,?,?,?)""",
            (ts, day, claim_id, operation, model,
             input_tokens, output_tokens, cost_usd),
        )


def usage_totals() -> dict:
    """Overall + last-30-day + this-month totals."""
    with get_conn() as c:
        total = c.execute(
            "SELECT COALESCE(SUM(cost_usd),0) AS c, COALESCE(SUM(input_tokens+output_tokens),0) AS t, COUNT(*) AS n FROM usage"
        ).fetchone()
        m = datetime.utcnow().strftime("%Y-%m")
        month = c.execute(
            "SELECT COALESCE(SUM(cost_usd),0) AS c, COUNT(*) AS n FROM usage WHERE day LIKE ?",
            (m + "%",),
        ).fetchone()
        today = datetime.utcnow().strftime("%Y-%m-%d")
        d = c.execute(
            "SELECT COALESCE(SUM(cost_usd),0) AS c, COUNT(*) AS n FROM usage WHERE day = ?",
            (today,),
        ).fetchone()
        return {
            "total_usd": round(total["c"], 4),
            "total_tokens": total["t"],
            "total_calls": total["n"],
            "month_usd": round(month["c"], 4),
            "month_calls": month["n"],
            "today_usd": round(d["c"], 4),
            "today_calls": d["n"],
        }


def usage_by_day(days: int = 14) -> list[dict]:
    with get_conn() as c:
        rows = c.execute(
            """SELECT day, ROUND(SUM(cost_usd), 4) AS cost_usd,
               SUM(input_tokens) AS in_tok, SUM(output_tokens) AS out_tok, COUNT(*) AS calls
               FROM usage GROUP BY day ORDER BY day DESC LIMIT ?""",
            (days,),
        ).fetchall()
        return [dict(r) for r in rows][::-1]


def usage_by_month(months: int = 6) -> list[dict]:
    with get_conn() as c:
        rows = c.execute(
            """SELECT substr(day, 1, 7) AS month,
               ROUND(SUM(cost_usd), 4) AS cost_usd,
               COUNT(*) AS calls
               FROM usage GROUP BY month ORDER BY month DESC LIMIT ?""",
            (months,),
        ).fetchall()
        return [dict(r) for r in rows][::-1]


def claim_stats_by_day(days: int = 14) -> list[dict]:
    """Per-day: total claims created, auto_approved (STP), assisted, manual."""
    with get_conn() as c:
        rows = c.execute(
            """SELECT substr(created_at, 1, 10) AS day,
               COUNT(*) AS total,
               SUM(CASE WHEN decision_outcome = 'auto_approved' THEN 1 ELSE 0 END) AS stp,
               SUM(CASE WHEN decision_outcome = 'assisted' THEN 1 ELSE 0 END) AS assisted,
               SUM(CASE WHEN decision_outcome = 'manual' THEN 1 ELSE 0 END) AS manual
               FROM claims GROUP BY day ORDER BY day DESC LIMIT ?""",
            (days,),
        ).fetchall()
        return [dict(r) for r in rows][::-1]


def claim_stats_by_month(months: int = 6) -> list[dict]:
    with get_conn() as c:
        rows = c.execute(
            """SELECT substr(created_at, 1, 7) AS month,
               COUNT(*) AS total,
               SUM(CASE WHEN decision_outcome = 'auto_approved' THEN 1 ELSE 0 END) AS stp,
               SUM(CASE WHEN decision_outcome = 'assisted' THEN 1 ELSE 0 END) AS assisted,
               SUM(CASE WHEN decision_outcome = 'manual' THEN 1 ELSE 0 END) AS manual
               FROM claims GROUP BY month ORDER BY month DESC LIMIT ?""",
            (months,),
        ).fetchall()
        return [dict(r) for r in rows][::-1]


def stp_summary() -> dict:
    with get_conn() as c:
        r = c.execute(
            """SELECT
               COUNT(*) AS total,
               SUM(CASE WHEN decision_outcome = 'auto_approved' THEN 1 ELSE 0 END) AS stp,
               SUM(CASE WHEN decision_outcome = 'assisted' THEN 1 ELSE 0 END) AS assisted,
               SUM(CASE WHEN decision_outcome = 'manual' THEN 1 ELSE 0 END) AS manual,
               SUM(CASE WHEN decision_outcome IS NULL THEN 1 ELSE 0 END) AS pending
               FROM claims"""
        ).fetchone()
        total = r["total"] or 0
        decided = (r["stp"] or 0) + (r["assisted"] or 0) + (r["manual"] or 0)
        stp_rate = (r["stp"] / decided) if decided else 0
        return {
            "total": total, "stp": r["stp"] or 0,
            "assisted": r["assisted"] or 0, "manual": r["manual"] or 0,
            "pending": r["pending"] or 0,
            "stp_rate": round(stp_rate, 3),
        }
