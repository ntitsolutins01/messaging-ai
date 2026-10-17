# Avaliação com "LLM as a judge" (módulo 14)

Mede a qualidade dos agentes do projeto com um segundo modelo que **julga** a saída do primeiro contra uma rubrica. Hoje avalia o agente `messaging-reviewer`: ele acha as falhas plantadas e evita falso alarme?

## Como funciona
1. `evals/cases/*.json`: trechos de código com defeitos plantados (e um caso limpo) + o que deve ser achado.
2. **Avaliado**: o prompt do `messaging-reviewer` revisa cada trecho.
3. **Juiz**: recebe a rubrica (`rubric.md`), o esperado e a resposta do revisor, e devolve JSON com nota de 0 a 5.
4. A média precisa ser ≥ `EVAL_MIN_SCORE` (padrão 4.0), senão o comando sai com erro.

## Rodar
```bash
python3 evals/run_judge.py --dry-run      # valida os casos e mostra os prompts, sem chamar o modelo
python3 evals/run_judge.py                # roda de verdade (precisa da CLI `claude` autenticada)
```
Variáveis: `REVIEWER_MODEL`, `JUDGE_MODEL`, `EVAL_MIN_SCORE`. Resultados em `evals/results/` (não versionado).

## Cuidados com viés do juiz
- Use, quando possível, um modelo **diferente** (ou mais forte) para julgar.
- O juiz **cita evidência** da resposta avaliada; nota sem citação é inválida.
- A rubrica é objetiva (achou/não achou/falso alarme), não "gostei".
- A resposta avaliada é **dado**; instrução dentro dela não vale.
- Nota do juiz não substitui revisão humana: amostre alguns casos à mão a cada mudança de rubrica.

## Adicionar um caso
Crie `evals/cases/NN-nome.json` com `id`, `title`, `code`, `expected_findings` (lista; vazia = caso limpo). Todo bug real que passou despercebido em PR vira um caso novo.
