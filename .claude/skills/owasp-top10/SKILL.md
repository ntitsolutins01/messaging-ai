---
name: owasp-top10
description: Use ao escrever ou revisar código exposto (webhook, API admin, acesso a dados, configuração, dependências) para checar as 10 falhas do OWASP Top 10 que a IA mais comete em .NET, com correção para cada uma.
---

# OWASP Top 10 aplicado ao projeto

Base: OWASP Top 10 **edição 2021**. Confirme em owasp.org/Top10 se existe edição mais nova antes de auditar; os controles abaixo valem de qualquer forma.

Para cada item: **erro típico de IA** → **como corrigir aqui**.

| # | Categoria | Erro típico de IA | Correção neste projeto |
|---|---|---|---|
| A01 | Broken Access Control | Endpoint `/admin/...` criado sem `[Authorize]`; checa só no front | Política `operations` em todo `/admin/*`; negar por padrão; teste de `403` |
| A02 | Cryptographic Failures | `==` para comparar assinatura; MD5/SHA1; chave fixa no código | `HMACSHA256` + `CryptographicOperations.FixedTimeEquals`; segredo em user-secrets/cofre; TLS em tudo |
| A03 | Injection | `$"SELECT ... {id}"`; `FromSqlRaw` com interpolação | Parâmetros sempre; `FromSqlInterpolated`; validar formato na borda |
| A04 | Insecure Design | Webhook processa antes de validar; sem limite de tamanho/taxa | Validar assinatura primeiro; `MaxRequestBodySize`; modelar ameaças na spec |
| A05 | Security Misconfiguration | Swagger e erros detalhados em produção; credenciais padrão | `Development` só local; `problem+json` genérico; sem `poc/poc` fora do compose |
| A06 | Vulnerable Components | Pacote antigo/abandonado, versão flutuante `*` | `dotnet list package --vulnerable --include-transitive`; versões fixas; Dependabot |
| A07 | Identification & Auth Failures | Token na query string; sem expiração; comparação ingênua | Header `Authorization`; expiração curta; janela de tolerância no webhook contra replay |
| A08 | Software & Data Integrity | Deserializar payload e confiar nele; pipeline sem proteção | Gravar bruto e parsear atrás de adaptador; `Actions` com versões fixas e permissões mínimas |
| A09 | Logging & Monitoring Failures | Logar o payload inteiro (vaza dado) ou não logar falha de assinatura | Logar `EventId`/`CorrelationId`/motivo; nunca payload/segredo; alertar picos de `401` |
| A10 | SSRF | Chamar URL vinda do payload sem validar | Nunca buscar URL fornecida pelo provedor; se inevitável, lista de hosts permitidos |

## Checklist de revisão (cole no PR)
- [ ] Rota nova: quem pode chamar? Está testado o acesso negado?
- [ ] Entrada nova: tamanho, formato e encoding validados na borda?
- [ ] SQL/comando/caminho montado com dado externo? Está parametrizado/validado?
- [ ] Segredo, token ou dado pessoal em código, config versionada ou log?
- [ ] Erro devolve detalhe interno ao cliente?
- [ ] Pacote novo/atualizado passou no `--vulnerable`?
- [ ] Há teste (ou caso no `scripts/pentest-local.sh`) que prova a defesa?

## Como provar
`bash scripts/pentest-local.sh` (defensivo, só localhost) e `/owasp-check` para auditar o diff.
