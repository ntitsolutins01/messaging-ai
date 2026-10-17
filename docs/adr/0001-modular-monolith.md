# ADR 0001: Monólito modular

- **Status:** aceito
- **Contexto:** a POC precisa provar idempotência ponta a ponta com pouco custo operacional, mas com fronteiras que permitam separar serviços depois. O esquema do provedor ainda não está confirmado, então a mudança precisa ficar isolada.
- **Decisão:** um único deploy com três módulos (`Ingestion`, `Processing`, `Operations`), um `SharedKernel` mínimo e um projeto `Contracts` como único canal entre módulos. Fronteiras verificadas por `ModuleBoundaryTests` (NetArchTest).
- **Consequências:**
  - (+) Transação local simples para estado + outbox; menos infraestrutura; refatorar é barato.
  - (+) Cada módulo pode virar serviço depois: o `Contracts` já é a API.
  - (−) Exige disciplina: só o teste de arquitetura impede o acoplamento de voltar.
  - (−) Banco compartilhado: cada módulo usa só as próprias tabelas (prefixo por módulo).
- **Alternativas descartadas:** camadas clássicas (Api/Core/Infrastructure), que acoplam tudo ao mesmo domínio; microsserviços desde o início, caros para uma POC.
