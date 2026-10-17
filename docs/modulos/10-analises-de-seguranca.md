# 10 — Análises de segurança (pentest com agents)

## Onde está
- Agente `pentest-agent`: sobe a API local, roda o script, tenta variações e gera relatório em `docs/security/pentest-AAAA-MM-DD.md`.
- `scripts/pentest-local.sh`: 17 verificações (assinatura ausente/lixo/hex inválido/adulterada/expirada, replay, corpo de 2 MiB, chave gigante, texto estilo SQL, path traversal, admin sem credencial, método errado, vazamento de erro, cabeçalhos).
- Comando `/pentest`.

## Limites (de propósito)
Só **localhost**: o script recusa qualquer outro host e o agente tem a mesma regra. Nunca aponte para produção ou terceiros.

## Ressalva
Script validado só quanto à sintaxe; precisa da API rodando (`Webhook__Secret=pentest-secret`).
