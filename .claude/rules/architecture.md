# Regra: Arquitetura (monólito modular)

Detalhes e justificativa: `docs/adr/0001-modular-monolith.md`.

- Um deploy, vários módulos com fronteira. Módulos: `Ingestion`, `Processing`, `Operations`.
- **Módulo → módulo só por `Messaging.Contracts`** (eventos de integração e portas como `IEventReprocessor`). Nunca referencie tipo interno de outro módulo.
- `Messaging.SharedKernel` não depende de nada. `Messaging.Contracts` não depende de módulos.
- Cada módulo tem seus próprios dados (tabelas com prefixo do módulo) e nunca consulta tabela de outro.
- Hosts (`Messaging.Api`, `Messaging.Worker`) só compõem: DI, endpoints, configuração. Sem regra de negócio.
- Kafka/RabbitMQ ficam em projetos de adapter (`Messaging.Brokers.*`) atrás de `IMessageBus`; nenhum módulo os referencia.
- O schema do provedor entra por adaptador com versão (`v1`, `v2`). Campos desconhecidos são guardados, não usados.
- Configuração via `IOptions<T>` validada no startup. Logs estruturados com `CorrelationId` e `EventId`.
- **As fronteiras são testadas** em `tests/Messaging.ArchitectureTests`. Violação quebra o quality gate.
- Novo módulo? Siga `.claude/rules/modular-monolith.md`.
