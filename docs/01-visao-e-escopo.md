# 1. Visão e escopo

## 1.1 O problema

O estudo hoje é controlado pela planilha `marcação.xlsx`, um "edital verticalizado" do
concurso PF – Perito Criminal Federal, Área 03. Ela tem quatro abas:

| Aba | O que faz |
|---|---|
| **Conteúdo Programático** | 325 tópicos em 23 disciplinas, divididas em 2 blocos (Conhecimentos Gerais e Específicos). Para cada tópico: status, anotações, questões feitas, certas e erradas, e desempenho no padrão Cebraspe. Nas linhas 331–354, um resumo por disciplina e a mediana de cada bloco. |
| **Desempenho** | 16 gráficos de barras, um por disciplina ou grupo de disciplinas. |
| **Simulados** | 12 linhas de simulado com 120 questões: brancos, certas, erradas, nota normal, nota Cebraspe e mediana. |
| **Simulado TAF** | 12 tentativas do teste físico (barra, salto, natação e corrida), com resultado, pontos, total e comentário. |

A cópia analisada não tem progresso registrado: os 325 tópicos estão como "A Estudar" e
não há questões lançadas.

## 1.2 O que a análise da planilha encontrou

### Erros de cálculo

| # | Onde | Problema | Efeito |
|---|---|---|---|
| E1 | Conteúdo Programático, coluna J | `=IF(H3<>0, ((H3-I3)/G3)*100, "")` só calcula quando há acertos | Um tópico com 0 certas e 10 erradas aparece em branco em vez de −100% |
| E2 | Coluna K | Média simples dos percentuais dos tópicos | Um tópico com 2 questões pesa o mesmo que um com 200 |
| E3 | Coluna K | `IF(SUM(J…)<>0, AVERAGE(…), "")` | Se os desempenhos se anulam (soma zero), a média da disciplina some |
| E4 | Simulados, H3 e I3 | `MEDIAN(F3:F12)`, com 12 linhas de simulado | Os simulados 11 e 12 ficam fora da mediana |
| E5 | Desempenho, gráficos de Criptografia e de Sistemas Operacionais | Usam a coluna C (disciplina) como rótulo, em vez da D (tópico) | As barras mostram o nome da disciplina repetido |
| E6 | Status | "Revisando" está na lista de validação, mas não tem regra de cor | Tópicos em revisão não se destacam |

### Limitações estruturais

- **Sem histórico:** as questões são totais acumulados, sem data. Não dá para ver a
  evolução nem quanto foi feito na semana.
- **Edital fixo:** outro concurso exige montar outra planilha à mão. Os 16 gráficos têm
  intervalos de células fixos.
- **Hierarquia no texto:** a numeração dos tópicos não segue um padrão. 220 dos 325 têm
  ponto final ("4.1."), 83 não têm ("4.1") e 21 começam com espaço. Um tópico não tem
  número ("ENGENHARIA REVERSA DE SOFTWARE", que deveria ser o item 3).
- **Numeração mista:** nos Conhecimentos Gerais, cada disciplina recomeça do 1. Nos
  Específicos, a numeração continua de uma disciplina para outra (Bancos de Dados é 2.x,
  Criptografia é 9.x).
- **Trabalho manual:** os pontos do TAF são digitados à mão, e o simulado tem 120
  questões fixas.
- **Sem controle de tempo de estudo.**
- **Sem backup nem uso no celular:** compartilhar com outro aluno significa mandar o
  arquivo.
- **Identidade visual de órgão público:** a planilha tem o brasão da PF embutido.

## 1.3 Visão

> Um lugar único, simples e confiável para acompanhar o que já foi estudado, quanto
> tempo foi dedicado, o desempenho em questões e a evolução nos simulados, para qualquer
> conteúdo, rodando no próprio Docker ou numa instância compartilhada, com os dados
> sempre exportáveis.

## 1.4 Público

1. **Agora:** o autor, substituindo a própria planilha.
2. **Em seguida:** os alunos que usam a mesma planilha, cada um no próprio Docker ou numa
   instância hospedada pelo autor.
3. **Depois:** qualquer pessoa que estude um conteúdo organizado em tópicos.

## 1.5 Escopo da versão 1.0

**Não há MVP.** A versão 1.0 é o programa final para este escopo: cada funcionalidade é
entregue completa, sem versões reduzidas nem atalhos provisórios. Funcionalidades além
desta lista entram depois, uma por vez, quando o autor validar que são úteis.

### Substitutos da planilha

| Funcionalidade | Equivalente na planilha | O que muda |
|---|---|---|
| Planos de estudo | O arquivo inteiro | Vários planos, para qualquer conteúdo |
| Blocos, disciplinas e tópicos com subtópicos | Colunas B, C e D | Hierarquia real, numeração normalizada |
| Status do tópico | Coluna E | Os 4 status com cor **e** texto, incluindo "Revisando" (E6) |
| Anotações por tópico | Coluna F | Markdown |
| Questões por tópico | Colunas G, H e I | Lançamentos com data e validação (certas + erradas ≤ total) |
| Desempenho por tópico, disciplina e bloco | Colunas J e K, linhas 331–354 | Calculado por período de datas (ex.: últimos 15 dias), com o tópico somando os subtópicos. Corrige E1 a E3 |
| Painel com gráficos | Aba Desempenho | Gerado automaticamente para qualquer plano (corrige E5) |
| Simulados | Aba Simulados | Quantidade livre, mediana de todos (corrige E4), gráfico de evolução |
| TAF, opcional por plano | Aba Simulado TAF | Provas configuráveis |
| Regra de pontuação por plano | Fixa (Cebraspe) | Cebraspe, tradicional ou sem pontuação |

### Novas funcionalidades

| Funcionalidade | Conteúdo |
|---|---|
| **Histórico de questões** | Evolução do desempenho e do volume por semana, no plano e por disciplina, e lista de lançamentos por tópico |
| **Tempo de estudo** | Cronômetro e pomodoro ligados a uma disciplina ou tópico, lançamento manual, e horas por disciplina, por dia e por semana |
| **Importação da planilha** | Importa o `.xlsx` no modelo atual, com prévia e relatório de avisos |
| **Backup e restauração** | Arquivo próprio, independente do banco |
| **Contas completas** | Cadastro com confirmação por e-mail, recuperação de senha, convites e administração |
| **Dois modos de uso** | Individual (cada um no seu Docker) e hospedado multiusuário |
| **Dois bancos** | SQLite e PostgreSQL |
| **Web + PWA** | Responsiva e instalável no celular |

## 1.6 Depois da 1.0: validação incremental

Candidatas ainda **não validadas**:

- Revisões espaçadas.
- Metas e ciclo de estudos.
- Colar o conteúdo do edital como texto e gerar a árvore.
- Modelos de conteúdo compartilháveis entre alunos.
- Pontos do TAF calculados pela tabela do edital.
- Uso offline completo.
- Demo pública com dados fictícios.
- Login com segundo fator (2FA), passkeys ou contas Google/GitHub.

## 1.7 Fora de escopo

- App nativo e app desktop.
- Resolver questões dentro do sistema (banco de questões).
- Integração com plataformas de questões.
- Qualquer vínculo com órgãos públicos ou bancas, ou uso da identidade visual deles.

## 1.8 Requisitos não funcionais

| Tema | Requisito |
|---|---|
| Implantação | 100% Docker. Modo individual: 1 container (SQLite). Modo hospedado: app + PostgreSQL + proxy HTTPS. |
| Portabilidade | Um backup feito com um banco restaura no outro. |
| Desempenho | A página de um plano com cerca de 1.000 tópicos e 10.000 lançamentos responde em menos de 300 ms no servidor. |
| Acessibilidade | WCAG 2.1 AA. O status nunca é indicado só por cor, e cada gráfico tem uma tabela equivalente. |
| Segurança | OWASP Top 10 coberto (ver [arquitetura](03-arquitetura.md#314-segurança)), com isolamento entre usuários testado automaticamente. |
| Privacidade (LGPD) | Dados mínimos (e-mail e senha). O usuário exporta e exclui a própria conta. |
| Idioma | Interface e documentação em pt-BR. Tudo que é código em inglês ([ADR 0007](adr/0007-idiomas.md)). |
| Licença | AGPL-3.0. |
