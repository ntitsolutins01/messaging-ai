# Specs (Spec Driven Development)

Nenhum código de funcionalidade nasce sem spec aprovada. A spec é o contrato; o teste é a prova.

## Fluxo
1. `/spec <tema>`: gera `specs/NNN-tema.md` a partir de `_template.md`. **Sem código.** Perguntas em aberto ficam listadas.
2. Revisão humana: resolve as perguntas em aberto e muda `Status` para `aprovada`.
3. `/implement-spec NNN`: cada critério de aceite vira um teste RED, depois GREEN, depois REFACTOR (`/tdd`).
4. `/review` e `/quality-gate`. A spec é atualizada se a realidade mudou (nunca o contrário em silêncio).

## Índice
| # | Spec | Status |
|---|---|---|
| 001 | [Ingestão de webhook](001-webhook-ingestion.md) | parcialmente implementada (T-01..T-04) |
| 002 | [Processamento idempotente](002-idempotent-processing.md) | rascunho |
| 003 | [Recuperação de presos e DLQ](003-recovery-and-dlq.md) | rascunho (T-01 implementada) |
