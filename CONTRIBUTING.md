# Contribuindo com o Trail

Obrigado pelo interesse. O Trail é desenvolvido em fases, uma por vez, seguindo o
[roteiro](docs/05-roteiro.md). Antes de contribuir, leia o
[plano do projeto](docs/README.md): ele define escopo, arquitetura, regras de
negócio e critérios de qualidade, e evita trabalho que não será aceito.

Ao participar você concorda em seguir o [Código de Conduta](CODE_OF_CONDUCT.md).
Problemas de segurança seguem o [SECURITY.md](SECURITY.md), nunca uma issue
pública.

## Formas de contribuir

- **Relatar um bug** com o [modelo de bug][bug].
- **Sugerir uma funcionalidade** com o [modelo de sugestão][feature]. Confira
  antes o que já está planejado no [roteiro](docs/05-roteiro.md) e o que foi
  deliberadamente deixado de fora em
  [3.7](docs/03-arquitetura.md#37-o-que-deliberadamente-não-fazemos).
- **Corrigir documentação**: os documentos em `docs/` e as ADRs são parte do
  produto.
- **Implementar uma entrega do roteiro**: comente na issue correspondente antes
  de começar, para não haver trabalho duplicado.

[bug]: https://github.com/vitorparras/Trail/issues/new?template=bug_report.yml
[feature]: https://github.com/vitorparras/Trail/issues/new?template=feature_request.yml

## Ambiente de desenvolvimento

Requisitos: o SDK do .NET indicado em `global.json`, Docker (para PostgreSQL e
para os testes end-to-end) e Git.

```bash
git clone https://github.com/vitorparras/Trail.git
cd Trail
dotnet restore --locked-mode
dotnet build --configuration Release
dotnet test --configuration Release
```

Para rodar a aplicação localmente com PostgreSQL, use `deploy/compose.dev.yml`.
Com SQLite, basta `dotnet run --project src/Trail.Web`. Os detalhes, incluindo
os testes end-to-end e o harness de desempenho, estão em
[Qualidade e entrega](docs/04-qualidade-e-entrega.md).

## Padrões

Os padrões obrigatórios estão em
[Qualidade e entrega](docs/04-qualidade-e-entrega.md) e nas
[ADRs](docs/adr/). Em resumo:

- **Idiomas** ([ADR 0007](docs/adr/0007-idiomas.md)): interface (via `.resx`) e
  documentação em pt-BR; código, commits e PRs em inglês; sem comentários no
  código, salvo exceção pontual.
- **Formatação**: `dotnet format --verify-no-changes` deve passar.
- **Pacotes**: restauração em modo travado (`packages.lock.json`); pacotes com
  vulnerabilidade conhecida quebram o build.
- **Arquitetura**: os testes em `tests/Trail.Architecture.Tests` fazem valer as
  camadas e as dependências permitidas.
- **Migrações**: se o modelo mudar, gere as migrações para SQLite e PostgreSQL;
  o CI verifica se há mudanças pendentes.
- **Cobertura**: Domain e Application precisam manter pelo menos 90% de linhas
  cobertas.
- **Dependências de front-end** (Tailwind, daisyUI, HTMX, Chart.js) são
  atualizadas à mão, com versões e checksums em `eng/frontend.props`
  ([ADR 0013](docs/adr/0013-frontend-tailwind-daisyui.md)).

## Fluxo de pull request

1. Faça um fork e crie uma branch a partir de `main` (ou da branch `phase/*` em
   andamento, quando a entrega pertencer a uma fase):
   `git switch -c <type>/<short-description>`.
2. Mudanças focadas: uma entrega, correção ou ADR por PR.
3. Commits em inglês, no padrão
   [Conventional Commits](https://www.conventionalcommits.org/). O release-please
   usa essas mensagens para gerar o `CHANGELOG.md` e a versão.
4. Abra o PR e preencha o modelo. Vincule a issue ou a entrega do roteiro.
5. O CI (build, formatação, migrações, testes, cobertura, end-to-end e CodeQL)
   precisa passar. Um alerta do CodeQL de severidade alta ou crítica bloqueia o
   merge.
6. Um mantenedor fará a revisão. Responda aos comentários; PRs parados por mais
   de 30 dias podem ser fechados.

## Licença das contribuições

O Trail é licenciado sob a [AGPL-3.0](LICENSE)
([ADR 0006](docs/adr/0006-licenca-agpl.md)). Ao abrir um PR você confirma que
tem o direito de licenciar sua contribuição nesses termos.

## Dúvidas

Use as [Discussions](https://github.com/vitorparras/Trail/discussions) para
perguntas e ideias que ainda não são issues concretas.
