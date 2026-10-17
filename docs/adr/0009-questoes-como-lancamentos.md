# ADR 0009 — Questões como lançamentos com data

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** proposta técnica, aprovada pelo autor

## Contexto

A planilha guarda, por tópico, só os totais acumulados (questões, certas, erradas). Para
registrar uma nova sessão, é preciso somar de cabeça e sobrescrever os totais. Isso
impede o histórico de questões e o desempenho por período
([ADR 0010](0010-periodo-de-desempenho.md)), que fazem parte da versão 1.0.

## Decisão

- Cada sessão é gravada como um **lançamento** (`QuestionEntry`): data, total, certas e
  erradas.
- Os totais do tópico são a soma dos lançamentos.
- O lançamento é "fiz 10, acertei 7, errei 3", sem somar de cabeça. Os lançamentos
  podem ser editados e excluídos.
- **Importação da planilha:** gera um lançamento por tópico, com a data da importação.

## Consequências

- ✅ Permite o histórico e o desempenho por período.
- ✅ Lançar fica mais simples do que na planilha.
- ❌ Corrigir um total significa editar ou excluir um lançamento, não digitar o número
  novo.
- ❌ Os totais importados da planilha viram um único lançamento: o histórico anterior à
  importação não existe, porque a planilha não o guarda.

## Alternativas consideradas

- **Totais no tópico, como na planilha:** mais simples, mas inviabiliza o histórico e o
  desempenho por período.
