"""Seed 8 realistic dummy FNOLs with synthetic photos + docs, then run analysis.

Usage:
    source .venv/bin/activate
    python -m uvicorn app:app --port 8765  # in another terminal
    python seed.py

Directly inserts claims and documents into the DB (bypasses the upload
endpoint's real LLM extraction — instead we set the extracted_json ourselves
so the rules engine and fraud detector can be exercised offline).
Then calls POST /claims/{id}/analyze on the running server so the full
aggregation + rules + fraud path runs end-to-end.
"""
import hashlib
import io
import json
import urllib.request
from datetime import date, timedelta
from pathlib import Path

from PIL import Image, ImageDraw
import imagehash

import db

BASE = Path(__file__).parent
UPLOADS = BASE / "uploads"
UPLOADS.mkdir(exist_ok=True)
BASE_URL = "http://127.0.0.1:8765"


def render_photo(claim_dir: Path, name: str, palette: tuple[int, int, int],
                 seed: int) -> tuple[bytes, str]:
    """Build a tiny synthetic damage photo, save to disk, return (bytes, path)."""
    img = Image.new("RGB", (320, 240), palette)
    d = ImageDraw.Draw(img)
    # deterministic "dent" pattern by seed
    for i in range(6):
        x = 40 + (seed * 17 + i * 31) % 220
        y = 40 + (seed * 13 + i * 23) % 140
        d.rectangle([x, y, x + 30, y + 20], fill=(20, 20, 20))
    d.text((10, 10), name, fill=(255, 255, 255))
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=85)
    raw = buf.getvalue()
    path = claim_dir / name
    path.write_bytes(raw)
    return raw, str(path.relative_to(BASE))


def render_pdf_text(claim_dir: Path, name: str, body: str) -> tuple[bytes, str]:
    """Minimal 'PDF-ish' file: plain text with .pdf extension. The seed writes
    extracted_json directly so we don't need a real parser."""
    raw = body.encode()
    path = claim_dir / name
    path.write_bytes(raw)
    return raw, str(path.relative_to(BASE))


def add_photo(cid: str, claim_dir: Path, name: str, seed: int,
              extracted: dict, palette=(90, 110, 140),
              shared_bytes: bytes | None = None):
    if shared_bytes is not None:
        raw = shared_bytes
        path = claim_dir / name
        path.write_bytes(raw)
        filepath = str(path.relative_to(BASE))
    else:
        raw, filepath = render_photo(claim_dir, name, palette, seed)
    ch = hashlib.sha256(raw).hexdigest()
    with Image.open(io.BytesIO(raw)) as im:
        phash = str(imagehash.phash(im))
    db.add_document(
        claim_id=cid, filename=name, filepath=filepath, doc_type="photo",
        content_hash=ch, perceptual_hash=phash, extracted=extracted,
    )
    return raw


def add_estimate(cid: str, claim_dir: Path, amount: float, confidence: float):
    body = f"Repair estimate — parts + labour: EUR {amount:.2f}\n"
    raw, filepath = render_pdf_text(claim_dir, "repair_estimate.pdf", body)
    ch = hashlib.sha256(raw).hexdigest()
    ex = {
        "estimated_amount_eur": {"value": amount, "confidence": confidence},
        "labour_hours": {"value": 4.0, "confidence": confidence},
        "overall_confidence": confidence,
    }
    db.add_document(
        claim_id=cid, filename="repair_estimate.pdf", filepath=filepath,
        doc_type="repair_estimate", content_hash=ch, perceptual_hash=None,
        extracted=ex,
    )


def add_police_report(cid: str, claim_dir: Path, number: str):
    body = f"Politie proces-verbaal {number}\nBetrokken voertuigen: 2\n"
    raw, filepath = render_pdf_text(claim_dir, "police_report.pdf", body)
    ch = hashlib.sha256(raw).hexdigest()
    db.add_document(
        claim_id=cid, filename="police_report.pdf", filepath=filepath,
        doc_type="police_report", content_hash=ch, perceptual_hash=None,
        extracted={"police_report_number": {"value": number, "confidence": 0.95},
                   "overall_confidence": 0.9},
    )


def add_email(cid: str, claim_dir: Path, subject: str, body: str):
    text = f"Subject: {subject}\n\n{body}"
    raw, filepath = render_pdf_text(claim_dir, "customer_email.txt", text)
    ch = hashlib.sha256(raw).hexdigest()
    db.add_document(
        claim_id=cid, filename="customer_email.txt", filepath=filepath,
        doc_type="email", content_hash=ch, perceptual_hash=None,
        extracted={"overall_confidence": 0.7},
    )


def analyze(cid: str) -> None:
    req = urllib.request.Request(f"{BASE_URL}/claims/{cid}/analyze", method="POST")
    try:
        with urllib.request.urlopen(req) as resp:
            _ = resp.read()
    except urllib.error.HTTPError as e:
        # 303 redirect is expected
        if e.code != 303:
            raise


def d(offset_days: int) -> str:
    return (date.today() - timedelta(days=offset_days)).isoformat()


def seed_all():
    scenarios = []

    # 1 — Auto-approve: minor scratch, timely, docs complete.
    scenarios.append({
        "fnol": dict(policyholder_name="Jan de Vries", policy_number="NL-1001",
                     license_plate="12-ABC-3", vin="WVWZZZ1KZAW123456",
                     loss_date=d(2), loss_location="Amsterdam Zuidas",
                     description="Minor rear bumper scratch parking lot.",
                     third_party_involved=False, injuries=False),
        "photo_seed": 1, "palette": (70, 90, 120),
        "damage_areas": ["rear_bumper"], "severity": "minor",
        "estimate": 850, "conf": 0.92, "police": False, "shared_photo": True,
    })

    # 2 — Auto-approve: fender damage, higher amount but below cap.
    scenarios.append({
        "fnol": dict(policyholder_name="Anna Bakker", policy_number="NL-1002",
                     license_plate="34-DEF-5", vin="",
                     loss_date=d(5), loss_location="Utrecht Centrum",
                     description="Fender scraped on bollard.",
                     third_party_involved=False, injuries=False),
        "photo_seed": 2, "palette": (100, 100, 130),
        "damage_areas": ["front_fender"], "severity": "minor",
        "estimate": 1200, "conf": 0.89, "police": False,
    })

    # 3 — Assisted: third-party involved, own damage moderate.
    scenarios.append({
        "fnol": dict(policyholder_name="Ruben Peters", policy_number="NL-1003",
                     license_plate="56-GHI-7", vin="",
                     loss_date=d(3), loss_location="Rotterdam Kralingen",
                     description="Side-swipe by other vehicle at intersection.",
                     third_party_involved=True, injuries=False,
                     police_report_number="PV-2026-88123"),
        "photo_seed": 3, "palette": (90, 120, 100),
        "damage_areas": ["driver_door", "front_fender"], "severity": "moderate",
        "estimate": 1950, "conf": 0.88, "police": True,
    })

    # 4 — Manual: injury reported (hard blocker).
    scenarios.append({
        "fnol": dict(policyholder_name="Sofie Jansen", policy_number="NL-1004",
                     license_plate="78-JKL-9", vin="",
                     loss_date=d(1), loss_location="Den Haag Bezuidenhout",
                     description="T-bone collision, passenger reported neck pain.",
                     third_party_involved=True, injuries=True,
                     police_report_number="PV-2026-88410"),
        "photo_seed": 4, "palette": (140, 90, 90),
        "damage_areas": ["passenger_door", "b_pillar", "rear_quarter"],
        "severity": "severe", "estimate": 4400, "conf": 0.9, "police": True,
    })

    # 5 — Manual: over the €2500 cap.
    scenarios.append({
        "fnol": dict(policyholder_name="Marc de Boer", policy_number="NL-1005",
                     license_plate="90-MNO-1", vin="WBAAA1305C1234567",
                     loss_date=d(7), loss_location="Eindhoven",
                     description="Front-end collision with concrete pillar.",
                     third_party_involved=False, injuries=False),
        "photo_seed": 5, "palette": (60, 60, 90),
        "damage_areas": ["hood", "front_bumper", "grille", "headlight_left"],
        "severity": "severe", "estimate": 5800, "conf": 0.91, "police": False,
    })

    # 6 — Manual: recycled photo (shares perceptual hash with #1).
    scenarios.append({
        "fnol": dict(policyholder_name="Piet van Dijk", policy_number="NL-1006",
                     license_plate="11-PQR-2", vin="",
                     loss_date=d(4), loss_location="Groningen",
                     description="Rear damage — see attached photo.",
                     third_party_involved=False, injuries=False),
        "photo_seed": 1, "palette": (70, 90, 120),  # SAME as #1 => same phash
        "damage_areas": ["rear_bumper"], "severity": "minor",
        "estimate": 1100, "conf": 0.85, "police": False, "share_from": 0,
    })

    # 7 — Assisted: reported very late (>60d, <180d).
    scenarios.append({
        "fnol": dict(policyholder_name="Emma Visser", policy_number="NL-1007",
                     license_plate="22-STU-3", vin="",
                     loss_date=d(120), loss_location="Nijmegen",
                     description="Hail dent noticed after long trip.",
                     third_party_involved=False, injuries=False),
        "photo_seed": 7, "palette": (110, 110, 110),
        "damage_areas": ["roof", "hood"], "severity": "moderate",
        "estimate": 1600, "conf": 0.86, "police": False,
    })

    # 8 — Manual: total loss classified.
    scenarios.append({
        "fnol": dict(policyholder_name="Lars Smit", policy_number="NL-1008",
                     license_plate="44-VWX-5", vin="",
                     loss_date=d(3), loss_location="Tilburg",
                     description="Vehicle deemed total loss after collision.",
                     third_party_involved=True, injuries=False,
                     police_report_number="PV-2026-90001"),
        "photo_seed": 8, "palette": (50, 40, 40),
        "damage_areas": ["hood", "front_bumper", "engine_bay", "windshield",
                         "roof", "a_pillar"],
        "severity": "total_loss", "estimate": 12500, "conf": 0.93, "police": True,
    })

    # 9 — Auto-approve: clean simple parking dent, everything filed cleanly.
    scenarios.append({
        "fnol": dict(policyholder_name="Femke Aarts", policy_number="NL-1009",
                     license_plate="55-YZA-6", vin="",
                     loss_date=d(1), loss_location="Haarlem",
                     description="Small parking dent from shopping cart.",
                     third_party_involved=False, injuries=False),
        "photo_seed": 9, "palette": (95, 105, 125),
        "damage_areas": ["driver_door"], "severity": "minor",
        "estimate": 620, "conf": 0.94, "police": False,
    })

    # 10 — Manual + fraud routing: reported 260d late AND no supporting docs.
    #      Combined fraud (0.2 late + 0.15 no evidence + 0.05 photo-noexif noise) ≥ 0.30 → fraud gate blocks.
    scenarios.append({
        "fnol": dict(policyholder_name="Kees Nijhof", policy_number="NL-1010",
                     license_plate="66-BCD-7", vin="",
                     loss_date=d(260), loss_location="Enschede",
                     description="Just noticed old damage, submitting now.",
                     third_party_involved=False, injuries=False),
        "photo_seed": 10, "palette": (130, 60, 60),
        "damage_areas": [], "severity": "unknown",
        "estimate": 900, "conf": 0.4,
        "no_documents": True,  # skip photo + estimate to trigger NO_EVIDENCE
    })

    # 11 — Assisted: customer email but no photos (soft evidence gap only).
    scenarios.append({
        "fnol": dict(policyholder_name="Yasmin Ozturk", policy_number="NL-1011",
                     license_plate="77-EFG-8", vin="",
                     loss_date=d(4), loss_location="Almere",
                     description="Front bumper crack, waiting on repair quote.",
                     third_party_involved=False, injuries=False),
        "photo_seed": 11, "palette": (100, 130, 110),
        "damage_areas": ["front_bumper"], "severity": "minor",
        "estimate": 1050, "conf": 0.82, "police": False, "email_only": True,
    })

    # Pass 1: create + upload docs.
    created_ids: list[str] = []
    photo_bytes_by_index: dict[int, bytes] = {}

    for idx, s in enumerate(scenarios):
        cid = db.create_claim(s["fnol"])
        created_ids.append(cid)
        claim_dir = UPLOADS / cid
        claim_dir.mkdir(exist_ok=True)

        if s.get("no_documents"):
            # NO_EVIDENCE scenario — deliberately upload nothing.
            print(f"[{idx+1}] created {cid} — {s['fnol']['policyholder_name']} (no docs)")
            continue

        if s.get("email_only"):
            # Only a customer email — no photo, no estimate. Triggers EVIDENCE_MINIMUM (soft).
            add_email(cid, claim_dir,
                      subject=f"Claim for {s['fnol']['license_plate']}",
                      body=s['fnol']['description'])
            print(f"[{idx+1}] created {cid} — {s['fnol']['policyholder_name']} (email only)")
            continue

        # Photo
        vision_extracted = {
            "damage_areas": s["damage_areas"],
            "severity": s["severity"],
            "confidence": 0.9,
            "notes": "seeded synthetic damage photo",
        }
        share_idx = s.get("share_from")
        shared = photo_bytes_by_index.get(share_idx) if share_idx is not None else None
        raw = add_photo(cid, claim_dir, f"damage_{idx+1}.jpg",
                        seed=s["photo_seed"], extracted=vision_extracted,
                        palette=s["palette"], shared_bytes=shared)
        if s.get("shared_photo") or share_idx is None:
            photo_bytes_by_index[idx] = raw

        # Repair estimate
        add_estimate(cid, claim_dir, s["estimate"], s["conf"])
        # Optional police report
        if s.get("police"):
            add_police_report(cid, claim_dir,
                              s["fnol"].get("police_report_number") or "PV-UNKNOWN")
        # Customer email — always include
        add_email(cid, claim_dir,
                  subject=f"Motor claim for {s['fnol']['license_plate']}",
                  body=s['fnol']['description'])

        print(f"[{idx+1}] created {cid} — {s['fnol']['policyholder_name']}")

    # Pass 2: run analysis on the live server.
    for cid in created_ids:
        analyze(cid)
        c = db.get_claim(cid)
        print(f"    analyzed {c['claim_number']}: "
              f"{c['decision_outcome']} · €{c['estimated_amount_eur']} · "
              f"fraud {c['fraud_score']}")


if __name__ == "__main__":
    db.init_db()
    seed_all()
    print("\nseed done — open http://127.0.0.1:8765/")
