# ADR 0005 — Web responsiva + PWA

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor

## Contexto

O registro de questões costuma acontecer logo depois de resolvê-las, muitas vezes pelo
celular. O sistema roda em Docker ([ADR 0004](0004-modos-de-implantacao.md)).

## Decisão

Interface web responsiva, **instalável como PWA**, servida pelo mesmo container. Sem app
nativo e sem app desktop.

## Consequências

- ✅ Um código só para desktop e celular.
- ✅ Instala no celular com ícone, como um app.
- ❌ O PWA exige HTTPS. Na rede local, sem certificado, o sistema funciona como site mas
  não instala. Por isso, o guia vai documentar Tailscale e Cloudflare Tunnel.
- ❌ O uso offline completo não faz parte da versão 1.0. É candidato a validação depois,
  e exigiria uma nova ADR.
- ❌ No iOS, o PWA tem limitações (notificações, armazenamento).

## Alternativas consideradas

- **Só web responsiva:** sem a experiência de app no celular.
- **Web + app nativo:** um segundo código para manter, publicação nas lojas, e cada
  pessoa teria que configurar o endereço do próprio servidor.
- **Desktop:** contradiz o requisito de rodar em Docker.
