import asyncio
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from kafka_consumer import LoginEventKafkaConsumer
from review_store import ReviewStore


logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s %(message)s",
)


@asynccontextmanager
async def lifespan(app: FastAPI):
    review_store = ReviewStore()
    consumer = LoginEventKafkaConsumer(review_store)
    task = asyncio.create_task(consumer.run_forever())
    app.state.review_store = review_store
    app.state.kafka_consumer = consumer
    app.state.kafka_task = task
    try:
        yield
    finally:
        await consumer.stop()
        await review_store.close()
        task.cancel()
        try:
            await task
        except asyncio.CancelledError:
            pass


app = FastAPI(title="ai-security-service", lifespan=lifespan)


@app.get("/health")
async def health() -> dict:
    return {"status": "ok"}


@app.get("/review-queue")
async def review_queue(limit: int = 25) -> dict:
    items = await app.state.review_store.list_open_items(limit)
    return {"items": items}


@app.post("/review-queue/{event_id}/resolve")
async def resolve_review_item(event_id: str, status: str = "resolved") -> dict:
    resolved = await app.state.review_store.resolve_item(event_id, status)
    return {"success": resolved}
