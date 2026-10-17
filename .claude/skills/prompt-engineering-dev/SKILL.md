---
name: prompt-engineering-dev
description: Use ao escrever ou melhorar prompts para o Claude implementar, revisar ou depurar código de mensageria. Técnicas de chain-of-thought, few-shot, restrições e critérios de pronto.
---

# Prompt engineering para desenvolvimento

Um bom prompt de código tem cinco partes. Se faltar uma, o resultado vira chute.

1. **Papel e contexto mínimo**: o que é o sistema e qual módulo. Aponte arquivos, não cole.
2. **Tarefa objetiva**: um resultado verificável, não "melhore isto".
3. **Restrições**: invariantes, o que não pode mudar, limites de escopo.
4. **Exemplos (few-shot)**: 1 a 3 pares entrada→saída esperada no estilo do projeto.
5. **Critério de pronto**: qual teste/comando prova que acabou.

## Chain-of-thought (raciocínio guiado)
Para bug, concorrência e idempotência, peça o raciocínio **em etapas verificáveis**, não "pense passo a passo" genérico:
> Antes de propor correção: (1) liste a sequência de eventos que leva ao efeito duplicado, com os dois workers em paralelo; (2) diga em qual passo a unicidade deveria ter barrado; (3) só então proponha o teste RED e a correção.

Use CoT quando houver ordem de eventos, estados intermediários ou causa raiz. Não use para tarefa trivial (só gasta contexto).

## Few-shot (exemplos que ancoram o estilo)
Mostre o formato exato desejado. Exemplo para revisão de consumidor — veja `docs/prompts/review-consumer.md`.
Regras: exemplos curtos, cobrindo um caso bom e um ruim; variem o suficiente para não virar cópia; rotule "Exemplo 1/2".

## Restrições que funcionam
- "Não altere arquivos fora de `src/Modules/Messaging.Processing/`."
- "Se faltar informação, liste as perguntas e pare; não suponha."
- "Mostre o diff mínimo." / "Um commit por ciclo."

## Anti-padrões
- Pedir várias coisas independentes no mesmo prompt.
- "Faça melhor", "otimize" sem métrica.
- Colar arquivo inteiro quando um caminho + trecho basta.
- Aceitar resposta sem rodar o critério de pronto.

## Receita rápida
`Contexto (1-2 linhas) → Tarefa → Restrições → Exemplo → Pronto quando <comando passa>.`
Veja também `docs/prompts/debug-duplicate-effect.md` (CoT) e o comando `/prompt`.
