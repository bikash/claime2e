-- V1 — claim core.
-- JSONB for every AI-produced payload (extraction, rules trace, fraud signals,
-- assessment) so the audit trail stays queryable without a second store.

CREATE TABLE handlers (
    id      TEXT PRIMARY KEY,
    name    TEXT NOT NULL,
    email   TEXT NOT NULL,
    role    TEXT NOT NULL,
    active  BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE email_templates (
    id       TEXT PRIMARY KEY,
    name     TEXT NOT NULL,
    audience TEXT NOT NULL,
    subject  TEXT NOT NULL,
    body     TEXT NOT NULL
);

-- Claim numbers come from a sequence so concurrent FNOLs cannot collide.
CREATE SEQUENCE claim_seq;

CREATE TABLE claims (
    id                    TEXT PRIMARY KEY,
    claim_number          TEXT UNIQUE NOT NULL
                          DEFAULT 'NL-' || to_char(now(), 'YYYY') || '-'
                                  || lpad(nextval('claim_seq')::text, 5, '0'),
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    status                TEXT NOT NULL DEFAULT 'draft',
    policyholder_name     TEXT,
    policy_number         TEXT,
    license_plate         TEXT,
    vin                   TEXT,
    loss_date             DATE,
    loss_location         TEXT,
    description           TEXT,
    third_party_involved  BOOLEAN NOT NULL DEFAULT false,
    injuries              BOOLEAN NOT NULL DEFAULT false,
    police_report_number  TEXT,
    estimated_amount_eur  DOUBLE PRECISION,
    extraction_confidence DOUBLE PRECISION,
    fraud_score           DOUBLE PRECISION NOT NULL DEFAULT 0,
    damage_categories     JSONB,
    summary               TEXT,
    decision_outcome      TEXT,
    decision_reasons      JSONB,
    rules_trace           JSONB,
    fraud_signals         JSONB,
    assessment            JSONB,
    assigned_handler_id   TEXT REFERENCES handlers(id)
);
CREATE INDEX idx_claims_created ON claims(created_at DESC);

CREATE TABLE documents (
    id              TEXT PRIMARY KEY,
    claim_id        TEXT NOT NULL REFERENCES claims(id) ON DELETE CASCADE,
    filename        TEXT NOT NULL,
    filepath        TEXT NOT NULL,
    doc_type        TEXT,
    content_hash    TEXT,
    perceptual_hash TEXT,
    extracted       JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_docs_claim ON documents(claim_id);
CREATE INDEX idx_docs_phash ON documents(perceptual_hash);

CREATE TABLE activity (
    id               BIGSERIAL PRIMARY KEY,
    claim_id         TEXT NOT NULL REFERENCES claims(id) ON DELETE CASCADE,
    actor_handler_id TEXT REFERENCES handlers(id),
    kind             TEXT NOT NULL,   -- note | assigned | delegated | email_saved | decision
    body             TEXT,
    meta             JSONB,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_activity_claim ON activity(claim_id, created_at DESC);

CREATE TABLE llm_usage (
    id            BIGSERIAL PRIMARY KEY,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    claim_id      TEXT,
    operation     TEXT NOT NULL,
    model         TEXT NOT NULL,
    input_tokens  INT NOT NULL DEFAULT 0,
    output_tokens INT NOT NULL DEFAULT 0,
    cost_usd      DOUBLE PRECISION NOT NULL DEFAULT 0
);
CREATE INDEX idx_usage_created ON llm_usage(created_at);
