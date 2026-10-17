# 03 — TDD (RED → GREEN → REFACTOR)

## Onde está
- Regra `.claude/rules/tdd.md` e `.cursor/rules/tdd.mdc`.
- Comando `/tdd <comportamento>`: um ciclo guiado, mostrando a falha e depois o verde.
- Skill `testing-patterns` (xUnit, Testcontainers, esqueletos de chaos).
- Primeira fatia implementada: `IdempotencyKey`, `WebhookSignatureValidator`, `WebhookIngestionService` (incluindo 20 envios concorrentes → 1 efeito), `StuckEventDetector`, `ReprocessEventHandler`, e testes de arquitetura e de endpoint.

## Ressalva importante
Código e testes desta fatia foram escritos juntos, **sem executar o ciclo RED→GREEN** (sem SDK .NET no ambiente de criação). Eles são o ponto de partida: rode `dotnet test` e trate qualquer falha como um RED legítimo a corrigir. A partir daqui, o fluxo é TDD de verdade.
