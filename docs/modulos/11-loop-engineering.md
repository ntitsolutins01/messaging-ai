# 11 — Loop Engineering

> **Interpretação minha.** A página do DAPIA lista o módulo mas **não detalha os tópicos**. Implementei o que o termo costuma significar em engenharia com agentes: um loop autônomo com verificador e freios. Se o módulo cobre outra coisa, me diga.

## Onde está
- `scripts/loop.sh "<tarefa>" [max]`: o Claude (`claude -p`) corrige → o **quality gate** decide → a saída do gate volta como feedback. Para quando: gate verde + `STATUS: DONE`, limite de iterações, ou **sem progresso** (mesma árvore e mesmas falhas em 3 iterações). Logs em `.loop/`.
- `/loop-fix`: o mesmo loop dentro da sessão.
- Regra `.claude/rules/loop-engineering.md`: objetivo verificável, verificador determinístico, freios, humano no fim; nunca merge/push/deploy.

## Ressalva
Não executado (precisa da CLI `claude` autenticada e do SDK .NET).
