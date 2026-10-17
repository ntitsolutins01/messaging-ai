# Regra: Testes

## Pirâmide
- **Unit**: domínio e handlers com fakes de `IMessageBus`/`IEventStore`.
- **Integração**: Testcontainers (PostgreSQL, Kafka, RabbitMQ). Sem mocks de banco.
- **Chaos**: cenários de falha reais (ver `/chaos-test`).

## Cenários de chaos obrigatórios
1. Mesmo webhook enviado N vezes (inclusive em paralelo) → 1 registro interno e 1 mensagem de saída.
2. Worker derrubado entre "marcar em processamento" e o commit → recuperação conclui sem duplicar.
3. Webhooks fora de ordem → estado final correto.
4. Dois workers disputando o mesmo evento → apenas um processa.
5. Reprocessamento da DLQ duas vezes → mesmo resultado.
6. Broker indisponível → outbox acumula e drena depois, sem perda.

## Convenções
- Nome: `Dado_Quando_Entao` ou `Metodo_Cenario_Resultado`.
- Cada teste de chaos termina com asserts de contagem: `COUNT(*)` no registro interno e na outbox de saída.
- Testes não dependem de ordem nem de relógio real (`IClock`).
- Toda correção de bug de duplicidade inclui teste que a reproduz primeiro.
