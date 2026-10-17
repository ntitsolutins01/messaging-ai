# 08 — OWASP Top 10

**Tema:** as 10 falhas críticas que a IA mais comete e como corrigir.

## Onde está
- Skill `owasp-top10`: tabela A01–A10 com o **erro típico de IA** e a **correção neste projeto**, mais checklist de PR.
- Regra `.claude/rules/security.md`.
- Comando `/owasp-check`: audita o diff com evidência `arquivo:linha`.
- Defesas já no código: assinatura HMAC com `FixedTimeEquals` e janela de tempo, limite de corpo, validação do nome do provedor e do tamanho da chave, segredo obrigatório fora do repositório, cabeçalhos e erros sem vazamento.

## Ressalva
Baseado na edição **2021** do OWASP Top 10 (a busca feita não confirmou edição mais nova). Se houver uma, ajuste a tabela.
