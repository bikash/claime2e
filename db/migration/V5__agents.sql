-- Agent studio (v1): configurable AI agents plus access grants.
--
-- The super admin designs agents and decides who may use them: a grant with a
-- NULL handler_id opens the agent to the whole company, a grant with a
-- handler_id opens it to that one person. UI in Pages/Agents.cshtml.

-- Studio events (create / deploy / revoke) are platform-level, not tied to a
-- claim, so the audit trail must accept rows without one.
ALTER TABLE activity ALTER COLUMN claim_id DROP NOT NULL;

CREATE TABLE agent (
    id           TEXT PRIMARY KEY,
    name         TEXT NOT NULL,
    template     TEXT NOT NULL,                    -- summariser | intake | fraud | comms | reserve
    lang         TEXT NOT NULL DEFAULT 'both',     -- en | nl | both
    tone         TEXT NOT NULL DEFAULT 'concise',  -- concise | formal | friendly
    tools        JSONB NOT NULL DEFAULT '[]',
    prompt       TEXT NOT NULL,
    active       BOOLEAN NOT NULL DEFAULT true,
    trigger_kind TEXT NOT NULL DEFAULT 'manual',   -- manual | new_claim | documents | status
    autonomy     TEXT NOT NULL DEFAULT 'suggest',  -- suggest | approval | auto
    created_by   TEXT REFERENCES handlers(id),
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE agent_grant (
    id         BIGSERIAL PRIMARY KEY,
    agent_id   TEXT NOT NULL REFERENCES agent(id) ON DELETE CASCADE,
    handler_id TEXT REFERENCES handlers(id) ON DELETE CASCADE,  -- NULL = entire company
    granted_by TEXT REFERENCES handlers(id),
    granted_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX uq_agent_grant ON agent_grant (agent_id, COALESCE(handler_id, ''));

INSERT INTO agent (id, name, template, lang, tone, tools, prompt, trigger_kind, autonomy) VALUES
('ag_summariser', 'Claim Summariser', 'summariser', 'both', 'concise',
 '["Policy lookup"]',
 'You summarise motor insurance claim files for the claim handlers of Boxora, a Dutch motor insurer. Produce a short, factual summary: what happened, damage and amounts, coverage position, open questions. Use the claim file only — never invent facts, and say explicitly when something is not in the file.',
 'manual', 'suggest'),
('ag_intake', 'FNOL Intake', 'intake', 'both', 'friendly',
 '["RDW vehicle registry","Policy lookup"]',
 'You handle first notice of loss (FNOL) intake for Boxora, a Dutch motor insurer. Collect: kenteken (Dutch license plate), date and location of loss, what happened, whether third parties are involved. If anyone is injured, stop and escalate to a human handler immediately. Classify the claim as collision, theft, glass, vandalism or storm.',
 'new_claim', 'approval'),
('ag_fraud', 'Fraud Screener', 'fraud', 'en', 'concise',
 '["CIS fraud database","Policy lookup"]',
 'You screen motor insurance claims of Boxora, a Dutch insurer, for fraud indicators: late reporting, recycled or manipulated photos, policy taken out shortly before the loss, inconsistent statements, prior CIS signals. Report the indicators you see and a risk score from 0 to 100. You flag risk for human investigation — you never accuse, and a score is never proof of fraud.',
 'new_claim', 'suggest'),
('ag_comms', 'Customer Comms', 'comms', 'nl', 'friendly',
 '["Email & letters"]',
 'You write customer communications for Boxora, a Dutch motor insurer. Write at B1 reading level in the claimant''s language: short sentences, no jargon, explain necessary insurance terms in plain words. Be empathetic and concrete about what happens next and what the claimant must do. Never promise coverage, payout amounts or timelines that the claim file does not confirm. Sign off as the Boxora claims team.',
 'status', 'approval'),
('ag_reserve', 'Reserve Advisor', 'reserve', 'en', 'formal',
 '["Repair network Schadegarant"]',
 'You advise on claim reserves for Boxora, a Dutch motor insurer, using Dutch repair-cost benchmarks (Schadegarant network rates, common parts and labour prices). Give a low, expected and high estimate in euros with a one-line rationale each. Your advice is advisory only; the handler sets the reserve.',
 'manual', 'suggest');

-- Ship every seeded agent company-wide so behaviour matches the pre-grant era.
INSERT INTO agent_grant (agent_id, handler_id, granted_by)
SELECT id, NULL, NULL FROM agent;
