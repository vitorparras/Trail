# Fase 0 — Fundação: detalhamento

> **Situação:** aprovado pelo autor (decisões D1 a D11 e tarefas T1 a T10), com o nome
> **Trail**.

## Objetivo

Deixar o repositório pronto, com o ciclo build → teste → imagem funcionando nos dois
bancos e as regras de arquitetura já verificadas. A fase não entrega funcionalidade de
negócio: ela entrega a base em que todas as outras se apoiam.

## Pré-requisitos verificados nesta máquina

| Ferramenta | Versão encontrada | Uso |
|---|---|---|
| .NET SDK | 10.0.402 | Build e testes |
| Docker | 29.7.2 | Imagem, compose e Testcontainers |
| GitHub CLI | 2.99.0, conta `vitorparras` | Repositório, PRs e CI |
| Git | 2.55 | Versionamento |

## Decisões da fase (aprovadas)

Confirmam as ferramentas que estavam como proposta na
[stack](../03-arquitetura.md#38-stack) e em [Qualidade e entrega](../04-qualidade-e-entrega.md).

| # | Tema | Proposta |
|---|---|---|
| D1 | Testes | xUnit v3, ArchUnitNET, Testcontainers, CsCheck, Playwright for .NET e Deque.AxeCore.Playwright |
| D2 | Imagem | `aspnet:10.0-noble-chiseled-extra`, usuário não root, `/data` pertencendo ao usuário `app` |
| D3 | Compose | `compose.dev.yml`: PostgreSQL, Mailpit e Aspire Dashboard. `compose.postgres.yml`: Caddy, app e PostgreSQL (o serviço de backup entra na Fase 6) |
| D4 | IDs tipados | Escritos à mão (`readonly record struct`), sem gerador de código, com um conversor do EF Core aplicado por convenção a todos |
| D5 | Registro dos handlers | Varredura por reflexão das classes `*Handler` da `Application`, sem biblioteca. A convenção de nome é garantida pelo teste de arquitetura |
| D6 | SDK fixo | `global.json` com o SDK 10.0 (`rollForward: latestFeature`) |
| D7 | Versões do frontend | `eng/frontend.props`, com as versões de Tailwind, daisyUI, HTMX, Chart.js e Lucide, e o SHA-256 do executável do Tailwind |
| D8 | Ponta a ponta desde já | Um teste de fumaça contra a imagem Docker (página inicial no desktop e no celular, temas claro e escuro, axe), para o CI nascer completo |
| D9 | Migração inicial vazia | A Fase 0 ainda não tem entidades. Uma migração vazia em cada banco valida o caminho todo: aplicação na inicialização, cópia do SQLite antes de migrar e verificação de pendências no CI. As tabelas chegam na Fase 1 |
| D10 | Regras com prova | Cada regra de arquitetura tem um caso de exemplo que a viola, e o teste confirma que a regra detecta a violação. Assim nenhuma regra passa "no vazio" |
| D11 | Padrões e Git | Passam a valer os itens de [4.2](../04-qualidade-e-entrega.md#42-padrões-de-código) a [4.4](../04-qualidade-e-entrega.md#44-integração-contínua-github-actions): analisadores, formatação, Conventional Commits, `main` protegida, Dependabot e CodeQL |

## Tarefas

Cada tarefa é um commit (ou poucos commits atômicos) na branch da fase, com os próprios
testes, e estende o CI com as suas verificações. A fase inteira vira um único PR.

### T1 — Repositório e documentação

- `git init` com a branch `main`.
- `.gitignore`: padrão do .NET, mais `*.xlsx`, `.env`, `.tools/` e o CSS gerado.
- `.gitattributes`: fim de linha LF para scripts, Dockerfile e YAML.
- `LICENSE` (AGPL-3.0) e `README.md` inicial em pt-BR: o que é, situação do projeto,
  aviso de que não há vínculo com órgãos nem bancas, e link para `docs/`.
- Primeiro commit, com a documentação. É o único commit direto na `main`.
- Repositório **público** no GitHub, na conta `vitorparras`, e a branch da fase.

**Pronto quando:** a planilha não aparece em `git ls-files`.

### T2 — Solução, padrões e CI inicial

- `global.json`, `Trail.slnx`, `Directory.Build.props`, `Directory.Packages.props` e
  `.editorconfig`.
- Projetos `Domain`, `Application`, `Infrastructure` e `Web`, com as referências da
  [seção 3.5](../03-arquitetura.md#35-camadas-módulos-e-padrões-internos).
- Projetos de teste `Domain.Tests`, `Application.Tests`, `Web.Tests`,
  `Architecture.Tests` e `E2E.Tests`. Cada um nasce na tarefa que traz o seu primeiro
  teste: um projeto sem testes faz o `dotnet test` falhar e não verifica nada.
- `nuget.config` com o nuget.org como única origem e mapeamento de pacotes. O build não
  depende de feeds configurados na máquina e fica protegido contra confusão de pacotes
  entre origens.
- As pastas dos módulos nascem com o primeiro arquivo de cada um, porque o Git não guarda
  pastas vazias.
- CI inicial: formatação, build e testes.
- Proteção da `main`: só por PR e com o CI verde.

**Pronto quando:** `dotnet build` sem avisos, `dotnet format --verify-no-changes` passa e
o CI fica verde na branch da fase.

### T3 — Blocos de base do domínio e da aplicação

- `Domain/Common`: `Result`, `Result<T>`, `Message`, `MessageSeverity`, `ResultStatus`,
  `AggregateRoot`, `IDomainEvent` e o padrão de ID tipado.
- `Application/Abstractions`: `IAppDbContext`, `ICurrentUser` e
  `IDomainEventHandler<TEvent>`.
- Registro dos handlers e os nomes do `Meter` e do `ActivitySource`.
- Testes de unidade: mensagens acumuladas (padrão Notification), `Result` com e sem
  dados, criação de IDs.

**Pronto quando:** cobertura de pelo menos 90% nesses tipos.

### T4 — Persistência nos dois bancos e eventos de domínio

- `AppDbContext`, os contextos derivados de cada banco e a escolha por
  `Database__Provider`, com a configuração validada na inicialização.
- Convenções do EF Core:
  - conversor dos IDs tipados;
  - `DateTime` sempre em UTC;
  - `Version` como token de concorrência;
  - filtro por dono em toda raiz com `OwnerId`.
- Interceptor que coleta os eventos e os despacha depois do commit, registra cada um no
  log e não desfaz a gravação quando um consumidor falha.
- Migração inicial em cada banco, aplicada na inicialização. No SQLite: modo WAL e cópia
  do banco antes de migrar.
- Health checks em `/health/live` e `/health/ready`.
- CI: verificação de migrações pendentes nos dois contextos e matriz SQLite/PostgreSQL.

**Pronto quando:** os testes de despacho, de falha de consumidor e de migração a partir do
zero passam nos dois bancos.

### T5 — Web: composição, ponto único de saída e interface base

- `Program.cs` com a composição e o modo `--healthcheck`.
- Ponto único de saída: cada `Status` do `Result` vira a resposta definida na
  [ADR 0015](../adr/0015-resultado-padrao.md). As mensagens são traduzidas pelo `.resx`, e
  a página de erro mostra o código de rastreio.
- Localização pt-BR com `.resx`.
- HTMX:
  - `allowEval = false` e `includeIndicatorStyles = false`;
  - `responseHandling` trocando o conteúdo em 422 e 409;
  - token antifalsificação em `hx-headers`.
- Frontend:
  - build do Tailwind + daisyUI pelo target do MSBuild, com download verificado por
    SHA-256, e um script de `--watch` para o desenvolvimento;
  - tema próprio claro e escuro;
  - Lucide, HTMX e Chart.js servidos pelo app.
- Layout base: barra superior, menu lateral com `<dialog>` no celular, troca de tema e
  área de toasts.
- Página inicial, que vira o painel nas próximas fases.

**Pronto quando:** o layout funciona no celular e no desktop, nos dois temas, e há testes
para a conversão de cada `Status`.

### T6 — Observabilidade

- OpenTelemetry com traces, métricas e logs, exportados por OTLP quando
  `OTEL_EXPORTER_OTLP_ENDPOINT` estiver definido. Sem a variável, só logs JSON no console.
- O ponto único de saída marca o span com o status e os códigos das mensagens e registra
  a métrica de resultados.
- `compose.dev.yml` com PostgreSQL, Mailpit e Aspire Dashboard.
- Testes com coletor em memória.

**Pronto quando:** uma requisição a `/health/ready` aparece no Aspire Dashboard, com o
trace da requisição até o banco.

### T7 — Docker e implantação

- `Dockerfile` multi-stage:
  - o build roda na arquitetura da máquina de build e gera o CSS;
  - o runtime é chiseled, com `/data` do usuário `app` e `HEALTHCHECK` pelo próprio app.
- `.dockerignore`, `deploy/compose.sqlite.yml`, `deploy/compose.postgres.yml`, `Caddyfile`
  e `.env.example`.
- CI: build da imagem.

**Pronto quando:** os dois arquivos compose sobem e respondem em `/health/ready`, com o
processo rodando como não root.

### T8 — Testes de arquitetura

- As regras 5 a 11 da [seção 3.5](../03-arquitetura.md#35-camadas-módulos-e-padrões-internos)
  e a regra de que todo evento tem consumidor.
- Para cada regra, um caso que a viola de propósito (D10).

**Pronto quando:** o teste detecta o `Domain` usando um tipo do EF Core e uma página
usando o `IAppDbContext`.

### T9 — Ponta a ponta de fumaça

- Playwright contra a imagem Docker: página inicial no desktop e no celular, nos dois
  temas, sem violações do axe.
- CI: os testes ponta a ponta rodam contra a imagem gerada.

**Pronto quando:** o teste passa no CI.

### T10 — Segurança do repositório

- CodeQL (C# e JavaScript).
- Dependabot para NuGet, GitHub Actions e imagens Docker.
- Verificação de pacotes vulneráveis no CI.

**Pronto quando:** as três verificações rodam na branch da fase.

## Entrega da fase

Ao fim da T10, abro o PR da fase e apresento:

- o que foi feito;
- como validar, com os comandos exatos no PowerShell;
- as decisões tomadas e os riscos.

O merge do PR e o início da Fase 1 dependem da sua aprovação.
