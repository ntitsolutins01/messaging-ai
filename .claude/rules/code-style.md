# Regra: Estilo de código

Complementa `dotnet-style.md` com regras gerais de legibilidade.

- Métodos curtos, com um propósito. Se precisa de comentário para explicar o bloco, extraia um método com bom nome.
- Comentários explicam o **porquê**, nunca o quê.
- Sem código morto, sem `TODO` sem dono e sem `Console.WriteLine` (use `ILogger`).
- Sem números e strings mágicos: constantes nomeadas ou opções (`IOptions<T>`).
- Early return em vez de aninhamento profundo.
- Falhe cedo com `ArgumentNullException.ThrowIfNull` e validação nas bordas (API, consumidores).
- Prefira composição a herança; interfaces só onde há mais de uma implementação real ou necessidade de teste.
