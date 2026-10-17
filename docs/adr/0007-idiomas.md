# ADR 0007 — Idiomas: interface e docs em pt-BR, código 100% em inglês

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor

## Contexto

O público é brasileiro (concurseiros). O código, como portfólio, deve ser legível para
qualquer desenvolvedor.

## Decisão

- **Interface e documentação** (`docs/`, README, guias, modelos de issue): pt-BR.
- **Tudo que é código: inglês.** Isso inclui identificadores, nomes de testes, esquema
  do banco, mensagens de log, mensagens de commit, nomes de branch, títulos e descrições
  de PR e o `CHANGELOG.md`.
- **Sem comentários no código.** O código se explica pelos nomes e pela estrutura. Um
  comentário só aparece quando é indispensável, como o porquê de uma decisão que o
  código não consegue mostrar, e sempre em inglês.
- **Textos da interface em arquivos de recursos** (`.resx`, pt-BR), acessados por chaves
  em inglês. O código não contém texto em português.
- A correspondência entre os termos fica no
  [glossário](../02-dominio-e-regras.md#21-glossário).

## Consequências

- ✅ O código segue a convenção do mercado e é legível para qualquer desenvolvedor.
- ✅ A interface é natural para o público-alvo.
- ✅ A interface fica pronta para outros idiomas sem refatoração: basta um novo `.resx`.
- ❌ As views usam chaves em vez do texto direto, o que exige olhar o `.resx` para ler a
  frase.
- ❌ O glossário precisa ser mantido em dia.

## Alternativas consideradas

- **Tudo em pt-BR,** inclusive os identificadores.
- **Textos da interface direto nas views:** mais simples de ler, mas o código teria
  português.
- **Comentários em pt-BR:** misturaria idiomas dentro do código.
