# ADR 0011 — Estilo arquitetural: Clean Architecture com fatias verticais

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor, que rejeitou a primeira proposta (monólito modular com 2
  projetos) porque ela "não segue nenhum padrão"

## Contexto

O projeto é software livre e portfólio. A arquitetura precisa seguir um padrão que quem
avalia o repositório reconheça e, ao mesmo tempo, ser proporcional ao problema: dezenas
de usuários por instância, um mantenedor, dados que valem anos de estudo e cálculos que
são o coração do sistema.

O sistema tem mais de uma porta de entrada (páginas, comandos do container e tarefas
agendadas) e vários pontos de variação na infraestrutura (dois bancos, e-mail com e sem
SMTP, formatos de importação).

## Decisão

1. **Clean Architecture** (Robert C. Martin), com quatro projetos de produção:
   - `Domain`: agregados, value objects, eventos e regras. Depende só do .NET.
   - `Application`: casos de uso e portas (interfaces) para o que é externo.
   - `Infrastructure`: EF Core com os dois bancos, Identity, MailKit, ClosedXML e
     tarefas agendadas.
   - `Web`: Razor Pages + HTMX, comandos do container e a composição da aplicação.
2. **Fatias verticais dentro das camadas:** a `Application` é organizada por módulo e
   por caso de uso (`Practice/RecordQuestions/`), não por tipo de arquivo.
3. **Monólito modular:** um processo e uma imagem. Os módulos (`Planning`, `Practice`,
   `StudyTime`, `Fitness`, `Dashboard`, `Interchange`, `Accounts`) são pastas e
   namespaces dentro de cada camada.
4. **Acesso a dados por `IAppDbContext`:** interface na `Application` que expõe só as
   raízes de agregado. Não há repositórios, porque o EF Core já é repositório e unit of
   work.
5. **Uma classe por caso de uso:** um comando ou consulta e um handler com um único
   `HandleAsync`, injetado direto na página. Sem MediatR, sem interface genérica e sem
   pipeline.
6. **Mapeamento explícito,** sem AutoMapper.
7. **Decisões complementares** em ADRs próprias:
   - DDD tático: [ADR 0014](0014-ddd-tatico.md);
   - resultado padrão: [ADR 0015](0015-resultado-padrao.md);
   - observabilidade: [ADR 0016](0016-observabilidade.md).

Detalhes em [3.5](../03-arquitetura.md#35-camadas-módulos-e-padrões-internos).

## Consequências

- ✅ O padrão é reconhecido de imediato no mercado .NET. Referências: os templates de
  Jason Taylor e de Ardalis.
- ✅ O compilador garante que o `Domain` e a `Application` não dependem do banco, da web
  nem das bibliotecas de infraestrutura.
- ✅ Os mesmos casos de uso atendem às páginas, aos comandos do container e às tarefas
  agendadas, e são testáveis sem HTTP.
- ✅ As fatias por caso de uso evitam o custo típico da Clean Architecture, em que uma
  mudança se espalha por pastas organizadas por tipo de arquivo.
- ❌ Uma funcionalidade passa por até quatro projetos.
- ❌ A `Application` depende do pacote base do EF Core (`DbSet` e LINQ assíncrono). É um
  desvio consciente da forma pura: os provedores (SQLite e PostgreSQL) continuam só na
  `Infrastructure`.
- ❌ Sem pipeline, o que seria transversal precisa de um lugar explícito: a transação é
  um `SaveChanges`, e a telemetria vem da instrumentação automática e do ponto único de
  saída da `Web` ([ADR 0015](0015-resultado-padrao.md) e
  [ADR 0016](0016-observabilidade.md)).
- ❌ As fronteiras entre módulos dentro de cada projeto dependem de testes de
  arquitetura, não do compilador.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Monólito modular com 2 projetos (primeira proposta) | Rejeitada pelo autor: não segue um padrão reconhecível |
| Vertical Slice pura (Jimmy Bogard) | Encaixa bem no Razor Pages, mas as fronteiras dependeriam só de testes, e a regra de negócio tende a vazar para os handlers |
| Hexagonal (Ports & Adapters) | É a mesma essência. No .NET é menos reconhecida e costuma ter um projeto por adaptador |
| Clean Architecture por módulo (Kamil Grzybek) | Cerca de 28 projetos, eventos entre módulos e outbox: desproporcional ao problema |
| Repositório por agregado | Mais código, e as leituras do painel exigiriam serviços de consulta à parte |
| Repositório só na escrita | Dois estilos de acesso a dados convivendo |
| MediatR, ou interface genérica com decorators | Indireção sem ganho proporcional: cada página já é o ponto de entrada do seu caso de uso |
