## Contexto
Spec: `specs/NNN-…` (tarefa T-..)

## O que mudou

## Como testar
`bash scripts/quality-gate.sh`

## Riscos
- O que acontece se o processamento **repetir**?
- O que acontece se o processo **cair no meio**?
- Quais invariantes do `CLAUDE.md` este PR toca?

## Checklist
- [ ] Teste RED escrito antes (TDD)
- [ ] Teste de chaos atualizado, se tocou consumidor/outbox/transação
- [ ] Sem segredos
