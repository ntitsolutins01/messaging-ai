---
description: Sobe o ambiente local da POC e valida que está saudável (não faz deploy remoto)
argument-hint: [kafka|rabbitmq]
---

Escopo: ambiente **local**. Esta POC não tem deploy remoto.

1. Defina o broker: `$ARGUMENTS` (padrão `rabbitmq`) em `Messaging__Broker`.
2. `docker compose up -d` e aguarde os health checks de postgres e do broker (`docker compose ps`).
3. Aplique as migrations: `dotnet ef database update --project src/Messaging.Infrastructure --startup-project src/Messaging.Api`.
4. Suba Api e Worker (`dotnet run`) e confira `/health/ready`.
5. Envie um webhook de teste duas vezes com o mesmo ID e confirme 1 registro interno e 1 mensagem de saída.
6. Reporte o estado de cada componente em uma tabela curta.

Se algo falhar, mostre o log relevante e pare; não mexa em volumes nem derrube dados (`down -v` é bloqueado pelo hook).
