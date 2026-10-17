# 02 — Spec Driven Development

**Tema:** especificação antes da implementação.

## Onde está
- `specs/README.md` (fluxo), `specs/_template.md`, specs `001`, `002`, `003`.
- Comandos `/spec <tema>` (gera spec, sem código, lista perguntas em aberto) e `/implement-spec <n>` (tarefa por tarefa, com TDD).
- Regra `.claude/rules/spec-driven.md`: sem spec aprovada não há funcionalidade; todo critério de aceite vira teste.

## Como usar
`/spec reprocessamento manual` → revisar e aprovar → `/implement-spec 004`.

## Ressalvas
A spec 001 registra uma **assunção** (esquema de assinatura do provedor) como pergunta em aberto. Resolver isso é pré-requisito de produção.
