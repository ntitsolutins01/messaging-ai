---
description: Cria um novo consumidor/handler idempotente seguindo as regras do projeto
argument-hint: <NomeDoEvento>
---

Crie o consumidor para o evento `$ARGUMENTS`.

1. Leia `.claude/rules/architecture.md` e `.claude/rules/messaging-reliability.md`.
2. Em `Messaging.Core`, crie o `record` do evento (com `IdempotencyKey`) e o handler `${ARGUMENTS}Handler`.
3. Em `Messaging.Infrastructure`, registre o consumidor no adapter do broker ativo e no DI.
4. O handler grava estado, registro interno (upsert) e outbox de saída em **uma transação**.
5. Crie teste unitário do handler e um teste de chaos de repetição (mesmo evento 5x → 1 efeito).
6. Rode `dotnet build` e `dotnet test`, e acione o agente `messaging-reviewer` sobre o diff.

Não conclua se algum invariante do `CLAUDE.md` for violado.
