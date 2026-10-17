---
description: Um ciclo TDD guiado (RED → GREEN → REFACTOR) para um comportamento
argument-hint: <comportamento ou critério de aceite, ex.: "CA3 da spec 001">
---

Conduza um ciclo TDD para: `$ARGUMENTS`. Leia `.claude/rules/tdd.md` e a skill `testing-patterns`.

**RED** — escreva o teste (nome `Dado_Quando_Entao`). Rode `dotnet test --filter <nome>`. Mostre a falha e confirme que é pelo motivo certo.
**GREEN** — o menor código possível. Rode e mostre o teste passando, junto com a suíte do projeto.
**REFACTOR** — limpe sem mudar comportamento. Rode a suíte inteira.

Entregue ao final: teste novo, mudança de produção e uma linha dizendo o que foi refatorado. Não avance para o próximo critério sem eu pedir.
