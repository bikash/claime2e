# Legal corpus — sourcing, licensing, versioning, review gate

The corpus is the part of this system with the shortest half-life and the highest
blast radius. A stale or wrong passage does not fail loudly; it produces a
confident, well-cited, wrong decision. This document is the operating manual for
keeping that from happening.

## 1. What is in it today

Corpus `v1.0.0` — 32 documents, 33 passages, all `review_status = 'draft'`.

| Class | Count | Examples |
|---|---:|---|
| `statute` | 21 | BW 3:310 · 6:98 · 6:101 · 6:162 · 6:170 · 7:928 · 7:941 · 7:952; WAM 2/3/6/11/22; WVW 5 · 185; RVV 15 · 18 · 19 · 54; AVG 22; AI Act Annex III 5(c) |
| `case_law` | 4 | IZA/Vrerink (50%-regel) · Ingrid Kolkman · Marbeth van Uitregt · reflexwerking |
| `market_agreement` | 2 | OVS · Bedrijfsregeling 7 |
| `protocol` | 3 | PIFI/EVR · GBL · WOK / RDW total loss |
| `kifid` | 1 | Kifid geschilbeslechting |
| `policy_wording` | 1 | WA / beperkt casco / allrisk coverage tiers |

Source of truth: [`db/migration/R__legal_corpus.sql`](../db/migration/R__legal_corpus.sql).

## 2. Licensing — why some passages are descriptions only

`legal_doc.passage_kind` records what a passage actually is:

| Kind | Meaning | Applies to |
|---|---|---|
| `verbatim` | authoritative text, safe to quote | *(none yet)* |
| `summary` | our faithful rendering of public statute text | BW, WAM, WVW, RVV, AVG, AI Act |
| `licensed_summary` | scope and effect described; **no verbatim text** | OVS, bedrijfsregelingen, PIFI, GBL, Kifid, policy wordings |

Statute text published on wetten.overheid.nl and EUR-Lex is freely reproducible.
The OVS collision schedules, the Verbond bedrijfsregelingen, the PIFI protocol and
insurer polisvoorwaarden are **third-party copyright**. They appear here as
descriptions of scope and effect only. Ingesting their text verbatim — which is
what you need for the OVS category tables to actually drive a liability split —
requires a licence from the Verbond van Verzekeraars, and per-insurer permission
for policy wordings.

**This is a commercial blocker on P1, not an engineering one.** Until the licence
exists, OVS category mapping is descriptive and cannot be cited as a rule.

Case law from rechtspraak.nl is publicly available; the four entries here are
doctrine summaries with the court and date, and are flagged in-text for the corpus
owner to complete with ECLI and NJ references before curation.

## 3. Ownership

The citation guarantees are only as good as the curation behind them, so the
corpus needs a named owner, not a team.

| Role | Who | Accountable for |
|---|---|---|
| **Corpus owner** | in-house counsel or a contracted NL insurance lawyer | promoting `draft → curated`; sign-off on every passage that grounds a production decision |
| **Corpus maintainer** | platform engineer | the ingestion and re-embedding pipeline, retrieval quality, integrity metrics |
| **Reviewer of record** | second lawyer, per release | spot-check of the diff before a corpus version goes active |

Recommendation: keep the corpus owner *outside* the delivery team. The failure
mode to design against is a maintainer editing a passage to make a retrieval test
pass.

## 4. Review gate

Nothing reaches a production decision at `draft`.

```
authored → draft → legal review → curated → active corpus version
```

- `review_status = 'draft'` — machine-authored or edited, unreviewed.
- `review_status = 'curated'` — signed off by the corpus owner against the primary
  source, with the citation verified to resolve.

The UI badges the status on every passage in `/legal` and on the claim workspace,
so a handler always sees whether the ground under a decision has been reviewed.

**Recommended production gate:** refuse to activate a corpus version while any
`statute` or `case_law` document in it is still `draft`. That is one predicate in
`Db.ActiveCorpusVersion()` and is deliberately not enforced yet — it would make the
demo corpus unusable.

## 5. Versioning

Two independent axes, and conflating them is the classic mistake:

**Corpus version** (`legal_corpus_version`) — which *build of our knowledge base*
informed a decision. Recorded on every claim as `legal_corpus_version`. Lets you
reproduce an assessment after the corpus has moved on.

**Temporal validity** (`legal_doc.valid_from` / `valid_to`) — which *law was in
force on the incident date*. Retrieval filters on the claim's loss date, not on
today. A 2019 collision is judged under 2019 law even if the article was amended
in 2023; you add a new row with the new `valid_from` and close the old one with
`valid_to`, you never edit history.

Both are covered by smoke checks (`the same query does retrieve it for a current
incident` / `law that post-dates the incident is filtered out`).

## 6. Cadence

| Trigger | Action | Owner |
|---|---|---|
| Staatsblad / Stb. amendment to BW, WAM, WVW, RVV | new `legal_doc` row with `valid_from`, close the predecessor | owner |
| Hoge Raad judgment touching art. 185 / 6:101 apportionment | new `case_law` doc | owner |
| Annual | full re-read of `statute` class against the primary source | owner |
| Quarterly | Kifid ruling sweep; OVS / bedrijfsregeling revision check | owner |
| Per release | `make corpus` + integrity metric review; re-embed | maintainer |
| On any passage edit | Flyway re-run nulls the embedding; `make embed` refills it | automatic |

## 7. Ingestion and re-embedding pipeline

The whole pipeline is two commands, by design — a corpus that is painful to update
does not get updated.

```bash
$EDITOR db/migration/R__legal_corpus.sql
make migrate     # Flyway re-runs the repeatable migration, upserts docs and chunks
make embed       # embeds only chunks whose embedding is NULL
```

The `ON CONFLICT` clause in the repeatable migration nulls a chunk's embedding
whenever its passage text changes, so "what needs re-embedding" is a database fact
rather than a thing anyone has to remember:

```sql
embedding = CASE WHEN legal_chunk.passage IS DISTINCT FROM excluded.passage
                 THEN NULL ELSE legal_chunk.embedding END
```

Embeddings are 1024-dimensional (explicit `dimensions` request), which keeps the
column under pgvector's 2000-dim HNSW index limit.

### Scaling past the demo corpus

At 33 passages, retrieval is trivial. Chunking becomes real work at the volumes
P1 implies — full case-law collections and per-insurer polisvoorwaarden:

- **Statutes** chunk naturally per article; keep one chunk per article until an
  article exceeds ~400 tokens, then split per *lid* and keep the article citation.
- **Case law** should be chunked per legal-ground paragraph (`r.o.`), with the ECLI
  and the paragraph number in the chunk id so a citation points at the exact passage.
- **Policy wordings** must carry `insurer` so the metadata filter can scope retrieval
  to the policy actually on the claim. Mixing insurers' wordings in one retrieval
  pool is a correctness bug, not a ranking problem.

## 8. Metrics to watch

| Metric | Target | Where |
|---|---|---|
| Citation integrity | 100% resolvable, 0 hallucinated | dashboard KPI, `/api/metrics` |
| Unresolved citations | 0 | `claim_legal_citation.verified = false` |
| Corpus embedded ratio | 100% | `make corpus`, `/api/health` |
| Passages still `draft` | 0 in production | `legal_doc.review_status` |
| Retrieval precision/recall | measured against a labelled query set | *not built yet* |

The last one is the honest gap: there is no golden query set. Building one — 100
claim narratives with the articles a senior handler would actually cite — is the
highest-value next piece of work on this component, and it is what turns
"retrieval looks reasonable" into a regression gate.

## 9. Open risks

1. **Licensing (blocking P1).** OVS category tables and bedrijfsregeling text
   cannot be ingested verbatim without a Verbond licence. Liability splits derived
   from OVS categories are therefore not citable today.
2. **Case-law references incomplete.** ECLI/NJ numbers are deliberately absent
   rather than guessed. A corpus whose purpose is citation integrity must not ship
   invented citations — the entries carry an in-text note for the owner.
3. **No golden evaluation set.** Retrieval quality is currently assessed by
   inspection plus a handful of smoke assertions.
4. **Dutch stemming only.** The lexical arm uses the `dutch` text-search config.
   English-language documents in a claim file will under-retrieve.
5. **Corpus maintenance is unfunded.** The cadence in §6 is perhaps 0.2 FTE of
   qualified legal time. Without it the corpus silently rots, and rotting is the
   one failure mode this whole design cannot detect on its own.
