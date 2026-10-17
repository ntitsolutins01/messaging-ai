---
description: Roda o quality gate completo e corrige o que falhar (sem afrouxar limites)
argument-hint: [--with-load]
---

Acione o agente `quality-gate` com este pedido: rodar `bash scripts/quality-gate.sh $ARGUMENTS`, diagnosticar cada falha pela causa raiz e corrigir.

Se o `dotnet` não estiver disponível ou a solution ainda não existir, rode `bash scripts/bootstrap.sh` primeiro (ou informe o que falta).

Resultado esperado: tabela portão | resultado | causa | correção, e o veredito final (APROVADO/REPROVADO). Nunca afrouxe limite para passar.
