# ADR 0001 — Escopo genérico: qualquer conteúdo de estudo

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor

## Contexto

A planilha atual atende a um único concurso (PF – Perito Criminal Federal, Área 03).
Vários alunos usam o mesmo modelo, e o autor quer usar o sistema também para outros
conteúdos, como programação.

## Decisão

O sistema modela **planos de estudo** genéricos:

- Estrutura: blocos (opcionais) → disciplinas → tópicos com subtópicos.
- Regra de pontuação escolhida por plano: Cebraspe, tradicional ou sem pontuação.
- O TAF é um módulo opcional, ativado por plano.
- O edital da PF vira apenas um exemplo de plano.

## Consequências

- ✅ Serve a qualquer concurso, banca ou trilha de estudo.
- ✅ Amplia o público do projeto open source.
- ❌ A interface precisa de termos neutros ("plano", não "edital").
- ❌ Criar um plano exige configurar a regra de pontuação e os módulos.

## Alternativas consideradas

- **Só PF Área 03:** mais simples, mas inútil para outros alunos e conteúdos.
- **Só bancas no modelo Cebraspe:** ainda não atende a conteúdos sem pontuação, como uma
  trilha de programação.
