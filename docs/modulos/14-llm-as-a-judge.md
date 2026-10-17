# 14 — Avaliação com "LLM as a judge"

**Tema:** usar LLMs para avaliar o desempenho de outros agentes.

## Onde está
- `evals/run_judge.py`: o `messaging-reviewer` revisa 5 trechos (4 com defeito plantado, 1 limpo); o juiz dá nota 0–5 pela `evals/rubric.md`, **citando evidência**; média mínima 4.0.
- Agente `llm-judge` e comando `/eval`.
- Cuidados com viés do juiz documentados em `evals/README.md` (modelo diferente, rubrica objetiva, resposta avaliada tratada como dado).

## Status
`python3 evals/run_judge.py --dry-run` **foi executado** e valida casos, agente e rubrica. A rodada real precisa da CLI `claude` autenticada.

## Como evoluir
Todo bug que passar despercebido em PR vira um caso novo em `evals/cases/`. Rode `/eval` ao mudar o prompt de qualquer agente.
