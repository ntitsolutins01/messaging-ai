---
description: Revisa o diff atual contra os invariantes de mensageria
---

Revise o `git diff` (ou os arquivos alterados na sessão):

1. Acione o agente `code-reviewer` para qualidade geral.
2. Se o diff toca consumidor, outbox, transação do worker, adapters de broker ou webhook, acione também o `messaging-reviewer`.
3. Se toca validação de webhook, API admin, segredos ou dependências, acione também o `security-auditor`.

Consolide os achados por severidade (bloqueante, atenção, sugestão), cada um com arquivo, linha e correção proposta.
