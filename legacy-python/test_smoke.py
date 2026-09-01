"""Smoke checks — run without Azure keys. `python test_smoke.py` must exit 0."""
import os
import shutil
from pathlib import Path

# Use a throwaway DB so the check is idempotent.
here = Path(__file__).parent
tmp_db = here / "data" / "smoke.db"
shutil.rmtree(tmp_db.parent, ignore_errors=True)
tmp_db.parent.mkdir(parents=True, exist_ok=True)

import db  # noqa: E402
db.DB_PATH = tmp_db
db.init_db()
import rules  # noqa: E402


def test_fnol_and_defaults():
    cid = db.create_claim({
        "policyholder_name": "Jan de Vries",
        "policy_number": "P-1001",
        "license_plate": "12-ABC-3",
        "vin": "WVWZZZ1KZAW123456",
        "loss_date": "2026-08-01",
        "loss_location": "Amsterdam A10",
        "description": "Minor rear bumper damage in parking lot.",
        "third_party_involved": False,
        "injuries": False,
        "police_report_number": "",
    })
    c = db.get_claim(cid)
    assert c["claim_number"].startswith("NL-")
    assert c["license_plate"] == "12ABC3"
    return cid


def test_rules_missing_evidence():
    cid = test_fnol_and_defaults()
    claim = db.get_claim(cid)
    claim["estimated_amount_eur"] = 1200.0
    claim["extraction_confidence"] = 0.9
    result = rules.evaluate(claim, documents=[], fraud_score=0.0)
    # No docs -> EVIDENCE_MINIMUM fails, so not auto-approved but no hard blocker → assisted.
    assert result["outcome"] == "assisted", result
    codes = {r["code"]: r["ok"] for r in result["reasons"]}
    assert codes["EVIDENCE_MINIMUM"] is False
    assert codes["NO_PERSONAL_INJURY"] is True


def test_rules_auto_approve_happy_path():
    cid = db.create_claim({
        "policyholder_name": "Anna Bakker",
        "policy_number": "P-2002",
        "license_plate": "34-DEF-5",
        "vin": "",
        "loss_date": "2026-08-05",
        "loss_location": "Utrecht",
        "description": "Scratched fender.",
        "third_party_involved": False,
        "injuries": False,
        "police_report_number": "",
    })
    claim = db.get_claim(cid)
    claim["estimated_amount_eur"] = 800.0
    claim["extraction_confidence"] = 0.92
    docs = [
        {"doc_type": "photo", "extracted": {"severity": "minor"}},
        {"doc_type": "repair_estimate",
         "extracted": {"estimated_amount_eur": {"value": 800, "confidence": 0.9}}},
    ]
    result = rules.evaluate(claim, docs, fraud_score=0.1)
    assert result["outcome"] == "auto_approved", result


def test_rules_hard_blocker_injury():
    cid = db.create_claim({
        "policyholder_name": "P Q",
        "policy_number": "P-9",
        "license_plate": "99-XYZ-9",
        "vin": "",
        "loss_date": "2026-08-05",
        "loss_location": "Rotterdam",
        "description": "Collision, someone hurt.",
        "third_party_involved": True,
        "injuries": True,
        "police_report_number": "PR-1",
    })
    claim = db.get_claim(cid)
    claim["estimated_amount_eur"] = 500.0
    claim["extraction_confidence"] = 0.95
    docs = [
        {"doc_type": "photo"},
        {"doc_type": "repair_estimate"},
        {"doc_type": "police_report"},
    ]
    result = rules.evaluate(claim, docs, fraud_score=0.0)
    # Injury is a hard blocker → manual.
    assert result["outcome"] == "manual", result


def test_fraud_detects_recycled_photo():
    claim = {"loss_date": "2026-08-05", "injuries": False,
             "third_party_involved": False, "estimated_amount_eur": 500,
             "extraction_confidence": 0.9}
    out = rules.compute_fraud(claim, documents=[{"doc_type": "photo"}],
                              duplicate_photo_hits=1)
    assert out["score"] >= 0.6, out
    codes = [s["code"] for s in out["signals"]]
    assert "PHOTO_RECYCLED" in codes


def test_fraud_photo_exif_signal_propagates():
    claim = {"loss_date": "2026-08-05", "injuries": False,
             "third_party_involved": False, "estimated_amount_eur": 500,
             "extraction_confidence": 0.9}
    photo_signals = [{"code": "PHOTO_EXIF_DATE_MISMATCH", "severity": "high",
                      "message": "Photo taken 2025-01-01 vs loss 2026-08-05."}]
    out = rules.compute_fraud(claim, documents=[{"doc_type": "photo"}],
                              duplicate_photo_hits=0,
                              photo_signals=photo_signals)
    codes = [s["code"] for s in out["signals"]]
    assert "PHOTO_EXIF_DATE_MISMATCH" in codes
    assert out["score"] >= 0.4  # high severity -> +0.4


def test_usage_and_stp_summary_empty_ok():
    u = db.usage_totals()
    assert u["total_calls"] == 0
    s = db.stp_summary()
    assert s["total"] >= 3  # from previous tests
    assert s["stp_rate"] == 0.0  # nothing decided yet


def main():
    test_fnol_and_defaults()
    test_rules_missing_evidence()
    test_rules_auto_approve_happy_path()
    test_rules_hard_blocker_injury()
    test_fraud_detects_recycled_photo()
    test_fraud_photo_exif_signal_propagates()
    test_usage_and_stp_summary_empty_ok()
    print("smoke: all checks passed")


if __name__ == "__main__":
    main()
