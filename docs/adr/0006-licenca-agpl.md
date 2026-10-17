# ADR 0006 — Licença AGPL-3.0

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor

## Contexto

O repositório será software livre e pode ser hospedado como serviço por terceiros
([ADR 0004](0004-modos-de-implantacao.md)).

## Decisão

Licenciar o projeto sob a **GNU Affero General Public License v3.0**.

## Consequências

- ✅ Quem hospedar uma versão modificada como serviço precisa oferecer o código aos
  usuários. Isso impede forks fechados.
- ✅ É coerente com a proposta de software livre.
- ❌ As dependências precisam ter licenças compatíveis. MIT, BSD e Apache-2.0 são.
- ❌ Empresas tendem a evitar AGPL. Isso pesa pouco num aplicativo final, que não é uma
  biblioteca.
- O rodapé terá um link "Código-fonte", para facilitar o cumprimento da licença por
  quem fizer um fork ([4.6](../04-qualidade-e-entrega.md#46-open-source)).

## Alternativas consideradas

- **GPL-3.0:** não obriga a publicar o código quando o software é hospedado como serviço.
- **MIT:** permite que qualquer um feche o código.
