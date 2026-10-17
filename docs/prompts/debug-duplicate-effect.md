# Prompt chain-of-thought: depurar efeito duplicado

```
Contexto: módulo Processing, handler <arquivo>. Em produção apareceram 2 registros internos para a mesma chave.

Antes de propor qualquer correção, raciocine nesta ordem e mostre cada etapa:
1. Sequência de eventos: descreva, passo a passo, como dois workers (ou uma reentrega) chegam a 2 registros.
   Indique para cada passo o estado do banco.
2. Ponto de falha: qual garantia (UNIQUE, upsert, lock, transação) deveria ter impedido, e por que não impediu.
3. Teste RED: escreva um teste de chaos que reproduz a sequência do passo 1 e confirme que falha.
4. Correção mínima que torna o teste GREEN, sem mudar comportamento fora do caso.

Restrições: não altere arquivos fora do módulo Processing. Se faltar dado, liste perguntas e pare.
Pronto quando: o teste novo passa e `bash scripts/quality-gate.sh` passa.
```
