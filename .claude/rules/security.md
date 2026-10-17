# Regra: Segurança (OWASP Top 10)

Checklist completo e exemplos: skill `owasp-top10`. Estas são as regras que valem em todo PR.

- **Webhook**: valide a assinatura (HMAC, comparação em tempo constante, janela de tempo) **antes** de qualquer efeito. Sem assinatura válida: `401`.
- **Entrada é hostil**: limite de tamanho, formato do nome do provedor, tamanho da chave. Valide na borda e rejeite com `400`.
- **SQL**: sempre parametrizado (EF Core/Dapper com parâmetros). Nunca concatene ou interpole texto em SQL.
- **Autorização**: toda rota `/admin/*` exige o perfil `operations` e registra quem agiu. Negue por padrão.
- **Segredos**: nunca no código, em `appsettings*.json` versionado ou em log. User-secrets, variável de ambiente ou cofre.
- **Logs**: nunca registre payload bruto, segredo, token ou dado pessoal. Registre `EventId` e `CorrelationId`.
- **Erros**: não vaze stack trace nem detalhe interno ao cliente (`application/problem+json` genérico + `traceId`).
- **Dependências**: `dotnet list package --vulnerable --include-transitive` limpo antes do merge. Versões fixas.
- **Criptografia**: só primitivas da plataforma (`HMACSHA256`, `CryptographicOperations.FixedTimeEquals`). Nunca invente a sua.
- **Aleatoriedade de segurança**: `RandomNumberGenerator`, nunca `Random`.
- **Defaults de dev** (`poc/poc` nos brokers e no banco) só em `docker-compose` local; nunca em ambiente real.

Código gerado por IA é tratado como código de terceiros: revise cada item acima antes de aceitar.
