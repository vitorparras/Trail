# ADR 0002 — Stack: ASP.NET Core Razor Pages + HTMX + EF Core

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor, depois da comparação de 5 stacks e de 3 opções mais enxutas

## Contexto

O sistema é essencialmente um cadastro com cálculos: uma árvore de tópicos, lançamentos,
gráficos, importação e exportação. O autor domina .NET. Uma primeira rodada de opções
(API + SPA) foi considerada grande demais para um sistema simples.

## Decisão

- .NET 10 (LTS) com ASP.NET Core **Razor Pages** e **HTMX** para a interatividade.
- **EF Core** para acesso a dados.
- Um único projeto web, publicado como uma única imagem Docker.

## Consequências

- ✅ Uma linguagem só, sem etapa de build de frontend e sem contrato de API entre front e
  back para manter.
- ✅ O EF Core permite suportar SQLite e PostgreSQL com o mesmo modelo
  ([ADR 0003](0003-sqlite-e-postgresql.md)).
- ✅ As páginas são geradas no servidor, então ficam leves no celular.
- ❌ Não aparece React no portfólio.
- ❌ Interações muito ricas e o uso offline completo custam mais do que numa SPA.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| API .NET + React (SPA) | Duas linguagens, dois builds e um contrato OpenAPI: escopo grande para o problema |
| TypeScript em tudo (Node + React) | O autor tem menos experiência, e os ORMs são menos maduros para este domínio |
| Next.js ou SvelteKit | Menos experiência. No SvelteKit com Drizzle, cada banco exige o próprio schema |
| Blazor | Ecossistema menor de componentes e gráficos, e menos valorizado no mercado |
| Django + HTMX ou FastAPI + React | Tipagem mais fraca para garantir as regras de pontuação |

## Atualizações

- **[ADR 0011](0011-estilo-arquitetural.md):** o "único projeto web" virou quatro
  projetos (Clean Architecture). Continua sendo uma única imagem Docker.
- **[ADR 0013](0013-frontend-tailwind-daisyui.md):** passa a existir um build de CSS,
  feito por um executável standalone do Tailwind, sem Node e sem `node_modules`.
