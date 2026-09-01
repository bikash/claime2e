"""Deterministic Dutch motor-claim rules engine.

Legal anchors (advisory — implementation MUST be reviewed by legal counsel
before production use):

- WAM (Wet aansprakelijkheidsverzekering motorrijtuigen) art. 3: mandatory
  third-party liability insurance for motor vehicles in NL.
- WAM art. 22: statutory minimum coverage (currently EUR 6.45M property /
  EUR 1.3B personal injury per event).
- BW art. 3:310: 5-year limitation from knowledge of damage & liable party.
- BW art. 6:98 / 6:162: causation & unlawful act (grondslag for liability).
- WVW art. 185: strict liability of motor vehicle owner toward
  non-motorised road users (pedestrians / cyclists).
- Notification: WAM does not fix a hard deadline, but standard NL policies
  require notification "zo spoedig mogelijk" — 30 days is used here as a
  soft outer bound flag.

The rules engine NEVER auto-denies. Denials always route to a human.
"""
import os
import re
from datetime import date, datetime
from typing import Any

VIN_RE = re.compile(r"^[A-HJ-NPR-Z0-9]{17}$")
NL_PLATE_RE = re.compile(r"^[A-Z0-9]{5,8}$")  # normalised (dashes stripped)


def _cap() -> float:
    return float(os.environ.get("AUTO_APPROVE_CAP_EUR", 2500))


def _conf_min() -> float:
    return float(os.environ.get("EXTRACTION_CONFIDENCE_MIN", 0.85))


def _parse_date(s: str | None) -> date | None:
    if not s:
        return None
    try:
        return datetime.strptime(s[:10], "%Y-%m-%d").date()
    except (ValueError, TypeError):
        return None


def evaluate(claim: dict, documents: list[dict], fraud_score: float) -> dict:
    """Run the deterministic checks. Return {outcome, reasons, trace}.

    outcome: auto_approved | assisted | manual
    reasons: list of {code, ok, message, legal_ref?}
    trace: dict — the raw values checked (audit trail)
    """
    reasons: list[dict] = []
    trace: dict[str, Any] = {}

    # --- 1. Basic FNOL completeness -------------------------------------------
    required = ["policyholder_name", "policy_number", "license_plate",
                "loss_date", "loss_location", "description"]
    missing = [k for k in required if not claim.get(k)]
    trace["missing_fnol_fields"] = missing
    reasons.append({
        "code": "FNOL_COMPLETE",
        "ok": not missing,
        "message": "All required FNOL fields present." if not missing
                   else f"Missing FNOL fields: {', '.join(missing)}.",
    })

    # --- 2. Plate / VIN format -------------------------------------------------
    plate = (claim.get("license_plate") or "").upper().replace("-", "")
    vin = (claim.get("vin") or "").upper()
    plate_ok = bool(NL_PLATE_RE.match(plate)) if plate else False
    vin_ok = bool(VIN_RE.match(vin)) if vin else True  # VIN optional at FNOL
    trace["plate"] = plate
    trace["vin"] = vin
    reasons.append({
        "code": "VEHICLE_ID_FORMAT",
        "ok": plate_ok and vin_ok,
        "message": (
            "Plate/VIN format valid." if plate_ok and vin_ok
            else "Plate or VIN has unexpected format. Adjuster review."
        ),
    })

    # --- 3. Loss date + notification window ----------------------------------
    loss = _parse_date(claim.get("loss_date"))
    today = date.today()
    days_since = (today - loss).days if loss else None
    trace["days_since_loss"] = days_since
    date_ok = loss is not None and loss <= today
    reasons.append({
        "code": "LOSS_DATE_VALID",
        "ok": date_ok,
        "message": "Loss date parseable and not in the future." if date_ok
                   else "Loss date missing or in the future.",
        "legal_ref": "BW 3:310 (limitation 5y)",
    })

    within_notice = days_since is not None and 0 <= days_since <= 30
    reasons.append({
        "code": "NOTIFICATION_TIMELY",
        "ok": within_notice,
        "message": (f"Reported {days_since} days after loss." if days_since is not None
                    else "Cannot determine notification delay."),
        "legal_ref": "Policy: notification zo spoedig mogelijk",
    })

    # BW 3:310 5-year absolute cut-off for limitation
    limitation_ok = days_since is not None and days_since <= 5 * 365
    reasons.append({
        "code": "WITHIN_LIMITATION",
        "ok": limitation_ok,
        "message": "Within 5-year limitation window." if limitation_ok
                   else "Outside 5-year limitation (BW 3:310). Manual review.",
        "legal_ref": "BW 3:310",
    })

    # --- 4. Injuries / third-party liability ---------------------------------
    injuries = bool(claim.get("injuries"))
    third_party = bool(claim.get("third_party_involved"))
    trace["injuries"] = injuries
    trace["third_party_involved"] = third_party
    reasons.append({
        "code": "NO_PERSONAL_INJURY",
        "ok": not injuries,
        "message": "No personal injury reported." if not injuries
                   else "Personal injury reported — WVW 185 / BW 6:162 exposure. Manual review.",
        "legal_ref": "WVW 185, BW 6:162",
    })
    reasons.append({
        "code": "NO_THIRD_PARTY_DISPUTE",
        "ok": not third_party,
        "message": "First-party only." if not third_party
                   else "Third-party involved — WAM liability path. Assisted review.",
        "legal_ref": "WAM art. 3",
    })

    # --- 5. Amount envelope ---------------------------------------------------
    amt = claim.get("estimated_amount_eur")
    cap = _cap()
    trace["estimated_amount_eur"] = amt
    trace["auto_approve_cap_eur"] = cap
    amt_ok = amt is not None and 0 < amt <= cap
    reasons.append({
        "code": "AMOUNT_WITHIN_CAP",
        "ok": amt_ok,
        "message": (
            f"Estimated EUR {amt:.2f} within auto-approve cap EUR {cap:.2f}."
            if amt_ok else
            f"Estimated amount EUR {amt} exceeds cap EUR {cap:.2f} or is missing."
        ),
    })

    # --- 6. Extraction confidence --------------------------------------------
    conf = claim.get("extraction_confidence")
    conf_min = _conf_min()
    trace["extraction_confidence"] = conf
    trace["extraction_confidence_min"] = conf_min
    conf_ok = conf is not None and conf >= conf_min
    reasons.append({
        "code": "EXTRACTION_CONFIDENT",
        "ok": conf_ok,
        "message": (
            f"Extraction confidence {conf:.2f} ≥ threshold {conf_min:.2f}."
            if conf_ok else
            f"Extraction confidence {conf} below threshold {conf_min:.2f}."
        ),
    })

    # --- 7. Fraud signals -----------------------------------------------------
    trace["fraud_score"] = fraud_score
    fraud_ok = fraud_score < 0.3
    reasons.append({
        "code": "FRAUD_SIGNALS_LOW",
        "ok": fraud_ok,
        "message": (f"Fraud score {fraud_score:.2f} below 0.30 threshold."
                    if fraud_ok else
                    f"Fraud signals present (score {fraud_score:.2f}). Manual review."),
    })

    # --- 8. Total loss --------------------------------------------------------
    dmg_cats = claim.get("damage_categories") or "[]"
    total_loss_flagged = "total_loss" in (dmg_cats if isinstance(dmg_cats, str) else "")
    reasons.append({
        "code": "NOT_TOTAL_LOSS",
        "ok": not total_loss_flagged,
        "message": "Not classified as total loss." if not total_loss_flagged
                   else "Total loss classified — requires salvage/valuation review.",
    })

    # --- 9. Supporting evidence present --------------------------------------
    has_photo = any(d.get("doc_type") == "photo" for d in documents)
    has_estimate = any(d.get("doc_type") == "repair_estimate" for d in documents)
    evidence_ok = has_photo and has_estimate
    reasons.append({
        "code": "EVIDENCE_MINIMUM",
        "ok": evidence_ok,
        "message": ("At least one photo and one repair estimate on file."
                    if evidence_ok else
                    "Missing photo and/or repair estimate. Adjuster to request."),
    })

    # --- Outcome tiering ------------------------------------------------------
    all_pass = all(r["ok"] for r in reasons)
    hard_blockers = {"NO_PERSONAL_INJURY", "WITHIN_LIMITATION",
                     "FRAUD_SIGNALS_LOW", "LOSS_DATE_VALID",
                     "NOT_TOTAL_LOSS"}
    hard_fail = any(not r["ok"] and r["code"] in hard_blockers for r in reasons)

    if all_pass:
        outcome = "auto_approved"
    elif hard_fail:
        outcome = "manual"
    else:
        outcome = "assisted"

    return {"outcome": outcome, "reasons": reasons, "trace": trace}


def compute_fraud(claim: dict, documents: list[dict],
                  duplicate_photo_hits: int,
                  photo_signals: list[dict] | None = None) -> dict:
    """Additive fraud scoring. Returns {score: 0..1, signals: [...]}.

    Signals include type-of-check + severity so the UI can flag them.
    Score > 0.3 blocks auto-approval via the AMOUNT/FRAUD rule gate.

    ponytail: additive heuristic. Upgrade path — swap for a trained classifier
    once leakage/false-approval telemetry justifies it.
    """
    signals: list[dict] = []
    score = 0.0

    # 1. Recycled photo across prior claims — strongest single signal.
    if duplicate_photo_hits > 0:
        score += 0.6
        signals.append({
            "code": "PHOTO_RECYCLED",
            "severity": "high",
            "message": f"Photo perceptual-hash matched {duplicate_photo_hits} document(s) in prior claim(s).",
        })

    # 2. Delayed reporting (NL practice: sooner is normal, months late is suspicious).
    loss = _parse_date(claim.get("loss_date"))
    if loss:
        days = (date.today() - loss).days
        if days > 180:
            score += 0.2
            signals.append({
                "code": "REPORT_VERY_LATE",
                "severity": "medium",
                "message": f"Reported {days} days after loss (>180d).",
            })
        elif days > 60:
            score += 0.1
            signals.append({
                "code": "REPORT_LATE",
                "severity": "low",
                "message": f"Reported {days} days after loss (>60d).",
            })

    # 3. No supporting evidence at all.
    if not documents:
        score += 0.15
        signals.append({
            "code": "NO_EVIDENCE",
            "severity": "medium",
            "message": "No documents or photos submitted.",
        })

    # 4. Layered risk: injuries + third party + high estimate.
    if claim.get("injuries") and claim.get("third_party_involved"):
        score += 0.1
        signals.append({
            "code": "INJURY_THIRD_PARTY_LAYERED",
            "severity": "medium",
            "message": "Personal injury AND third-party liability reported — layered exposure.",
        })

    # 5. Photo-specific signals surfaced by app.py (EXIF mismatch, no EXIF).
    for ps in (photo_signals or []):
        sev = ps.get("severity", "low")
        signals.append(ps)
        score += {"high": 0.4, "medium": 0.2, "low": 0.05}.get(sev, 0.05)

    # 6. Very high estimate with low extraction confidence.
    amt = claim.get("estimated_amount_eur") or 0
    conf = claim.get("extraction_confidence") or 0
    if amt > 5000 and conf and conf < 0.6:
        score += 0.1
        signals.append({
            "code": "AMOUNT_HIGH_CONF_LOW",
            "severity": "medium",
            "message": f"Estimate €{amt:.0f} with low extraction confidence ({conf:.2f}).",
        })

    return {"score": min(score, 1.0), "signals": signals}


def compute_fraud_score(claim, documents, duplicate_photo_hits):
    """Back-compat shim. Prefer compute_fraud()."""
    return compute_fraud(claim, documents, duplicate_photo_hits)["score"]
