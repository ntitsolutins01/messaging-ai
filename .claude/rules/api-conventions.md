# Regra: Convenções de API

## Webhook
- `POST /webhooks/{provider}`. Valida a assinatura (HMAC) **antes** de qualquer processamento.
- Responde `200` rápido após gravar o evento bruto. Repetição do mesmo evento também responde `200`.
- Assinatura inválida: `401`. Corpo malformado: grava o bruto mesmo assim e responde `200`; o parse falha depois, no worker, e o evento vai para a DLQ.

## API administrativa (operações)
- `GET  /admin/events?status=Failed&page=1` — lista paginada.
- `GET  /admin/events/{id}` — detalhe com payload bruto, tentativas e último erro.
- `POST /admin/events/{id}/reprocess` — reprocessa pelo mesmo caminho e a mesma chave. Idempotente: chamar duas vezes não duplica efeito.
- Acesso só para o perfil `operations`.

## Geral
- JSON em `camelCase`. Datas em UTC, ISO 8601.
- Erros no formato `application/problem+json` (RFC 7807) com `traceId`.
- Todo request recebe/gera `X-Correlation-Id` e devolve no response.
- Versionamento por rota quando houver quebra (`/v2/...`).
- Endpoints `/health/live` e `/health/ready` (ready checa banco e broker).
