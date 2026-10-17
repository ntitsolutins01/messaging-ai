---
description: Executa os cenários de chaos e reporta duplicidades
argument-hint: [cenario opcional]
---

1. Garanta a infra: `docker compose up -d` e aguarde os health checks.
2. Rode `dotnet test tests/Messaging.ChaosTests --filter "$ARGUMENTS"` (sem filtro, rode todos).
3. Para cada cenário, informe: passou/falhou, contagem de registros internos e de mensagens de saída.
4. Se algum cenário falhar, use o agente `chaos-tester` para diagnosticar a causa raiz antes de propor correção.
5. Resuma em uma tabela curta: cenário | resultado | observação.

Não altere testes para fazê-los passar; corrija o código.
