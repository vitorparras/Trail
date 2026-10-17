# ADR 0003 — SQLite e PostgreSQL desde a primeira versão

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor

## Contexto

O sistema roda em dois modos ([ADR 0004](0004-modos-de-implantacao.md)). No modo
individual, cada pessoa quer subir o mínimo possível. No modo hospedado, várias pessoas
usam a mesma instância.

## Decisão

Suportar **SQLite e PostgreSQL desde a primeira versão**, escolhidos por configuração
(`Database__Provider`). O mesmo modelo do EF Core serve aos dois, e cada banco tem o
próprio conjunto de migrações.

## Consequências

- ✅ O modo individual roda em um container, e o banco é um arquivo.
- ✅ O modo hospedado usa PostgreSQL.
- ✅ O backup por arquivo da aplicação permite migrar de um banco para o outro.
- ❌ Toda mudança de modelo gera duas migrações.
- ❌ Os testes de integração rodam em dobro no CI.
- ❌ O código precisa seguir regras de portabilidade (datas, decimais, busca), descritas
  na [arquitetura, seção 3.10](../03-arquitetura.md#310-dados-dois-bancos-integridade-e-concorrência).

## Alternativas consideradas

- **Só PostgreSQL:** o modo individual passaria a ter dois containers.
- **Só SQLite:** atenderia bem dezenas de usuários, mas o autor preferiu ter o
  PostgreSQL no modo hospedado.
- **SQLite primeiro, PostgreSQL depois:** menos trabalho no início, mas o autor preferiu
  garantir a portabilidade desde o começo.
