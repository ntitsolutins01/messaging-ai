# 13 — Observabilidade: debugging e tracing assistido por IA

**Tema:** tracing com IA; stack ELK com agentes que monitoram, abrem issues e disparam correção.

## Fluxo
```
API/Worker --OTLP--> Collector --> Elasticsearch --> Kibana
                                        |
              scripts/observability/watch-errors.sh  (erro recorrente → issue `auto-detected`)
                                        |
        humano adiciona o rótulo `auto-fix`  →  .github/workflows/claude-auto-fix.yml  →  PR para revisão
```

## Onde está
- `docker-compose.observability.yml` + `observability/otel-collector.yaml`.
- `src/Shared/Messaging.Observability` (opt-in via `OTEL_EXPORTER_OTLP_ENDPOINT`) e `Telemetry.Source` no SharedKernel; o `webhook.ingest` já gera trace (só provedor e resultado, nunca payload).
- Skill `observability-debugging`, agente `observability-analyst`, comando `/debug-trace`.

## Decisão de segurança
O agente **abre a issue sozinho**, mas a **correção só começa quando um humano rotula `auto-fix`**, e o resultado é sempre um PR para revisão. Detecção automática + correção sem aprovação seria um vetor de abuso (log malicioso → código alterado).

## Ressalvas
Não executado (sem Docker/Elasticsearch no ambiente de criação). Os **nomes de campo** do índice (`SeverityText`, `Body.keyword`) dependem do mapeamento do exporter: confirme no Kibana e ajuste `ES_QUERY`/`ES_AGG_FIELD`. Segurança do Elasticsearch está **desligada** (só dev).
