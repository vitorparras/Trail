# ADR 0010 — Desempenho calculado por período

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor. A primeira versão desta ADR usava as últimas N questões,
  e o autor a revisou: "o período do filtro não é por quantidade, é por data (semana
  passada, último mês, últimos 15 dias)"

## Contexto

Na planilha:

- A disciplina mostra a média simples dos percentuais dos tópicos. Um tópico com 2
  questões pesa o mesmo que um com 200 (E2).
- O resultado some quando a soma dos percentuais dá zero (E3).
- Tudo considera todas as questões já feitas, então o desempenho antigo dilui o atual.

O autor quer ver o desempenho de um período recente, escolhido por data.

## Decisão

- **Fórmula:** a da regra do plano, aplicada às questões somadas do período. Na regra
  Cebraspe, Σ(C − E) / ΣT × 100. Na tradicional, ΣC / ΣT × 100. Cada questão pesa igual.
- **Período:** entram os lançamentos do escopo (tópico, disciplina, bloco ou plano) cuja
  data está dentro do período. Cada lançamento entra inteiro, então não há estimativa.
- **Volume sempre visível:** a tela mostra quantas questões entraram ("42 questões nos
  últimos 15 dias"), porque um percentual sobre poucas questões é pouco confiável.
- **Período vazio:** mostra "—" e "nenhuma questão nos últimos 15 dias".
- Um resultado igual a zero aparece como 0%, nunca em branco.

### Detalhes

- **Opções do seletor:** Todas, últimos 7, 15, 30 e 90 dias, e um intervalo de datas
  livre.
  - "Últimos N dias" conta o dia de hoje, no fuso do usuário.
  - "Semana passada" e "último mês" correspondem aos últimos 7 e 30 dias. As semanas de
    calendário já aparecem no histórico semanal.
- **Padrão por plano:** configurável. Um plano novo começa em "Todas".
- **Questões importadas:** a planilha não guarda datas. Na prévia da importação, o
  usuário informa a data a atribuir a elas (padrão: o dia da importação). Sem isso, todo
  o histórico importado contaria como recente nas primeiras semanas.

## Consequências

- ✅ Responde à pergunta natural: "como fui nos últimos 15 dias?".
- ✅ Só dados reais: cada lançamento entra inteiro, sem estimativa.
- ✅ O volume real de questões é respeitado, o que corrige E2 e E3, e a mesma regra vale
  em todos os níveis.
- ✅ A consulta é um filtro por data, simples e coberto por índice.
- ❌ O volume varia de um período para outro, e um período com poucas questões tem um
  percentual pouco confiável. Por isso o volume aparece sempre junto.
- ❌ Um período pode ficar vazio.
- ❌ Com os mesmos dados, os números serão diferentes dos da planilha. A diferença fica
  documentada em testes de regressão.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Últimas N questões (primeira versão desta ADR) | Substituída pelo autor. Também exigia uma regra para o lançamento que cruza o limite (entrar proporcional, inteiro ou ficar de fora) |
| Semanas e meses de calendário (ex.: a semana passada, de segunda a domingo) | O histórico semanal já mostra essa visão. No filtro, períodos móveis respondem melhor a "como estou agora" |
| Média simples dos tópicos, como na planilha | Mantém o erro E2 |
