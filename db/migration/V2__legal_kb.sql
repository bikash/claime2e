-- V2 — FR-11: versioned Dutch motor-law corpus with hybrid retrieval and
-- verifiable citations.
--
-- Three ideas the schema has to carry:
--   1. temporal versioning — a claim is judged under the law in force on the
--      incident date, so documents are valid_from/valid_to ranged, not "latest";
--   2. corpus versioning — every decision records which corpus build informed
--      it, so an assessment stays reproducible after the corpus moves on;
--   3. citation integrity — the exact passage text is snapshotted onto the
--      claim, so an audit never depends on the live corpus row.

CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE legal_corpus_version (
    id           TEXT PRIMARY KEY,          -- 'v1.0.0'
    label        TEXT NOT NULL,
    published_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_active    BOOLEAN NOT NULL DEFAULT false,
    notes        TEXT
);
-- Exactly one active corpus build at a time.
CREATE UNIQUE INDEX idx_corpus_single_active ON legal_corpus_version(is_active) WHERE is_active;

CREATE TABLE legal_doc (
    id             TEXT PRIMARY KEY,
    corpus_version TEXT NOT NULL REFERENCES legal_corpus_version(id),
    citation       TEXT NOT NULL,           -- 'BW 6:162', 'WVW 185', 'OVS'
    source         TEXT NOT NULL,           -- 'Burgerlijk Wetboek', 'Verbond van Verzekeraars'
    doc_class      TEXT NOT NULL,           -- statute | market_agreement | protocol |
                                            -- case_law | kifid | policy_wording
    title          TEXT NOT NULL,
    jurisdiction   TEXT NOT NULL DEFAULT 'NL',
    insurer        TEXT,                    -- set for polisvoorwaarden; metadata filter
    -- Temporal versioning: the law in force on the incident date.
    valid_from     DATE NOT NULL,
    valid_to       DATE,                    -- NULL = still in force
    -- verbatim         = authoritative text, safe to quote
    -- summary          = our faithful rendering of public statute text
    -- licensed_summary = description only; verbatim ingestion needs a licence
    passage_kind   TEXT NOT NULL DEFAULT 'summary',
    -- draft = machine-authored, awaiting the legal review gate; curated = signed
    -- off by the corpus owner. Only `curated` should ground a production decision.
    review_status  TEXT NOT NULL DEFAULT 'draft',
    url            TEXT,
    UNIQUE (corpus_version, citation, valid_from)
);
CREATE INDEX idx_legal_doc_lookup ON legal_doc(corpus_version, doc_class, valid_from);
CREATE INDEX idx_legal_doc_citation ON legal_doc(citation);

-- 1024 dims: both text-embedding-3-small and -3-large accept an explicit
-- `dimensions` request, and staying ≤2000 keeps the column HNSW-indexable.
CREATE TABLE legal_chunk (
    id        TEXT PRIMARY KEY,
    doc_id    TEXT NOT NULL REFERENCES legal_doc(id) ON DELETE CASCADE,
    ordinal   INT NOT NULL,
    passage   TEXT NOT NULL,
    tags      TEXT NOT NULL DEFAULT '',
    embedding VECTOR(1024),                 -- NULL until the embedding pass runs
    tsv       TSVECTOR GENERATED ALWAYS AS
              (to_tsvector('dutch', passage || ' ' || tags)) STORED,
    UNIQUE (doc_id, ordinal)
);
-- Lexical arm of hybrid retrieval.
CREATE INDEX idx_legal_chunk_tsv ON legal_chunk USING GIN (tsv);
-- Dense arm. Cosine, matching the normalised embeddings we store.
CREATE INDEX idx_legal_chunk_vec ON legal_chunk
    USING hnsw (embedding vector_cosine_ops);

-- Snapshot of what the model was actually shown, per claim. `verified` is set by
-- the citation checker: a citation the model emitted that is not in this table
-- never reaches the handler and blocks straight-through processing.
CREATE TABLE claim_legal_citation (
    id             BIGSERIAL PRIMARY KEY,
    claim_id       TEXT NOT NULL REFERENCES claims(id) ON DELETE CASCADE,
    chunk_id       TEXT NOT NULL,
    citation       TEXT NOT NULL,
    title          TEXT NOT NULL,
    passage        TEXT NOT NULL,           -- snapshot, not a live join
    url            TEXT,
    score          DOUBLE PRECISION NOT NULL DEFAULT 0,
    retrieval_mode TEXT NOT NULL,           -- dense | lexical | hybrid
    corpus_version TEXT NOT NULL,
    used_in        TEXT NOT NULL,           -- retrieved | summary | liability | decision
    verified       BOOLEAN NOT NULL DEFAULT true,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_claim_citation ON claim_legal_citation(claim_id, used_in);

ALTER TABLE claims ADD COLUMN legal_corpus_version TEXT;
ALTER TABLE claims ADD COLUMN legal_citations JSONB;
-- share of emitted citations that resolved to a retrieved passage; 1.0 = clean
ALTER TABLE claims ADD COLUMN citation_integrity DOUBLE PRECISION;
