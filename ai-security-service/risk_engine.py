from models import LoginEventMessage


async def analyze(event: LoginEventMessage) -> dict:
    return {
        "event_id": event.event_id,
        "risk_score": 0.0,
        "label": "normal"
    }
