---
description: Resume o estado da sessão em docs/context/HANDOFF.md para a próxima sessão (context engineering)
---

Escreva `docs/context/HANDOFF.md` (sobrescreva) com no máximo 40 linhas:

1. **Objetivo** da sessão e a spec relacionada (`specs/NNN`).
2. **Feito**: arquivos e testes criados/alterados.
3. **Decisões** tomadas e o porquê (se durável, sugira um ADR).
4. **Estado atual**: build e testes passam? Rode `dotnet test` e informe o resultado real.
5. **Próximos passos** em ordem, o primeiro já como um teste RED a escrever.
6. **Armadilhas**: o que parecia certo e estava errado.

Não cole código. Aponte arquivo:linha. O arquivo não é versionado (está no `.gitignore`).
