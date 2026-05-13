import asyncio
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from kafka_consumer import LoginEventKafkaConsumer


logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s %(message)s",
)


@asynccontextmanager
async def lifespan(app: FastAPI):
    consumer = LoginEventKafkaConsumer()
    task = asyncio.create_task(consumer.run_forever())
    app.state.kafka_consumer = consumer
    app.state.kafka_task = task
    try:
        yield
    finally:
        await consumer.stop()
        task.cancel()
        try:
            await task
        except asyncio.CancelledError:
            pass


app = FastAPI(title="ai-security-service", lifespan=lifespan)


@app.get("/health")
async def health() -> dict:
    return {"status": "ok"}
