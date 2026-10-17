# ADR 0003: Broker atrás de abstração

- **Status:** aceito
- **Contexto:** a POC compara Kafka e RabbitMQ e termina com uma recomendação baseada em medição.
- **Decisão:** módulos publicam e consomem só por `IMessageBus` (a ser criado na spec 004). Cada broker é um adapter em `Messaging.Brokers.Kafka` / `Messaging.Brokers.RabbitMq`, escolhido por `Messaging__Broker`. Nenhum módulo referencia pacote de broker; o teste de arquitetura garante.
- **Consequências:**
  - (+) Trocar de broker é configuração; a comparação usa o mesmo fluxo e os mesmos testes.
  - (−) A abstração é o mínimo comum (publish/subscribe/ack/retry/DLQ); recursos específicos ficam fora.
- **Revisitar quando:** os números da POC (`docs/decisao-broker.md`) indicarem que um recurso específico é indispensável.
