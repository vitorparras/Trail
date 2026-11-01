# Política de segurança

Obrigado por ajudar a manter o **Trail** e quem o usa em segurança.

## Versões com suporte

A versão mais recente publicada (imagem `ghcr.io/vitorparras/trail` com a tag
`latest` e a tag de versão correspondente) e a branch `main`. Versões anteriores
não recebem correções: atualize com `docker compose pull && docker compose up -d`.

## Como relatar uma vulnerabilidade

**Não abra uma issue pública para problemas de segurança.**

Relate em privado pelo relato de vulnerabilidades do GitHub:

1. Abra <https://github.com/vitorparras/Trail/security/advisories/new>.
2. Descreva o problema, onde está, como reproduzir e o impacto esperado. Uma
   prova de conceito é bem-vinda, mas não obrigatória.

Se não puder usar o GitHub, envie um e-mail para <contato.vitorparras@gmail.com>
com as mesmas informações e o assunto `[SECURITY] Trail`.

## O que esperar

| Etapa | Prazo-alvo |
|---|---|
| Confirmação de recebimento | até 3 dias úteis |
| Avaliação inicial e severidade | até 7 dias |
| Correção ou mitigação de problemas confirmados | até 90 dias, antes para problemas críticos |
| Divulgação pública | coordenada com você depois que a correção estiver disponível |

Você será creditado no aviso de segurança, a menos que prefira anonimato.

## Escopo

Dentro do escopo:

- A aplicação (`src/`): autenticação e contas, isolamento entre usuários no modo
  hospedado, importação e backup de arquivos, injeção, XSS, CSRF, controle de
  acesso.
- Imagens Docker, arquivos de `deploy/` e configuração padrão.
- Scripts, configuração e workflows do GitHub Actions deste repositório.
- Problemas de cadeia de suprimentos: pacotes NuGet, bibliotecas de front-end
  vendorizadas, actions e imagens fixadas, processo de build e release.

Fora do escopo:

- Vulnerabilidades em dependências de terceiros sem um caminho de exploração no
  Trail. Relate-as ao projeto de origem; avisos do HTMX e do Chart.js são
  acompanhados conforme a [documentação de qualidade](docs/04-qualidade-e-entrega.md).
- Instâncias auto-hospedadas por terceiros: relate ao administrador da instância.
- Problemas que exigem acesso físico à máquina ou à conta de um mantenedor.

## Porto seguro

Pesquisa de boa-fé que siga esta política está autorizada. Não tomaremos medidas
legais contra pesquisadores que evitem violar privacidade, destruir dados ou
interromper serviços, e que nos deem tempo razoável para corrigir o problema
antes de divulgá-lo. Teste apenas em instâncias suas.

## Como este repositório é protegido

- `main` só recebe mudanças por pull request, com o CI verde (build,
  formatação, migrações, testes, cobertura e end-to-end); force push e exclusão
  da branch são bloqueados.
- CodeQL em C#, JavaScript/TypeScript e Actions; um alerta de severidade alta ou
  crítica bloqueia o merge.
- Varredura de segredos com proteção no push, alertas e atualizações de
  segurança do Dependabot (NuGet, Actions, Docker) e revisão de dependências em
  cada PR.
- Restauração de pacotes em modo travado; pacotes com vulnerabilidade conhecida
  quebram o build.
- As GitHub Actions ficam restritas a autores do GitHub e verificados, fixadas
  por SHA de commit, com permissões padrão somente leitura.
- O [OpenSSF Scorecard](https://scorecard.dev) roda semanalmente e publica os
  resultados.

Os detalhes das decisões de segurança estão em
[Arquitetura](docs/03-arquitetura.md) e [Qualidade e entrega](docs/04-qualidade-e-entrega.md).
