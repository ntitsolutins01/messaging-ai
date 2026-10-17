---
description: Lista a DLQ e reprocessa eventos com segurança (ambiente local)
argument-hint: [event-id opcional]
---

1. Consulte os eventos em `Failed` pelo MCP `postgres` (somente leitura): id, tipo, tentativas, último erro.
2. Se `$ARGUMENTS` foi informado, selecione só esse evento; senão, mostre a lista e peça confirmação antes de continuar.
3. Reprocesse pela API (`POST /admin/events/{id}/reprocess`), nunca por UPDATE direto no banco.
4. Depois, confirme que existe exatamente 1 registro interno e 1 mensagem de saída para a chave do evento.
5. Reporte o resultado por evento.

Esta rotina é para ambiente local/POC. Não execute contra bancos remotos.
