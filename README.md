# Trail

Acompanhamento de estudos auto-hospedado: conteúdo programático em tópicos, questões
resolvidas, desempenho por período e tempo de estudo. Serve para editais de concurso,
trilhas de programação ou qualquer conteúdo organizado em tópicos.

> **Situação:** em construção. O planejamento completo está em [`docs/`](docs/README.md),
> e a implementação segue o [roteiro](docs/05-roteiro.md), uma fase por vez.

## Por que existe

O Trail nasceu para substituir uma planilha de controle de estudos usada por vários
alunos. Ele corrige os erros de cálculo encontrados nela e acrescenta o histórico de
questões e o controle do tempo de estudo.

## Como vai funcionar

- Roda em Docker, em dois modos: individual (SQLite) ou hospedado para vários usuários
  (PostgreSQL).
- Interface web responsiva, instalável como PWA.
- Backup e restauração por arquivo, e importação da planilha original.

## Stack

.NET 10, ASP.NET Core Razor Pages, HTMX, EF Core (SQLite e PostgreSQL), Tailwind CSS +
daisyUI e OpenTelemetry. Os motivos de cada escolha estão na
[arquitetura](docs/03-arquitetura.md) e nas [ADRs](docs/adr/).

## Aviso

O Trail não tem vínculo com nenhum órgão público nem banca examinadora.

## Licença

[AGPL-3.0](LICENSE)
