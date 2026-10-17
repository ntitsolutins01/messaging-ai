---
description: Implementa uma spec aprovada, tarefa por tarefa, com TDD
argument-hint: <número da spec, ex.: 001>
---

Implemente `specs/$ARGUMENTS-*.md`.

1. Confirme `Status: aprovada` e que não há pergunta em aberto bloqueando. Se houver, pare e liste.
2. Pegue a primeira tarefa `[ ]`. Siga `/tdd` para cada critério de aceite ligado a ela.
3. Ao fechar a tarefa: marque `[x]` na spec, rode `dotnet test`, faça um commit pequeno (Conventional Commits).
4. Repita para a próxima tarefa. A cada duas tarefas rode `/review`.
5. No fim: `/quality-gate` e atualize o `Status` da spec.

Se um critério se mostrar errado ou impossível, pare e proponha a mudança na spec antes de seguir.
