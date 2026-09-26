# AI Hair Routine API

API .NET 10 que recebe um questionário capilar, gera um **perfil tipado + prioridades** e devolve um
**cronograma capilar de 4 semanas** (ciclo hidratação/nutrição/reconstrução) com **produtos do catálogo**.
Integra dois serviços:

- **JEV (TypeSafe AI)** — refina as prioridades quando o objetivo em texto é ambíguo (System One Model).
- **Provedor de IA** — escreve os textos do cronograma (resumo, como/por quê de cada produto, dicas).
  Suporta **Anthropic (Claude)**, **OpenAI**, **Google Gemini** e **DeepSeek**, selecionáveis por configuração.

O **calendário é calculado por regras** (determinístico e testável); a IA só escreve o texto e não altera
dias, frequências nem produtos. O desenho prioriza **degradação graciosa**: sem chaves e sem banco, a API
continua funcionando (perfil por **regras** + texto por **template**).

## Arquitetura

```
src/
  AIHairRoutine.Api             Minimal API, OpenAPI/Scalar, health, rate limiting, ProblemDetails
  AIHairRoutine.Application     Domínio: modelos, regras de perfil, seleção de produtos, cronograma, facade
  AIHairRoutine.Infrastructure  JEV, provedores de IA, Dapper/SQL Server, HybridCache, resiliência (Polly)
tests/
  AIHairRoutine.Tests           unit (parser, profiler, seletor, cronograma, validador) + integração (endpoint)
```

Fluxo: `validar → IHairProfiler → IProductCatalog → IProductSelector → IScheduleBuilder → IRoutineGenerator (texto) → DiagnosisResult`.

Padrões: **Adapter** (`JevProfiler`; `IChatModelClient` por provedor de IA), **Abstract Factory**
(`IChatModelClientFactory` + `ChatModelClientFactory` selecionam a família do provedor ativo),
**Strategy** (`IRoutineGenerator`; `IApiKeyAuthenticator` para o esquema de autenticação por provedor;
`HybridProfiler`, `ProductSelector`), **Facade** (`DiagnosisService`), **Decorator**
(`CachingRoutineGenerator`), **Builder** (`JevRequestBuilder`, `RoutinePromptBuilder`),
**Fallback/graceful degradation** (`FallbackRoutineGenerator`).

### Provedores de IA (Strategy + Adapter + Abstract Factory)

A variação entre provedores fica isolada em três eixos:

- **API key (Strategy — `IApiKeyAuthenticator`)**: Anthropic usa `x-api-key` + `anthropic-version`,
  OpenAI/DeepSeek usam `Authorization: Bearer`, Gemini usa `x-goog-api-key`.
- **Formato/endpoint (Adapter — `IChatModelClient`)**: cada API tem corpo, rota e resposta próprios
  (`v1/messages`, `v1/chat/completions`, `v1beta/models/{model}:generateContent`). DeepSeek reaproveita
  o adapter OpenAI-compatível. O `ChatRoutineGenerator` permanece agnóstico ao provedor.
- **Montagem (Abstract Factory — `IChatModelClientFactory`)**: uma factory por provedor cria o par
  adapter+autenticação sobre um HttpClient nomeado; `ChatModelClientFactory` resolve a do provedor
  configurado. Adicionar um provedor = registrar mais uma factory.

## Cronograma capilar

### Questionário (entrada)

| Campo | Tipo | Valores |
|-------|------|---------|
| `hairType` | escolha única | `straight`, `wavy` (2A-2C), `curly` (3A-3C), `coily` (4A-4C) |
| `thickness` | escolha única | `fine`, `medium`, `coarse` |
| `tone` | escolha única | `light_blonde`, `blonde`, `light_brown`, `brown`, `dark_brown`, `black` |
| `conditions` | múltipla escolha | `dry`, `normal`, `oily`, `damaged`, `frizzy`, `dull` (`normal` não combina com `dry`/`oily`) |
| `chemical.hasChemical` | sim/não | se `true`, exige `type`, `performed` e `touchUpFrequency` |
| `chemical.type` | escolha única | `relaxation`, `straightening`, `perm`, `coloring`, `bleaching`, `other` |
| `chemical.performed` | texto | `"há 2 meses"`, `"3 semanas"`, `"1 ano"`, `"10/06/2026"`, `"2026-06-10"`, `"06/2026"` (sem datas futuras) |
| `chemical.touchUpFrequency` | escolha única | `monthly`, `bimonthly`, `quarterly`, `semiannual`, `annual` |
| `mainGoal` | texto | objetivo/queixa principal (queda, frizz, brilho, volume...) |
| `allergies` | checkbox (lista fechada) | `fragrance`, `sulfate`, `paraben`, `silicone`, `coconut_oil`, `nut_oils`, `lanolin`, `wheat_protein`, `essential_oils`, `formaldehyde` |

### Regras

- **Perfil**: prioridades vêm de condições + tipo + espessura + química + palavras-chave do objetivo
  (o objetivo pesa mais). Necessidade H/N/R parte de uma base por tipo (liso 2/1/1, ondulado 2/2/1,
  cacheado 3/2/1, crespo 3/3/1) ajustada por condições, espessura, química recente e objetivo.
- **Seleção de produtos**: tipo de cabelo + condição alvo; química recente (< 90 dias) exclui produtos
  não seguros para química; contraindicações (ex.: queratina × relaxamento recente); alergias marcadas
  excluem produtos que contêm o alérgeno. Excluídos voltam em `excludedProducts` com o motivo.
- **Lavagens/semana**: liso 4, ondulado 3, cacheado 3, crespo 2; oleoso +2, ressecado −1 (entre 2 e 7).
- **Tratamentos/semana**: 1; 2 para ressecado, danificado, cacheado ou crespo; oleoso limita a 1.
- **Ciclo H/N/R (4 semanas)**: slots proporcionais à necessidade, no máximo 1 reconstrução por semana e
  nunca duas seguidas; cacheado/crespo com hidratação toda semana. Um produto pode ter mais de um eixo
  H/N/R e então preenche slots de tipos diferentes.
- **Intervalo mínimo** (`MinIntervalDays`) respeitado nos 28 dias; **ordem de aplicação**
  shampoo → condicionador → máscara → tratamento → leave-in → sérum/óleo → finalizador.

### Saída (resumo)

`schedule.overview` agrupa como no exemplo da especificação (`"Diário"`, `"2x por semana (seg e qui)"`,
`"Cronograma H/N/R — 2x por semana (seg e qui)"`); `schedule.weeks[].days[].steps[]` traz o calendário
completo com `how`/`why` por passo; `schedule.specialCare` traz os cuidados especiais (intervalos,
química recente, alergias).

O documento de projeto completo (requisitos, escalabilidade, diagramas) está em
`../.claude/plans/preciso-criar-uma-api-typed-brook.md`.

## Como rodar

### 1) Local, sem dependências (regras + template)

```bash
dotnet run --project src/AIHairRoutine.Api --no-launch-profile
```

Sobe em `http://localhost:5080` (ou configure `ASPNETCORE_URLS`). Sem chaves/banco, usa os fallbacks.

### 2) Com Docker Compose (API + SQL Server)

```bash
JEV_API_KEY=... ANTHROPIC_API_KEY=... docker compose up --build
```

As migrações (DbUp) criam e populam a tabela `Products` no startup (`Script0003` recria a tabela com o
schema do cronograma e `Script0004` carrega o catálogo de exemplo). As chaves são opcionais.

### Testar o endpoint

```bash
curl -s http://localhost:5080/api/v1/diagnoses \
  -H "content-type: application/json" \
  -d '{"hairType":"wavy","thickness":"fine","tone":"brown","conditions":["dry","frizzy"],"chemical":{"hasChemical":true,"type":"coloring","performed":"há 2 meses","touchUpFrequency":"quarterly"},"mainGoal":"reduzir frizz e ganhar brilho","allergies":["fragrance","sulfate"],"locale":"pt-BR"}'
```

## Configuração (appsettings / variáveis de ambiente)

| Chave | Env | Efeito |
|-------|-----|--------|
| `Jev:ApiKey` | `Jev__ApiKey` | Vazio → perfil só por regras. Preenchido → híbrido (JEV nos casos ambíguos). |
| `Generation:Provider` | `Generation__Provider` | Provedor ativo: `Anthropic` (padrão), `OpenAI`, `Gemini` ou `DeepSeek`. |
| `Generation:Providers:<Prov>:ApiKey` | `Generation__Providers__<Prov>__ApiKey` | Vazio (no provedor ativo) → textos por template. Preenchido → textos pela IA. |
| `Generation:Providers:<Prov>:Model` | `Generation__Providers__<Prov>__Model` | Modelo do provedor (ex.: `claude-sonnet-5`, `gpt-4o-mini`, `gemini-2.0-flash`, `deepseek-chat`). |
| `Generation:MaxTokens` / `Generation:TimeoutSeconds` | | Orçamento de tokens / timeout compartilhado. |
| `Database:ConnectionString` | `Database__ConnectionString` | Vazio → catálogo vazio (sem recomendações). |
| `RoutineCache:ExpirationMinutes` | | TTL do cache dos textos do cronograma. |

> Nunca comite chaves. Local: use `dotnet user-secrets`. Produção: variáveis/secret store.

## Endpoints

- `POST /api/v1/diagnoses` — perfil, produtos recomendados/excluídos e cronograma de 4 semanas.
- `GET /health` — liveness. `GET /ready` — readiness (checa o banco quando configurado).
- `GET /openapi/v1.json` e `GET /scalar/v1` — documentação da API.

## Testes

```bash
dotnet test
```

## Notas de escala

- Tudo `async` (I/O-bound) → alta concorrência por instância.
- `HybridCache` colapsa perfis equivalentes (mesmo perfil → mesmo cronograma → sem nova chamada ao LLM). L2 Redis quando escalar.
- Catálogo em memória → sem banco no hot path. Resiliência via `AddStandardResilienceHandler` (Polly).
- API stateless → escala horizontal. Rate limiting protege quotas de JEV e do provedor de IA.
