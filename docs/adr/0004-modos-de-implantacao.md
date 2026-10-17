# ADR 0004 — Dois modos de implantação: individual e hospedado

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor

## Contexto

No início, só o autor usa o sistema. Depois, ele quer compartilhar com outros alunos que
usam a mesma planilha. O requisito é rodar 100% em Docker, com backup e importação dos
dados.

## Decisão

Suportar as duas formas com a **mesma imagem**:

- **Individual:** cada pessoa roda o próprio Docker, e os dados ficam com ela.
- **Hospedado:** uma instância multiusuário (ex.: numa VPS), com uma conta por aluno.

## Consequências

- ✅ Começa só com o autor e cresce sem reescrever nada.
- ✅ Cada aluno escolhe entre ter o controle dos próprios dados e a praticidade de uma
  instância pronta.
- ❌ Login e isolamento entre usuários são necessários desde o início, mesmo no uso
  individual.
- ❌ O modo hospedado traz obrigações: HTTPS, LGPD e backup do servidor.

## Alternativas consideradas

- **Só individual:** exigiria que todo aluno soubesse usar Docker.
- **Só hospedado:** tiraria do aluno o controle dos próprios dados.
- **Local-first no navegador:** sem servidor, mas difícil de compartilhar e de fazer
  backup confiável.
