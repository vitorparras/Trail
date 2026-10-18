# Onda 0 — Base compartilhada: detalhamento

> **Situação:** proposta, aguardando aprovação do autor. Prepara a Onda A, em que as Fases
> 3, 4 e 5 e a parte de PWA da Fase 7 serão desenvolvidas em paralelo, conforme o plano em
> ondas aprovado pelo autor em 17/10/2026.

## Objetivo

Criar antes as peças que mais de uma fase da Onda A vai usar e as convenções que evitam
conflitos quando várias fases são desenvolvidas ao mesmo tempo. A Onda 0 não entrega
funcionalidade nova para o aluno, exceto as duas decisões pendentes da Fase 2.

## Por que antes

Verificado no código da `main` (`2cf9d81`):

| Ponto de conflito | Situação hoje | Efeito se as fases rodarem em paralelo |
|---|---|---|
| Migrações | Cada fase cria uma migração, e todas alteram o snapshot do modelo nos dois bancos | Conflito garantido nos arquivos de snapshot |
| Configuração do EF Core | Todas as entidades são configuradas num único `OnModelCreating` do `AppDbContext` | Todas as fases editam o mesmo método |
| Registro de serviços | Os stores de cada módulo são registrados no mesmo `Infrastructure/DependencyInjection.cs` | Todas as fases editam o mesmo arquivo |
| Textos da interface | Um único `SharedResource.resx` com 843 linhas | Todas as fases acrescentam chaves no mesmo arquivo |
| Peças comuns | Gráficos, painel e `QuestionCount` ainda não existem | As Fases 3 e 4 criariam versões diferentes da mesma peça |

## Tarefas

### W0-T1 — Configuração do EF Core por entidade

- Cada entidade passa a ter sua classe `IEntityTypeConfiguration<T>`, em
  `Infrastructure/Persistence/Configurations/<Módulo>/`, aplicadas com
  `ApplyConfigurationsFromAssembly`.
- As configurações existentes (Contas e Planejamento) são movidas sem mudar o modelo: a
  verificação `has-pending-model-changes` continua sem pendências nos dois contextos.

**Pronto quando:** o modelo gerado é idêntico ao atual, e os testes dos dois bancos passam.

### W0-T2 — Registro de serviços por módulo

- Cada módulo registra os próprios serviços num método de extensão próprio
  (`AddPlanningInfrastructure`, `AddAccountsInfrastructure` etc.), chamado uma vez pelo
  `AddInfrastructure`.
- Uma fase nova cria o seu arquivo de registro e acrescenta uma única linha no ponto de
  composição.

**Pronto quando:** nenhum registro de módulo fica no arquivo geral, e um teste de
arquitetura garante isso.

### W0-T3 — Textos por módulo

- Os módulos novos (Prática, Tempo de estudo, TAF, Intercâmbio) usam um `.resx` próprio,
  por meio de uma classe marcadora por módulo. O `SharedResource.resx` fica só com os
  textos do layout e os comuns.
- Os textos que já existem de Contas e Planejamento não são movidos agora, para não gerar
  mudança sem ganho.

**Pronto quando:** a convenção está documentada e um teste verifica que todo `.resx` tem as
mesmas chaves usadas pelas views do seu módulo.

### W0-T4 — Componente de gráfico

- Um tag helper `<chart>` que renderiza o `<canvas>` e os dados em JSON num atributo
  `data-*`. Um script externo inicializa o Chart.js, compatível com a CSP com nonce.
- Reinicialização depois das trocas do HTMX, cores do tema claro e escuro (variáveis do
  daisyUI) e redimensionamento no celular.
- Todo gráfico tem a tabela equivalente, como pede o roteiro, para acessibilidade.

**Pronto quando:** um gráfico de exemplo nos testes passa no axe nos dois temas, e o
teste ponta a ponta confirma que ele é desenhado depois de uma troca do HTMX.

### W0-T5 — Painel montado por blocos

- A página inicial do usuário autenticado vira o painel, montado por blocos (view
  components) que cada módulo registra, com ordem e título próprios.
- A Fase 3 acrescenta o bloco de desempenho, a Fase 4 o de simulados e TAF, e a Fase 5 o
  de horas estudadas, sem editar a página do painel.

**Pronto quando:** um bloco de exemplo registrado em teste aparece no painel, e o painel
sem blocos mostra uma mensagem de boas-vindas.

### W0-T6 — `QuestionCount` no domínio

- O value object `QuestionCount` (total, certas e erradas, brancos derivados), com as
  invariantes da [seção 2.2](../02-dominio-e-regras.md#modelo-tático-ddd), testes de
  propriedade e conversão no EF Core como tipo complexo.
- É usado pelos lançamentos de questões (Fase 3) e pelos simulados (Fase 4).

**Pronto quando:** cobertura de 100% e testes de propriedade das invariantes.

### W0-T7 — Convenção de migrações em paralelo

- Durante a Onda A, cada fase trabalha com a sua migração local.
- Na integração, o orquestrador descarta a migração da fase e gera de novo uma migração
  por fase, em sequência, nos dois bancos, sobre a branch já integrada. Depois verifica
  `has-pending-model-changes`.

**Pronto quando:** a convenção está registrada aqui e nos detalhamentos das Fases 3, 4 e 5.

### W0-T8 — Decisões pendentes da Fase 2

Implementar conforme a escolha do autor:

1. **Convites no modo `Closed`:** esconder ou avisar sobre os convites nesse modo, ou deixar
   um convite válido criar a conta mesmo com o cadastro fechado.
2. **Login no app inteiro:** manter a convenção por pasta ou adotar uma política única que
   exige login em todas as páginas, liberando só as públicas.

**Pronto quando:** o comportamento escolhido tem testes, e as telas de Preferências e
Administração seguem a mesma regra das páginas de planos.

### W0-T9 — Pontos de extensão comuns

Proposta pelos planejadores das Fases 3, 4 e 5, que chegaram à mesma lacuna de forma
independente (P9 da Fase 3, pergunta 7 da Fase 4 e pergunta 10 da Fase 5):

- **Calendário do usuário (`IUserCalendar`):** expõe o `TimeZoneInfo` do usuário, o dia de
  hoje no fuso dele e a conversão de instantes para o dia local. É usado para recusar datas
  futuras (Fases 3 e 4) e para somar horas por dia e semana (Fase 5). Os testes usam o
  `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`).
- **Prévia de exclusão por contribuição (`IDeletionImpactContributor`):** fica em
  `Application/Abstractions`. Cada módulo informa o que será apagado junto: lançamentos
  (Fase 3), simulados e tentativas do TAF (Fase 4) e sessões (Fase 5). A prévia atual da
  Fase 2 passa a somar as contribuições.
- **Links por módulo na página do plano:** cada módulo registra o seu link (Questões,
  Simulados, TAF, Tempo de estudo), sem editar a página do plano.

**Pronto quando:** as três peças têm testes, e a prévia de exclusão da Fase 2 continua com o
mesmo comportamento.

## Ordem

As tarefas W0-T1 a W0-T6 e W0-T9 são independentes entre si e podem rodar em paralelo. A
W0-T7 é só documentação, e a W0-T8 depende das respostas do autor.

## Pronto quando (onda)

- O CI fica 100% verde no PR da onda.
- Os detalhamentos das Fases 3, 4, 5 e 7a citam estas convenções.
