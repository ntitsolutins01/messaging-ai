# Regra: TDD (RED → GREEN → REFACTOR)

Vale para qualquer código de produção em `src/`.

1. **RED**: escreva **um** teste que expresse um critério de aceite. Rode e **veja falhar pelo motivo certo** (não por erro de compilação ou setup). Mostre a saída da falha.
2. **GREEN**: escreva o **menor** código que faz o teste passar. Sem generalizar, sem "já que estou aqui". Rode e veja passar.
3. **REFACTOR**: com tudo verde, melhore nomes, duplicação e complexidade. Rode os testes de novo. Não mude comportamento.
4. Commit por ciclo (`test:`/`feat:` + `refactor:` quando houver).

## Proibido
- Código de produção sem teste que falhou antes.
- Enfraquecer ou apagar assert para passar; corrija o código.
- `Thread.Sleep` em teste; espere por condição com timeout.
- Teste que não pode falhar (sem assert de comportamento).
- Mock de banco em teste de integração (use Testcontainers).

## Bug = teste primeiro
Todo bug começa com um teste que o reproduz (RED). Bug de duplicidade ganha também um cenário de chaos.

## Tamanho do passo
Se o RED→GREEN levou mais de ~15 minutos, o passo foi grande demais: divida o critério.
