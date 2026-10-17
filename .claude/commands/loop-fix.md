---
description: Itera (corrige → gate → corrige) até o quality gate ficar verde, com freios
argument-hint: <tarefa> [max iterações, padrão 5]
---

Execute um loop controlado para: `$ARGUMENTS`. Leia `.claude/rules/loop-engineering.md`.

Para cada iteração (máximo 5, ou o número pedido):
1. Rode `bash scripts/quality-gate.sh` e leia só as falhas.
2. Corrija a **causa raiz** de uma falha por vez, em passo pequeno (TDD quando houver código novo).
3. Rode o gate de novo.

Pare quando: (a) o gate passar e a tarefa estiver feita; (b) atingir o máximo; (c) mesma falha em 3 iterações seguidas; (d) a única saída for afrouxar limite ou spec.
Nunca faça commit, push ou merge. No fim, mostre o `git diff --stat`, a tabela iteração | falha | correção e o veredito.

Para rodar fora da sessão, sem acompanhar: `bash scripts/loop.sh "<tarefa>" 5`.
