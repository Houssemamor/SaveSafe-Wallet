import json
import os
from typing import Any

import redis.asyncio as redis
from models import RiskAnalysis


QUEUE_KEY = "ai-security:review-queue"
ITEM_KEY_PREFIX = "ai-security:review-item:"


class ReviewStore:
    def __init__(self) -> None:
        self._redis_url = os.getenv("REDIS_URL", "redis://localhost:6379")
        self._client = redis.from_url(self._redis_url, decode_responses=True)

    async def close(self) -> None:
        await self._client.aclose()

    async def save_analysis(self, analysis: RiskAnalysis) -> None:
        payload = analysis.model_dump(mode="json")
        key = f"{ITEM_KEY_PREFIX}{analysis.event_id}"

        await self._client.set(key, json.dumps(payload), ex=60 * 60 * 24 * 7)

        if analysis.label != "normal":
            priority = analysis.risk_score * 1_000_000_000 + analysis.analyzed_at.timestamp()
            await self._client.zadd(QUEUE_KEY, {analysis.event_id: priority})
            await self._client.zremrangebyrank(QUEUE_KEY, 0, -101)

    async def list_open_items(self, limit: int = 25) -> list[dict[str, Any]]:
        safe_limit = max(1, min(limit, 100))
        event_ids = await self._client.zrevrange(QUEUE_KEY, 0, safe_limit - 1)
        items: list[dict[str, Any]] = []

        for event_id in event_ids:
            raw = await self._client.get(f"{ITEM_KEY_PREFIX}{event_id}")
            if not raw:
                await self._client.zrem(QUEUE_KEY, event_id)
                continue

            item = json.loads(raw)
            if item.get("review_status", "open") == "open":
                items.append(item)

        return items

    async def resolve_item(self, event_id: str, status: str = "resolved") -> bool:
        key = f"{ITEM_KEY_PREFIX}{event_id}"
        raw = await self._client.get(key)
        if not raw:
            await self._client.zrem(QUEUE_KEY, event_id)
            return False

        item = json.loads(raw)
        item["review_status"] = status
        await self._client.set(key, json.dumps(item), ex=60 * 60 * 24 * 7)
        await self._client.zrem(QUEUE_KEY, event_id)
        return True
