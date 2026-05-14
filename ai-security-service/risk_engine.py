from datetime import datetime, timezone
from models import LoginEventMessage, RiskAnalysis


async def analyze(event: LoginEventMessage) -> RiskAnalysis:
    score = 0.0
    reasons: list[str] = []

    if not event.success:
        score += 35
        reasons.append(event.failure_reason or "Login failed")

    if not event.user_id:
        score += 15
        reasons.append("No authenticated user id was attached to the event")

    if not event.country_code:
        score += 10
        reasons.append("Country could not be resolved")

    user_agent = (event.user_agent or "").lower()
    suspicious_user_agent_terms = ["curl", "python", "bot", "spider", "scanner", "postman"]
    if any(term in user_agent for term in suspicious_user_agent_terms):
        score += 20
        reasons.append("User agent looks automated or tool-driven")

    event_hour = event.timestamp_utc.hour
    if event_hour < 5 or event_hour > 23:
        score += 8
        reasons.append("Login happened outside common business hours")

    if event.asn:
        score += 4
        reasons.append("ASN metadata was present for network review")

    score = min(score, 100)
    label = "normal"
    recommended_action = "monitor"

    if score >= 70:
        label = "high_risk"
        recommended_action = "suspend_or_force_reset"
    elif score >= 40:
        label = "suspicious"
        recommended_action = "contact_user"
    elif score >= 20:
        label = "watch"
        recommended_action = "monitor"

    if not reasons:
        reasons.append("No unusual indicators detected")

    return RiskAnalysis(
        event_id=event.event_id,
        user_id=event.user_id,
        email=event.email,
        ip_address=event.ip_address,
        country_code=event.country_code,
        user_agent=event.user_agent,
        success=event.success,
        failure_reason=event.failure_reason,
        timestamp_utc=event.timestamp_utc,
        risk_score=score,
        label=label,
        reasons=reasons,
        recommended_action=recommended_action,
        analyzed_at=datetime.now(timezone.utc),
    )
