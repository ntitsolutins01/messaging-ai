# 003 — Recuperação de eventos presos e DLQ

- **Status:** rascunho (T-01 implementada)
- **Módulo:** Processing + Operations

## Problema
Se um worker cai no meio, o evento fica `Processing` para sempre. Eventos que falham várias vezes precisam de um destino e de um caminho seguro de reprocessamento.

## Requisitos
- R1 Evento `Processing` além do timeout volta para a fila; com tentativas esgotadas vai para a DLQ.
- R2 Reprocessamento manual usa o mesmo caminho e a mesma chave; é idempotente.
- R3 `GET /admin/events?status=Failed`, `GET /admin/events/{id}`, `POST /admin/events/{id}/reprocess`, só perfil `operations`.
- R4 Alerta para eventos presos e para a DLQ.

## Critérios de aceite
- **CA1** Preso há mais que o timeout → `Requeue`; dentro do timeout → nada.
- **CA2** Preso com tentativas esgotadas → `MoveToDlq`.
- **CA3** Reprocessar duas vezes o mesmo evento → 1 efeito.
- **CA4** Chamada à API admin sem perfil `operations` → `403`.

## Tarefas
- [x] T-01 `StuckEventDetector` (decisão pura)
- [ ] T-02 Job de recuperação com lock por chave
- [ ] T-03 `IEventReprocessor` em Processing + endpoints admin
- [ ] T-04 Alertas (ver observabilidade, módulo 13)
