-- V3 — authentication for staff, and a self-service portal for policyholders.
--
-- Two audiences share one cookie scheme, separated by an authorization policy:
--   staff    → the claim workspace, everything
--   customer → their own claims only, and only the customer-safe slice of them
--
-- The privacy line is drawn in the data, not in the templates: activity rows are
-- internal by default and must be explicitly marked visible_to_customer. A fraud
-- escalation or a delegation note can therefore never leak into the portal by
-- someone forgetting a filter in a view.

ALTER TABLE handlers ADD COLUMN password_hash TEXT;
ALTER TABLE handlers ADD COLUMN last_login_at TIMESTAMPTZ;
CREATE UNIQUE INDEX idx_handlers_email ON handlers(lower(email));

CREATE TABLE portal_users (
    id            TEXT PRIMARY KEY,
    email         TEXT NOT NULL,
    name          TEXT NOT NULL,
    phone         TEXT,
    password_hash TEXT NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_login_at TIMESTAMPTZ
);
CREATE UNIQUE INDEX idx_portal_users_email ON portal_users(lower(email));

-- Which portal account may see a claim. NULL = staff-created, not portal-visible.
ALTER TABLE claims ADD COLUMN portal_user_id TEXT REFERENCES portal_users(id);
CREATE INDEX idx_claims_portal_user ON claims(portal_user_id, created_at DESC);

-- Internal by default. Only rows explicitly flagged reach the policyholder.
ALTER TABLE activity ADD COLUMN visible_to_customer BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE activity ADD COLUMN portal_user_id TEXT REFERENCES portal_users(id);

-- Who uploaded a document: a handler, or the policyholder through the portal.
ALTER TABLE documents ADD COLUMN uploaded_by_portal_user_id TEXT REFERENCES portal_users(id);
