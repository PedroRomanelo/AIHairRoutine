# AI Hair Routine API

API .NET 10 que recebe um questionário capilar, gera um **perfil tipado + prioridades** e devolve
uma **rotina de cuidados** com **produtos recomendados** do catálogo. Integra dois serviços:

- **JEV (TypeSafe AI)** — classifica o perfil/prioridades (System One Model).
- **Claude (Anthropic)** — escreve a rotina final em linguagem natural.

O desenho prioriza **degradação graciosa**: sem chaves e sem banco, a API continua funcionando
(perfil por **regras** + rotina por **template**). As chaves habilitam progressivamente as partes de IA.

## Arquitetura

```
src/
  AIHairRoutine.Api             Minimal API, OpenAPI/Scalar, health, rate limiting, ProblemDetails
  AIHairRoutine.Application     Domínio: modelos, interfaces (seams), regras, matching, facade
  AIHairRoutine.Infrastructure  JEV, Claude, Dapper/SQL Server, HybridCache, resiliência (Polly)
tests/
  AIHairRoutine.Tests           unit (profiler, matcher) + integração (endpoint)
```

Fluxo: `validar → IHairProfiler → IProductMatcher → IRoutineGenerator → montar DiagnosisResult`.

Padrões: **Adapter** (JEV/Claude), **Facade** (`DiagnosisService`), **Strategy** (`HybridProfiler`,
`ProductMatcher`), **Decorator** (`CachingRoutineGenerator`), **Builder** (`JevRequestBuilder`,
`ClaudePromptBuilder`), **Fallback/graceful degradation** (`FallbackRoutineGenerator`).

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

As migrações (DbUp) criam e populam a tabela `Products` no startup. As chaves são opcionais.

### Testar o endpoint

```bash
curl -s http://localhost:5080/api/v1/diagnoses \
  -H "content-type: application/json" \
  -d '{"hairType":"wavy","chemicalTreatment":"progressive","colorTreated":false,"concerns":{"dryness":8,"frizz":7,"breakage":4,"oiliness":2,"hairLoss":1},"locale":"pt-BR"}'
```

## Configuração (appsettings / variáveis de ambiente)

| Chave | Env | Efeito |
|-------|-----|--------|
| `Jev:ApiKey` | `Jev__ApiKey` | Vazio → perfil só por regras. Preenchido → híbrido (JEV nos casos ambíguos). |
| `Anthropic:ApiKey` | `Anthropic__ApiKey` | Vazio → rotina por template. Preenchido → rotina pelo Claude. |
| `Anthropic:Model` | `Anthropic__Model` | Modelo de geração (padrão `claude-sonnet-5`). |
| `Database:ConnectionString` | `Database__ConnectionString` | Vazio → catálogo vazio (sem recomendações). |
| `RoutineCache:ExpirationMinutes` | | TTL do cache de rotinas. |

> Nunca comite chaves. Local: use `dotnet user-secrets`. Produção: variáveis/secret store.

## Endpoints

- `POST /api/v1/diagnoses` — diagnóstico + rotina.
- `GET /health` — liveness. `GET /ready` — readiness (checa o banco quando configurado).
- `GET /openapi/v1.json` e `GET /scalar/v1` — documentação da API.

## Testes

```bash
dotnet test
```

## Notas de escala

- Tudo `async` (I/O-bound) → alta concorrência por instância.
- `HybridCache` colapsa perfis equivalentes (evita chamadas repetidas ao Claude). L2 Redis quando escalar.
- Catálogo em memória → sem banco no hot path. Resiliência via `AddStandardResilienceHandler` (Polly).
- API stateless → escala horizontal. Rate limiting protege quotas de JEV/Claude.
