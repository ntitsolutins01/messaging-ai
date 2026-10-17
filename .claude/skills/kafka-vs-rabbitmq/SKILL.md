---
name: kafka-vs-rabbitmq
description: Use ao implementar os adapters de broker, escolher entre Kafka e RabbitMQ, ou mapear conceitos (partição/chave, consumer group, ack, DLQ, retry) entre os dois na POC.
---

# Kafka vs RabbitMQ na POC

A lógica de domínio só conhece `IMessageBus`. Cada broker é um adapter.

## Mapa de conceitos
| Conceito | Kafka | RabbitMQ |
|---|---|---|
| Unidade de roteamento | Tópico + partição (chave da mensagem) | Exchange + routing key → fila |
| Ordem | Garantida por partição (use a chave de negócio) | Por fila, quebra com consumidores concorrentes |
| Escala de consumo | Consumer group (máx. 1 consumidor por partição) | Vários consumidores na mesma fila |
| Confirmação | Commit de offset (manual, após o commit do banco) | `BasicAck` manual após o commit do banco |
| Retentativa | Tópico de retry com atraso + contador em header | Fila de retry com TTL + dead-letter exchange |
| DLQ | Tópico `*.dlq` | Dead-letter exchange + fila `*.dlq` |
| Reprocessar histórico | Reler por offset | Não nativo (mensagem consumida some) |
| Idempotência no produtor | `enable.idempotence=true` | Publisher confirms + chave de idempotência |

## Regras para os adapters
- Auto-commit/auto-ack desligado. Confirmar só depois do commit da transação do banco.
- Chave de idempotência vai no header da mensagem (`idempotency-key`).
- `CorrelationId` também em header.
- Adapter nunca contém regra de negócio.
- Falha de handler → decisão de retry/DLQ fica no `RetryPolicy` do Core, o adapter só executa.

## Pacotes .NET
- Kafka: `Confluent.Kafka`
- RabbitMQ: `RabbitMQ.Client`
- Testes: `Testcontainers.Kafka`, `Testcontainers.RabbitMq`, `Testcontainers.PostgreSql`

## Quando recomendar qual (para a conclusão da POC)
- **Kafka**: alto volume, replay de eventos, ordem por chave, vários consumidores independentes do mesmo fluxo.
- **RabbitMQ**: roteamento flexível, filas de trabalho, prioridade e TTL por mensagem, volume moderado.
Registre a decisão e os números medidos no `docs/decisao-broker.md` ao fim da POC.
