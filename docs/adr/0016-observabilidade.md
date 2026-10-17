# ADR 0016 — Observabilidade elevada com OpenTelemetry

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** diretriz do autor

## Contexto

Na versão anterior do plano, a observabilidade era opcional e só entrava na Fase 7. O
autor pediu observabilidade elevada.

O sistema roda em dois modos:

- no individual, quase ninguém terá um coletor de telemetria;
- no hospedado, o autor precisa diagnosticar problemas de vários usuários.

## Decisão

1. **OpenTelemetry desde a Fase 0,** com os três sinais: traces, métricas e logs.
   - Com `OTEL_EXPORTER_OTLP_ENDPOINT` definido, tudo é exportado por OTLP.
   - Sem a variável, os logs continuam em JSON no console, e nada mais é exportado.
2. **Instrumentação automática:** ASP.NET Core, EF Core, HttpClient e runtime do .NET.
3. **Resultado de cada caso de uso:** o ponto único de saída
   ([ADR 0015](0015-resultado-padrao.md)) marca o span da requisição com o status e os
   códigos das mensagens e registra a métrica de resultados. Isso dispensa pipeline e
   decorators ([ADR 0011](0011-estilo-arquitetural.md)).
4. **Spans próprios só nas operações longas:** importação, backup, restauração e envio de
   e-mail.
5. **Métricas de negócio** alimentadas pelos eventos de domínio
   ([ADR 0014](0014-ddd-tatico.md)): questões registradas, sessões e minutos estudados,
   simulados, importações e e-mails enviados ou com falha.
6. **Correlação:** o `traceId` aparece em toda linha de log e nas mensagens de erro
   mostradas ao usuário ("código de rastreio"), para localizar o problema relatado.
7. **Privacidade e cardinalidade:**
   - nenhum dado pessoal em logs, spans ou métricas;
   - o usuário aparece só pelo ID e nunca como dimensão de métrica.
8. **Onde ver:**
   - no desenvolvimento, o Aspire Dashboard, no `compose.dev.yml`;
   - no modo hospedado, qualquer backend compatível com OTLP. O guia de instalação trará
     um exemplo, escolhido na Fase 7.
9. **Saúde e configuração:** `/health/live`, `/health/ready` e configuração validada na
   inicialização, como antes.

Detalhes em [3.16](../03-arquitetura.md#316-observabilidade-e-operação).

## Consequências

- ✅ Um problema relatado pode ser seguido da tela até a consulta SQL pelo código de
  rastreio.
- ✅ As métricas de uso real ajudam a validar as funcionalidades depois da 1.0, como o
  autor pretende fazer.
- ✅ Padrão aberto: nenhum fornecedor de observabilidade é obrigatório.
- ❌ Mais dependências (pacotes do OpenTelemetry) e um pouco de código de instrumentação.
- ❌ No modo individual, sem um coletor, só os logs ficam disponíveis.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Só logs e health checks (plano anterior) | Insuficiente para a diretriz do autor |
| Serilog | Ótimo para logs, mas não cobre traces e métricas. O OpenTelemetry cobre os três sinais e tem suporte nativo no .NET |
| Application Insights ou outro fornecedor | Prende o self-hosting a um serviço pago |
| Decorators para medir cada caso de uso | Exigiriam a interface genérica rejeitada na ADR 0011. O ponto único de saída cobre o mesmo |
