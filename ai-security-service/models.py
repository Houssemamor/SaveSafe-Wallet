from datetime import datetime
from pydantic import BaseModel, ConfigDict, Field


class LoginEventMessage(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event_id: str = Field(alias="EventId")
    user_id: str | None = Field(default=None, alias="UserId")
    email: str = Field(alias="Email")
    ip_address: str = Field(alias="IpAddress")
    country_code: str | None = Field(default=None, alias="CountryCode")
    asn: str | None = Field(default=None, alias="Asn")
    user_agent: str = Field(alias="UserAgent")
    device_fingerprint: str | None = Field(default=None, alias="DeviceFingerprint")
    success: bool = Field(alias="Success")
    failure_reason: str | None = Field(default=None, alias="FailureReason")
    timestamp_utc: datetime = Field(alias="TimestampUtc")


class RiskAnalysis(BaseModel):
    event_id: str
    user_id: str | None = None
    email: str
    ip_address: str | None = None
    country_code: str | None = None
    user_agent: str | None = None
    success: bool
    failure_reason: str | None = None
    timestamp_utc: datetime
    risk_score: float
    label: str
    reasons: list[str]
    recommended_action: str
    review_status: str = "open"
    analyzed_at: datetime
