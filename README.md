# Boxora

End-to-end motor-insurance claim pipeline for the Dutch market, on .NET 9 and
PostgreSQL. FNOL → document extraction → damage-photo analysis → RAG-grounded
liability reasoning → deterministic rules engine → decision tier + audit trail.

**Two architecture rules the whole thing hangs on:**

1. The model never makes the approve/deny call. It extracts structured data with
   confidences; a deterministic rule engine decides. It never denies — denials
   always route to a human.
2. The model never free-recalls law. Every legal statement must cite a passage
   that was retrieved into its prompt, and each citation is verified afterwards.
   An unresolvable citation blocks straight-through processing.

## Run it

```bash
make up
```

That creates the database inside the running `counted-db-1` Docker Postgres,
applies the Flyway migrations, builds, seeds twelve demo claims if the database
is empty, and starts the app on <http://localhost:8080>.

### Sign in

Two audiences, two front doors, one cookie scheme separated by policy.

| | URL | Demo account | Password |
|---|---|---|---|
| **Handler workspace** | `/login` | `alex.terlouw@example.nl` (senior adjuster) | `demo1234` |
| | | `lin.voormans@example.nl` (fraud specialist) | `demo1234` |
| | | `dara.aksoy@example.nl` (injury department) | `demo1234` |
| | | `bas.kortenhof@example.nl` (liability department) | `demo1234` |
| | | `sam.dejong@example.nl` · `mira.wortel@example.nl` | `demo1234` |
| **Customer portal** | `/portal/login` | `jan.devries@example.nl` | `demo1234` |

Demo passwords are set once at seed time and never overwrite a changed password.
Production path is SSO (NFR-3); `Auth.cs` keeps the cookie scheme so only the
sign-in endpoints change.

```
make help      list every target
make smoke     53 offline checks, no Azure credentials needed
make seed      reload the demo claims
make reseed    wipe, migrate, re-seed
make embed     vectorise new/changed legal passages
make reset     drop and recreate the database
make psql      psql shell on the app database
make corpus    corpus status by document class
```

The app runs fully **without** Azure OpenAI credentials: every model call returns
a deterministic stub and legal retrieval falls back to its lexical arm. Fill in
`AZURE_OPENAI_KEY` in `.env` to switch the real pipeline on, then `make embed`.

## Stack

| Layer | Choice |
|---|---|
| Web | ASP.NET Core 9, Razor Pages + minimal APIs |
| Data | PostgreSQL 17 + pgvector, Dapper (no ORM), JSONB for AI payloads |
| Schema | Flyway (`db/migration`), repeatable migration for the legal corpus |
| Models | Azure OpenAI over `HttpClient` — chat, vision, embeddings |
| Media | ImageSharp (pHash, EXIF), PdfPig (text) |
| UI | Hand-written CSS in the Claude palette, NL/EN, no CDN, no build step |

Postgres lives in the existing `counted-db-1` container; `start.sh` only creates
its own database (`jb_auto_ai`) and role inside it.

## Layout

```
db/migration/     V1 baseline · V2 legal KB · V3 auth+portal · R__ corpus (repeatable)
src/JbAutoAi/
  Program.cs      routes, CLI modes (--smoke --seed --embed), language switch
  Db.cs           Dapper data access + row models
  Rules.cs        deterministic Dutch motor-claim rules engine
  Legal.cs        hybrid retrieval, RRF, citation verification, embedding pass
  Llm.cs          Azure OpenAI: classify, extract, vision, summarise, liability, chat
  Pipeline.cs     ingest + the FNOL→decision orchestration
  Media.cs        pHash, EXIF forensics, PDF text
  I18n.cs         NL/EN strings
  Auth.cs         PBKDF2 hashing, staff/customer principals
  PortalView.cs   what a policyholder is allowed to see
  Seed.cs         twelve demo claims covering every decision path
  Smoke.cs        the check suite
  Pages/          Index · Fnol · Claim · Legal · Login
  Pages/Portal/   Login · Register · Index · New · Claim
legacy-python/    the FastAPI/SQLite original this replaced
```

## Endpoints

| Method | Path | Purpose |
|---:|---|---|
| GET | `/` | Dashboard — STP rate, citation integrity, spend, charts |
| GET | `/fnol` · POST | Register a claim |
| GET | `/claims/{id}` | Claim workspace |
| POST | `/claims/{id}/upload` | Attach documents and photos |
| POST | `/claims/{id}/analyze` | Run the full pipeline |
| POST | `/claims/{id}/assign` · `/delegate` · `/note` · `/email` | Handler workflow |
| GET | `/legal` | Handler-facing law lookup |
| GET | `/api/legal/search?q=&asOf=&docClass=` | Hybrid retrieval as JSON |
| GET | `/api/legal/chunk/{id}` | One passage by id |
| GET | `/api/metrics` | STP, spend, citation health |
| GET | `/api/health` | Probe, including corpus status |
| POST | `/api/chat` | SSE claim assistant, grounded + cited |
| GET | `/lang/{nl\|en}` | Language switch |
| GET | `/login` · `/portal/login` · `/portal/register` | Sign-in (anonymous) |
| POST | `/logout` | Sign out |
| GET | `/portal` | The policyholder's own claims |
| GET | `/portal/new` · POST | Self-service FNOL |
| GET | `/portal/claims/{id}` | Progress, timeline, documents |
| POST | `/portal/claims/{id}/upload` · `/comment` | Customer uploads and messages |

## Customer portal

A policyholder signs in at `/portal/login`, reports a claim, attaches photos and
documents, messages their handler, and watches a three-step progress timeline.

The privacy line is drawn in the data, not in the templates. `activity` rows are
internal by default; only rows explicitly flagged `visible_to_customer` reach the
portal, so a fraud escalation or a delegation note cannot leak by someone
forgetting a filter in a view. Claim reads go through
`Db.GetClaimForPortalUser(claimId, userId)` — ownership is part of the query, so a
guessed id is a 404 rather than someone else's file. Decision states are collapsed
to three coarse steps: "manual review" and "referred to the fraud team" look
identical from outside, because telling a claimant they are under investigation is
neither our call nor PIFI-compliant.

Verified access matrix:

| | anon | customer | staff |
|---|---|---|---|
| `/`, `/claims/{id}`, `/legal`, `/api/metrics` | 302 | 302 | 200 |
| `/portal`, own claim | 302 | 200 | 302 |
| another user's claim | 302 | **404** | 302 |
| `/api/health` | 200 | 200 | 200 |

## Pipeline

```
FNOL → ingest → classify → extract → merge → fraud screen → persist
     → retrieve law in force on the incident date
     → liability analysis (RAG)  → summary (RAG)
     → verify citations → rules engine → decision → auto-route → audit
```

Re-running analysis is idempotent: it recomputes from the documents on file and
replaces the decision, keeping the activity trail.

## Auto-approval envelope (default)

All of:

- amount ≤ `AUTO_APPROVE_CAP_EUR` (default €2500)
- extraction confidence ≥ `EXTRACTION_CONFIDENCE_MIN` (default 0.85)
- no personal injury, no third-party dispute
- fraud score < 0.30
- within the 5-year BW 3:310 limitation, notified within 30 days
- not classified total loss
- at least one photo and one repair estimate
- **citation integrity = 1.0** — every legal reference resolves

Hard blockers (→ manual, never auto-denied): personal injury, limitation, fraud
score, invalid loss date, total loss, unresolvable citations.

## Legal knowledge base (FR-11)

A versioned Dutch motor-law corpus lives in Postgres and grounds every legal
statement the system makes.

- **Corpus**: 34 documents / 35 passages — BW, WAM, WVW 1994, RVV 1990, Hoge Raad
  doctrine, OVS, Bedrijfsregeling 7, PIFI, GBL, Kifid, AVG art. 22, AI Act Annex III,
  coverage tiers, WOK/total loss. Three limitation regimes are kept distinct —
  BW 3:310 (tort), BW 7:942 (own insurer), WAM 10 (direct action).
- **Versioned twice over**: `legal_corpus_version` pins which corpus build informed
  a decision; `valid_from`/`valid_to` on each document applies *the law in force on
  the incident date*, so a 2019 claim is never grounded in 2024 law.
- **Hybrid retrieval**: dense (pgvector cosine, HNSW, 1024-d) + lexical (GIN +
  `ts_rank_cd` over an OR-of-lexemes query), fused with Reciprocal Rank Fusion,
  filtered by document class and date.
- **Citation verification**: the model emits `[[cite:CHUNK_ID]]` markers. Each id
  must be in the set that was actually put in its prompt — resolving against the
  wider corpus would let free-recall through, so we deliberately do not. Anything
  else is recorded as unresolved, shown to the handler, and blocks STP.
- **Audit**: `claim_legal_citation` snapshots the exact passage text, score,
  retrieval mode and corpus version per claim, so an assessment stays reproducible
  after the corpus moves on.
- **Handler lookup**: `/legal` runs the same retrieval, with an as-of date picker.

Corpus content, licensing and the review gate: [docs/LEGAL-CORPUS.md](docs/LEGAL-CORPUS.md).

### Updating the corpus

```bash
$EDITOR db/migration/R__legal_corpus.sql   # edit a passage
make migrate                               # Flyway re-runs it, nulls changed embeddings
make embed                                 # re-embeds only what changed
```

## Fraud detection

Signals recorded per claim (severity → contribution):

| Code | Severity | Δ |
|---|---|---|
| `PHOTO_RECYCLED` | high | +0.60 | perceptual-hash match against a prior claim's photo |
| `PHOTO_EXIF_DATE_MISMATCH` | high / medium | +0.40 / +0.20 | EXIF capture >30 / >3 days from the loss date |
| `PHOTO_NO_EXIF` | low | +0.05 | screenshot or re-saved image |
| `REPORT_VERY_LATE` | medium | +0.20 | filed > 180 days after loss |
| `REPORT_LATE` | low | +0.10 | filed > 60 days after loss |
| `NO_EVIDENCE` | medium | +0.15 | nothing attached |
| `INJURY_THIRD_PARTY_LAYERED` | medium | +0.10 | injury and third party combined |
| `AMOUNT_HIGH_CONF_LOW` | medium | +0.10 | >€5k with extraction confidence < 0.6 |

Score ≥ 0.30 blocks auto-approval via `FRAUD_SIGNALS_LOW` and routes to the SIU
queue. Fraud indicators never trigger an autonomous denial; EVR registration
remains a human decision under PIFI.

## Cost tracking

Every model call records input/output tokens and USD cost against a pricing table
in `Llm.cs` (longest-key match, so a `-mini` deployment is not priced as its
parent). The dashboard shows today / month / lifetime plus daily and monthly
rollups.

## Prompt-injection guard

Repair estimates, emails and photo text are untrusted input. Every extraction
system prompt instructs the model to ignore instructions found in the document
body, and the rules engine — the sole decisionmaker — cannot be influenced by
document content.

## Legal anchors (advisory)

WAM art. 2/3/6/11/22 · BW 3:310, 6:98, 6:101, 6:162, 6:170, 7:928, 7:941, 7:952 ·
WVW 1994 art. 5 and 185 · RVV 1990 art. 15, 18, 19, 54 · OVS · Bedrijfsregeling 7 ·
PIFI · GBL · AVG art. 22 · AI Act Annex III.

Rule-to-article mapping lives in `Rules.cs`; the passages behind it live in the
corpus. **Both must be reviewed by legal counsel before production use.**

## Known gaps

- Extraction returns stubs without `AZURE_OPENAI_KEY`, so seeded amounts come from
  the seed payloads rather than real document parsing.
- The corpus ships `review_status = 'draft'`. Case-law ECLI/NJ references are
  marked for the corpus owner to complete before curation.
- Passwords are demo-seeded and there is no reset flow; SSO/RBAC is NFR-3 work.
- Correspondence is drafted and stored, never sent — no SMTP wiring by design.
# claime2e
