-- Agentic workflows (FR-workflow v1).
--
-- Three tables: definition, ordered steps, run journal. Runner in
-- WorkflowRunner.cs dispatches per step-kind to existing helpers
-- (Llm, Pipeline, Http, email templates). Manual trigger only in v1.

CREATE TABLE workflow (
    id           TEXT PRIMARY KEY,
    name         TEXT NOT NULL,
    trigger_kind TEXT NOT NULL DEFAULT 'manual',   -- manual | new_claim
    active       BOOLEAN NOT NULL DEFAULT true,
    config       JSONB,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by   TEXT REFERENCES handlers(id)
);

CREATE TABLE workflow_step (
    id          BIGSERIAL PRIMARY KEY,
    workflow_id TEXT NOT NULL REFERENCES workflow(id) ON DELETE CASCADE,
    ordinal     INT  NOT NULL,
    kind        TEXT NOT NULL,   -- classify | extract | email | crm_push | webhook_call | decision | note
    config      JSONB,
    UNIQUE (workflow_id, ordinal)
);
CREATE INDEX idx_workflow_step ON workflow_step(workflow_id, ordinal);

CREATE TABLE workflow_run (
    id           BIGSERIAL PRIMARY KEY,
    workflow_id  TEXT NOT NULL REFERENCES workflow(id) ON DELETE CASCADE,
    claim_id     TEXT REFERENCES claims(id) ON DELETE SET NULL,
    trigger_ref  TEXT,
    status       TEXT NOT NULL DEFAULT 'pending',   -- pending | running | ok | error
    started_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    finished_at  TIMESTAMPTZ,
    error        TEXT,
    context      JSONB
);
CREATE INDEX idx_workflow_run ON workflow_run(workflow_id, started_at DESC);
CREATE INDEX idx_workflow_run_claim ON workflow_run(claim_id) WHERE claim_id IS NOT NULL;
