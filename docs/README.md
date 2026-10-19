# Trail — plano do projeto

> Esta pasta reúne o planejamento completo: visão, regras de negócio, arquitetura,
> qualidade e roteiro. As Fases 0, 1 e 2 e a Onda 0 já estão implementadas.

## O que é

Um sistema web, auto-hospedável em Docker, para acompanhar o estudo de um conteúdo
programático: edital de concurso, trilha de programação ou qualquer conteúdo organizado
em tópicos.

Ele substitui a planilha `marcação.xlsx` (PF – Perito Criminal Federal, Área 03), que
vários alunos usam, corrige os problemas que a análise dela encontrou e acrescenta
histórico de questões e controle do tempo de estudo.

**Não há MVP:** a versão 1.0 é o programa final para o escopo definido.

## Documentos

| # | Documento | Conteúdo |
|---|---|---|
| 1 | [Visão e escopo](01-visao-e-escopo.md) | Problema, análise da planilha, escopo da 1.0, o que fica para depois, requisitos |
| 2 | [Domínio e regras](02-dominio-e-regras.md) | Glossário, modelo de dados, modelo tático, regras de negócio, fórmulas, período de desempenho, tempo de estudo, importação e backup |
| 3 | [Arquitetura](03-arquitetura.md) | Padrões adotados, características, camadas e módulos, composição por módulo e pontos de extensão, casos de uso, resultado padrão, textos e mensagens, eventos, pontos de variação, o que não fazemos, stack, formulários e gráficos, dados, contas e login, segurança, observabilidade, verificações automáticas e gatilhos de evolução |
| 4 | [Qualidade e entrega](04-qualidade-e-entrega.md) | Testes (inclusive a fixture do E2E e o harness de desempenho), padrões de código, Git e integração das fases paralelas, CI/CD, releases, open source |
| 5 | [Roteiro](05-roteiro.md) | Fases 0 a 7 em ondas, com entregas e critérios de pronto. Detalhamentos: [Fase 0](fases/fase-0-fundacao.md), [Fase 1](fases/fase-1-contas-email-isolamento.md), [Fase 2](fases/fase-2-planos-e-conteudo.md), [Onda 0](fases/onda-0-base-compartilhada.md), [Fase 3](fases/fase-3-questoes-desempenho-historico.md), [Fase 4](fases/fase-4-simulados-e-taf.md), [Fase 5](fases/fase-5-tempo-de-estudo.md), [Fase 7a](fases/fase-7a-pwa.md) |
| — | [ADRs](adr/) | Registros de decisão, um por decisão |

## Decisões aceitas

| Tema | Decisão | ADR |
|---|---|---|
| Escopo | Qualquer conteúdo de estudo: editais de qualquer banca, programação etc. | [0001](adr/0001-escopo-generico.md) |
| Stack | ASP.NET Core (Razor Pages) + HTMX + EF Core, .NET 10 | [0002](adr/0002-stack-aspnetcore-razor-htmx.md) |
| Banco | SQLite e PostgreSQL desde a primeira versão | [0003](adr/0003-sqlite-e-postgresql.md) |
| Implantação | 100% Docker, em dois modos: individual e hospedado multiusuário | [0004](adr/0004-modos-de-implantacao.md) |
| Plataforma | Web responsiva + PWA | [0005](adr/0005-web-responsiva-pwa.md) |
| Licença | AGPL-3.0 | [0006](adr/0006-licenca-agpl.md) |
| Idioma | Interface (via `.resx`) e docs em pt-BR. Tudo que é código em inglês, incluindo commits e PRs. Sem comentários, salvo exceção pontual em inglês | [0007](adr/0007-idiomas.md) |
| Dados | Backup/restauração por arquivo e importação da planilha `.xlsx` | [0008](adr/0008-backup-e-importacao.md) |
| Questões | Lançamentos com data, não totais | [0009](adr/0009-questoes-como-lancamentos.md) |
| Desempenho | Calculado por período de datas (ex.: últimos 15 dias, último mês ou tudo), com cada lançamento inteiro e o volume sempre visível | [0010](adr/0010-periodo-de-desempenho.md) |
| Regras do domínio | Excluir algo com histórico apaga em cascata, após confirmação. Um tópico só muda de pai dentro da disciplina. O desempenho do tópico soma os subtópicos | [2.2](02-dominio-e-regras.md#regras-de-negócio-e-onde-são-garantidas) |
| Arquitetura | Clean Architecture com fatias verticais (`Domain`, `Application`, `Infrastructure`, `Web`), módulos por pasta, uma porta de store por módulo e uma classe por caso de uso, sem MediatR | [0011](adr/0011-estilo-arquitetural.md) |
| Autenticação | Identity completo com e-mail: confirmação, recuperação de senha e convites. Sem SMTP, o admin gera os links | [0012](adr/0012-autenticacao-identity.md) |
| Interface | Tailwind CSS 4 + daisyUI 5, com build pelo executável standalone (sem Node), HTMX e Chart.js | [0013](adr/0013-frontend-tailwind-daisyui.md) |
| DDD tático | Agregados, value objects, IDs tipados e eventos de domínio | [0014](adr/0014-ddd-tatico.md) |
| Retorno | `Result` próprio, envelope único orientado a mensagens: só o `Data` varia | [0015](adr/0015-resultado-padrao.md) |
| Observabilidade | Elevada: OpenTelemetry (traces, métricas e logs) desde a Fase 0, métricas de negócio pelos eventos, código de rastreio nas mensagens de erro e Aspire Dashboard no desenvolvimento | [0016](adr/0016-observabilidade.md) |
| Nome | **Trail**: repositório `vitorparras/trail` (público), imagem `ghcr.io/vitorparras/trail`, namespaces `Trail.*` | — |
| Versão 1.0 | Programa final, sem MVP: substitui a planilha e inclui histórico de questões e tempo de estudo | [Visão e escopo](01-visao-e-escopo.md#15-escopo-da-versão-10) |
| Roteiro | Fases com parada para aprovação ao fim de cada uma | [Roteiro](05-roteiro.md) |

## Aguardando aprovação

Quatro perguntas levantadas na implementação da Onda 0 aguardam o autor, sem bloquear a
Onda A ([P8 a P11](fases/onda-0-base-compartilhada.md#perguntas-levantadas-na-implementação)):
o envio de e-mail fora da requisição, a mensagem do bloqueio no login, a regra do plural
nos textos com quantidade e o formato da prévia de exclusão. As ferramentas ainda marcadas
como proposta (na [stack](03-arquitetura.md#38-stack) e em
[Qualidade e entrega](04-qualidade-e-entrega.md)) são confirmadas no detalhamento da fase
que as usa.

## Próximo passo

Fases 0, 1 e 2 concluídas. A [Onda 0 — Base compartilhada](fases/onda-0-base-compartilhada.md)
foi implementada, com as convenções registradas na arquitetura e as correções às Fases 1 e
2 listadas no detalhamento.

1. Apresentar a Onda 0 ao autor, com as perguntas P8 a P11, e integrá-la à `main`.
2. Em seguida, a Onda A: Fases 3, 4, 5 e 7a em paralelo, com os PRs integrados na ordem
   3 → 4 → 5 → 7a ([4.3](04-qualidade-e-entrega.md#43-fluxo-de-trabalho-no-git)).
