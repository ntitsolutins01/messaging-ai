---
name: cyclomatic-complexity
description: Use quando o build falhar com CA1502 (complexidade ciclomática alta) ou ao revisar métodos longos e cheios de if/switch/loops: como medir e reduzir.
---

# Complexidade ciclomática

**Limite do projeto: 10 por método** (`CodeMetricsConfig.txt`, regra `CA1502`). No quality gate, passar do limite quebra o build.

## O que conta
+1 para cada `if`, `case`, `for`/`foreach`/`while`, `catch`, `&&`, `||`, `??`, `?:`, `?.` quando ramifica, e padrões `is`/`switch` com guardas.

## Como medir
`dotnet build -p:QualityGate=true` mostra `CA1502: ... complexidade ... de N` com o método. Também: Rider (Code Metrics) ou `dotnet-counters`/analisadores equivalentes.

## Como reduzir (nesta ordem)
1. **Extrair método** com nome do *porquê* (foi assim que `WebhookSignatureValidator` separou `TryParseHeader`, `Compute`, `IsFresh`).
2. **Guard clauses**: retornar cedo em vez de aninhar.
3. **Tabela em vez de if/switch**: `Dictionary<Tipo, Handler>` ou lista de regras, quando há muitos casos por tipo/status.
4. **Polimorfismo/estratégia** quando o mesmo `switch` aparece em vários lugares.
5. **Dividir a responsabilidade**: método que valida, decide e grava vira três.

## O que NÃO fazer
- Subir o limite ou `#pragma warning disable CA1502` para passar.
- Quebrar em métodos sem nome significativo só para baixar o número (`Step1`, `Step2`).
- Esconder ramificação em `try/catch` genérico.

## Depois de reduzir
Rode os testes (o comportamento não pode mudar) e o gate completo. Se faltava teste do ramo, escreva-o antes de refatorar (RED/GREEN/REFACTOR).
