---
description: Roda a avaliação LLM-as-a-judge dos agentes e reporta notas por caso
argument-hint: [--dry-run]
---

Rode `python3 evals/run_judge.py $ARGUMENTS` e reporte:

1. Tabela caso | nota | falsos alarmes | evidência do juiz.
2. Média e veredito (mínimo `EVAL_MIN_SCORE`, padrão 4.0).
3. Para cada nota < 4, a causa provável no agente avaliado (`.claude/agents/...`) e uma mudança concreta de prompt para testar.

Se a CLI `claude` não estiver autenticada, rode com `--dry-run` e diga isso. Não altere a rubrica nem os casos para subir a nota.
