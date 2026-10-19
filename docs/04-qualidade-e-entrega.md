# 4. Qualidade e entrega

As regras de idioma (4.2 e 4.3) estão aceitas na [ADR 0007](adr/0007-idiomas.md). O
restante foi aprovado no [detalhamento da Fase 0](fases/fase-0-fundacao.md).

## 4.1 Estratégia de testes

O esforço de teste segue o risco: mais pesado nos cálculos e nos dados
([3.2](03-arquitetura.md#32-características-de-arquitetura)), leve no que é genérico.

| Nível | Projeto | O que cobre | Ferramentas |
|---|---|---|---|
| Unidade | `Domain.Tests` | Regras de pontuação, período de desempenho, sessões de estudo, invariantes e parser de numeração. Cada erro da planilha (E1 a E5) vira um caso de regressão. | xUnit v3 |
| Propriedades | `Domain.Tests` | Invariantes dos cálculos testadas com milhares de entradas geradas (ex.: desempenho entre −100 e 100; só entram lançamentos do período; soma das partes = total) | CsCheck |
| Arquitetura | `Architecture.Tests` | Regras de composição, módulos, agregados, bibliotecas e convenções dos handlers ([3.5](03-arquitetura.md#35-camadas-módulos-e-padrões-internos)), e todo evento com consumidor | ArchUnitNET |
| Casos de uso | `Application.Tests` | Handlers contra banco real: o `Result` de cada caminho, eventos despachados, métricas geradas, filtro por dono, importação e backup, **em SQLite e PostgreSQL** | xUnit v3, Testcontainers |
| Integração | `Web.Tests` | Páginas e respostas HTMX, conversão de cada `Status` do `Result` em resposta, isolamento entre usuários em todas as rotas, e-mail e migrações a partir do zero, **em SQLite e PostgreSQL** | xUnit v3, `WebApplicationFactory`, Testcontainers |
| Ponta a ponta | `E2E.Tests` | Fluxos críticos no navegador, em tela de desktop e de celular | Playwright for .NET |
| Acessibilidade | `E2E.Tests` | Páginas principais sem violações WCAG 2.1 AA | Deque.AxeCore.Playwright |

**Fluxos ponta a ponta:**

1. Criar conta → confirmar pelo e-mail (Mailpit) → importar a planilha de exemplo → ver
   o painel.
2. Lançar questões → o desempenho e o histórico se atualizam → trocar o período (últimos
   15 dias, todas).
3. Iniciar o pomodoro → avançar o relógio (API de relógio do Playwright) → a sessão
   aparece nas horas da disciplina.
4. Exportar backup → restaurar em outra conta → os dados batem.
5. Recuperar a senha pelo e-mail.
6. Registrar um simulado e uma tentativa do TAF.

**Planilha de teste:** gerada por código, no mesmo formato da original, mas sem o brasão
e sem dados pessoais. A planilha original não entra no repositório.

**Relógio:** todo código que depende de data ou hora recebe um `TimeProvider`, e os
testes controlam o tempo (`FakeTimeProvider`).

**Independência dos E2E** (Onda 0, W0-T11): cada teste cria o seu usuário com o
`CreateUserAsync()` (convite do admin, cadastro pelo convite, confirmação pelo link manual
e login, tudo por requisições HTTP com o token antifalsificação), que devolve um `E2EUser`
com `Email` e `Password`. O `DockerAppFixture` abre:

- `CreatePageAsync(user, width, height)` ou `CreatePageAsync(user, options)`: uma página
  já autenticada como o usuário;
- `CreatePageAsync(options)`: uma página anônima, para as telas de entrada;
- `CreateOtherDevicePageAsync(user, options)`: o mesmo usuário em outro contexto, com o
  seu próprio cookie ("outro aparelho");
- `CreateAdminPageAsync(width, height)`: só para os testes de Contas.

Os contextos do navegador nascem pelo auxiliar interno `E2EBrowserContexts`
(`NewPageAsync(IBrowser, BrowserNewContextOptions?, string? storageState)`), com
`ServiceWorkers = Block` por padrão; só o `PwaE2ETests` pede `ServiceWorkerPolicy.Allow`.
Cada página nova fecha os contextos que testes anteriores abriram no mesmo navegador
(pelo `TestContext.Current.Test?.UniqueID`), e o `DisposeAsync` da fixture fecha o resto.
O mesmo auxiliar (`CloseContextsOfBrowserAsync`, `AuthenticationCookieName` e
`AuthenticationCookieAsync`) e o `AppHttpSession`, que cadastra e faz login por HTTP em
qualquer URL base, servem a outros hosts do E2E, como o host em processo do pomodoro da
Fase 5. Uma falha mostra o texto do `<main>` da página.

O container do E2E roda com o override `tests/Trail.E2E.Tests/compose.e2e.yml` (cadastro
`InviteOnly` e limite de tentativas do login alto, só no E2E), e o container do
Testcontainers recebe as mesmas variáveis. O `ComposeOverrideTests` aceita no override só
`services:`, `app:` e `environment:` com entradas `CHAVE: valor`, e exige que ele declare
exatamente as variáveis do Testcontainers (`DockerAppFixture.E2EEnvironment`). Cada classe de
teste passa sozinha, sem depender da ordem da suíte. Uma execução precisa de uma instância
nova, porque o admin é a primeira conta.

**Desempenho** (Onda 0, W0-T12): o harness fica em `Trail.Web.Tests/Performance/`. Uma
classe de fixture derivada de `PerformanceFixture` define o volume (`Shape`, por padrão
4 blocos, 12 disciplinas, 1.000 tópicos em até 3 níveis e 100 anotações), os serviços
(`ConfigureServices`) e a carga extra (`SeedAsync(PerformanceSeedContext)`). Cada cenário
(`PerformanceScenario(Name, Path, ExpectedTexts, HtmxPartial)`) mede o tempo do servidor,
pela duração do span do ASP.NET Core, e falha acima do `PerformanceBudget.Default`: 300 ms
no p95 de 20 requisições, depois de 10 de aquecimento. As classes de teste levam
`[PerformanceTest]` (o traço `Category=Performance`) e
`[Collection(PerformanceCollections.Sequential)]`. Uma fase acrescenta uma classe nova,
com a sua fixture e o seu volume, sem editar os arquivos do harness.

**Cobertura:**

- Meta de ≥ 90% no `Domain` e na `Application`, onde estão as regras e os casos de uso.
- Na `Web`, a cobertura é medida pelos fluxos testados, não por percentual.

## 4.2 Padrões de código

- **Tudo que é código em inglês:** identificadores, nomes de testes, esquema do banco e
  mensagens de log.
- **Sem comentários.** O código se explica pelos nomes e pela estrutura. Um comentário só
  aparece quando é indispensável (ex.: o porquê de uma decisão que o código não consegue
  mostrar), e sempre em inglês. Exceção aprovada pelo autor: a anotação de versão das
  GitHub Actions fixadas por SHA (`uses: actions/checkout@<sha> # v7.0.1`), que o
  Dependabot mantém junto com o SHA.
- **Textos da interface** ficam em arquivos `.resx` (pt-BR), acessados por chaves em
  inglês. O código não contém texto em português. As views usam só `IStringLocalizer<T>`,
  que codifica o texto; `IViewLocalizer` e `IHtmlLocalizer` não são usados.
- **Rigor do compilador:** `Nullable` habilitado, `TreatWarningsAsErrors` e analisadores
  do .NET no nível `latest-recommended`.
- **Formatação:** `.editorconfig` + `dotnet format --verify-no-changes` no CI.
- **Pacotes:** versões centralizadas em `Directory.Packages.props`, com arquivos de lock
  (`packages.lock.json`) e restore travado (`--locked-mode`) no CI, decisão do autor. O
  conteúdo de cada pacote é verificado por hash e o build é reproduzível.

## 4.3 Fluxo de trabalho no Git

- **Tudo em inglês:** mensagens de commit, nomes de branch e títulos e descrições de PR.
- **Conventional Commits** (`feat:`, `fix:`, `docs:`…), em commits atômicos.
- **Branch `main` protegida:** só recebe código por PR, com o CI verde.
- **Uma branch e um PR por fase** (decisão do autor), com commits atômicos, um por
  tarefa. O autor aprova o PR na apresentação do fim da fase.
- **Fases em paralelo** (Onda A, decisão do autor na
  [P5 da Onda 0](fases/onda-0-base-compartilhada.md#p5--fluxo-de-integração-da-onda-a)):
  cada fase continua com a sua branch e o seu PR para a `main`, integrados na ordem
  3 → 4 → 5 → 7a, salvo outra ordem do autor. Antes do merge de cada uma:
  1. a branch é rebaseada na `main` atual;
  2. a migração local da fase é apagada nos dois contextos, e os snapshots do modelo ficam
     como na `main` (um conflito no snapshot se resolve assim, nunca à mão);
  3. a migração é gerada de novo pelo `dotnet ef`, com o mesmo nome (`AddQuestionEntries`,
     `AddMockExamsAndFitnessTest` ou `AddStudyTime`; a 7a não tem migração), em
     `SqliteAppDbContext` e `PostgreSqlAppDbContext`;
  4. `has-pending-model-changes` não acusa mudança nos dois contextos;
  5. o CI fica verde, e o autor aprova o PR.

  As fases que ainda não entraram refazem o rebase depois de cada merge.
- **Migrações 100% geradas** pelo `dotnet ef`, nunca editadas à mão, nem na integração.
  Na Fase 3, por exemplo:

  ```powershell
  dotnet ef migrations add AddQuestionEntries --project src/Trail.Infrastructure --context SqliteAppDbContext --output-dir Persistence/Sqlite/Migrations
  dotnet ef migrations add AddQuestionEntries --project src/Trail.Infrastructure --context PostgreSqlAppDbContext --output-dir Persistence/PostgreSql/Migrations
  dotnet ef migrations has-pending-model-changes --project src/Trail.Infrastructure --context SqliteAppDbContext
  dotnet ef migrations has-pending-model-changes --project src/Trail.Infrastructure --context PostgreSqlAppDbContext
  ```
- **Repositório público desde o início** (decisão do autor): o histórico de commits, PRs
  e CI também conta no portfólio.

## 4.4 Integração contínua (GitHub Actions)

Em todo PR para a `main` ou para `phase/**` (as ondas não mudam os gatilhos, porque cada
fase da Onda A abre o seu PR para a `main`, conforme a [4.3](#43-fluxo-de-trabalho-no-git)):

1. Verificação de formatação.
2. Build, incluindo a geração do CSS (avisos viram erros).
3. Testes de unidade, de propriedades e de arquitetura.
4. Verificação de migrações pendentes nos dois contextos.
5. Testes de casos de uso e de integração, em matriz: `sqlite` e `postgresql`. O passo
   exclui os testes de desempenho (`--filter-not-trait "Category=Performance"`) e avisa no
   log que eles rodam no `performance.yml`.
6. Testes ponta a ponta (Playwright) contra a imagem Docker gerada, no
   `deploy/compose.sqlite.yml` com o override `tests/Trail.E2E.Tests/compose.e2e.yml`
   (`--file` nos dois arquivos, ao subir, ao ler os logs e ao derrubar), que liga o cadastro
   `InviteOnly` para a fixture criar um usuário por teste. A exceção aprovada são os fluxos
   que precisam controlar o relógio do servidor (pomodoro e sessão esquecida, Fase 5): eles
   rodam contra o app no processo do teste, com `FakeTimeProvider`, pelos pacotes que a
   Onda 0 acrescentou ao projeto de E2E (`Microsoft.Extensions.TimeProvider.Testing`,
   `Microsoft.AspNetCore.Mvc.Testing` e a referência ao `Trail.Web`).
7. Verificação de pacotes vulneráveis: o restore do CI trata os avisos de auditoria do
   NuGet (diretos e transitivos) como erro. Uma exceção justificada usa
   `NuGetAuditSuppress`, registrada no repositório.
8. CodeQL: C#, JavaScript e os próprios workflows, com o código de terceiros excluído da
   análise.
9. Build da imagem Docker, sem publicar.

Orçamento de desempenho ([3.15](03-arquitetura.md#315-verificações-automáticas-da-arquitetura)):
o `.github/workflows/performance.yml` roda só os testes com o traço
`Category=Performance`, em matriz `sqlite` e `postgresql`:

- todo dia (05:17 UTC) e sob demanda (`workflow_dispatch`);
- num PR para a `main` ou para `phase/**` que altere `tests/Trail.Web.Tests/Performance/`
  ou o próprio workflow, para que quem mexe no harness veja o resultado no PR.

Fora disso, os testes de desempenho não entram no CI dos PRs, para não gerar falso alarme.

Dependabot para NuGet, GitHub Actions (fixadas por SHA) e imagens Docker (fixadas por
digest), com alertas e correções automáticas de segurança ativos. Tailwind, daisyUI, HTMX
e Chart.js são atualizados à mão, com as versões num só arquivo
([ADR 0013](adr/0013-frontend-tailwind-daisyui.md)). Por isso, os avisos de segurança dos
repositórios do HTMX e do Chart.js são acompanhados no GitHub (risco residual registrado).

Proteções do repositório, decididas pelo autor:

- `main` só recebe código por PR, com o CI verde, sem force push nem exclusão.
- Um PR com alerta do CodeQL de severidade alta ou crítica não pode ser mergeado.
- Relato privado de vulnerabilidades ativo (usado pelo `SECURITY.md`).

## 4.5 Releases

- Versionamento semântico (SemVer). O release-please lê os commits, gera o
  `CHANGELOG.md` (em inglês, como os commits) e cria a tag.
- A tag publica a imagem no GHCR para amd64 e arm64, com a versão e `latest`.
- As notas de versão destacam qualquer mudança no formato de backup.
- **Atualizar** é `docker compose pull && docker compose up -d`. As migrações são
  automáticas, e no SQLite o app faz uma cópia do banco antes de migrar.

## 4.6 Open source

### Arquivos da raiz

| Arquivo | Conteúdo |
|---|---|
| `LICENSE` | AGPL-3.0 |
| `README.md` | O que é, capturas de tela, instalação com um comando, links para a documentação |
| `CONTRIBUTING.md` | Como rodar localmente, padrões e como propor funcionalidades |
| `CODE_OF_CONDUCT.md` | Contributor Covenant |
| `SECURITY.md` | Como reportar vulnerabilidades em privado (GitHub Security Advisories) |
| `.github/` | Modelos de issue (bug, sugestão) e de PR |
| `CHANGELOG.md` | Gerado pelo release-please |

### AGPL na prática

O rodapé de toda página tem um link "Código-fonte", configurável, apontando para o
repositório. A AGPL exige que uma versão modificada, usada pela rede, ofereça o código
aos seus usuários. O link pronto facilita que quem fizer um fork cumpra isso.

### Cuidados antes de publicar

- **Não versionar `marcação.xlsx`:** ela tem o brasão da PF, e o modelo foi distribuído
  a vários alunos, então a autoria pode ser de terceiros. Incluir `*.xlsx` no
  `.gitignore`.
- **Sem identidade visual de órgãos:** nenhum brasão, logo ou nome de órgão como marca.
  O README deixa claro que o projeto não tem vínculo com nenhum órgão nem banca.
- **Conteúdo de edital:** o conteúdo programático é parte de um ato oficial, que não é
  protegido por direito autoral (Lei 9.610/1998, art. 8º, IV). Pode servir de exemplo,
  citando a fonte.

### Portfólio

- README com GIFs dos fluxos principais e badges de CI, cobertura e licença.
- ADRs mostrando o raciocínio por trás de cada decisão, inclusive do que **não** foi
  feito ([3.7](03-arquitetura.md#37-o-que-deliberadamente-não-fazemos)).
- Demo pública com dados fictícios: candidata para depois da 1.0.
