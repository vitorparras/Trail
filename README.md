# Trail

[![CI](https://github.com/vitorparras/Trail/actions/workflows/ci.yml/badge.svg)](https://github.com/vitorparras/Trail/actions/workflows/ci.yml)
[![CodeQL](https://github.com/vitorparras/Trail/actions/workflows/codeql.yml/badge.svg)](https://github.com/vitorparras/Trail/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/vitorparras/Trail/badge)](https://scorecard.dev/viewer/?uri=github.com/vitorparras/Trail)
[![Licença: AGPL-3.0](https://img.shields.io/badge/Licen%C3%A7a-AGPL--3.0-blue.svg)](LICENSE)
[![Contributor Covenant](https://img.shields.io/badge/Contributor%20Covenant-2.1-4baaaa.svg)](CODE_OF_CONDUCT.md)

Acompanhamento de estudos auto-hospedado: conteúdo programático em tópicos, questões
resolvidas, desempenho por período e tempo de estudo. Serve para editais de concurso,
trilhas de programação ou qualquer conteúdo organizado em tópicos.

> **Situação:** em construção. As Fases 0, 1 e 2 e a Onda 0 do
> [roteiro](docs/05-roteiro.md) estão implementadas; o restante segue uma fase por
> vez. O planejamento completo está em [`docs/`](docs/README.md).

## Sumário

- [Por que existe](#por-que-existe)
- [Como funciona](#como-funciona)
- [Rodar com Docker](#rodar-com-docker)
- [Stack](#stack)
- [Documentação](#documentação)
- [Contribuindo](#contribuindo)
- [Segurança](#segurança)
- [Código de conduta](#código-de-conduta)
- [Aviso](#aviso)
- [Licença](#licença)

## Por que existe

O Trail nasceu para substituir uma planilha de controle de estudos usada por vários
alunos. Ele corrige os erros de cálculo encontrados nela e acrescenta o histórico de
questões e o controle do tempo de estudo.

## Como funciona

- Roda em Docker, em dois modos: individual (SQLite) ou hospedado para vários usuários
  (PostgreSQL).
- Interface web responsiva, instalável como PWA.
- Backup e restauração por arquivo, e importação da planilha original.

## Rodar com Docker

Modo individual, com SQLite, em um comando:

```bash
git clone https://github.com/vitorparras/Trail.git
cd Trail
docker compose --file deploy/compose.sqlite.yml up --detach
```

Abra <http://localhost:8080>. Os dados ficam no volume `trail-data`.

Para o modo hospedado com PostgreSQL, copie `.env.example` para `.env`, defina
`POSTGRES_PASSWORD` e `TRAIL_DOMAIN`, e use `deploy/compose.postgres.yml`. Os
detalhes de implantação e atualização estão em
[Qualidade e entrega](docs/04-qualidade-e-entrega.md).

## Stack

.NET 10, ASP.NET Core Razor Pages, HTMX, EF Core (SQLite e PostgreSQL), Tailwind CSS +
daisyUI e OpenTelemetry. Os motivos de cada escolha estão na
[arquitetura](docs/03-arquitetura.md) e nas [ADRs](docs/adr/). As licenças das
bibliotecas de terceiros estão em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Documentação

| Documento | Conteúdo |
|---|---|
| [Visão e escopo](docs/01-visao-e-escopo.md) | Problema, escopo da 1.0 e requisitos |
| [Domínio e regras](docs/02-dominio-e-regras.md) | Glossário, modelo de dados, regras de negócio e fórmulas |
| [Arquitetura](docs/03-arquitetura.md) | Camadas, módulos, segurança, observabilidade |
| [Qualidade e entrega](docs/04-qualidade-e-entrega.md) | Testes, padrões, CI/CD, releases, open source |
| [Roteiro](docs/05-roteiro.md) | Fases e critérios de pronto |
| [ADRs](docs/adr/) | Uma decisão por arquivo |

## Contribuindo

Leia o [CONTRIBUTING.md](CONTRIBUTING.md): ele explica como rodar o projeto, os
padrões obrigatórios (idiomas, formatação, pacotes travados, cobertura) e o fluxo de
pull request. A branch `main` só recebe código por PR com o CI verde.

## Segurança

Encontrou uma vulnerabilidade? Siga o [SECURITY.md](SECURITY.md) e relate em
privado pelo GitHub.

## Código de conduta

Este projeto segue o [Contributor Covenant](CODE_OF_CONDUCT.md). Ao participar,
você concorda em respeitá-lo.

## Aviso

O Trail não tem vínculo com nenhum órgão público nem banca examinadora.

## Licença

[AGPL-3.0](LICENSE). Uma versão modificada usada pela rede precisa oferecer o
código-fonte aos seus usuários; o rodapé da aplicação traz o link para isso
([ADR 0006](docs/adr/0006-licenca-agpl.md)).
