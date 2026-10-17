# ADR 0012 — Autenticação completa com ASP.NET Core Identity e e-mail

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor ("quero por completo, não teremos MVP")

## Contexto

Os dois modos de implantação ([ADR 0004](0004-modos-de-implantacao.md)) exigem login e
isolamento entre usuários. No modo hospedado, o autor precisa controlar quem cria conta,
e os usuários precisam recuperar a senha sozinhos. A versão 1.0 é o programa final, sem
fluxos provisórios.

## Decisão

- **ASP.NET Core Identity** com cookie, sem serviço externo de identidade.
- **E-mail completo**, enviado com MailKit por um SMTP configurável:
  - confirmação de conta;
  - recuperação de senha;
  - convites.
- **A primeira conta criada vira administradora.**
- **Modos de cadastro** (`Registration__Mode`):
  - `Closed`: padrão no modo individual.
  - `InviteOnly`: padrão no modo hospedado.
  - `Open`.
- **Admin:** listar usuários, desativar contas, convidar, reenviar e-mails e enviar um
  e-mail de teste.
- **Proteções:**
  - bloqueio de conta após tentativas falhas;
  - rate limiting no login;
  - respostas que não revelam se um e-mail existe;
  - tokens com validade curta.
- **Sem SMTP configurado:** comum no modo individual. O admin gera os links de
  confirmação, recuperação e convite para entregar manualmente, e um comando no container
  redefine a senha do admin.

## Consequências

- ✅ Os usuários do modo hospedado são autônomos (cadastro e recuperação de senha).
- ✅ Nenhuma dependência externa obrigatória: o modo individual funciona sem SMTP.
- ❌ O modo hospedado exige um SMTP configurado, e e-mail sem SPF/DKIM tende a cair no
  spam. O guia de instalação vai cobrir isso.

## Alternativas consideradas

- **Sem e-mail, com recuperação de senha pelo admin:** rejeitada pelo autor, porque o
  fluxo ficaria incompleto.
- **Provedor externo (Keycloak, Auth0):** pesado demais para self-hosting.
- **2FA, passkeys e login com Google/GitHub:** não selecionados para a 1.0. São
  candidatos a validação depois.
