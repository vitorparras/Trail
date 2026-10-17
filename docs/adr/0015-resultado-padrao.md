# ADR 0015 — Resultado padrão orientado a mensagens

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** decisão do autor (`Result` próprio) e diretriz dele: "na saída da
  aplicação, com Result padrão, o que muda é o data, que contém informações próprias
  (padrão de retorno orientado a mensagem)"

## Contexto

Os casos de uso precisam informar falhas esperadas (entrada inválida, registro não
encontrado, conflito de versão) e mensagens para o usuário. Com HTMX, cada resposta
precisa virar o parcial certo, com o status HTTP certo. O autor quer um formato único de
retorno, em que só os dados variam de um caso de uso para outro.

## Decisão

1. **Falhas esperadas são valores, não exceções.** Exceção só para o inesperado (bug,
   banco fora do ar).
2. **Envelope único,** `Result<T>` (e `Result`, sem dados), definido no `Domain` e sem
   biblioteca externa:

   ```text
   Result<T>
   ├─ Status     Ok | Invalid | NotFound | Conflict | Forbidden
   ├─ Data       T, só quando Ok: a única parte que varia por caso de uso
   └─ Messages   [ { Code, Severity, Target? } ]
   ```

   - `Code`: estável e em inglês (ex.: `QuestionCount.Exceeded`).
   - `Severity`: `Error`, `Warning`, `Info` ou `Success`.
   - `Target`: o campo do formulário a que a mensagem se refere, quando houver.
3. **Padrão Notification** (Martin Fowler): a validação acumula todas as mensagens em vez
   de parar na primeira, e o formulário mostra todos os erros de uma vez.
4. **Mensagens são códigos, não textos:** a `Web` traduz o `Code` pelo `.resx`
   ([ADR 0007](0007-idiomas.md)), e o mesmo código vai para os logs e as métricas
   ([ADR 0016](0016-observabilidade.md)).
5. **Ponto único de saída na `Web`:** um único método converte qualquer
   `Result` em resposta.

   | Status | HTTP | Com HTMX | Sem HTMX |
   |---|---|---|---|
   | `Ok` | 200 | Parcial com o `Data`, e as mensagens como toasts (`hx-swap-oob`) | Redirect com as mensagens (Post/Redirect/Get) |
   | `Invalid` | 422 | Formulário com as mensagens junto de cada campo | Página com o formulário |
   | `NotFound` | 404 | Parcial "não encontrado" | Página 404 |
   | `Conflict` | 409 | Aviso de alteração feita em outro aparelho, com opção de recarregar | Página com o aviso |
   | `Forbidden` | 403 | Parcial "acesso negado" | Página 403 |
   | Exceção | 500 | Parcial de erro com o código de rastreio | Página de erro com o código de rastreio |

   - Dado de outro usuário responde `NotFound`, nunca `Forbidden`, para não revelar que
     o dado existe.
   - Por padrão, o HTMX não troca o conteúdo em respostas 4xx. A configuração
     `responseHandling` passa a trocar em 422 e 409.

## Consequências

- ✅ Toda saída da aplicação tem a mesma forma: quem lê um handler sabe o que esperar, e
  a `Web` trata todos os casos num lugar só.
- ✅ O fluxo de erro fica explícito no tipo de retorno, sem exceções como controle de
  fluxo.
- ✅ Os códigos estáveis servem à tradução, aos logs, às métricas e aos testes.
- ✅ Se surgir uma API ([3.17](../03-arquitetura.md#317-gatilhos-de-evolução)), o mesmo
  envelope vira o JSON da resposta.
- ❌ Há código próprio para manter (o tipo e o ponto de saída). É pequeno e coberto por
  testes.
- ❌ Os handlers ficam um pouco mais longos do que com exceções.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| ErrorOr ou FluentResults | Mesma ideia, mas o `Domain` deixaria de depender só do .NET |
| Exceções de domínio com filtro global | Fluxo de erro escondido, e é difícil devolver o parcial certo ao HTMX |
| Um erro só por resultado | O formulário mostraria um erro de cada vez |
