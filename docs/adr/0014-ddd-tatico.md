# ADR 0014 — DDD tático: agregados, value objects, IDs tipados e eventos de domínio

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor (DDD tático completo, com eventos)

## Contexto

O `Domain` concentra as regras que são o valor do sistema
([ADR 0011](0011-estilo-arquitetural.md)). O autor escolheu aplicar os padrões táticos do
DDD por completo, incluindo eventos de domínio.

## Decisão

1. **Agregados:** a raiz protege as invariantes, e um agregado referencia outro só por
   ID. Regra geral: um caso de uso altera um agregado por transação. As exceções são
   declaradas no modelo (importação, restauração, criar e excluir disciplina, exclusões
   em cascata).
2. **Value objects** imutáveis para os conceitos que têm regra própria (ex.:
   `QuestionCount`, `TopicCode`, `PerformancePeriod`).
3. **IDs tipados** (`readonly record struct SubjectId(Guid Value)`), gerados com
   `Guid.CreateVersion7()` (ordenáveis pelo tempo, bons para índices) e convertidos pelo
   EF Core.
4. **Eventos de domínio:** registros imutáveis com nome no passado (`TopicStatusChanged`),
   levantados pela raiz do agregado.
5. **Despacho:**
   - em processo, depois do commit, por um `SaveChangesInterceptor` na `Infrastructure`;
   - para as implementações de `IDomainEventConsumer<TEvent>` na `Application` (o sufixo `Handler` fica reservado aos casos de uso, e o analisador CA1711 rejeita `EventHandler`);
   - o despachante registra cada evento no log estruturado;
   - a falha de um consumidor vai para o log e não desfaz a gravação.
6. **Consumidores na 1.0:** métricas de negócio
   ([ADR 0016](0016-observabilidade.md)). Funcionalidades futuras, como revisão espaçada e
   metas, assinam os mesmos eventos sem alterar os agregados.
7. **Todo evento tem ao menos um consumidor próprio,** verificado por teste. Isso impede
   eventos decorativos.

O modelo (agregados, regras de negócio e onde cada uma é garantida, value objects e
eventos) foi validado contra as regras de negócio e está em
[2.2](../02-dominio-e-regras.md#modelo-tático-ddd).

## Consequências

- ✅ As invariantes ficam num só lugar e não podem ser contornadas pela interface.
- ✅ O compilador impede passar um `TopicId` onde se espera um `SubjectId`.
- ✅ Os eventos alimentam a observabilidade desde já e são o ponto de extensão das
  funcionalidades futuras.
- ❌ Mais tipos e mais conversões no EF Core (IDs e value objects).
- ❌ Cada raiz de agregado guarda o próprio `OwnerId` (desnormalização), porque o filtro
  por dono não pode navegar entre agregados.
- ❌ Uma pequena infraestrutura própria para coletar e despachar os eventos.
- ❌ Sem outbox, um evento se perde se o processo cair entre o commit e o despacho. É
  aceitável enquanto os consumidores forem métricas.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Agregados e value objects, sem eventos (recomendação inicial) | Preterida pelo autor. O risco de eventos sem uso é tratado com consumidores reais e com o teste do item 7 |
| Mínimo, com tipos primitivos | Menos proteção contra erros de tipo e invariantes espalhadas |
| Despacho antes do commit, na mesma transação | Só é necessário quando um consumidor grava dados. Fica como gatilho de evolução |
| Outbox | Garante a entrega, mas exige tabela, processamento em segundo plano e idempotência, sem nenhum consumidor que precise disso |
