import asyncio
import json
import logging
import os
from aiokafka import AIOKafkaConsumer
from aiokafka.errors import KafkaError
from models import LoginEventMessage
import risk_engine


logger = logging.getLogger("ai_security.kafka_consumer")


class LoginEventKafkaConsumer:
    def __init__(self) -> None:
        self._bootstrap_servers = os.getenv("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092")
        self._topic = "login-events"
        self._group_id = "ai-security-service"
        self._consumer = AIOKafkaConsumer(
            self._topic,
            bootstrap_servers=self._bootstrap_servers,
            group_id=self._group_id,
            auto_offset_reset="earliest",
            enable_auto_commit=False,
            value_deserializer=lambda v: json.loads(v.decode("utf-8")),
            key_deserializer=lambda v: v.decode("utf-8") if v else None,
        )
        self._stopped = asyncio.Event()

    async def start(self) -> None:
        await self._consumer.start()
        logger.info(
            "kafka_consumer_started",
            extra={
                "topic": self._topic,
                "group_id": self._group_id,
                "bootstrap_servers": self._bootstrap_servers,
            },
        )

    async def stop(self) -> None:
        self._stopped.set()
        await self._consumer.stop()
        logger.info("kafka_consumer_stopped", extra={"topic": self._topic, "group_id": self._group_id})

    async def run_forever(self) -> None:
        await self.start()
        try:
            while not self._stopped.is_set():
                result_batch = await self._consumer.getmany(timeout_ms=1000)
                for topic_partition, messages in result_batch.items():
                    for message in messages:
                        try:
                            event = LoginEventMessage.model_validate(message.value)
                            risk = await risk_engine.analyze(event)
                            logger.info(
                                "login_event_received",
                                extra={
                                    "topic": message.topic,
                                    "partition": message.partition,
                                    "offset": message.offset,
                                    "key": message.key,
                                    "event_id": event.event_id,
                                    "email": event.email,
                                    "success": event.success,
                                    "risk_score": risk.get("risk_score", 0.0),
                                    "risk_label": risk.get("label", "normal"),
                                },
                            )
                            await self._consumer.commit()
                        except Exception as ex:
                            logger.exception(
                                "login_event_processing_failed",
                                extra={
                                    "topic": message.topic,
                                    "partition": message.partition,
                                    "offset": message.offset,
                                    "key": message.key,
                                    "error": str(ex),
                                },
                            )
        except KafkaError:
            logger.exception("kafka_consumer_runtime_error", extra={"topic": self._topic, "group_id": self._group_id})
            raise
        finally:
            await self.stop()
