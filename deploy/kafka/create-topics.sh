#!/usr/bin/env bash
set -euo pipefail

KAFKA_TOPICS="/opt/kafka/bin/kafka-topics.sh"
BOOTSTRAP_SERVER="kafka:19092"

main_topics=(
  "orders.events"
  "inventory.events"
  "payments.events"
  "orders.commands"
  "inventory.commands"
  "payments.commands"
)

dlq_topics=(
  "saga.events.dlq"
  "orders.commands.dlq"
  "inventory.commands.dlq"
  "payments.commands.dlq"
)

create_topic() {
  local topic="$1"
  local retention_ms="$2"

  "${KAFKA_TOPICS}" \
    --bootstrap-server "${BOOTSTRAP_SERVER}" \
    --create \
    --if-not-exists \
    --topic "${topic}" \
    --partitions 3 \
    --replication-factor 1 \
    --config "retention.ms=${retention_ms}"
}

for topic in "${main_topics[@]}"; do
  create_topic "${topic}" "604800000"
done

for topic in "${dlq_topics[@]}"; do
  create_topic "${topic}" "2592000000"
done

echo "PayFlow Kafka topics:"
"${KAFKA_TOPICS}" \
  --bootstrap-server "${BOOTSTRAP_SERVER}" \
  --list
