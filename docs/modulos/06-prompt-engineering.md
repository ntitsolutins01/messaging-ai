# 06 — Prompt Engineering para desenvolvimento

**Tema:** técnicas que levam a código de nível de engenheiro, com chain-of-thought e few-shot.

## Onde está
- Skill `prompt-engineering-dev`: as 5 partes de um bom prompt (contexto, tarefa, restrições, exemplo, pronto-quando), quando usar CoT e few-shot, anti-padrões.
- `docs/prompts/review-consumer.md` (**few-shot**) e `docs/prompts/debug-duplicate-effect.md` (**chain-of-thought** em etapas verificáveis).
- Comando `/prompt <prompt bruto>`: reescreve um prompt fraco no formato do projeto.

## Como usar
`/prompt faz o consumer ficar idempotente` devolve um prompt com contexto, restrições, critério de pronto e exemplo.
