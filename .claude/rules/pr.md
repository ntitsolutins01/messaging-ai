# Regra: Pull requests e commits

- Commits no padrão Conventional Commits: `feat:`, `fix:`, `test:`, `docs:`, `refactor:`, `chore:`.
- PR pequeno e focado em um tema. Título curto no imperativo.
- Descrição do PR com: **Contexto**, **O que mudou**, **Como testar** e **Riscos**.
- Todo PR que toca consumidor, outbox ou transação do worker deve:
  - passar pelo `/review` (agente `messaging-reviewer`);
  - incluir ou atualizar um teste de chaos;
  - dizer na descrição o que acontece se o processamento repetir ou cair no meio.
- Não faça merge com build ou testes quebrados.
- Sem segredos, sem arquivos gerados (`bin/`, `obj/`) e sem mudanças de formatação misturadas com lógica.
