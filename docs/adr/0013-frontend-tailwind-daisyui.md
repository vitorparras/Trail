# ADR 0013 — Interface: Tailwind CSS 4 + daisyUI 5 + HTMX + Chart.js

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** o autor pediu bibliotecas consolidadas ("fazer todo o CSS é extremo para
  dar manutenção, principalmente na responsividade") e escolheu entre três opções:
  Bootstrap 5.3, Tabler e Tailwind CSS 4 + daisyUI 5

## Contexto

A interface é gerada no servidor com Razor Pages + HTMX
([ADR 0002](0002-stack-aspnetcore-razor-htmx.md)). Ela precisa ser responsiva (o uso no
celular é central), ter tema claro e escuro e exigir pouca manutenção de um mantenedor
só.

Em outubro de 2026, o Bootstrap 6 entrou em alpha com mudanças incompatíveis. Quem
começasse no Bootstrap 5.3 teria uma migração garantida pela frente.

## Decisão

- **Tailwind CSS 4** para o layout e a responsividade (prefixos `sm:`, `md:`, `lg:`).
- **daisyUI 5** para os componentes (botão, card, badge, tabela, formulário, modal,
  abas, toast). As classes semânticas (`btn`, `card`, `badge`) mantêm o HTML legível.
- **Temas do daisyUI:**
  - claro e escuro, seguindo `prefers-color-scheme`, com troca manual por `data-theme`;
  - identidade visual própria por um tema customizado (cores, raio, fonte), sem CSS
    próprio.
- **Build sem Node:** o executável standalone do Tailwind, com o bundle do daisyUI
  (`daisyui.mjs`), gera um único `app.css` minificado.
- **HTMX** para a interatividade e **Chart.js** para os gráficos.
- **Sem CDN:** tudo é servido pelo próprio app, com versão fixa. Funciona na rede local,
  sem rastreamento de terceiros e com CSP estrita.
- **Componentes só em CSS:** nada precisa ser reinicializado depois das trocas do HTMX.

### Detalhes

- **Build integrado:**
  - um target do MSBuild na `Web` baixa o executável do Tailwind na versão fixada,
    confere o SHA-256 e gera o CSS antes da compilação;
  - assim, `dotnet build`, o CI e o Dockerfile funcionam sem passo manual;
  - no desenvolvimento, o executável roda com `--watch` ao lado do `dotnet watch`;
  - o CSS gerado (`wwwroot/css/app.css`) fica fora do Git.
- **Versões num só lugar:** Tailwind, daisyUI, HTMX e Chart.js ficam fixados num único
  arquivo do repositório e são atualizados à mão, porque o Dependabot não acompanha esses
  arquivos.
- **Classes sempre completas:** o Tailwind encontra as classes lendo os arquivos do
  projeto. Uma classe montada em tempo de execução (ex.: `"badge-" + color`) não é
  gerada.
- **Ícones:** Lucide (licença ISC), num sprite SVG servido pelo app.
- **Acessibilidade:**
  - os modais e o menu lateral do celular usam o `<dialog>` nativo (`showModal()`), que
    já prende o foco, fecha com `Esc` e torna o fundo inerte. A gaveta do daisyUI,
    baseada em checkbox, não é usada;
  - tooltips não carregam informação essencial, porque o texto em pseudo-elemento não é
    lido por leitores de tela. A informação vai visível ou em `aria-label`;
  - os testes ponta a ponta verificam a navegação por teclado nos fluxos principais,
    além do axe.
- **Toasts:** as mensagens do `Result` ([ADR 0015](0015-resultado-padrao.md)) chegam por
  `hx-swap-oob` num contêiner de toasts. Um script próprio, pequeno e em arquivo externo
  (por causa da CSP), remove cada toast depois de alguns segundos.

## Consequências

- ✅ Bibliotecas consolidadas e amplamente usadas, com as versões principais estáveis.
- ✅ A responsividade é declarada no próprio HTML, sem CSS próprio para manter.
- ✅ Os componentes só em CSS combinam com o HTMX: nada a reinicializar a cada troca.
- ✅ O CSS final contém só as classes usadas.
- ✅ Visual moderno e identidade própria pelo tema, sem escrever CSS.
- ❌ Há uma etapa de build (um executável, sem Node) no desenvolvimento, no CI e no
  Dockerfile.
- ❌ O HTML leva mais classes do que com o Bootstrap, o que as classes semânticas do
  daisyUI atenuam.
- ❌ Teclado e foco dependem dos elementos nativos. Menus e gaveta exigem mais cuidado do
  que os componentes com JavaScript do Bootstrap (mitigado nos detalhes acima).
- ❌ As versões são atualizadas à mão.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Bootstrap 5.3 com tema próprio | O mais consolidado e sem build, mas com visual genérico sem um tema bem cuidado, migração para o Bootstrap 6 pela frente e componentes com JavaScript a reinicializar após as trocas do HTMX |
| Tabler (sobre o Bootstrap 5) | Era a recomendação. Tem visual pronto, mas poucos mantenedores, CSS maior, estética de painel administrativo e a mesma migração para o Bootstrap 6 |
| Tailwind com Node e npm | O executável standalone faz o mesmo sem `node_modules` |
| Bulma | Comunidade menor e sem componentes interativos |
| CSS próprio | Rejeitado pelo autor: manutenção cara, principalmente da responsividade |
| Bibliotecas via CDN | Dependem de internet e expõem os usuários a terceiros |
